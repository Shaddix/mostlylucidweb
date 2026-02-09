using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.Models.Blog;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.AI.Services;

/// <summary>
/// Service that filters blog articles to only AI-related content.
/// </summary>
public class AIArticleService : IAIArticleService
{
    private static readonly string[] AICategories =
    [
        "AI", "LLM", "RAG", "Semantic", "Ollama", "Qdrant",
        "Machine Learning", "OpenAI", "GPT", "Embeddings",
        "Vector", "NLP", "Claude", "Anthropic"
    ];

    private readonly IBlogViewService _blogViewService;
    private readonly ILogger<AIArticleService> _logger;

    public AIArticleService(IBlogViewService blogViewService, ILogger<AIArticleService> logger)
    {
        _blogViewService = blogViewService;
        _logger = logger;
    }

    public async Task<AIArticleListViewModel> GetArticlesAsync(int page = 1, int pageSize = 10, string? language = null)
    {
        language ??= MarkdownBaseService.EnglishLanguage;

        _logger.LogInformation("Fetching AI articles for page {Page}, pageSize {PageSize}, language {Language}",
            page, pageSize, language);

        // Get all posts that match any of the AI categories
        var allPosts = new List<PostListModel>();

        foreach (var category in AICategories)
        {
            var categoryPosts = await _blogViewService.GetPostsForRange(
                categories: [category],
                language: language);

            allPosts.AddRange(categoryPosts);
        }

        // Remove duplicates (posts can have multiple AI categories)
        var uniquePosts = allPosts
            .GroupBy(p => p.Slug)
            .Select(g => g.First())
            .OrderByDescending(p => p.PublishedDate)
            .ToList();

        // Apply pagination
        var totalItems = uniquePosts.Count;
        var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        var pagedPosts = uniquePosts
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Convert to shared PostListModel type
        var sharedPosts = pagedPosts.Select(p => new Mostlylucid.Shared.Models.Blog.PostListModel
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            PublishedDate = p.PublishedDate,
            UpdatedDate = p.UpdatedDate,
            Views = p.Views,
            Language = p.Language,
            Languages = p.Languages,
            Categories = p.Categories,
            WordCount = p.WordCount,
            Summary = p.Summary
        }).ToList();

        return new AIArticleListViewModel
        {
            Data = sharedPosts,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            LinkUrl = "/articles"
        };
    }

    public async Task<BlogPostDto?> GetArticleBySlugAsync(string slug, string? language = null)
    {
        language ??= MarkdownBaseService.EnglishLanguage;

        var post = await _blogViewService.GetPost(slug, language);

        if (post == null) return null;

        // Verify it's an AI-related post
        var hasAICategory = post.Categories?.Any(c =>
            AICategories.Any(ai => c.Equals(ai, StringComparison.OrdinalIgnoreCase))) ?? false;

        if (!hasAICategory)
        {
            _logger.LogWarning("Post {Slug} is not an AI-related article", slug);
            return null;
        }

        return new BlogPostDto
        {
            Id = post.Id ?? string.Empty,
            Title = post.Title,
            Slug = post.Slug,
            HtmlContent = post.HtmlContent,
            PlainTextContent = post.PlainTextContent,
            PublishedDate = post.PublishedDate,
            UpdatedDate = post.UpdatedDate,
            Language = post.Language,
            Categories = post.Categories ?? [],
            WordCount = post.WordCount,
            Languages = post.Languages ?? []
        };
    }
}
