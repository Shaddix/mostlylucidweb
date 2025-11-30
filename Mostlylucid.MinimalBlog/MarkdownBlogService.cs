using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Caching.Memory;

namespace Mostlylucid.MinimalBlog;

public partial class MarkdownBlogService(IConfiguration config, IMemoryCache cache)
{
    private readonly string _markdownPath = config["MarkdownPath"]
        ?? throw new InvalidOperationException("MarkdownPath not configured");

    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();

    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
    };

    public IReadOnlyList<BlogPost> GetAllPosts()
    {
        return cache.GetOrCreate("all_posts", entry =>
        {
            entry.SetOptions(CacheOptions);
            return LoadAllPosts();
        }) ?? [];
    }

    public BlogPost? GetPost(string slug)
    {
        return cache.GetOrCreate($"post_{slug}", entry =>
        {
            entry.SetOptions(CacheOptions);
            var filePath = Path.Combine(_markdownPath, $"{slug}.md");
            return File.Exists(filePath) ? ParseFile(filePath) : null;
        });
    }

    public IReadOnlyList<BlogPost> GetPostsByCategory(string category)
    {
        return cache.GetOrCreate($"category_{category.ToLowerInvariant()}", entry =>
        {
            entry.SetOptions(CacheOptions);
            return GetAllPosts()
                .Where(p => p.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }) ?? [];
    }

    public IReadOnlyList<string> GetAllCategories()
    {
        return cache.GetOrCreate("all_categories", entry =>
        {
            entry.SetOptions(CacheOptions);
            return GetAllPosts()
                .SelectMany(p => p.Categories)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();
        }) ?? [];
    }

    private List<BlogPost> LoadAllPosts()
    {
        if (!Directory.Exists(_markdownPath)) return [];

        return Directory.GetFiles(_markdownPath, "*.md", SearchOption.TopDirectoryOnly)
            .Where(f => Path.GetFileName(f).Count(c => c == '.') == 1) // Only base .md files
            .Select(ParseFile)
            .Where(p => p is { IsHidden: false })
            .OrderByDescending(p => p!.PublishedDate)
            .ToList()!;
    }

    private BlogPost? ParseFile(string filePath)
    {
        var markdown = File.ReadAllText(filePath);
        var slug = Path.GetFileNameWithoutExtension(filePath);
        var document = Markdown.Parse(markdown, _pipeline);

        // Extract title from first H1
        var title = document.Descendants<HeadingBlock>()
            .FirstOrDefault(h => h.Level == 1)?
            .Inline?.FirstChild?.ToString() ?? slug;

        // Extract categories: <!-- category -- Cat1, Cat2 -->
        var categoryMatch = CategoryRegex().Match(markdown);
        var categories = categoryMatch.Success
            ? categoryMatch.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries)
            : [];

        // Extract date: <datetime class="hidden">2024-01-01T00:00</datetime>
        var dateMatch = DateTimeRegex().Match(markdown);
        var publishedDate = dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var dt)
            ? dt : File.GetCreationTimeUtc(filePath);

        return new BlogPost
        {
            Slug = slug,
            Title = title,
            Categories = categories,
            PublishedDate = publishedDate,
            HtmlContent = Markdown.ToHtml(markdown, _pipeline),
            IsHidden = markdown.Contains("<hidden")
        };
    }

    [GeneratedRegex(@"<!--\s*category\s*--\s*(.+?)\s*-->", RegexOptions.IgnoreCase)]
    private static partial Regex CategoryRegex();

    [GeneratedRegex(@"<datetime[^>]*>(.+?)</datetime>", RegexOptions.IgnoreCase)]
    private static partial Regex DateTimeRegex();
}

public record BlogPost
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string[] Categories { get; init; }
    public required DateTime PublishedDate { get; init; }
    public required string HtmlContent { get; init; }
    public bool IsHidden { get; init; }
}
