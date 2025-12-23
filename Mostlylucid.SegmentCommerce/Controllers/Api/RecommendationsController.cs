using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Services;
using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

/// <summary>
/// API controller for product recommendations.
/// Returns personalized recommendations based on session/profile.
/// </summary>
[Route("api/recommendations")]
public class RecommendationsController : Controller
{
    private readonly IRecommendationService _recommendationService;
    private readonly IProfileResolver _profileResolver;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationService recommendationService,
        IProfileResolver profileResolver,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _profileResolver = profileResolver;
        _logger = logger;
    }

    /// <summary>
    /// Get personalized recommendations for the current session.
    /// Returns HTML partial for HTMX.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRecommendations([FromQuery] int count = 8, [FromQuery] string? title = null)
    {
        var sessionKey = GetSessionKey();
        
        List<RecommendedProduct> recommendations;
        
        if (!string.IsNullOrEmpty(sessionKey))
        {
            recommendations = await _recommendationService.GetRecommendationsForSessionAsync(sessionKey, count);
        }
        else
        {
            // No session - return trending products
            recommendations = await _recommendationService.GetRecommendationsAsync(null, count);
        }

        // If HTMX request, return partial view
        if (Request.Headers.ContainsKey("HX-Request"))
        {
            var model = new RecommendationsViewModel
            {
                Title = title ?? "Recommended for You",
                Products = recommendations,
                ShowReasons = true
            };
            return PartialView("Partials/_SuggestedProducts", model);
        }

        // Otherwise return JSON
        return Ok(recommendations);
    }

    /// <summary>
    /// Get products similar to a given product.
    /// </summary>
    [HttpGet("similar/{productId:int}")]
    public async Task<IActionResult> GetSimilarProducts(int productId, [FromQuery] int count = 4)
    {
        var products = await _recommendationService.GetSimilarProductsAsync(productId, count);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            var model = new RecommendationsViewModel
            {
                Title = "You Might Also Like",
                Products = products,
                ShowReasons = false,
                Layout = "grid" // Grid layout for similar products
            };
            return PartialView("Partials/_SuggestedProducts", model);
        }

        return Ok(products);
    }

    /// <summary>
    /// Get cross-sell recommendations based on cart contents.
    /// </summary>
    [HttpGet("cross-sell")]
    public async Task<IActionResult> GetCrossSell([FromQuery] string? productIds, [FromQuery] int count = 4)
    {
        var ids = new List<int>();
        if (!string.IsNullOrEmpty(productIds))
        {
            ids = productIds.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
        }

        // Get profile ID from session if available
        Guid? profileId = null;
        var session = await _profileResolver.GetOrCreateSessionAsync(HttpContext);
        profileId = session.PersistentProfileId;

        var products = await _recommendationService.GetCrossSellAsync(ids, profileId, count);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            var model = new RecommendationsViewModel
            {
                Title = "Complete Your Order",
                Products = products,
                ShowReasons = true,
                Layout = "horizontal" // Horizontal scroll for cross-sell
            };
            return PartialView("Partials/_SuggestedProducts", model);
        }

        return Ok(products);
    }

    /// <summary>
    /// Get session key from header, query, or HttpContext.Items.
    /// </summary>
    private string? GetSessionKey()
    {
        // Try X-Session-ID header first (from TrackingManager)
        if (Request.Headers.TryGetValue("X-Session-ID", out var headerValue) && 
            !string.IsNullOrEmpty(headerValue.ToString()))
        {
            return headerValue.ToString();
        }

        // Try _sid query parameter
        if (Request.Query.TryGetValue("_sid", out var queryValue) && 
            !string.IsNullOrEmpty(queryValue.ToString()))
        {
            return queryValue.ToString();
        }

        // Try HttpContext.Items (set by middleware)
        if (HttpContext.Items.TryGetValue("SessionKey", out var itemValue) && 
            itemValue is string sessionKey)
        {
            return sessionKey;
        }

        return null;
    }
}

/// <summary>
/// View model for the _SuggestedProducts partial.
/// </summary>
public class RecommendationsViewModel
{
    public string Title { get; set; } = "Recommended for You";
    public List<RecommendedProduct> Products { get; set; } = [];
    public bool ShowReasons { get; set; } = true;
    /// <summary>
    /// Layout type: "carousel" (horizontal scroll), "grid", or "horizontal" (smaller scroll)
    /// </summary>
    public string Layout { get; set; } = "carousel";
}
