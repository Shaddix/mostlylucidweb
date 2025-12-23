using System.Collections.Concurrent;

namespace Mostlylucid.SegmentCommerce.Services;

/// <summary>
/// LFU (Least Frequently Used) sliding cache for transient checkout/account data.
/// Customer PII is stored here only during the checkout session and auto-expires.
/// No persistent storage - data is lost on restart by design.
/// </summary>
public class TransientCheckoutCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultExpiration;
    private readonly int _maxEntries;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public TransientCheckoutCache(TimeSpan? defaultExpiration = null, int maxEntries = 10000)
    {
        _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(30);
        _maxEntries = maxEntries;
        
        // Run cleanup every minute
        _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Get or create checkout data for a session.
    /// </summary>
    public CheckoutData GetOrCreate(string sessionKey)
    {
        var entry = _cache.GetOrAdd(sessionKey, _ => new CacheEntry
        {
            Data = new CheckoutData { SessionKey = sessionKey },
            LastAccess = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration),
            AccessCount = 0
        });

        // Update access info (sliding expiration)
        entry.LastAccess = DateTime.UtcNow;
        entry.ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration);
        entry.AccessCount++;

        return entry.Data;
    }

    /// <summary>
    /// Get checkout data if it exists.
    /// </summary>
    public CheckoutData? Get(string sessionKey)
    {
        if (_cache.TryGetValue(sessionKey, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                entry.LastAccess = DateTime.UtcNow;
                entry.ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration);
                entry.AccessCount++;
                return entry.Data;
            }
            
            // Expired - remove it
            _cache.TryRemove(sessionKey, out _);
        }
        return null;
    }

    /// <summary>
    /// Update checkout data for a session.
    /// </summary>
    public void Update(string sessionKey, Action<CheckoutData> updateAction)
    {
        var data = GetOrCreate(sessionKey);
        updateAction(data);
    }

    /// <summary>
    /// Remove checkout data (e.g., after successful order).
    /// </summary>
    public void Remove(string sessionKey)
    {
        _cache.TryRemove(sessionKey, out _);
    }

    /// <summary>
    /// Clear all cached data.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Get current cache statistics.
    /// </summary>
    public CacheStats GetStats()
    {
        var now = DateTime.UtcNow;
        var entries = _cache.Values.ToList();
        
        return new CacheStats
        {
            TotalEntries = entries.Count,
            ActiveEntries = entries.Count(e => e.ExpiresAt > now),
            ExpiredEntries = entries.Count(e => e.ExpiresAt <= now),
            TotalAccessCount = entries.Sum(e => e.AccessCount)
        };
    }

    private void Cleanup(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        // If still over max, evict least frequently used
        if (_cache.Count > _maxEntries)
        {
            var toEvict = _cache
                .OrderBy(kvp => kvp.Value.AccessCount)
                .ThenBy(kvp => kvp.Value.LastAccess)
                .Take(_cache.Count - _maxEntries + 100) // Remove a batch
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toEvict)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cleanupTimer.Dispose();
        _cache.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class CacheEntry
    {
        public CheckoutData Data { get; set; } = null!;
        public DateTime LastAccess { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int AccessCount { get; set; }
    }
}

/// <summary>
/// Transient checkout data - NOT persisted to database.
/// Generated with Bogus for demo purposes.
/// </summary>
public class CheckoutData
{
    public string SessionKey { get; set; } = string.Empty;
    
    // Customer info (fake, transient)
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    
    // Shipping address (fake, transient)
    public AddressData? ShippingAddress { get; set; }
    
    // Billing address (fake, transient)
    public bool BillingSameAsShipping { get; set; } = true;
    public AddressData? BillingAddress { get; set; }
    
    // Payment info (fake, transient - never real card data)
    public PaymentData? Payment { get; set; }
    
    // Cart state
    public List<CartItemData> CartItems { get; set; } = [];
    
    // Checkout state
    public CheckoutStep CurrentStep { get; set; } = CheckoutStep.Cart;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class AddressData
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class PaymentData
{
    public string Method { get; set; } = "card";
    public string? CardBrand { get; set; }
    public string? CardLastFour { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardholderName { get; set; }
}

public class CartItemData
{
    public int ProductId { get; set; }
    public int? VariationId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal? OriginalPrice { get; set; }
}

public enum CheckoutStep
{
    Cart,
    CustomerInfo,
    Shipping,
    Payment,
    Review,
    Complete
}

public class CacheStats
{
    public int TotalEntries { get; set; }
    public int ActiveEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public long TotalAccessCount { get; set; }
}
