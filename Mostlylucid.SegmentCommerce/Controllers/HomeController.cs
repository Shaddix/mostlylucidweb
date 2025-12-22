using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers;

public class HomeController(ProductService productService, ISessionService sessionService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var signature = sessionService.GetInterestSignature();

        var viewModel = new HomeViewModel
        {
            PersonalisedProducts = (await productService.GetPersonalisedAsync(signature, 8)).ToList(),
            TrendingProducts = (await productService.GetTrendingAsync(8)).ToList(),
            OnSaleProducts = (await productService.GetOnSaleAsync(4)).ToList(),
            Categories = (await productService.GetCategoriesAsync()).ToList(),
            InterestSignature = signature
        };

        return View(viewModel);
    }

    public IActionResult Error()
    {
        return View();
    }
}
