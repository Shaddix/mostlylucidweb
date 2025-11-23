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
                // Semantic results are already sorted by score
                return semanticResults.Select(r => new SearchResults(
                    r.Title,
                    r.Slug,
                    $"/blog/{r.Slug}",
                    r.Score
                )).ToList();
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - fall back to PostgreSQL
            Log.Logger.Warning(ex, "Semantic search failed, falling back to PostgreSQL full-text search");
        }

        // Fall back to PostgreSQL full-text search
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
        if (string.IsNullOrWhiteSpace(query))
            return new BasePagingModel<BlogPostDto>();

        using var activity = Log.Logger.StartActivity("HybridSearchWithPaging");
        activity.AddProperty("Query", query);
        activity.AddProperty("Page", page);
        activity.AddProperty("PageSize", pageSize);

        // Try semantic search first
        var semanticResults = await semanticSearchService.SearchAsync(query, pageSize * 2); // Get more for potential filtering

        if (semanticResults.Count > 0)
        {
            // Get full blog posts for the semantic results
            var slugs = semanticResults.Select(r => r.Slug).ToList();
            var now = DateTimeOffset.UtcNow;

            var posts = await context.BlogPosts
                .Include(x => x.Categories)
                .Include(x => x.LanguageEntity)
                .AsNoTracking()
                .Where(x => slugs.Contains(x.Slug)
                            && !x.IsHidden
                            && (x.ScheduledPublishDate == null || x.ScheduledPublishDate <= now)
                            && x.LanguageEntity.Name == "en")
                .ToListAsync();

            // Order by semantic search ranking
            var orderedPosts = slugs
                .Select(slug => posts.FirstOrDefault(p => p.Slug == slug))
                .Where(p => p != null)
                .Cast<BlogPostEntity>()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new BasePagingModel<BlogPostDto>
            {
                Data = orderedPosts.Select(x => x.ToDto()).ToList(),
                TotalItems = posts.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        // Fall back to PostgreSQL full-text search
        return await GetPosts(query, page, pageSize);
    }
}