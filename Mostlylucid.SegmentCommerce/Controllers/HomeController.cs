using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers;

public class HomeController : Controller
{
    private readonly ProductService _productService;

    public HomeController(ProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        // Get the user's interest signature from session (if any)
        var signature = GetSignatureFromSession();

        var viewModel = new HomeViewModel
        {
            PersonalisedProducts = (await _productService.GetPersonalisedAsync(signature, 8)).ToList(),
            TrendingProducts = (await _productService.GetTrendingAsync(8)).ToList(),
            OnSaleProducts = (await _productService.GetOnSaleAsync(4)).ToList(),
            Categories = (await _productService.GetCategoriesAsync()).ToList(),
            InterestSignature = signature
        };

        return View(viewModel);
    }

    public IActionResult Error()
    {
        return View();
    }

    private InterestSignature GetSignatureFromSession()
    {
        var signature = HttpContext.Session.GetString("InterestSignature");
        if (string.IsNullOrEmpty(signature))
        {
            return new InterestSignature();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<InterestSignature>(signature)
                   ?? new InterestSignature();
        }
        catch
        {
            return new InterestSignature();
        }
    }
}
