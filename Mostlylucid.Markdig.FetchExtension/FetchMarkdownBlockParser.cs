using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Block parser for the <fetch/> tag placed on its own line.
/// Ensures we intercept before the HtmlBlock parser would treat it as raw HTML.
/// </summary>
public class FetchMarkdownBlockParser : BlockParser
{
    private static readonly Regex FetchTagRegex = new(
        @"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'](?:[^>]*?transformlinks\s*=\s*[""'](true|false)[""'])?[^>]*?/\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IServiceProvider? _serviceProvider;

    public FetchMarkdownBlockParser(IServiceProvider? serviceProvider)
    {
        OpeningCharacters = new[] { '<' };
        _serviceProvider = serviceProvider;
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        // Only consider at the start of a line
        var slice = processor.Line;
        if (slice.IsEmpty)
            return BlockState.None;

        var text = slice.Text;
        var start = slice.Start;

        // Quick check for '<fetch'
        if (slice.CurrentChar != '<' || slice.Length < 6)
            return BlockState.None;

        if (char.ToLower(text[start + 1]) != 'f' ||
            char.ToLower(text[start + 2]) != 'e' ||
            char.ToLower(text[start + 3]) != 't' ||
            char.ToLower(text[start + 4]) != 'c' ||
            char.ToLower(text[start + 5]) != 'h')
            return BlockState.None;

        var lookAheadLength = Math.Min(300, slice.Length);
        var lookAheadText = text.Substring(start, lookAheadLength);

        var match = FetchTagRegex.Match(lookAheadText);
        if (!match.Success)
            return BlockState.None;

        var url = match.Groups[1].Value;
        var pollFrequencyHours = int.Parse(match.Groups[2].Value);
        var transformLinks = match.Groups.Count > 3 &&
                            match.Groups[3].Success &&
                            match.Groups[3].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

        string fetchedContent = string.Empty;
        bool fetchSuccessful = false;

        if (_serviceProvider != null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<FetchMarkdownBlockParser>>();

                var result = fetchService.FetchMarkdownAsync(url, pollFrequencyHours, blogPostId: 0)
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
        }
        else
        {
            fetchedContent = "<!-- Markdown fetch service not configured -->";
        }

        var block = new FetchMarkdownBlock(this)
        {
            Url = url,
            PollFrequencyHours = pollFrequencyHours,
            FetchedContent = fetchedContent,
            FetchSuccessful = fetchSuccessful,
            Span = new SourceSpan(start, start + match.Length - 1)
        };

        processor.NewBlocks.Push(block);
        // Advance past the tag so the rest of the line is ignored
        slice.Start = start + match.Length;
        return BlockState.Break;
    }
}
