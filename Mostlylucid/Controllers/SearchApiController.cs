using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Services.Blog;

namespace Mostlylucid.Controllers;

/// <summary>
/// API controller for search functionality (used by typeahead/autocomplete)
/// </summary>
[ApiController]
[Route("api/search")]
public class SearchApiController(BlogSearchService searchService, ILogger<SearchApiController> logger) : ControllerBase
{
    /// <summary>
    /// Hybrid search endpoint for typeahead autocomplete
    /// Tries semantic search first, falls back to PostgreSQL full-text search
    /// Returns up to 10 results weighted by match quality
    /// </summary>
    [HttpGet("{query}")]
    [OutputCache(Duration = 300, VaryByRouteValueNames = new[] { "query" })]
    public async Task<IActionResult> Search([FromRoute] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return Ok(Array.Empty<object>());
        }

        try
        {
            var results = await searchService.HybridSearchAsync(query, 10);

            // Return in the format expected by typeahead.js
            // Results are already weighted/ranked by the search service
            var response = results.Select((r, index) => new
            {
                title = r.Title,
                slug = r.Slug,
                url = r.Url,
                rank = index + 1 // Include rank for potential UI use
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in search API for query: {Query}", query);
            return StatusCode(500, new { error = "Search failed" });
        }
    }
}
