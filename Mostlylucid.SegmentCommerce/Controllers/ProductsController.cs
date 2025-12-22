using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers;

public class ProductsController : Controller
{
    private readonly ProductService _productService;
    private readonly InteractionService _interactionService;

    public ProductsController(ProductService productService, InteractionService interactionService)
    {
        _productService = productService;
        _interactionService = interactionService;
    }

    public async Task<IActionResult> Index(string? category = null)
    {
        var products = string.IsNullOrEmpty(category)
            ? await _productService.GetAllAsync()
            : await _productService.GetByCategoryAsync(category);

        ViewData["CurrentCategory"] = category;
        ViewData["Categories"] = await _productService.GetCategoriesAsync();

        return View(products.ToList());
    }

    public async Task<IActionResult> Category(string id)
    {
        var products = await _productService.GetByCategoryAsync(id);
        var productList = products.ToList();

        if (!productList.Any())
        {
            return NotFound();
        }

        // Track category browse
        await _interactionService.RecordEventAsync(
            GetSessionId(),
            EventTypes.CategoryBrowse,
            category: id);

        // Reinforce interest in this category
        await TrackCategoryInterestAsync(id, 0.15);

        ViewData["CurrentCategory"] = id;
        ViewData["Categories"] = await _productService.GetCategoriesAsync();

        return View("Index", productList);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        // Track this view for interest signature
        await TrackProductViewAsync(product);

        // Get related products from the same category
        var relatedProducts = (await _productService.GetByCategoryAsync(product.Category))
            .Where(p => p.Id != id)
            .Take(4)
            .ToList();

        ViewData["RelatedProducts"] = relatedProducts;

        return View(product);
    }

    /// <summary>
    /// HTMX endpoint to track when a product comes into view.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TrackView(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product != null)
        {
            await TrackProductViewAsync(product, weight: 0.05); // Small weight for impressions
        }

        return Ok();
    }

    /// <summary>
    /// HTMX endpoint to track explicit interest (clicks).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TrackInterest(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product != null)
        {
            await _interactionService.RecordEventAsync(
                GetSessionId(),
                EventTypes.Click,
                productId: id,
                category: product.Category);

            await TrackProductViewAsync(product, weight: 0.2);
        }

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return RedirectToAction(nameof(Index));
        }

        await _interactionService.RecordEventAsync(
            GetSessionId(),
            EventTypes.Search,
            metadata: new Dictionary<string, object> { { "query", q } });

        var products = await _productService.SearchAsync(q);
        
        ViewData["SearchQuery"] = q;
        ViewData["Categories"] = await _productService.GetCategoriesAsync();

        return View("Index", products.ToList());
    }

    private async Task TrackProductViewAsync(Product product, double weight = 0.1)
    {
        // Record the event
        await _interactionService.RecordEventAsync(
            GetSessionId(),
            EventTypes.View,
            productId: product.Id,
            category: product.Category);

        // Update interest signature
        await TrackCategoryInterestAsync(product.Category, weight);
    }

    private Task TrackCategoryInterestAsync(string category, double weight)
    {
        var signature = GetSignatureFromSession();

        if (signature.Interests.TryGetValue(category, out var existing))
        {
            // Reinforce existing interest
            existing.Weight = Math.Min(1.0, existing.Weight + weight);
            existing.LastReinforced = DateTime.UtcNow;
            existing.ReinforcementCount++;
        }
        else
        {
            // Create new interest
            signature.Interests[category] = new InterestWeight
            {
                Category = category,
                Weight = weight,
                LastReinforced = DateTime.UtcNow,
                ReinforcementCount = 1
            };
        }

        signature.LastUpdated = DateTime.UtcNow;
        SaveSignatureToSession(signature);

        return Task.CompletedTask;
    }

    private string GetSessionId()
    {
        var sessionId = HttpContext.Session.GetString("SessionId");
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("SessionId", sessionId);
        }
        return sessionId;
    }

    private InterestSignature GetSignatureFromSession()
    {
        var json = HttpContext.Session.GetString("InterestSignature");
        if (string.IsNullOrEmpty(json))
        {
            return new InterestSignature();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<InterestSignature>(json)
                   ?? new InterestSignature();
        }
        catch
        {
            return new InterestSignature();
        }
    }

    private void SaveSignatureToSession(InterestSignature signature)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(signature);
        HttpContext.Session.SetString("InterestSignature", json);
    }
}
