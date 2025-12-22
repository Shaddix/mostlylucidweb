using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers;

public class CartController(
    ProductService productService,
    InteractionService interactionService,
    ICartService cartService,
    IInterestTrackingService interestTrackingService,
    ISessionService sessionService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var cart = await cartService.GetCartAsync();
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int id)
    {
        var product = await productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        await cartService.AddItemAsync(id);

        await interactionService.RecordEventAsync(
            sessionService.GetSessionId(),
            EventTypes.AddToCart,
            productId: id,
            category: product.Category);

        await interestTrackingService.TrackPurchaseIntentAsync(product.Category);

        return PartialView("Partials/_CartCount", cartService.GetItemCount());
    }

    [HttpPost]
    public async Task<IActionResult> Increase(int id)
    {
        await cartService.IncreaseQuantityAsync(id);
        var cart = await cartService.GetCartAsync();
        return PartialView("Partials/_Cart", cart);
    }

    [HttpPost]
    public async Task<IActionResult> Decrease(int id)
    {
        var product = await productService.GetByIdAsync(id);
        await cartService.DecreaseQuantityAsync(id);
        
        if (product != null)
        {
            var cart = await cartService.GetCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.Product.Id == id);
            
            if (item == null)
            {
                await interactionService.RecordEventAsync(
                    sessionService.GetSessionId(),
                    EventTypes.RemoveFromCart,
                    productId: id,
                    category: product.Category);
            }
        }

        var updatedCart = await cartService.GetCartAsync();
        return PartialView("Partials/_Cart", updatedCart);
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int id)
    {
        var product = await productService.GetByIdAsync(id);
        await cartService.RemoveItemAsync(id);
        
        if (product != null)
        {
            await interactionService.RecordEventAsync(
                sessionService.GetSessionId(),
                EventTypes.RemoveFromCart,
                productId: id,
                category: product.Category);
        }

        var cart = await cartService.GetCartAsync();
        return PartialView("Partials/_Cart", cart);
    }

    [HttpGet]
    public async Task<IActionResult> Count()
    {
        var cart = await cartService.GetCartAsync();
        return PartialView("Partials/_CartCount", cart.ItemCount);
    }
}
