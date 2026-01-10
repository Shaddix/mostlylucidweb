using Microsoft.EntityFrameworkCore;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Mapper;
using Mostlylucid.Shared.Models;
using Serilog;
using SerilogTracing;

namespace Mostlylucid.Services.Blog;

public class BlogSearchService(
    MostlylucidDbContext context,
    ISemanticSearchService semanticSearchService,
    SearchRanker? searchRanker = null,
    SearchQueryParser? queryParser = null)
{
    private readonly SearchRanker _ranker = searchRanker ?? new SearchRanker();
    private readonly SearchQueryParser _queryParser = queryParser ?? new SearchQueryParser();

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
        // Lowercase the query for PostgreSQL full-text search (case-insensitive)
        var lowerQuery = processedQuery.ToLower();
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
                     lowerQuery)) // Use precomputed SearchVector for title and content
                 || x.Categories.Any(c =>
                     EF.Functions.ToTsVector("english", c.Name)
                         .Matches(EF.Functions.WebSearchToTsQuery("english", lowerQuery)))) // Search in categories
                && x.LanguageEntity.Name == "en") // Filter by language
            .OrderByDescending(x =>
                // Rank based on the precomputed SearchVector
                x.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english",
                    lowerQuery)));
    }

    private IQueryable<BlogPostEntity> QueryForWildCard(string query)
    {
        var now = DateTimeOffset.UtcNow;
        // Lowercase the query for PostgreSQL full-text search (case-insensitive)
        var lowerQuery = query.ToLower();
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
                     lowerQuery + ":*")) // Use precomputed SearchVector for title and content
                 || x.Categories.Any(c =>
                     EF.Functions.ToTsVector("english", c.Name)
                         .Matches(EF.Functions.ToTsQuery("english", lowerQuery + ":*")))) // Search in categories
                && x.LanguageEntity.Name == "en") // Filter by language
            .OrderByDescending(x =>
                // Rank based on the precomputed SearchVector
                x.SearchVector.Rank(EF.Functions.ToTsQuery("english",
                    lowerQuery + ":*"))); // Use precomputed SearchVector for ranking
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
                // Deduplicate semantic results by slug (keep highest score)
                var uniqueResults = semanticResults
                    .GroupBy(r => r.Slug)
                    .Select(g => g.OrderByDescending(r => r.Score).First())
                    .ToList();

                // Get slugs from semantic search, look up English titles from PostgreSQL
                var slugs = uniqueResults.Select(r => r.Slug).ToList();
                var posts = await context.BlogPosts
                    .Include(p => p.LanguageEntity)
                    .Where(p => slugs.Contains(p.Slug) && p.LanguageEntity.Name == "en")
                    .Select(p => new { p.Slug, p.Title })
                    .ToListAsync();

                // Match back with semantic scores, preserving order
                // Only return results that have an English version in the database
                return uniqueResults
                    .Select(r =>
                    {
                        var post = posts.FirstOrDefault(p => p.Slug == r.Slug);
                        return post != null
                            ? new SearchResults(post.Title, r.Slug, $"/blog/{r.Slug}", r.Score)
                            : null;
                    })
                    .Where(r => r != null)
                    .Cast<SearchResults>()
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
    /// If no results found, returns recent posts as suggestions
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
        var noMatchFound = false;

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
                    // Deduplicate semantic results by slug (keep highest score)
                    var uniqueSemanticResults = semanticResults
                        .GroupBy(r => r.Slug)
                        .Select(g => g.OrderByDescending(r => r.Score).First())
                        .ToDictionary(r => r.Slug, r => r.Score);

                    IEnumerable<BlogPostEntity> orderedPosts;

                    // Use RRF fusion for relevance ordering
                    if (order == "relevance" || order == "relevance_desc" || string.IsNullOrEmpty(order))
                    {
                        // Get PostgreSQL BM25 results for RRF fusion
                        try
                        {
                            var pgResults = await GetPostsWithFiltersInternal(query, targetLanguage, startDate, endDate, pageSize * 2);

                            // Prepare vector results (from semantic search) with scores
                            var vectorResults = posts
                                .Select(p => (
                                    Post: p.ToDto(),
                                    Score: uniqueSemanticResults.TryGetValue(p.Slug, out var score) ? score : 0f
                                ))
                                .ToList();

                            // Prepare BM25 results
                            var bm25Results = pgResults.Select(p => p.ToDto()).ToList();

                            // Fuse using RRF with category/freshness boosts
                            var fusedDtos = _ranker.FuseResults(bm25Results, vectorResults, query);

                            var pagedDtos = fusedDtos
                                .Skip((page - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

                            return new BasePagingModel<BlogPostDto>
                            {
                                Data = pagedDtos,
                                TotalItems = fusedDtos.Count,
                                Page = page,
                                PageSize = pageSize
                            };
                        }
                        catch (Exception ex)
                        {
                            // Fall back to simple semantic ordering if RRF fusion fails
                            Log.Logger.Warning(ex, "RRF fusion failed, falling back to simple semantic ranking");
                            orderedPosts = posts.OrderByDescending(p =>
                                uniqueSemanticResults.TryGetValue(p.Slug, out var score) ? score : 0f);
                        }
                    }
                    else
                    {
                        // Non-relevance orderings use simple sorting
                        orderedPosts = order switch
                        {
                            "date_asc" => posts.OrderBy(p => p.PublishedDate),
                            "date_desc" => posts.OrderByDescending(p => p.PublishedDate),
                            "title_asc" => posts.OrderBy(p => p.Title),
                            "title_desc" => posts.OrderByDescending(p => p.Title),
                            _ => posts.OrderByDescending(p =>
                                uniqueSemanticResults.TryGetValue(p.Slug, out var score) ? score : 0f)
                        };
                    }

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
            var pgResults = await GetPostsWithFilters(query, targetLanguage, startDate, endDate, page, pageSize, order);

            // If no results found, mark it so we can return recent posts as suggestions
            if (pgResults.TotalItems == 0)
            {
                noMatchFound = true;
            }
            else
            {
                return pgResults;
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "PostgreSQL full-text search failed for query '{Query}'", query);
            noMatchFound = true;
        }

        // No match found - return recent posts as suggestions
        if (noMatchFound)
        {
            Log.Logger.Information("No search results for '{Query}', returning recent posts as suggestions", query);
            return await GetRecentPostsAsSuggestions(targetLanguage, startDate, endDate, page, pageSize);
        }

        return new BasePagingModel<BlogPostDto>();
    }

    /// <summary>
    /// Internal method to get filtered posts without paging (for RRF fusion).
    /// </summary>
    private async Task<List<BlogPostEntity>> GetPostsWithFiltersInternal(
        string query,
        string language,
        DateTime? startDate,
        DateTime? endDate,
        int limit = 50)
    {
        var orderedQuery = BuildSearchQuery(query, language, startDate, endDate, "relevance");
        return await orderedQuery.Take(limit).ToListAsync();
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
        var orderedQuery = BuildSearchQuery(query, language, startDate, endDate, order);

        var totalPosts = await orderedQuery.CountAsync();
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

    /// <summary>
    /// Build the PostgreSQL full-text search query with filters and ordering.
    /// Supports Google-style search operators: quoted phrases, -excluded terms, wildcards
    /// </summary>
    private IOrderedQueryable<BlogPostEntity> BuildSearchQuery(
        string query,
        string language,
        DateTime? startDate,
        DateTime? endDate,
        string order)
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

        // Parse the query for special operators
        var parsed = _queryParser.Parse(query);

        // Build the search query based on parsed components
        IQueryable<BlogPostEntity> searchQuery = baseQuery;

        // Handle phrases (exact substring matching)
        foreach (var phrase in parsed.Phrases)
        {
            var lowerPhrase = phrase.ToLower();
            searchQuery = searchQuery.Where(x =>
                EF.Functions.ILike(x.Title, $"%{phrase}%")
                || EF.Functions.ILike(x.PlainTextContent, $"%{phrase}%")
                || x.Categories.Any(c => EF.Functions.ILike(c.Name, $"%{phrase}%")));
        }

        // Build tsquery for include terms and wildcards
        var tsQuery = _queryParser.BuildTsQuery(parsed);

        // Apply full-text search if we have terms
        if (!string.IsNullOrWhiteSpace(tsQuery))
        {
            searchQuery = searchQuery.Where(x =>
                x.SearchVector.Matches(EF.Functions.ToTsQuery("english", tsQuery))
                || x.Categories.Any(c =>
                    EF.Functions.ToTsVector("english", c.Name)
                        .Matches(EF.Functions.ToTsQuery("english", tsQuery))));
        }

        // Handle acronyms with case-insensitive substring search
        var acronymTerms = parsed.IncludeTerms
            .Concat(parsed.WildcardTerms)
            .Where(t => _queryParser.IsAcronymLike(t))
            .ToList();

        foreach (var acronym in acronymTerms)
        {
            searchQuery = searchQuery.Where(x =>
                EF.Functions.ILike(x.Title, $"%{acronym}%")
                || EF.Functions.ILike(x.PlainTextContent, $"%{acronym}%"));
        }

        // Handle excluded terms (must NOT contain these)
        foreach (var excludeTerm in parsed.ExcludeTerms)
        {
            searchQuery = searchQuery.Where(x =>
                !EF.Functions.ILike(x.Title, $"%{excludeTerm}%")
                && !EF.Functions.ILike(x.PlainTextContent, $"%{excludeTerm}%")
                && !x.Categories.Any(c => EF.Functions.ILike(c.Name, $"%{excludeTerm}%")));
        }

        // Apply ordering - relevance uses ts_rank, others use column values
        IOrderedQueryable<BlogPostEntity> orderedQuery;
        if (order == "relevance" || order == "relevance_desc")
        {
            // Order by full-text search rank (how well the query matches)
            // Only rank if we have a tsquery, otherwise use date
            if (!string.IsNullOrWhiteSpace(tsQuery))
            {
                orderedQuery = searchQuery.OrderByDescending(x =>
                    x.SearchVector.Rank(EF.Functions.ToTsQuery("english", tsQuery)));
            }
            else
            {
                // No ts_rank available (only phrases/excluded terms), sort by date
                orderedQuery = searchQuery.OrderByDescending(x => x.PublishedDate);
            }
        }
        else
        {
            orderedQuery = order switch
            {
                "date_asc" => searchQuery.OrderBy(x => x.PublishedDate),
                "date_desc" => searchQuery.OrderByDescending(x => x.PublishedDate),
                "title_asc" => searchQuery.OrderBy(x => x.Title),
                "title_desc" => searchQuery.OrderByDescending(x => x.Title),
                _ => !string.IsNullOrWhiteSpace(tsQuery)
                    ? searchQuery.OrderByDescending(x => x.SearchVector.Rank(EF.Functions.ToTsQuery("english", tsQuery)))
                    : searchQuery.OrderByDescending(x => x.PublishedDate)
            };
        }

        return orderedQuery;
    }

    /// <summary>
    /// Get recent posts as suggestions when search returns no results.
    /// Returns posts ordered by date descending (most recent first).
    /// </summary>
    private async Task<BasePagingModel<BlogPostDto>> GetRecentPostsAsSuggestions(
        string language,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize)
    {
        var now = DateTimeOffset.UtcNow;
        var query = context.BlogPosts
            .Include(x => x.Categories)
            .Include(x => x.LanguageEntity)
            .AsNoTracking()
            .Where(x =>
                !x.IsHidden
                && (x.ScheduledPublishDate == null || x.ScheduledPublishDate <= now)
                && x.LanguageEntity.Name == language);

        // Apply date filters
        if (startDate.HasValue)
            query = query.Where(x => x.PublishedDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(x => x.PublishedDate <= endDate.Value);

        var totalPosts = await query.CountAsync();
        var results = await query
            .OrderByDescending(x => x.PublishedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new BasePagingModel<BlogPostDto>
        {
            Data = results.Select(x => x.ToDto()).ToList(),
            TotalItems = totalPosts,
            Page = page,
            PageSize = pageSize,
            NoMatchFound = true
        };
    }
}