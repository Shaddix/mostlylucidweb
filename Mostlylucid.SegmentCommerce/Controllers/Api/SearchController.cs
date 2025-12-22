using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Mostlylucid.SegmentCommerce.Services.Embeddings;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(IEmbeddingService embeddingService, ILogger<SearchController> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Semantic search for products.
    /// </summary>
    [HttpGet("semantic")]
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

            // Exclude the source product
            return Ok(results.Where(r => r.ProductId != productId));
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
