using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services.Events;

namespace Mostlylucid.SegmentCommerce.Services;

public interface ICartService
{
    Task<Cart> GetCartAsync();
    Task AddItemAsync(int productId, int? variationId = null);
    Task IncreaseQuantityAsync(int productId);
    Task DecreaseQuantityAsync(int productId);
    Task RemoveItemAsync(int productId);
    void ClearCart();
    int GetItemCount();
    
    /// <summary>
    /// Get cart data without product hydration (for checkout).
    /// </summary>
    CartSessionData? GetCartData();
}

public class CartService : ICartService
{
    private const string CartSessionKey = "ShoppingCart";
    
    private readonly SegmentCommerceDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEventPublisher _events;
    private readonly ILogger<CartService> _logger;
    
    public CartService(
        SegmentCommerceDbContext db,
        IHttpContextAccessor httpContextAccessor,
        IEventPublisher events,
        ILogger<CartService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _events = events;
        _logger = logger;
    }
    
    private ISession Session => _httpContextAccessor.HttpContext?.Session 
        ?? throw new InvalidOperationException("HttpContext is not available");

    private string? SessionId => _httpContextAccessor.HttpContext?.Session.Id;
    private Guid? ProfileId => _httpContextAccessor.HttpContext?.Items["ProfileId"] as Guid?;

    public async Task<Cart> GetCartAsync()
    {
        var json = Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return new Cart();
        }

        try
        {
            var cartData = System.Text.Json.JsonSerializer.Deserialize<CartSessionData>(json);
            if (cartData == null || cartData.Items.Count == 0)
            {
                return new Cart();
            }

            // Batch load all products in a single query (fixes N+1)
            var productIds = cartData.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .Include(p => p.Variations)
                .Select(p => new Product
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    OriginalPrice = p.OriginalPrice,
                    ImageUrl = p.ImageUrl,
                    Category = p.Category,
                    Tags = p.Tags,
                    IsTrending = p.IsTrending,
                    Variations = p.Variations.Select(v => new ProductVariation
                    {
                        Id = v.Id,
                        Color = v.Color,
                        Size = v.Size,
                        Price = v.Price,
                        OriginalPrice = v.OriginalPrice,
                        ImageUrl = v.ImageUrl,
                        Sku = v.Sku,
                        StockQuantity = v.StockQuantity,
                        IsActive = v.IsActive
                    }).ToList()
                })
                .ToDictionaryAsync(p => p.Id);

            var cart = new Cart();
            foreach (var item in cartData.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    var variation = item.VariationId.HasValue 
                        ? product.Variations.FirstOrDefault(v => v.Id == item.VariationId)
                        : null;
                    
                    cart.Items.Add(new CartItem 
                    { 
                        Product = product, 
                        Quantity = item.Quantity,
                        VariationId = item.VariationId,
                        Variation = variation
                    });
                }
            }

            return cart;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize cart data");
            return new Cart();
        }
    }

    public CartSessionData? GetCartData()
    {
        var json = Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<CartSessionData>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task AddItemAsync(int productId, int? variationId = null)
    {
        var cartData = GetCartData() ?? new CartSessionData();
        
        // Check if product exists
        var product = await _db.Products
            .Include(p => p.Variations)
            .FirstOrDefaultAsync(p => p.Id == productId);
        
        if (product == null)
        {
            _logger.LogWarning("Attempted to add non-existent product {ProductId} to cart", productId);
            return;
        }

        decimal unitPrice = product.Price;
        if (variationId.HasValue)
        {
            var variation = product.Variations.FirstOrDefault(v => v.Id == variationId);
            if (variation == null)
            {
                _logger.LogWarning("Attempted to add non-existent variation {VariationId} to cart", variationId);
                return;
            }
            unitPrice = variation.Price;
        }

        var existingItem = cartData.Items.FirstOrDefault(i => 
            i.ProductId == productId && i.VariationId == variationId);

        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            cartData.Items.Add(new CartItemData 
            { 
                ProductId = productId, 
                VariationId = variationId,
                Quantity = 1 
            });
        }

        SaveCartData(cartData);

        // Publish event for async processing
        _events.Publish(new CartItemAddedEvent(
            SessionId ?? "unknown",
            ProfileId,
            productId,
            variationId,
            1,
            unitPrice));

        _logger.LogDebug("Added product {ProductId} to cart", productId);
    }

    public async Task IncreaseQuantityAsync(int productId)
    {
        var cartData = GetCartData();
        if (cartData == null) return;

        var item = cartData.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            item.Quantity++;
            SaveCartData(cartData);
        }
    }

    public async Task DecreaseQuantityAsync(int productId)
    {
        var cartData = GetCartData();
        if (cartData == null) return;

        var item = cartData.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            item.Quantity--;
            if (item.Quantity <= 0)
            {
                cartData.Items.Remove(item);
                
                _events.Publish(new CartItemRemovedEvent(
                    SessionId ?? "unknown",
                    ProfileId,
                    productId,
                    item.VariationId));
            }
            SaveCartData(cartData);
        }
    }

    public async Task RemoveItemAsync(int productId)
    {
        var cartData = GetCartData();
        if (cartData == null) return;

        var item = cartData.Items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            cartData.Items.Remove(item);
            SaveCartData(cartData);
            
            _events.Publish(new CartItemRemovedEvent(
                SessionId ?? "unknown",
                ProfileId,
                productId,
                item.VariationId));
        }
    }

    public void ClearCart()
    {
        Session.Remove(CartSessionKey);
    }

    public int GetItemCount()
    {
        var cartData = GetCartData();
        return cartData?.Items.Sum(i => i.Quantity) ?? 0;
    }

    private void SaveCartData(CartSessionData cartData)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(cartData);
        Session.SetString(CartSessionKey, json);
    }
}
