using Mostlylucid.SegmentCommerce.Models;

namespace Mostlylucid.SegmentCommerce.Services;

public interface ICartService
{
    Task<Cart> GetCartAsync();
    Task AddItemAsync(int productId);
    Task IncreaseQuantityAsync(int productId);
    Task DecreaseQuantityAsync(int productId);
    Task RemoveItemAsync(int productId);
    void ClearCart();
    int GetItemCount();
}

public class CartService(ProductService productService, IHttpContextAccessor httpContextAccessor) : ICartService
{
    private const string CartSessionKey = "ShoppingCart";
    
    private ISession Session => httpContextAccessor.HttpContext?.Session 
        ?? throw new InvalidOperationException("HttpContext is not available");

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
            if (cartData == null)
            {
                return new Cart();
            }

            var cart = new Cart();
            foreach (var item in cartData.Items)
            {
                var product = await productService.GetByIdAsync(item.ProductId);
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

    public async Task AddItemAsync(int productId)
    {
        var cart = await GetCartAsync();
        var product = await productService.GetByIdAsync(productId);
        if (product == null) return;

        var existingItem = cart.Items.FirstOrDefault(i => i.Product.Id == productId);

        if (existingItem != null)
        {
            existingItem.Quantity++;
        }
        else
        {
            cart.Items.Add(new CartItem { Product = product, Quantity = 1 });
        }

        SaveCart(cart);
    }

    public async Task IncreaseQuantityAsync(int productId)
    {
        var cart = await GetCartAsync();
        var item = cart.Items.FirstOrDefault(i => i.Product.Id == productId);

        if (item != null)
        {
            item.Quantity++;
            SaveCart(cart);
        }
    }

    public async Task DecreaseQuantityAsync(int productId)
    {
        var cart = await GetCartAsync();
        var item = cart.Items.FirstOrDefault(i => i.Product.Id == productId);

        if (item != null)
        {
            item.Quantity--;
            if (item.Quantity <= 0)
            {
                cart.Items.Remove(item);
            }

            SaveCart(cart);
        }
    }

    public async Task RemoveItemAsync(int productId)
    {
        var cart = await GetCartAsync();
        var item = cart.Items.FirstOrDefault(i => i.Product.Id == productId);

        if (item != null)
        {
            cart.Items.Remove(item);
            SaveCart(cart);
        }
    }

    public void ClearCart()
    {
        Session.Remove(CartSessionKey);
    }

    public int GetItemCount()
    {
        var json = Session.GetString(CartSessionKey);
        if (string.IsNullOrEmpty(json))
        {
            return 0;
        }

        try
        {
            var cartData = System.Text.Json.JsonSerializer.Deserialize<CartSessionData>(json);
            return cartData?.Items.Sum(i => i.Quantity) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private void SaveCart(Cart cart)
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
        Session.SetString(CartSessionKey, json);
    }

    private record CartSessionData
    {
        public List<CartItemData> Items { get; set; } = [];
    }

    private record CartItemData
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
