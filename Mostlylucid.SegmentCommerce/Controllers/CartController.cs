using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;

namespace Mostlylucid.SegmentCommerce.Controllers;

public class CartController : Controller
{
    private readonly ProductService _productService;
    private readonly InteractionService _interactionService;
    private const string CartSessionKey = "ShoppingCart";

    public CartController(ProductService productService, InteractionService interactionService)
    {
        _productService = productService;
        _interactionService = interactionService;
    }

    public async Task<IActionResult> Index()
    {
        var cart = await GetCartFromSessionAsync();
        return View(cart);
    }

    /// <summary>
    /// Add a product to the cart. Returns the updated cart count for HTMX.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Add(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        var cart = await GetCartFromSessionAsync();
        var existingItem = cart.Items.FirstOrDefault(i => i.Product.Id == id);

        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            cart.Items.Add(new CartItem { Product = product, Quantity = 1 });
        }

        SaveCartToSession(cart);

        // Track add-to-cart event
        await _interactionService.RecordEventAsync(
            GetSessionId(),
            EventTypes.AddToCart,
            productId: id,
            category: product.Category);

        // Track purchase intent in interest signature (higher weight)
        await TrackPurchaseIntentAsync(product);

        // Return just the cart count badge for HTMX
        return PartialView("Partials/_CartCount", cart.ItemCount);
    }

    /// <summary>
    /// Increase quantity of an item in the cart.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Increase(int id)
    {
        var cart = await GetCartFromSessionAsync();
        var item = cart.Items.FirstOrDefault(i => i.Product.Id == id);

        if (item != null)
        {
            item.Quantity++;
            SaveCartToSession(cart);
        }

        return PartialView("Partials/_Cart", cart);
    }

    /// <summary>
    /// Decrease quantity of an item in the cart.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Decrease(int id)
    {
        var cart = await GetCartFromSessionAsync();
        var item = cart.Items.FirstOrDefault(i => i.Product.Id == id);

        if (item != null)
        {
            item.Quantity--;
            if (item.Quantity <= 0)
            {
                cart.Items.Remove(item);

                // Track removal
                await _interactionService.RecordEventAsync(
                    GetSessionId(),
                    EventTypes.RemoveFromCart,
                    productId: id,
                    category: item.Product.Category);
            }

            SaveCartToSession(cart);
        }

        return PartialView("Partials/_Cart", cart);
    }

    /// <summary>
    /// Remove an item from the cart entirely.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Remove(int id)
    {
        var cart = await GetCartFromSessionAsync();
        var item = cart.Items.FirstOrDefault(i => i.Product.Id == id);

        if (item != null)
        {
            cart.Items.Remove(item);
            SaveCartToSession(cart);

            // Track removal
            await _interactionService.RecordEventAsync(
                GetSessionId(),
                EventTypes.RemoveFromCart,
                productId: id,
                category: item.Product.Category);
        }

        return PartialView("Partials/_Cart", cart);
    }

    /// <summary>
    /// Get the current cart count (for header badge updates).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Count()
    {
        var cart = await GetCartFromSessionAsync();
        return PartialView("Partials/_CartCount", cart.ItemCount);
    }

    private Task TrackPurchaseIntentAsync(Product product)
    {
        var signature = GetSignatureFromSession();
        var category = product.Category;

        if (signature.Interests.TryGetValue(category, out var existing))
        {
            // Strong reinforcement for add-to-cart
            existing.Weight = Math.Min(1.0, existing.Weight + 0.4);
            existing.LastReinforced = DateTime.UtcNow;
            existing.ReinforcementCount++;
        }
        else
        {
            signature.Interests[category] = new InterestWeight
            {
                Category = category,
                Weight = 0.4,
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

    private async Task<Cart> GetCartFromSessionAsync()
    {
        var json = HttpContext.Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return new Cart();
        }

        try
        {
            var cartData = System.Text.Json.JsonSerializer.Deserialize<CartSessionData>(json);
            if (cartData == null)
            {
                return new Cart();
            }

            var cart = new Cart();
            foreach (var item in cartData.Items)
            {
                var product = await _productService.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    cart.Items.Add(new CartItem { Product = product, Quantity = item.Quantity });
                }
            }

            return cart;
        }
        catch
        {
            return new Cart();
        }
    }

    private void SaveCartToSession(Cart cart)
    {
        var cartData = new CartSessionData
        {
            Items = cart.Items.Select(i => new CartItemData
            {
                ProductId = i.Product.Id,
                Quantity = i.Quantity
            }).ToList()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(cartData);
        HttpContext.Session.SetString(CartSessionKey, json);
    }

    private class CartSessionData
    {
        public List<CartItemData> Items { get; set; } = [];
    }

    private class CartItemData
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
