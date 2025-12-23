using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Mostlylucid.SegmentCommerce.Services.Embeddings;
using Mostlylucid.SegmentCommerce.Services.Search;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

/// <summary>
/// Search controller providing both public search and authenticated admin operations.
/// </summary>
[Route("api/search")]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        ISearchService searchService,
        IEmbeddingService embeddingService, 
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Main search endpoint - public, no authentication required.
    /// Supports HTMX (returns partial) and JSON.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        [FromQuery] string? category = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool semantic = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            if (Request.Headers.ContainsKey("HX-Request"))
            {
                return PartialView("Partials/_SearchResults", new SearchResults { Items = [], TotalCount = 0 });
            }
            return BadRequest(new { error = "Query parameter 'q' is required" });
        }

        try
        {
            var results = await _searchService.SearchAsync(new SearchRequest
            {
                Query = q,
                Limit = Math.Min(limit, 50), // Cap at 50
                Offset = offset,
                Category = category,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortBy = sortBy,
                EnableSemantic = semantic
            }, cancellationToken);

            if (Request.Headers.ContainsKey("HX-Request"))
            {
                return PartialView("Partials/_SearchResults", results);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", q);
            
            if (Request.Headers.ContainsKey("HX-Request"))
            {
                return PartialView("Partials/_SearchError", new { Message = "Search temporarily unavailable" });
            }
            return StatusCode(500, new { error = "Search service unavailable" });
        }
    }

    /// <summary>
    /// Get search suggestions for autocomplete.
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> Suggestions(
        [FromQuery] string q,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(Array.Empty<string>());
        }

        try
        {
            var suggestions = await _searchService.GetSuggestionsAsync(q, limit, cancellationToken);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Suggestions failed for prefix: {Prefix}", q);
            return Ok(Array.Empty<string>());
        }
    }

    /// <summary>
    /// Semantic search for products (direct embedding search).
    /// Requires authentication for admin/API use.
    /// </summary>
    [HttpGet("semantic")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> SemanticSearch(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query parameter 'q' is required");
        }

        try
        {
            var results = await _embeddingService.SearchProductsAsync(q, limit, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Semantic search failed for query: {Query}", q);
            return StatusCode(500, "Search service unavailable");
        }
    }

    /// <summary>
    /// Find similar products to a given product.
    /// </summary>
    [HttpGet("similar/{productId:int}")]
    public async Task<IActionResult> FindSimilar(
        int productId,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the product's embedding and find similar
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                $"product id {productId}", cancellationToken);

            var results = await _embeddingService.FindSimilarProductsAsync(
                embedding, limit, 0.5f, cancellationToken);

            var filtered = results.Where(r => r.ProductId != productId).ToList();

            if (Request.Headers.ContainsKey("HX-Request"))
            {
                // Convert to SearchResultItem for partial view
                var searchItems = filtered.Select(r => new SearchResultItem
                {
                    ProductId = r.ProductId,
                    Name = r.ProductName,
                    Category = r.Category,
                    Price = r.Price,
                    Score = r.Similarity,
                    SearchType = "semantic"
                }).ToList();

                return PartialView("Partials/_SearchResults", new SearchResults
                {
                    Items = searchItems,
                    TotalCount = searchItems.Count,
                    Query = $"similar to product {productId}"
                });
            }

            return Ok(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Find similar failed for product: {ProductId}", productId);
            return StatusCode(500, "Search service unavailable");
        }
    }

    /// <summary>
    /// Trigger reindexing of all products (admin operation).
    /// </summary>
    [HttpPost("reindex")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> ReindexAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _embeddingService.ReindexAllProductsAsync(cancellationToken);
            return Ok(new { indexed = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reindex failed");
            return StatusCode(500, "Reindex failed");
        }
    }

    /// <summary>
    /// Index a specific product.
    /// </summary>
    [HttpPost("index/{productId:int}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> IndexProduct(
        int productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _embeddingService.IndexProductAsync(productId, cancellationToken);
            return success ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index failed for product: {ProductId}", productId);
            return StatusCode(500, "Index failed");
        }
    }
}
