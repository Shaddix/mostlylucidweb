using Microsoft.EntityFrameworkCore;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Mapper;
using Mostlylucid.Shared.Models;
using Serilog;
using SerilogTracing;

namespace Mostlylucid.Services.Blog;

public class BlogSearchService(MostlylucidDbContext context, ISemanticSearchService semanticSearchService)
{
    public async Task<List<string>> GetAvailableLanguagesAsync()
    {
        return await context.Languages
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
    }

    public async Task<BasePagingModel<BlogPostDto>> GetPosts(string? query, int page = 1, int pageSize = 10)
    {
       using var activity = Log.Logger.StartActivity("GetPosts");
       activity.AddProperty("Query", query);
        activity.AddProperty("Page", page);
        activity.AddProperty("PageSize", pageSize);
        if(string.IsNullOrEmpty(query))
        {
            return new BasePagingModel<BlogPostDto>();
        }
        IQueryable<BlogPostEntity> blogPostQuery = query.Contains(" ") ? QueryForSpaces(query) : QueryForWildCard(query);
        var totalPosts = await blogPostQuery.CountAsync();
        var results = await blogPostQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return new BasePagingModel<BlogPostDto>()
        {
            Data = results.Select(x => x.ToDto()).ToList(),
            TotalItems = totalPosts,
            Page = page,
            PageSize = pageSize
        };
        
    }

    private IQueryable<BlogPostEntity> QueryForSpaces(string processedQuery)
    {
        var now = DateTimeOffset.UtcNow;
        return context.BlogPosts
            .Include(x => x.Categories)
            .Include(x => x.LanguageEntity)
            .AsNoTracking()
            //.AsSplitQuery()
            .Where(x =>
                // Filter out hidden and scheduled posts
                !x.IsHidden
                && (x.ScheduledPublishDate == null || x.ScheduledPublishDate <= now)
                // Search using the precomputed SearchVector
                && (x.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("english",
                     processedQuery)) // Use precomputed SearchVector for title and content
                 || x.Categories.Any(c =>
                     EF.Functions.ToTsVector("english", c.Name)
                         .Matches(EF.Functions.WebSearchToTsQuery("english", processedQuery)))) // Search in categories
                && x.LanguageEntity.Name == "en") // Filter by language
            .OrderByDescending(x =>
                // Rank based on the precomputed SearchVector
                x.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english",
                    processedQuery)));
    }

    private IQueryable<BlogPostEntity> QueryForWildCard(string query)
    {
        var now = DateTimeOffset.UtcNow;
        return context.BlogPosts
            .Include(x => x.Categories)
            .Include(x => x.LanguageEntity)
            .AsNoTracking()
            //.AsSplitQuery()
            .Where(x =>
                // Filter out hidden and scheduled posts
                !x.IsHidden
                && (x.ScheduledPublishDate == null || x.ScheduledPublishDate <= now)
                // Search using the precomputed SearchVector
                && (x.SearchVector.Matches(EF.Functions.ToTsQuery("english",
                     query + ":*")) // Use precomputed SearchVector for title and content
                 || x.Categories.Any(c =>
                     EF.Functions.ToTsVector("english", c.Name)
                         .Matches(EF.Functions.ToTsQuery("english", query + ":*")))) // Search in categories
                && x.LanguageEntity.Name == "en") // Filter by language
            .OrderByDescending(x =>
                // Rank based on the precomputed SearchVector
                x.SearchVector.Rank(EF.Functions.ToTsQuery("english",
                    query + ":*"))); // Use precomputed SearchVector for ranking
    }
    
    public async Task<List<(string Title, string Slug)>> GetSearchResultForQuery(string query)
    {
        var processedQuery = query;
        var posts = await QueryForSpaces(processedQuery)
            .Select(x => new { x.Title, x.Slug, })
            .Take(5)
            .ToListAsync();
        return posts.Select(x => (x.Title, x.Slug)).ToList();
    }

    
    
    public async Task<List<(string Title, string Slug)>> GetSearchResultForComplete(string query)
    {
        var posts = await QueryForWildCard(query) 
            .Select(x => new { x.Title, x.Slug, })
            .Take(5)
            .ToListAsync();
        return posts.Select(x => (x.Title, x.Slug)).ToList();
    }

    public record SearchResults(string Title, string Slug, string Url, float Score = 1.0f);

    /// <summary>
    /// Hybrid search: tries semantic search first, falls back to PostgreSQL full-text search
    /// Results are ordered by match quality (score)
    /// </summary>
    public async Task<List<SearchResults>> HybridSearchAsync(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResults>();

        // Try semantic search first (with graceful fallback on error)
        try
        {
            var semanticResults = await semanticSearchService.SearchAsync(query, limit);

            if (semanticResults.Count > 0)
            {
                // Get slugs from semantic search, look up titles from PostgreSQL
                var slugs = semanticResults.Select(r => r.Slug).ToList();
                var posts = await context.BlogPosts
                    .Where(p => slugs.Contains(p.Slug))
                    .Select(p => new { p.Slug, p.Title })
                    .ToListAsync();

                // Match back with semantic scores, preserving order
                return semanticResults
                    .Select(r =>
                    {
                        var post = posts.FirstOrDefault(p => p.Slug == r.Slug);
                        return new SearchResults(
                            post?.Title ?? r.Slug,
                            r.Slug,
                            $"/blog/{r.Slug}",
                            r.Score
                        );
                    })
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - fall back to PostgreSQL
            Log.Logger.Warning(ex, "Semantic search failed, falling back to PostgreSQL full-text search");
        }

        // Fall back to PostgreSQL full-text search
        try
        {
            var pgResults = query.Contains(" ")
                ? await GetSearchResultForQueryWithLimit(query, limit)
                : await GetSearchResultForCompleteWithLimit(query, limit);

            // PostgreSQL results are already ranked by ts_rank
            return pgResults.Select((r, index) => new SearchResults(
                r.Title,
                r.Slug,
                $"/blog/{r.Slug}",
                1.0f - (index * 0.05f) // Approximate score based on ranking
            )).ToList();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "PostgreSQL full-text search failed for query '{Query}'", query);
            return new List<SearchResults>();
        }
    }

    private async Task<List<(string Title, string Slug)>> GetSearchResultForQueryWithLimit(string query, int limit)
    {
        var posts = await QueryForSpaces(query)
            .Select(x => new { x.Title, x.Slug })
            .Take(limit)
            .ToListAsync();
        return posts.Select(x => (x.Title, x.Slug)).ToList();
    }

    private async Task<List<(string Title, string Slug)>> GetSearchResultForCompleteWithLimit(string query, int limit)
    {
        var posts = await QueryForWildCard(query)
            .Select(x => new { x.Title, x.Slug })
            .Take(limit)
            .ToListAsync();
        return posts.Select(x => (x.Title, x.Slug)).ToList();
    }

    /// <summary>
    /// Hybrid search with paging: tries semantic search first, falls back to PostgreSQL
    /// </summary>
    public async Task<BasePagingModel<BlogPostDto>> HybridSearchWithPagingAsync(string? query, int page = 1, int pageSize = 10)
    {
        return await HybridSearchWithPagingAsync(query, null, null, null, page, pageSize);
    }

    /// <summary>
    /// Hybrid search with paging and filters: tries semantic search first, falls back to PostgreSQL
    /// </summary>
    public async Task<BasePagingModel<BlogPostDto>> HybridSearchWithPagingAsync(
        string? query,
        string? language,
        DateTime? startDate,
        DateTime? endDate,
        int page = 1,
        int pageSize = 10,
        string order = "date_desc")
    {
        if (string.IsNullOrWhiteSpace(query))
            return new BasePagingModel<BlogPostDto>();

        using var activity = Log.Logger.StartActivity("HybridSearchWithPaging");
        activity.AddProperty("Query", query);
        activity.AddProperty("Language", language ?? "all");
        activity.AddProperty("Page", page);
        activity.AddProperty("PageSize", pageSize);
        activity.AddProperty("Order", order);

        var targetLanguage = string.IsNullOrEmpty(language) ? "en" : language;

        // Try semantic search first (with fallback to PostgreSQL on any failure)
        try
        {
            var semanticResults = await semanticSearchService.SearchAsync(query, pageSize * 3); // Get more for potential filtering

            if (semanticResults.Count > 0)
            {
                // Get full blog posts for the semantic results
                var slugs = semanticResults.Select(r => r.Slug).ToList();
                var now = DateTimeOffset.UtcNow;

                var postsQuery = context.BlogPosts
                    .Include(x => x.Categories)
                    .Include(x => x.LanguageEntity)
                    .AsNoTracking()
                    .Where(x => slugs.Contains(x.Slug)
                                && !x.IsHidden
                                && (x.ScheduledPublishDate == null || x.ScheduledPublishDate <= now)
                                && x.LanguageEntity.Name == targetLanguage);

                // Apply date filters
                if (startDate.HasValue)
                    postsQuery = postsQuery.Where(x => x.PublishedDate >= startDate.Value);
                if (endDate.HasValue)
                    postsQuery = postsQuery.Where(x => x.PublishedDate <= endDate.Value);

                var posts = await postsQuery.ToListAsync();

                // Only use semantic results if we actually found matching posts
                if (posts.Count > 0)
                {
                    // Apply ordering
                    IEnumerable<BlogPostEntity> orderedPosts = order switch
                    {
                        "date_asc" => posts.OrderBy(p => p.PublishedDate),
                        "title_asc" => posts.OrderBy(p => p.Title),
                        "title_desc" => posts.OrderByDescending(p => p.Title),
                        _ => posts.OrderByDescending(p => p.PublishedDate) // date_desc default
                    };

                    var pagedPosts = orderedPosts
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    return new BasePagingModel<BlogPostDto>
                    {
                        Data = pagedPosts.Select(x => x.ToDto()).ToList(),
                        TotalItems = posts.Count,
                        Page = page,
                        PageSize = pageSize
                    };
                }

                // Semantic search returned results but none matched DB filters - fall through to PostgreSQL
                Log.Logger.Debug("Semantic search returned {Count} results but none matched DB filters, falling back to PostgreSQL", semanticResults.Count);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - fall back to PostgreSQL
            Log.Logger.Warning(ex, "Semantic search failed for query '{Query}', falling back to PostgreSQL full-text search", query);
        }

        // Fall back to PostgreSQL full-text search with filters
        try
        {
            return await GetPostsWithFilters(query, targetLanguage, startDate, endDate, page, pageSize, order);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "PostgreSQL full-text search failed for query '{Query}'", query);
            return new BasePagingModel<BlogPostDto>();
        }
    }

    private async Task<BasePagingModel<BlogPostDto>> GetPostsWithFilters(
        string query,
        string language,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        string order = "date_desc")
    {
        var now = DateTimeOffset.UtcNow;
        var baseQuery = context.BlogPosts
            .Include(x => x.Categories)
            .Include(x => x.LanguageEntity)
            .AsNoTracking()
            .Where(x =>
                !x.IsHidden
                && (x.ScheduledPublishDate == null || x.ScheduledPublishDate <= now)
                && x.LanguageEntity.Name == language);

        // Apply date filters
        if (startDate.HasValue)
            baseQuery = baseQuery.Where(x => x.PublishedDate >= startDate.Value);
        if (endDate.HasValue)
            baseQuery = baseQuery.Where(x => x.PublishedDate <= endDate.Value);

        // Apply text search filter
        IQueryable<BlogPostEntity> searchQuery;
        if (query.Contains(" "))
        {
            searchQuery = baseQuery.Where(x =>
                x.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("english", query))
                || x.Categories.Any(c =>
                    EF.Functions.ToTsVector("english", c.Name)
                        .Matches(EF.Functions.WebSearchToTsQuery("english", query))));
        }
        else
        {
            searchQuery = baseQuery.Where(x =>
                x.SearchVector.Matches(EF.Functions.ToTsQuery("english", query + ":*"))
                || x.Categories.Any(c =>
                    EF.Functions.ToTsVector("english", c.Name)
                        .Matches(EF.Functions.ToTsQuery("english", query + ":*"))));
        }

        // Apply ordering
        IOrderedQueryable<BlogPostEntity> orderedQuery = order switch
        {
            "date_asc" => searchQuery.OrderBy(x => x.PublishedDate),
            "title_asc" => searchQuery.OrderBy(x => x.Title),
            "title_desc" => searchQuery.OrderByDescending(x => x.Title),
            _ => searchQuery.OrderByDescending(x => x.PublishedDate) // date_desc default
        };

        var totalPosts = await searchQuery.CountAsync();
        var results = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new BasePagingModel<BlogPostDto>
        {
            Data = results.Select(x => x.ToDto()).ToList(),
            TotalItems = totalPosts,
            Page = page,
            PageSize = pageSize
        };
    }
}