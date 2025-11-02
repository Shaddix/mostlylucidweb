using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers.Html.Inlines;
using Microsoft.Extensions.DependencyInjection;

namespace Mostlylucid.Services.Markdown.MarkDigExtensions;

/// <summary>
/// Markdig extension for fetching remote markdown content
/// Syntax: &lt;fetch markdownurl="url" pollfrequency="12h"/&gt;
/// </summary>
public class FetchMarkdownExtension : IMarkdownExtension
{
    // Static service provider for dependency resolution
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Configure the service provider for the extension
    /// Call this during application startup
    /// </summary>
    public static void ConfigureServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.InlineParsers.Contains<FetchMarkdownInlineParser>())
        {
            // Insert before HTML inline parser to ensure we process fetch tags before they're treated as HTML
            pipeline.InlineParsers.Insert(0, new FetchMarkdownInlineParser(_serviceProvider));
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
        {
            if (!htmlRenderer.ObjectRenderers.Contains<FetchMarkdownInlineRenderer>())
            {
                htmlRenderer.ObjectRenderers.InsertBefore<LinkInlineRenderer>(
                    new FetchMarkdownInlineRenderer());
            }
        }
    }
}

/// <summary>
/// Custom inline element to represent a fetch directive
/// </summary>
public class FetchMarkdownInline : Inline
{
    public string Url { get; set; } = string.Empty;
    public int PollFrequencyHours { get; set; }
    public string FetchedContent { get; set; } = string.Empty;
    public bool FetchSuccessful { get; set; }
}

/// <summary>
/// Parser for the &lt;fetch&gt; tag
/// </summary>
public class FetchMarkdownInlineParser : InlineParser
{
    private static readonly Regex FetchTagRegex = new(
        @"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'][^>]*?/\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        var match = FetchTagRegex.Match(lookAheadText);
        if (!match.Success)
            return false;

        var url = match.Groups[1].Value;
        var pollFrequencyHours = int.Parse(match.Groups[2].Value);

        // Try to fetch the content
        string fetchedContent = string.Empty;
        bool fetchSuccessful = false;

        if (_serviceProvider != null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<FetchMarkdownInlineParser>>();

                // Attempt synchronous fetch (with caching)
                // Note: blogPostId is 0 here since we don't have context. The background service will update later.
                var result = fetchService.FetchMarkdownAsync(url, pollFrequencyHours, blogPostId: 0)
                    .GetAwaiter()
                    .GetResult();

                if (result.Success)
                {
                    fetchedContent = result.Content;
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
            fetchedContent = $"<!-- Markdown fetch service not configured -->";
        }

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

/// <summary>
/// Renderer for the fetch markdown inline element
/// </summary>
public class FetchMarkdownInlineRenderer : HtmlObjectRenderer<FetchMarkdownInline>
{
    protected override void Write(HtmlRenderer renderer, FetchMarkdownInline obj)
    {
        if (obj.FetchSuccessful && !string.IsNullOrWhiteSpace(obj.FetchedContent))
        {
            // Parse the fetched markdown content and convert to HTML
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            var html = Markdig.Markdown.ToHtml(obj.FetchedContent, pipeline);

            // Write the HTML directly to the writer (not escaped)
            renderer.Writer.Write(html);
        }
        else
        {
            // Render comment or fallback (already HTML comment)
            renderer.Write(obj.FetchedContent);
        }
    }
}

/// <summary>
/// Service interface for fetching remote markdown
/// </summary>
public interface IMarkdownFetchService
{
    Task<MarkdownFetchResult> FetchMarkdownAsync(string url, int pollFrequencyHours, int blogPostId);
}

/// <summary>
/// Result of fetching markdown
/// </summary>
public class MarkdownFetchResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
