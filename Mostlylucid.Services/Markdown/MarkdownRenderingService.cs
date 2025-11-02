using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Services.Markdown.MarkDigExtensions;
using Mostlylucid.Shared.Helpers;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Services.Markdown;

public class MarkdownRenderingService : MarkdownBaseService
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<MarkdownRenderingService>? _logger;

    public MarkdownRenderingService()
    {
        // Parameterless constructor for when DI is not available
    }

    public MarkdownRenderingService(IServiceProvider serviceProvider, ILogger<MarkdownRenderingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private static readonly Regex DateRegex = new(
        @"<datetime class=""hidden"">(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})</datetime>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
    private static readonly Regex CategoryRegex = new(@"<!--\s*category\s*--\s*(.+?)\s*-->", RegexOptions.Compiled);
    private static readonly Regex FetchTagRegex = new(
        @"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'][^>]*?/\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static string[] GetCategories(string markdownText)
    {
        var matches = CategoryRegex.Match(markdownText);
        if(matches.Success)
            return matches.Groups[1].Value.Split(',').Select(x => x.Trim()).ToArray();
        return Array.Empty<string>();
    }

    private static Regex SplitRegex => new(@"\r\n|\r|\n", RegexOptions.Compiled);
    public BlogPostDto GetPageFromMarkdown(string markdown, DateTime publishedDate, string filePath)
    {
        var pipeline = Pipeline();
        var lines =  SplitRegex.Split(markdown);
        // Get the title from the first line
        var title = lines.Length > 0 ? Markdig.Markdown.ToPlainText(lines[0].Trim()) : string.Empty;

        title = title.Trim();
        // Concatenate the rest of the lines with newline characters
        var restOfTheLines = string.Join(Environment.NewLine, lines.Skip(1));

        // Extract categories from the text
        var categories = GetCategories(restOfTheLines);

        var publishDate = DateRegex.Match(restOfTheLines).Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(publishDate))
            publishedDate = DateTime.ParseExact(publishDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

        // Remove category tags from the text
        restOfTheLines = CategoryRegex.Replace(restOfTheLines, "");
        restOfTheLines = DateRegex.Replace(restOfTheLines, "");

        // Pre-process fetch tags if service provider is available (fallback for direct calls)
        if (_serviceProvider != null && _logger != null)
        {
            restOfTheLines = PreProcessFetchTags(restOfTheLines);
        }

        // Process the rest of the lines as either HTML or plain text
        var processed = Markdig.Markdown.ToHtml(restOfTheLines, pipeline);
        var plainText = Markdig.Markdown.ToPlainText(restOfTheLines, pipeline);

        // Generate the slug from the page filename
        var slug = GetSlug(filePath);

        // Return the parsed and processed content
        return new BlogPostDto()
        {
            Markdown =  markdown,
            Categories = categories,
            WordCount = restOfTheLines.WordCount(),
            HtmlContent = processed,
            PlainTextContent = plainText,
            PublishedDate = publishedDate,
            Slug = slug,
            Title = title
        };
    }

    private string PreProcessFetchTags(string markdown)
    {
        if (_serviceProvider == null || _logger == null)
            return markdown;

        return FetchTagRegex.Replace(markdown, match =>
        {
            var url = match.Groups[1].Value;
            var pollFrequency = int.Parse(match.Groups[2].Value);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();

                var result = fetchService.FetchMarkdownAsync(url, pollFrequency, blogPostId: 0)
                    .GetAwaiter()
                    .GetResult();

                if (result.Success && !string.IsNullOrWhiteSpace(result.Content))
                {
                    return result.Content;
                }
                else
                {
                    _logger.LogWarning("Failed to fetch markdown from {Url}: {Error}", url, result.ErrorMessage);
                    return $"<!-- Failed to fetch content from {url}: {result.ErrorMessage} -->";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching markdown from {Url}", url);
                return $"<!-- Error fetching content from {url}: {ex.Message} -->";
            }
        });
    }

    private string GetSlug(string fileName)
    {
        var slug = Path.GetFileNameWithoutExtension(fileName);
        if (slug.Contains(".")) slug = slug.Substring(0, slug.IndexOf(".", StringComparison.Ordinal));

        return slug.ToLowerInvariant();
    }
}