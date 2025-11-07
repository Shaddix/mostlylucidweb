using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mostlylucid.Shared.Config;
using Polly;
using Polly.CircuitBreaker;

namespace Mostlylucid.MarkdownTranslator;

public class MarkdownTranslatorService(
    TranslateServiceConfig translateServiceConfig,
    ILogger<IMarkdownTranslatorService> logger,
    HttpClient client) : IMarkdownTranslatorService
{
    private record PostRecord(
        string target_lang,
        string[] text,
        string source_lang = "en",
        bool perform_sentence_splitting = true);


    private record PostResponse(string target_lang, string[] translated, string source_lang, float translation_time);

    public int IPCount => IPs.Length;

    private string[] IPs = translateServiceConfig.IPs;

    // Track language failures to skip problematic languages temporarily
    private static readonly LanguageFailureTracker _failureTracker = new(
        maxConsecutiveFailures: 3,
        resetInterval: TimeSpan.FromHours(24));

    // Circuit breaker to handle service overload (403 responses)
    private readonly ResiliencePipeline _circuitBreakerPolicy = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0, // Break after all attempts fail
            MinimumThroughput = 3, // Require 3 failures
            BreakDuration = TimeSpan.FromMinutes(2),
            ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>(ex =>
                ex.StatusCode == HttpStatusCode.Forbidden ||
                ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                ex.StatusCode == HttpStatusCode.TooManyRequests),
            OnOpened = args =>
            {
                logger.LogWarning(
                    "Circuit breaker opened due to translation service overload. Will retry after {Duration}",
                    args.BreakDuration);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                logger.LogInformation("Circuit breaker reset. Translation service is available again.");
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                logger.LogInformation("Circuit breaker half-open. Testing translation service...");
                return ValueTask.CompletedTask;
            }
        })
        .Build();

    public async ValueTask<bool> IsServiceUp(CancellationToken cancellationToken)
    {
        var workingIPs = new List<string>();

        try
        {
            logger.LogWarning("Checking service status for {IPs}", string.Join(", ", IPs));
            foreach (var ip in IPs)
            {
                logger.LogInformation("Checking service status at {IP}", ip);
                try
                {
                    var response = await client.GetAsync($"{ip}/model_name", cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        workingIPs.Add(ip);
                    }
                    else
                    {
                        logger.LogWarning("Service at {IP} is not available", ip);
                    }
                }
                catch (Exception)
                {
                    logger.LogWarning("Service at {IP} is not available", ip);
                }
            }

            IPs = workingIPs.ToArray();
            if (!IPs.Any()) return false;
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error checking service status");
            return false;
        }
    }

    private int currentIPIndex = 0;

    private async Task<string[]> Post(string[] elements, string targetLang, CancellationToken cancellationToken)
    {
        return await _circuitBreakerPolicy.ExecuteAsync(async ct =>
        {
            if (!IPs.Any())
            {
                logger.LogError("No IPs available for translation");
                throw new Exception("No IPs available for translation");
            }

            var ip = IPs[currentIPIndex];

            logger.LogInformation("Sending request to {IP}", ip);

            // Update the index for the next request
            currentIPIndex = (currentIPIndex + 1) % IPs.Length;
            var postObject = new PostRecord(targetLang, elements);

            var response = await client.PostAsJsonAsync($"{ip}/translate", postObject, ct);

            // Handle service overload (403 Forbidden) explicitly
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // Suppress noisy overload warnings - handled by retry logic
                logger.LogDebug("Translation service at {IP} returned 403 (overloaded)", ip);
                throw new HttpRequestException(
                    "Translation service is overloaded (403)",
                    null,
                    HttpStatusCode.Forbidden);
            }

            // Handle other non-success status codes
            if (!response.IsSuccessStatusCode)
            {
                var statusCode = response.StatusCode;
                logger.LogError("Translation service at {IP} returned {StatusCode}", ip, statusCode);
                throw new HttpRequestException(
                    $"Translation service returned {statusCode}",
                    null,
                    statusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<PostResponse>(cancellationToken: ct);

            logger.LogInformation("Translation took {Time} seconds", result.translation_time);
            return result.translated;
        }, cancellationToken);
    }


    public async Task<string> TranslateMarkdown(string markdown, string targetLang, CancellationToken cancellationToken,
        Activity? activity)
    {
        // Check if this language should be skipped due to consecutive failures
        if (_failureTracker.ShouldSkip(targetLang))
        {
            var failureCount = _failureTracker.GetFailureCount(targetLang);
            logger.LogWarning(
                "Skipping translation to {Language} due to {FailureCount} consecutive failures. Will retry after reset.",
                targetLang, failureCount);
            activity?.SetTag("LanguageSkipped", true);
            activity?.SetTag("FailureCount", failureCount);
            throw new TranslateException(
                $"Language {targetLang} temporarily skipped due to {failureCount} consecutive failures",
                Array.Empty<string>());
        }

        // Note: We don't preprocess fetch tags here because we want to translate the original markdown
        // The fetch preprocessing should only happen during rendering, not translation
        // Translation works on the source markdown files, not the rendered/fetched content

        var pipeline = new MarkdownPipelineBuilder().UsePreciseSourceLocation().ConfigureNewLine(Environment.NewLine)
            .Build();
        var document = global::Markdig.Markdown.Parse(markdown, pipeline);
        var textStrings = ExtractTextStrings(document);

        // Batch by character limit instead of fixed count for better EasyNMT optimization
        var batches = BatchTextByCharLimit(textStrings);
        List<string> translatedStrings = new();

        logger.LogInformation(
            "Translating {TotalStrings} text elements in {BatchCount} batches to {Language}",
            textStrings.Count, batches.Count, targetLang);

        foreach (var batch in batches)
        {
            try
            {
                var translatedBatch = await Post(batch, targetLang, cancellationToken);
                activity?.SetTag("BatchSize", batch.Length);
                activity?.SetTag("BatchChars", batch.Sum(s => s.Length));
                activity?.SetTag("Starting time", DateTime.UtcNow);
                translatedStrings.AddRange(translatedBatch);
                activity?.SetTag("Ending time", DateTime.UtcNow);
            }
            catch (BrokenCircuitException e)
            {
                logger.LogWarning("Circuit breaker is open - translation service is temporarily unavailable for {Language}", targetLang);
                activity?.SetTag("CircuitBreakerOpen", true);
                _failureTracker.RecordFailure(targetLang);
                throw new TranslateException(
                    "Translation service is temporarily unavailable due to overload. Circuit breaker is open.",
                    batch);
            }
            catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.Forbidden)
            {
                // Suppress noisy overload warnings - handled by retry logic
                logger.LogDebug("Translation service returned 403 (overloaded) for {Language}", targetLang);
                activity?.SetTag("ServiceOverloaded", true);
                _failureTracker.RecordFailure(targetLang);
                throw new TranslateException(
                    "Translation service is overloaded (403)",
                    batch);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error translating markdown to {Language}: {Message} for strings {Strings}",
                    targetLang, e.Message,
                    string.Concat(Environment.NewLine, batch));
                _failureTracker.RecordFailure(targetLang);
                throw new TranslateException(e.Message, batch);
            }
        }

        // Translation succeeded - reset failure count for this language
        _failureTracker.RecordSuccess(targetLang);

        ReinsertTranslatedStrings(document, translatedStrings.ToArray());
        var outString = document.ToMarkdownString();
        outString = outString.Replace("</summary>", $"</summary>{Environment.NewLine}");
        return outString;
    }

    private List<string> ExtractTextStrings(MarkdownDocument document)
    {
        var textStrings = new List<string>();

        foreach (var node in document.Descendants())
        {
            if (node is LiteralInline literalInline)
            {
                if (literalInline?.Parent?.FirstChild is HtmlInline { Tag: "<datetime class=\"hidden\">" }) continue;

                var content = literalInline?.Content.ToString();
                if (content == null) continue;
                if (!IsWord(content)) continue;

                // Split content into sentences for better translation quality
                var sentences = SplitIntoSentences(content);
                textStrings.AddRange(sentences);
            }
        }

        return textStrings;
    }


    private bool IsWord(string text)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg" };
        if (imageExtensions.Any(text.Contains)) return false;

        if (text == "TOC]") return false;

        // Skip emoticons/emoji - they break translation
        if (ContainsEmoji(text)) return false;

        return text.Any(char.IsLetter);
    }

    private bool ContainsEmoji(string text)
    {
        // Check for common emoji Unicode ranges
        foreach (var c in text)
        {
            var code = (int)c;

            // Emoticons (U+1F600 to U+1F64F)
            // Miscellaneous Symbols and Pictographs (U+1F300 to U+1F5FF)
            // Transport and Map Symbols (U+1F680 to U+1F6FF)
            // Supplemental Symbols and Pictographs (U+1F900 to U+1F9FF)
            // Symbols and Pictographs Extended-A (U+1FA70 to U+1FAFF)
            if (code >= 0x1F600 && code <= 0x1F64F ||
                code >= 0x1F300 && code <= 0x1F5FF ||
                code >= 0x1F680 && code <= 0x1F6FF ||
                code >= 0x1F900 && code <= 0x1F9FF ||
                code >= 0x1FA70 && code <= 0x1FAFF ||
                // Additional emoji ranges
                code >= 0x2600 && code <= 0x26FF ||   // Miscellaneous Symbols
                code >= 0x2700 && code <= 0x27BF ||   // Dingbats
                code >= 0xFE00 && code <= 0xFE0F ||   // Variation Selectors
                code >= 0x1F000 && code <= 0x1F02F || // Mahjong Tiles
                code >= 0x1F0A0 && code <= 0x1F0FF)   // Playing Cards
            {
                return true;
            }

            // Check for surrogate pairs (emoji outside BMP)
            if (char.IsHighSurrogate(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Split text into sentences for better translation quality and batching.
    /// Handles common abbreviations and edge cases.
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var sentences = new List<string>();

        // Common abbreviations that shouldn't trigger sentence breaks
        var abbreviations = new[]
        {
            "Dr", "Mr", "Mrs", "Ms", "Prof", "Sr", "Jr",
            "etc", "i.e", "e.g", "vs", "Inc", "Ltd", "Corp",
            "Fig", "Vol", "No", "Approx", "Dept"
        };

        // Replace abbreviations temporarily to avoid false sentence breaks
        var tempText = text;
        var abbrevReplacements = new Dictionary<string, string>();
        for (int i = 0; i < abbreviations.Length; i++)
        {
            var abbrev = abbreviations[i];
            var placeholder = $"<<ABBREV{i}>>";
            abbrevReplacements[placeholder] = abbrev + ".";
            tempText = tempText.Replace(abbrev + ".", placeholder);
        }

        // Split on sentence boundaries: . ! ? followed by space and capital letter
        // or end of string
        var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])|(?<=[.!?])$";
        var parts = Regex.Split(tempText, sentencePattern);

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            // Restore abbreviations
            var restored = part;
            foreach (var kvp in abbrevReplacements)
            {
                restored = restored.Replace(kvp.Key, kvp.Value);
            }

            sentences.Add(restored.Trim());
        }

        // If no sentences found, return the whole text as one sentence
        if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            sentences.Add(text.Trim());
        }

        return sentences;
    }

    /// <summary>
    /// Batch text strings by character limit and sentence count to optimize for EasyNMT input constraints.
    /// This replaces the old fixed-count batching (batch size = 5) with dynamic batching
    /// based on actual content length and sentence boundaries.
    /// </summary>
    private List<string[]> BatchTextByCharLimit(List<string> textStrings)
    {
        var batches = new List<string[]>();
        var currentBatch = new List<string>();
        var currentLength = 0;

        foreach (var text in textStrings)
        {
            var textLength = text.Length;

            // If adding this text would exceed character limit OR sentence limit, start a new batch
            if (currentBatch.Count > 0 &&
                (currentLength + textLength > translateServiceConfig.MaxBatchCharacters ||
                 currentBatch.Count >= translateServiceConfig.MaxSentencesPerBatch))
            {
                batches.Add(currentBatch.ToArray());
                currentBatch = new List<string>();
                currentLength = 0;
            }

            // For very long single text elements, send alone (with warning)
            if (textLength > translateServiceConfig.MaxBatchCharacters)
            {
                logger.LogWarning(
                    "Single text element exceeds max batch size ({Length} > {Max}). Sending as single item.",
                    textLength, translateServiceConfig.MaxBatchCharacters);

                if (currentBatch.Count > 0)
                {
                    batches.Add(currentBatch.ToArray());
                    currentBatch = new List<string>();
                    currentLength = 0;
                }

                batches.Add(new[] { text });
            }
            else
            {
                currentBatch.Add(text);
                currentLength += textLength;
            }
        }

        // Add remaining text elements
        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch.ToArray());
        }

        // If no batches created but we have text, create one batch
        if (batches.Count == 0 && textStrings.Count > 0)
        {
            batches.Add(textStrings.ToArray());
        }

        return batches;
    }

    /// <summary>
    /// Batch sentences into groups that respect character limits for EasyNMT.
    /// </summary>
    private List<string[]> BatchSentencesByCharLimit(List<string> sentences)
    {
        var batches = new List<string[]>();
        var currentBatch = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            var sentenceLength = sentence.Length;

            // If adding this sentence would exceed limits, start a new batch
            if (currentBatch.Count > 0 &&
                (currentLength + sentenceLength > translateServiceConfig.MaxBatchCharacters ||
                 currentBatch.Count >= translateServiceConfig.MaxSentencesPerBatch))
            {
                batches.Add(currentBatch.ToArray());
                currentBatch = new List<string>();
                currentLength = 0;
            }

            // If a single sentence exceeds the limit, split it further or add it alone
            if (sentenceLength > translateServiceConfig.MaxBatchCharacters)
            {
                // Log warning for oversized sentence
                logger.LogWarning(
                    "Sentence exceeds max batch size ({Length} > {Max}). Sending as single item.",
                    sentenceLength, translateServiceConfig.MaxBatchCharacters);

                if (currentBatch.Count > 0)
                {
                    batches.Add(currentBatch.ToArray());
                    currentBatch = new List<string>();
                    currentLength = 0;
                }

                batches.Add(new[] { sentence });
            }
            else
            {
                currentBatch.Add(sentence);
                currentLength += sentenceLength;
            }
        }

        // Add remaining sentences
        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch.ToArray());
        }

        return batches;
    }

    /// <summary>
    /// Get list of languages currently skipped due to consecutive failures
    /// </summary>
    public IEnumerable<string> GetSkippedLanguages()
    {
        return _failureTracker.GetSkippedLanguages();
    }

    private void ReinsertTranslatedStrings(MarkdownDocument document, string[] translatedStrings)
    {
        int index = 0;

        foreach (var node in document.Descendants())
        {
            if (node is LiteralInline literalInline && index < translatedStrings.Length)
            {
                if (literalInline?.Parent?.FirstChild is HtmlInline { Tag: "<datetime class=\"hidden\">" }) continue;
                if (literalInline == null) continue;
                var content = literalInline.Content.ToString();
                if (!IsWord(content)) continue;
                var translatedContent = translatedStrings[index];
                literalInline.Content = new StringSlice(translatedContent, NewLine.CarriageReturnLineFeed);
                index++;
            }
        }
    }
}

public class TranslateException(string message, string[] strings)
    : Exception($"Error translating markdown: {message} for strings {string.Join(", ", strings)}")
{
}