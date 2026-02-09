using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;
using Microsoft.Recognizers.Text.Sequence;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Models;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
///     Extract structured signals from text using Microsoft.Recognizers.Text.
///     Extracts dates, numbers, URLs, phone numbers, emails, and IP addresses.
///     Each extraction is wrapped in try-catch for safety.
/// </summary>
public class TextRecognizerService : ITextRecognizerService
{
    private readonly ILogger<TextRecognizerService> _logger;
    private readonly string _defaultCulture;

    public TextRecognizerService(
        ILogger<TextRecognizerService> logger,
        IOptions<OcrNerConfig> config)
    {
        _logger = logger;
        _defaultCulture = config.Value.RecognizerCulture;
    }

    /// <inheritdoc />
    public RecognizedSignals ExtractAll(string text, string? culture = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new RecognizedSignals();

        var c = culture ?? _defaultCulture;
        return new RecognizedSignals
        {
            Culture = c,
            DateTimes = ExtractDateTimes(text, c),
            Numbers = ExtractNumbers(text, c),
            Urls = ExtractUrls(text, c),
            PhoneNumbers = ExtractPhoneNumbers(text, c),
            Emails = ExtractEmails(text, c),
            IpAddresses = ExtractIpAddresses(text, c)
        };
    }

    private List<RecognizedDateTime> ExtractDateTimes(string text, string culture)
    {
        var results = new List<RecognizedDateTime>();
        try
        {
            var recognized = DateTimeRecognizer.RecognizeDateTime(text, culture);
            foreach (var r in recognized)
            {
                var resolution = r.Resolution;
                if (resolution == null) continue;

                results.Add(new RecognizedDateTime
                {
                    Text = r.Text,
                    Start = r.Start,
                    End = r.End,
                    TypeName = r.TypeName,
                    Resolution = resolution
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DateTime recognition failed");
        }

        return results;
    }

    private List<RecognizedNumber> ExtractNumbers(string text, string culture)
    {
        var results = new List<RecognizedNumber>();
        try
        {
            var recognized = NumberRecognizer.RecognizeNumber(text, culture);
            foreach (var r in recognized)
                if (r.Resolution?.TryGetValue("value", out var val) == true)
                    results.Add(new RecognizedNumber
                    {
                        Text = r.Text,
                        Start = r.Start,
                        Value = val?.ToString(),
                        TypeName = r.TypeName
                    });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Number recognition failed");
        }

        return results;
    }

    private List<RecognizedSequence> ExtractUrls(string text, string culture)
    {
        var results = new List<RecognizedSequence>();
        try
        {
            var recognized = SequenceRecognizer.RecognizeURL(text, culture);
            foreach (var r in recognized)
                results.Add(new RecognizedSequence
                {
                    Text = r.Text,
                    Start = r.Start,
                    TypeName = "URL"
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "URL recognition failed");
        }

        return results;
    }

    private List<RecognizedSequence> ExtractPhoneNumbers(string text, string culture)
    {
        var results = new List<RecognizedSequence>();
        try
        {
            var recognized = SequenceRecognizer.RecognizePhoneNumber(text, culture);
            foreach (var r in recognized)
                results.Add(new RecognizedSequence
                {
                    Text = r.Text,
                    Start = r.Start,
                    TypeName = "PhoneNumber"
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Phone number recognition failed");
        }

        return results;
    }

    private List<RecognizedSequence> ExtractEmails(string text, string culture)
    {
        var results = new List<RecognizedSequence>();
        try
        {
            var recognized = SequenceRecognizer.RecognizeEmail(text, culture);
            foreach (var r in recognized)
                results.Add(new RecognizedSequence
                {
                    Text = r.Text,
                    Start = r.Start,
                    TypeName = "Email"
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Email recognition failed");
        }

        return results;
    }

    private List<RecognizedSequence> ExtractIpAddresses(string text, string culture)
    {
        var results = new List<RecognizedSequence>();
        try
        {
            var recognized = SequenceRecognizer.RecognizeIpAddress(text, culture);
            foreach (var r in recognized)
                results.Add(new RecognizedSequence
                {
                    Text = r.Text,
                    Start = r.Start,
                    TypeName = "IpAddress"
                });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IP address recognition failed");
        }

        return results;
    }
}
