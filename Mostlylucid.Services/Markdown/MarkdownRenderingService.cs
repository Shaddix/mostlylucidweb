using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Shared.Helpers;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Services.Markdown;

public partial class MarkdownRenderingService : MarkdownBaseService
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

    [System.Text.RegularExpressions.GeneratedRegex(@"<datetime class=""hidden"">(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})</datetime>", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex DateRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"<!--\s*category\s*--\s*(.+?)\s*-->")]
    private static partial Regex CategoryRegex();

    private static string[] GetCategories(string markdownText)
    {
        var matches = CategoryRegex().Match(markdownText);
        if(matches.Success)
            return matches.Groups[1].Value.Split(',').Select(x => x.Trim()).ToArray();
        return Array.Empty<string>();
    }

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex SplitRegex();

    public BlogPostDto GetPageFromMarkdown(string markdown, DateTime publishedDate, string filePath)
    {
        return GetPageFromMarkdown(markdown, publishedDate, filePath, sourceUrl: null);
    }

    public BlogPostDto GetPageFromMarkdown(string markdown, DateTime publishedDate, string filePath, string? sourceUrl)
    {
        // Preprocess markdown to inject fetched content BEFORE parsing
        // This ensures everything goes through the pipeline once
        if (_serviceProvider != null)
        {
            var preprocessor = new Mostlylucid.Markdig.FetchExtension.MarkdownFetchPreprocessor(_serviceProvider);
            markdown = preprocessor.Preprocess(markdown);
        }

        // Use RemoteLinkRewriteExtension if we have a source URL (for fetched content)
        var pipeline = string.IsNullOrEmpty(sourceUrl)
            ? Pipeline()
            : Pipeline(builder =>
            {
                var extension = new MarkDigExtensions.RemoteLinkRewriteExtension(sourceUrl);
                builder.Extensions.Add(extension);
            });

        var lines =  SplitRegex().Split(markdown);
        // Get the title from the first line
        var title = lines.Length > 0 ? global::Markdig.Markdown.ToPlainText(lines[0].Trim()) : string.Empty;

        title = title.Trim();
        // Concatenate the rest of the lines with newline characters
        var restOfTheLines = string.Join(Environment.NewLine, lines.Skip(1));

        // Extract categories from the text
        var categories = GetCategories(restOfTheLines);

        var publishDate = DateRegex().Match(restOfTheLines).Groups[1].Value;
        if (!string.IsNullOrWhiteSpace(publishDate))
            publishedDate = DateTime.ParseExact(publishDate, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

        // Remove category tags from the text
        restOfTheLines = CategoryRegex().Replace(restOfTheLines, "");
        restOfTheLines = DateRegex().Replace(restOfTheLines, "");

        // Process the rest of the lines as either HTML or plain text
        var processed = global::Markdig.Markdown.ToHtml(restOfTheLines, pipeline);
        var plainText = global::Markdig.Markdown.ToPlainText(restOfTheLines, pipeline);

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

    private string GetSlug(string fileName)
    {
        var slug = Path.GetFileNameWithoutExtension(fileName);
        if (slug.Contains(".")) slug = slug.Substring(0, slug.IndexOf(".", StringComparison.Ordinal));
        if(slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            slug = slug[..^3];
        
        // Normalize slug: convert underscores/spaces to hyphens and lowercase for consistent lookup
        slug = slug.Replace('_', '-').Replace(' ', '-').ToLowerInvariant();
        return slug.ToLowerInvariant();
    }
}