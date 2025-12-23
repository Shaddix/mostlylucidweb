using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Embeddings;

namespace Mostlylucid.SegmentCommerce.Services.Search;

/// <summary>
/// Unified search service combining semantic search (embeddings) and PostgreSQL full-text search.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Perform a hybrid search combining semantic and keyword search.
    /// </summary>
    Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get search suggestions/autocomplete.
    /// </summary>
    Task<IEnumerable<string>> GetSuggestionsAsync(string prefix, int limit = 5, CancellationToken cancellationToken = default);
}

public class SearchService : ISearchService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        SegmentCommerceDbContext db,
        IEmbeddingService embeddingService,
        ILogger<SearchService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<SearchResults> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new SearchResults { Items = [], TotalCount = 0 };
        }

        var query = request.Query.Trim();
        var results = new List<SearchResultItem>();
        
        // Run both searches in parallel when semantic is enabled
        Task<List<SearchResultItem>>? semanticTask = null;
        
        if (request.EnableSemantic && IsEmbeddingServiceAvailable())
        {
            semanticTask = PerformSemanticSearchAsync(query, request.Limit * 2, cancellationToken);
        }
        
        // Full-text search (always enabled as fallback)
        var ftsResults = await PerformFullTextSearchAsync(query, request, cancellationToken);
        results.AddRange(ftsResults);

        // Merge semantic results if available
        if (semanticTask != null)
        {
            try
            {
                var semanticResults = await semanticTask;
                results = MergeResults(results, semanticResults, request.Limit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Semantic search failed, using FTS only");
            }
        }

        // Apply category filter
        if (!string.IsNullOrEmpty(request.Category))
        {
            results = results.Where(r => r.Category.Equals(request.Category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Apply price range filter
        if (request.MinPrice.HasValue)
        {
            results = results.Where(r => r.Price >= request.MinPrice.Value).ToList();
        }
        if (request.MaxPrice.HasValue)
        {
            results = results.Where(r => r.Price <= request.MaxPrice.Value).ToList();
        }

        // Apply sorting
        results = request.SortBy?.ToLowerInvariant() switch
        {
            "price_asc" => results.OrderBy(r => r.Price).ToList(),
            "price_desc" => results.OrderByDescending(r => r.Price).ToList(),
            "name" => results.OrderBy(r => r.Name).ToList(),
            "newest" => results.OrderByDescending(r => r.CreatedAt).ToList(),
            _ => results.OrderByDescending(r => r.Score).ToList() // Default: relevance
        };

        var totalCount = results.Count;
        var pagedResults = results
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToList();

        return new SearchResults
        {
            Items = pagedResults,
            TotalCount = totalCount,
            Query = query,
            Filters = new SearchFilters
            {
                Category = request.Category,
                MinPrice = request.MinPrice,
                MaxPrice = request.MaxPrice
            }
        };
    }

    public async Task<IEnumerable<string>> GetSuggestionsAsync(string prefix, int limit = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 2)
        {
            return [];
        }

        // Get matching product names (simple prefix matching)
        var suggestions = await _db.Products
            .Where(p => p.Status == ProductStatus.Active)
            .Where(p => EF.Functions.ILike(p.Name, $"{prefix}%"))
            .Select(p => p.Name)
            .Distinct()
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Also check categories
        if (suggestions.Count < limit)
        {
            var categories = await _db.Products
                .Where(p => p.Status == ProductStatus.Active)
                .Where(p => EF.Functions.ILike(p.Category, $"%{prefix}%"))
                .Select(p => p.Category)
                .Distinct()
                .Take(limit - suggestions.Count)
                .ToListAsync(cancellationToken);

            suggestions.AddRange(categories);
        }

        return suggestions.Take(limit);
    }

    private async Task<List<SearchResultItem>> PerformFullTextSearchAsync(
        string query, SearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Use PostgreSQL full-text search with ranking
            // ts_query from the search query
            var searchQuery = string.Join(" & ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            
            // Build the query with FTS
            var results = await _db.Products
                .Where(p => p.Status == ProductStatus.Active)
                .Where(p => 
                    EF.Functions.ILike(p.Name, $"%{query}%") ||
                    EF.Functions.ILike(p.Description, $"%{query}%") ||
                    EF.Functions.ILike(p.Category, $"%{query}%") ||
                    (p.Brand != null && EF.Functions.ILike(p.Brand, $"%{query}%")) ||
                    p.Tags.Any(t => EF.Functions.ILike(t, $"%{query}%")))
                .OrderByDescending(p => p.IsTrending)
                .ThenByDescending(p => p.UpdatedAt)
                .Take(request.Limit * 2)
                .Select(p => new SearchResultItem
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Description = p.Description.Substring(0, Math.Min(p.Description.Length, 200)),
                    Price = p.Price,
                    OriginalPrice = p.OriginalPrice,
                    ImageUrl = p.ImageUrl,
                    Category = p.Category,
                    Subcategory = p.Subcategory,
                    Brand = p.Brand,
                    Tags = p.Tags.ToArray(),
                    IsTrending = p.IsTrending,
                    IsOnSale = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
                    Score = CalculateFtsScore(p, query),
                    SearchType = "fts",
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full-text search failed for query: {Query}", query);
            return [];
        }
    }

    private async Task<List<SearchResultItem>> PerformSemanticSearchAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        var similarProducts = await _embeddingService.SearchProductsAsync(query, limit, cancellationToken);
        
        var productIds = similarProducts.Select(s => s.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return similarProducts
            .Where(s => products.ContainsKey(s.ProductId))
            .Select(s =>
            {
                var p = products[s.ProductId];
                return new SearchResultItem
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Description = p.Description.Length > 200 
                        ? p.Description.Substring(0, 200) + "..." 
                        : p.Description,
                    Price = p.Price,
                    OriginalPrice = p.OriginalPrice,
                    ImageUrl = p.ImageUrl,
                    Category = p.Category,
                    Subcategory = p.Subcategory,
                    Brand = p.Brand,
                    Tags = p.Tags.ToArray(),
                    IsTrending = p.IsTrending,
                    IsOnSale = p.OriginalPrice.HasValue && p.OriginalPrice > p.Price,
                    Score = s.Similarity,
                    SearchType = "semantic",
                    CreatedAt = p.CreatedAt
                };
            })
            .ToList();
    }

    private List<SearchResultItem> MergeResults(
        List<SearchResultItem> ftsResults, 
        List<SearchResultItem> semanticResults, 
        int limit)
    {
        // Create a dictionary to track best scores by product ID
        var merged = new Dictionary<int, SearchResultItem>();

        // Add FTS results (score boost for exact matches)
        foreach (var item in ftsResults)
        {
            if (!merged.ContainsKey(item.ProductId) || merged[item.ProductId].Score < item.Score)
            {
                merged[item.ProductId] = item;
            }
        }

        // Add/boost semantic results
        foreach (var item in semanticResults)
        {
            if (merged.TryGetValue(item.ProductId, out var existing))
            {
                // Product found in both - boost score significantly
                existing.Score = Math.Max(existing.Score, item.Score) * 1.5f;
                existing.SearchType = "hybrid";
            }
            else
            {
                merged[item.ProductId] = item;
            }
        }

        return merged.Values
            .OrderByDescending(r => r.Score)
            .Take(limit * 2) // Get extra for filtering
            .ToList();
    }

    private static float CalculateFtsScore(ProductEntity product, string query)
    {
        var queryLower = query.ToLowerInvariant();
        float score = 0f;

        // Exact name match gets highest score
        if (product.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.0f;
            if (product.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.5f; // Prefix match bonus
            }
        }

        // Category match
        if (product.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.7f;
        }

        // Brand match
        if (product.Brand?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 0.6f;
        }

        // Tag match
        if (product.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.4f;
        }

        // Description match (lower weight)
        if (product.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.2f;
        }

        // Trending boost
        if (product.IsTrending) score += 0.1f;

        // On sale boost
        if (product.OriginalPrice > product.Price) score += 0.1f;

        return score;
    }

    private bool IsEmbeddingServiceAvailable()
    {
        try
        {
            // Simple check - more sophisticated health check could be added
            return _embeddingService != null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Search request parameters.
/// </summary>
public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SortBy { get; set; }
    public bool EnableSemantic { get; set; } = true;
}

/// <summary>
/// Search results container.
/// </summary>
public class SearchResults
{
    public List<SearchResultItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public string Query { get; set; } = string.Empty;
    public SearchFilters? Filters { get; set; }
}

/// <summary>
/// Applied search filters.
/// </summary>
public class SearchFilters
{
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

/// <summary>
/// Individual search result item.
/// </summary>
public class SearchResultItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string? Brand { get; set; }
    public string[] Tags { get; set; } = [];
    public bool IsTrending { get; set; }
    public bool IsOnSale { get; set; }
    
    /// <summary>
    /// Relevance score (higher is better).
    /// </summary>
    public float Score { get; set; }
    
    /// <summary>
    /// Type of search that found this result: "fts", "semantic", or "hybrid".
    /// </summary>
    public string SearchType { get; set; } = "fts";
    
    public DateTime CreatedAt { get; set; }
}
