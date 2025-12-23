using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers;

public class ProductsController(
    ProductService productService,
    InteractionService interactionService,
    ISessionService sessionService,
    IInterestTrackingService interestTrackingService) : Controller
{
    public async Task<IActionResult> Index(string? category = null)
    {
        var products = string.IsNullOrEmpty(category)
            ? await productService.GetAllAsync()
            : await productService.GetByCategoryAsync(category);

        ViewData["CurrentCategory"] = category;
        ViewData["Categories"] = await productService.GetCategoriesAsync();

        return View(products.ToList());
    }

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

    [HttpPost]
    public async Task<IActionResult> TrackView(int id)
    {
        var product = await productService.GetByIdAsync(id);
        if (product != null)
        {
            await TrackProductViewAsync(product, weight: 0.05);
        }

        return Ok();
    }

    [HttpPost]
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

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return RedirectToAction(nameof(Index));
        }

        await interactionService.RecordEventAsync(
            sessionService.GetSessionId(),
            EventTypes.Search,
            metadata: new InteractionMetadata { SearchQuery = q });

        var products = await productService.SearchAsync(q);
        
        ViewData["SearchQuery"] = q;
        ViewData["Categories"] = await productService.GetCategoriesAsync();

        return View("Index", products.ToList());
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
