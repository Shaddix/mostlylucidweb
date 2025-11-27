using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Utilities;

namespace Mostlylucid.Markdig.FetchExtension.Parsers;

/// <summary>
///     Parser for the <fetch-summary> tag - displays metadata for a previously fetched URL
/// </summary>
public partial class FetchSummaryInlineParser : InlineParser
{
    [GeneratedRegex(@"<fetch-summary\s+(?:[^>]*?disable\s*=\s*[""'](true|false)[""'][^>]*?)?[^>]*?url\s*=\s*[""']([^""']+)[""'](?:[^>]*?template\s*=\s*[""']([^""']+)[""'])?(?:[^>]*?cssclass\s*=\s*[""']([^""']+)[""'])?[^>]*?/\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex FetchSummaryTagRegex();

    private readonly ILogger<FetchSummaryInlineParser>? _logger;

    public FetchSummaryInlineParser(ILogger<FetchSummaryInlineParser>? logger = null)
    {
        OpeningCharacters = new[] { '<' };
        _logger = logger;
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var text = slice.Text;
        var startPosition = slice.Start;

        // Quick check for opening
        if (startPosition + 13 >= text.Length) // "<fetch-summary".Length = 14
            return false;

        // Check if it starts with <fetch-summary (case insensitive)
        if (char.ToLower(text[startPosition]) != '<' ||
            char.ToLower(text[startPosition + 1]) != 'f' ||
            char.ToLower(text[startPosition + 2]) != 'e' ||
            char.ToLower(text[startPosition + 3]) != 't' ||
            char.ToLower(text[startPosition + 4]) != 'c' ||
            char.ToLower(text[startPosition + 5]) != 'h' ||
            char.ToLower(text[startPosition + 6]) != '-' ||
            char.ToLower(text[startPosition + 7]) != 's' ||
            char.ToLower(text[startPosition + 8]) != 'u' ||
            char.ToLower(text[startPosition + 9]) != 'm' ||
            char.ToLower(text[startPosition + 10]) != 'm' ||
            char.ToLower(text[startPosition + 11]) != 'a' ||
            char.ToLower(text[startPosition + 12]) != 'r' ||
            char.ToLower(text[startPosition + 13]) != 'y')
            return false;

        // Look ahead to find the complete tag
        var lookAheadLength = Math.Min(300, slice.Length);
        var lookAheadText = text.Substring(startPosition, lookAheadLength);

        var match = FetchSummaryTagRegex().Match(lookAheadText);
        if (!match.Success)
            return false;

        // Check if disabled
        var disabled = match.Groups[1].Success &&
                      match.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (disabled)
            return false; // Don't process this tag, leave it as-is

        var url = match.Groups[2].Value;
        var template = match.Groups.Count > 3 && match.Groups[3].Success
            ? match.Groups[3].Value
            : null;
        var cssClass = match.Groups.Count > 4 && match.Groups[4].Success
            ? match.Groups[4].Value
            : null;

        _logger?.LogInformation(
            "Parsed <fetch-summary> tag: url={Url}, template={Template}, cssClass={CssClass}",
            url, template ?? "(none)", cssClass ?? "(none)");

        // Try to get the fetch result from context
        var document = processor.Document;
        var context = FetchResultContext.TryGet(document);
        var result = context?.GetResult(url);

        string summaryContent;
        if (result != null && result.Success)
        {
            // Format the summary using the result metadata
            summaryContent = FetchMetadataSummary.Format(result, template, cssClass);
        }
        else
        {
            // No result found - either fetch hasn't happened yet or failed
            summaryContent = $"<!-- No fetch data available for {url} -->";
        }

        // Create a literal inline with the summary
        var inline = new LiteralInline(summaryContent);
        processor.Inline = inline;

        // Move past the tag
        slice.Start += match.Length;
        return true;
    }
}
