using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;
using Mostlylucid.Markdig.FetchExtension.Utilities;

namespace Mostlylucid.Markdig.FetchExtension.Parsers;

/// <summary>
///     Parser for the <fetch> tag
/// </summary>
public partial class FetchMarkdownInlineParser : InlineParser
{
    [GeneratedRegex(@"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'](?:[^>]*?transformlinks\s*=\s*[""'](true|false)[""'])?(?:[^>]*?showsummary\s*=\s*[""'](true|false)[""'])?(?:[^>]*?summarytemplate\s*=\s*[""']([^""']+)[""'])?(?:[^>]*?cssclass\s*=\s*[""']([^""']+)[""'])?[^>]*?/\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FetchTagRegex();

    private static bool _warnedNoUpdateService;

    private readonly IServiceProvider? _serviceProvider;

    public FetchMarkdownInlineParser(IServiceProvider? serviceProvider)
    {
        OpeningCharacters = new[] { '<' };
        _serviceProvider = serviceProvider;
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var text = slice.Text;
        var startPosition = slice.Start;

        // Quick check: must start with "<fetch"
        if (slice.Length < 6)
            return false;

        // Manual check for "<fetch" at current position (case-insensitive)
        if (text[startPosition] != '<' ||
            char.ToLower(text[startPosition + 1]) != 'f' ||
            char.ToLower(text[startPosition + 2]) != 'e' ||
            char.ToLower(text[startPosition + 3]) != 't' ||
            char.ToLower(text[startPosition + 4]) != 'c' ||
            char.ToLower(text[startPosition + 5]) != 'h')
            return false;

        // Look ahead up to 200 characters to find a complete fetch tag
        var lookAheadLength = Math.Min(200, slice.Length);
        var lookAheadText = text.Substring(startPosition, lookAheadLength);

        var match = FetchTagRegex().Match(lookAheadText);
        if (!match.Success)
            return false;

        var url = match.Groups[1].Value;
        var pollFrequencyHours = int.Parse(match.Groups[2].Value);
        var transformLinks = match.Groups.Count > 3 &&
                             match.Groups[3].Success &&
                             match.Groups[3].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        var showSummary = match.Groups.Count > 4 &&
                          match.Groups[4].Success &&
                          match.Groups[4].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        var summaryTemplate = match.Groups.Count > 5 && match.Groups[5].Success
            ? match.Groups[5].Value
            : null;
        var cssClass = match.Groups.Count > 6 && match.Groups[6].Success
            ? match.Groups[6].Value
            : null;

        // Try to fetch the content
        var fetchedContent = string.Empty;
        var fetchSuccessful = false;

        if (_serviceProvider != null)
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var provider = scope.ServiceProvider;
                var fetchService = provider.GetRequiredService<IMarkdownFetchService>();
                var logger = provider.GetRequiredService<ILogger<FetchMarkdownInlineParser>>();

                logger.LogInformation(
                    "Parsed <fetch> tag: url={Url}, pollFrequency={PollFrequency}h, transformLinks={TransformLinks}, showSummary={ShowSummary}, summaryTemplate={SummaryTemplate}, cssClass={CssClass}",
                    url, pollFrequencyHours, transformLinks, showSummary, summaryTemplate ?? "(none)", cssClass ?? "(none)");

                // Register with optional update service for polling/eventing
                var updateService = provider.GetService<IMarkdownFetchUpdateService>();
                if (updateService != null)
                {
                    updateService.Register(url, pollFrequencyHours);
                }
                else if (!_warnedNoUpdateService)
                {
                    _warnedNoUpdateService = true;
                    logger.LogWarning(
                        "IMarkdownFetchUpdateService not configured; fetch polling events will be disabled.");
                }

                // Attempt synchronous fetch (with caching)
                // Note: blogPostId is 0 here since we don't have context. The background service will update later.
                var result = fetchService.FetchMarkdownAsync(url, pollFrequencyHours, 0)
                    .GetAwaiter()
                    .GetResult();

                if (result.Success)
                {
                    fetchedContent = result.Content;

                    // Transform relative links to absolute URLs pointing back to source
                    if (transformLinks)
                    {
                        fetchedContent = MarkdownLinkRewriter.RewriteLinks(fetchedContent, url);
                        logger.LogDebug("Transformed links in fetched markdown from {Url}", url);
                    }

                    // Append metadata summary if requested
                    if (showSummary)
                    {
                        var summary = FetchMetadataSummary.Format(result, summaryTemplate, cssClass);
                        if (!string.IsNullOrEmpty(summary))
                        {
                            fetchedContent = fetchedContent + "\n\n" + summary;
                            logger.LogDebug("Added metadata summary to fetched content from {Url}", url);
                        }
                    }

                    fetchSuccessful = true;
                }
                else
                {
                    logger.LogWarning("Failed to fetch markdown from {Url}: {Error}", url, result.ErrorMessage);
                    fetchedContent = $"<!-- Failed to fetch content from {url}: {result.ErrorMessage} -->";
                }
            }
            catch (Exception ex)
            {
                fetchedContent = $"<!-- Error fetching content from {url}: {ex.Message} -->";
            }
        else
            fetchedContent = "<!-- Markdown fetch service not configured -->";

        // Create the inline element
        var inline = new FetchMarkdownInline
        {
            Url = url,
            PollFrequencyHours = pollFrequencyHours,
            FetchedContent = fetchedContent,
            FetchSuccessful = fetchSuccessful
        };

        processor.Inline = inline;

        // Advance the slice past the matched tag
        slice.Start = startPosition + match.Length;

        return true;
    }
}