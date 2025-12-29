using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;
using Mostlylucid.SegmentCommerce.Services.Search;

namespace Mostlylucid.SegmentCommerce.Controllers;

[Route("[controller]")]
public class ProductsController(
    ProductService productService,
    InteractionService interactionService,
    ISessionService sessionService,
    IInterestTrackingService interestTrackingService,
    ISearchService searchService) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(string? category = null)
    {
        var products = string.IsNullOrEmpty(category)
            ? await productService.GetAllAsync()
            : await productService.GetByCategoryAsync(category);

        ViewData["CurrentCategory"] = category;
        ViewData["Categories"] = await productService.GetCategoriesAsync();

        return View(products.ToList());
    }

    [HttpGet("category/{id}")]
    public async Task<IActionResult> Category(string id)
    {
        var products = await productService.GetByCategoryAsync(id);
        var productList = products.ToList();

        if (!productList.Any())
        {
            return NotFound();
        }

        await interactionService.RecordEventAsync(
            sessionService.GetSessionId(),
            EventTypes.CategoryBrowse,
            category: id);

        await interestTrackingService.TrackCategoryInterestAsync(id, 0.15);

        ViewData["CurrentCategory"] = id;
        ViewData["Categories"] = await productService.GetCategoriesAsync();

        return View("Index", productList);
    }

    [HttpGet("details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var product = await productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        await TrackProductViewAsync(product);

        var relatedProducts = (await productService.GetByCategoryAsync(product.Category))
            .Where(p => p.Id != id)
            .Take(4)
            .ToList();

        ViewData["RelatedProducts"] = relatedProducts;

        return View(product);
    }

    [HttpPost("track-view/{id:int}")]
    public async Task<IActionResult> TrackView(int id)
    {
        var product = await productService.GetByIdAsync(id);
        if (product != null)
        {
            await TrackProductViewAsync(product, weight: 0.05);
        }

        return Ok();
    }

    [HttpPost("track-interest/{id:int}")]
    public async Task<IActionResult> TrackInterest(int id)
    {
        var product = await productService.GetByIdAsync(id);
        if (product != null)
        {
            await interactionService.RecordEventAsync(
                sessionService.GetSessionId(),
                EventTypes.Click,
                productId: id,
                category: product.Category);

            await interestTrackingService.TrackCategoryInterestAsync(product.Category, 0.2);
        }

        return Ok();
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        string q,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return RedirectToAction(nameof(Index));
        }

        await interactionService.RecordEventAsync(
            sessionService.GetSessionId(),
            EventTypes.Search,
            metadata: new InteractionMetadata { SearchQuery = q });

        var results = await searchService.SearchAsync(new SearchRequest
        {
            Query = q,
            Category = category,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            SortBy = sortBy,
            Limit = 40,
            EnableSemantic = true
        });
        
        ViewData["SearchQuery"] = q;
        ViewData["Categories"] = await productService.GetCategoriesAsync();
        ViewData["SearchFilters"] = results.Filters;

        return View("Search", results);
    }

    private async Task TrackProductViewAsync(Product product, double weight = 0.1)
    {
        await interactionService.RecordEventAsync(
            sessionService.GetSessionId(),
            EventTypes.View,
            productId: product.Id,
            category: product.Category);

        await interestTrackingService.TrackCategoryInterestAsync(product.Category, weight);
    }
}
