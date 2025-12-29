using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.Services;

/// <summary>
/// Service for generating personalized product recommendations based on user profiles.
/// Uses profile interests, brand affinities, and price preferences.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Get recommended products for a profile.
    /// </summary>
    Task<List<RecommendedProduct>> GetRecommendationsAsync(Guid? profileId, int count = 8);
    
    /// <summary>
    /// Get recommended products for a session (may not have persistent profile).
    /// </summary>
    Task<List<RecommendedProduct>> GetRecommendationsForSessionAsync(string sessionKey, int count = 8);
    
    /// <summary>
    /// Get products similar to a given product (for "You might also like").
    /// </summary>
    Task<List<RecommendedProduct>> GetSimilarProductsAsync(int productId, int count = 4);
    
    /// <summary>
    /// Get cross-sell recommendations based on cart contents.
    /// </summary>
    Task<List<RecommendedProduct>> GetCrossSellAsync(List<int> cartProductIds, Guid? profileId, int count = 4);
}

/// <summary>
/// A product with recommendation context.
/// </summary>
public class RecommendedProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public string? Brand { get; set; }
    public string[] Tags { get; set; } = [];
    
    /// <summary>
    /// Seller information for display.
    /// </summary>
    public RecommendedProductSeller? Seller { get; set; }
    
    /// <summary>
    /// Relevance score (0-1) based on profile matching.
    /// </summary>
    public double RelevanceScore { get; set; }
    
    /// <summary>
    /// Why this product was recommended.
    /// </summary>
    public string RecommendationReason { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of recommendation (interest, brand, similar, trending, etc.)
    /// </summary>
    public string RecommendationType { get; set; } = "interest";
    
    public bool IsTrending { get; set; }
    public bool IsOnSale { get; set; }
}

/// <summary>
/// Seller info for recommended products.
/// </summary>
public class RecommendedProductSeller
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IsVerified { get; set; }
    public double Rating { get; set; }
}

public class RecommendationService : IRecommendationService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ISessionProfileCache _sessionCache;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        SegmentCommerceDbContext db, 
        ISessionProfileCache sessionCache,
        ILogger<RecommendationService> logger)
    {
        _db = db;
        _sessionCache = sessionCache;
        _logger = logger;
    }

    public async Task<List<RecommendedProduct>> GetRecommendationsAsync(Guid? profileId, int count = 8)
    {
        PersistentProfileEntity? profile = null;
        
        if (profileId.HasValue)
        {
            profile = await _db.PersistentProfiles.FindAsync(profileId.Value);
        }

        if (profile == null)
        {
            // No profile - return trending products
            return await GetTrendingRecommendationsAsync(count);
        }

        var recommendations = new List<ScoredProduct>();

        // 1. Get products from top interest categories
        var topInterests = profile.Interests
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .ToList();

        if (topInterests.Any())
        {
            var interestCategories = topInterests.Select(kv => kv.Key).ToList();
            var interestProducts = await _db.Products
                .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
                .Where(p => interestCategories.Contains(p.Category))
                .OrderByDescending(p => p.IsTrending)
                .ThenByDescending(p => p.UpdatedAt)
                .Take(count * 2)
                .ToListAsync();

            foreach (var product in interestProducts)
            {
                var interestScore = topInterests.FirstOrDefault(i => i.Key == product.Category).Value;
                recommendations.Add(new ScoredProduct
                {
                    Product = product,
                    Score = interestScore * 0.8, // Weight for interest-based
                    Reason = $"Based on your interest in {product.Category}",
                    Type = "interest"
                });
            }
        }

        // 2. Get products from preferred brands
        var topBrands = profile.BrandAffinities
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => kv.Key)
            .ToList();

        if (topBrands.Any())
        {
            var brandProducts = await _db.Products
                .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
                .Where(p => topBrands.Contains(p.Brand ?? ""))
                .Take(count)
                .ToListAsync();

            foreach (var product in brandProducts)
            {
                var existing = recommendations.FirstOrDefault(r => r.Product.Id == product.Id);
                if (existing != null)
                {
                    existing.Score += 0.2; // Boost if also brand match
                    existing.Reason += $" and brand preference for {product.Brand}";
                }
                else
                {
                    recommendations.Add(new ScoredProduct
                    {
                        Product = product,
                        Score = profile.BrandAffinities.GetValueOrDefault(product.Brand ?? "", 0) * 0.6,
                        Reason = $"From your preferred brand {product.Brand}",
                        Type = "brand"
                    });
                }
            }
        }

        // 3. Apply price preference filtering
        if (profile.PricePreferences != null)
        {
            var minPrice = profile.PricePreferences.MinObserved ?? 0;
            var maxPrice = profile.PricePreferences.MaxObserved ?? 1000;

            foreach (var rec in recommendations)
            {
                // Boost products in their price range
                if (rec.Product.Price >= minPrice && rec.Product.Price <= maxPrice)
                {
                    rec.Score *= 1.1;
                }
                // Penalize products outside price range
                else if (rec.Product.Price > maxPrice * 1.5m)
                {
                    rec.Score *= 0.7;
                }
            }

            // Boost deals if user prefers deals
            if (profile.PricePreferences.PrefersDeals)
            {
                foreach (var rec in recommendations.Where(r => r.Product.OriginalPrice > r.Product.Price))
                {
                    rec.Score *= 1.2;
                    rec.Type = "deal";
                    rec.Reason = "Great deal for you!";
                }
            }
        }

        // 4. Add some trending products for discovery (if we don't have enough)
        if (recommendations.Count < count)
        {
            var existingIds = recommendations.Select(r => r.Product.Id).ToHashSet();
            var trendingProducts = await _db.Products
                .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
                .Where(p => p.IsTrending && !existingIds.Contains(p.Id))
                .Take(count - recommendations.Count)
                .ToListAsync();

            foreach (var product in trendingProducts)
            {
                recommendations.Add(new ScoredProduct
                {
                    Product = product,
                    Score = 0.3, // Lower score for discovery
                    Reason = "Trending now",
                    Type = "trending"
                });
            }
        }

        // Sort by score and take top N
        return recommendations
            .OrderByDescending(r => r.Score)
            .Take(count)
            .Select(r => MapToRecommendedProduct(r.Product, r.Score, r.Reason, r.Type))
            .ToList();
    }

    public async Task<List<RecommendedProduct>> GetRecommendationsForSessionAsync(string sessionKey, int count = 8)
    {
        // Find session from in-memory cache
        var session = _sessionCache.Get(sessionKey);

        if (session == null)
        {
            return await GetTrendingRecommendationsAsync(count);
        }

        // If linked to profile, use profile recommendations
        if (session.PersistentProfileId.HasValue)
        {
            return await GetRecommendationsAsync(session.PersistentProfileId, count);
        }

        // Use session interests for recommendations
        if (session.Interests.Any())
        {
            var recommendations = new List<ScoredProduct>();
            var topInterests = session.Interests
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .ToList();

            var interestCategories = topInterests.Select(kv => kv.Key).ToList();
            var products = await _db.Products
                .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
                .Where(p => interestCategories.Contains(p.Category))
                .OrderByDescending(p => p.IsTrending)
                .Take(count * 2)
                .ToListAsync();

            foreach (var product in products)
            {
                var interestScore = topInterests.FirstOrDefault(i => i.Key == product.Category).Value;
                recommendations.Add(new ScoredProduct
                {
                    Product = product,
                    Score = interestScore,
                    Reason = $"Based on your browsing in {product.Category}",
                    Type = "session_interest"
                });
            }

            return recommendations
                .OrderByDescending(r => r.Score)
                .Take(count)
                .Select(r => MapToRecommendedProduct(r.Product, r.Score, r.Reason, r.Type))
                .ToList();
        }

        return await GetTrendingRecommendationsAsync(count);
    }

    public async Task<List<RecommendedProduct>> GetSimilarProductsAsync(int productId, int count = 4)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return [];

        // Find products in same category with similar tags
        var similarProducts = await _db.Products
            .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
            .Where(p => p.Id != productId)
            .Where(p => p.Category == product.Category || 
                       (p.Subcategory != null && p.Subcategory == product.Subcategory))
            .OrderByDescending(p => p.IsTrending)
            .Take(count * 2)
            .ToListAsync();

        // Score by tag overlap
        var productTags = product.Tags?.ToHashSet() ?? new HashSet<string>();
        
        return similarProducts
            .Select(p => new
            {
                Product = p,
                TagOverlap = p.Tags?.Count(t => productTags.Contains(t)) ?? 0,
                SameSubcategory = p.Subcategory == product.Subcategory
            })
            .OrderByDescending(x => x.TagOverlap)
            .ThenByDescending(x => x.SameSubcategory)
            .Take(count)
            .Select(x => MapToRecommendedProduct(
                x.Product, 
                0.5 + (x.TagOverlap * 0.1),
                $"Similar to {product.Name}",
                "similar"))
            .ToList();
    }

    public async Task<List<RecommendedProduct>> GetCrossSellAsync(List<int> cartProductIds, Guid? profileId, int count = 4)
    {
        if (!cartProductIds.Any()) return [];

        // Get cart products to understand what categories/brands are in cart
        var cartProducts = await _db.Products
            .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
            .Where(p => cartProductIds.Contains(p.Id))
            .ToListAsync();

        var cartCategories = cartProducts.Select(p => p.Category).Distinct().ToList();
        var cartBrands = cartProducts.Where(p => p.Brand != null).Select(p => p.Brand!).Distinct().ToList();

        // Find complementary products (same category or brand, but not in cart)
        var complementary = await _db.Products
            .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
            .Where(p => !cartProductIds.Contains(p.Id))
            .Where(p => cartCategories.Contains(p.Category) || 
                       (p.Brand != null && cartBrands.Contains(p.Brand)))
            .OrderByDescending(p => p.IsTrending)
            .Take(count * 2)
            .ToListAsync();

        // If we have a profile, boost items matching their preferences
        PersistentProfileEntity? profile = null;
        if (profileId.HasValue)
        {
            profile = await _db.PersistentProfiles.FindAsync(profileId.Value);
        }

        return complementary
            .Select(p => new
            {
                Product = p,
                Score = CalculateCrossSellScore(p, profile, cartProducts)
            })
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => MapToRecommendedProduct(
                x.Product,
                x.Score,
                "Pairs well with items in your cart",
                "cross_sell"))
            .ToList();
    }

    private async Task<List<RecommendedProduct>> GetTrendingRecommendationsAsync(int count)
    {
        var trending = await _db.Products
            .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
            .Where(p => p.IsTrending)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToListAsync();

        return trending
            .Select(p => MapToRecommendedProduct(p, 0.5, "Trending now", "trending"))
            .ToList();
    }

    private static double CalculateCrossSellScore(ProductEntity product, PersistentProfileEntity? profile, List<ProductEntity> cartProducts)
    {
        double score = 0.5;

        // Boost if product is trending
        if (product.IsTrending) score += 0.1;

        // Boost if on sale
        if (product.OriginalPrice > product.Price) score += 0.1;

        // Boost if matches profile interest
        if (profile?.Interests.ContainsKey(product.Category) == true)
        {
            score += profile.Interests[product.Category] * 0.3;
        }

        // Boost if matches profile brand preference
        if (profile?.BrandAffinities.ContainsKey(product.Brand ?? "") == true)
        {
            score += profile.BrandAffinities[product.Brand!] * 0.2;
        }

        return Math.Min(score, 1.0);
    }

    private static RecommendedProduct MapToRecommendedProduct(ProductEntity entity, double score, string reason, string type)
    {
        return new RecommendedProduct
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            OriginalPrice = entity.OriginalPrice,
            ImageUrl = entity.ImageUrl,
            Category = entity.Category,
            Subcategory = entity.Subcategory,
            Brand = entity.Brand,
            Tags = entity.Tags?.ToArray() ?? [],
            Seller = entity.Seller?.SellerProfile != null ? new RecommendedProductSeller
            {
                Id = entity.Seller.Id,
                Name = entity.Seller.SellerProfile.BusinessName,
                LogoUrl = entity.Seller.SellerProfile.LogoUrl ?? entity.Seller.AvatarUrl,
                IsVerified = entity.Seller.SellerProfile.IsVerified,
                Rating = entity.Seller.SellerProfile.Rating
            } : null,
            RelevanceScore = score,
            RecommendationReason = reason,
            RecommendationType = type,
            IsTrending = entity.IsTrending,
            IsOnSale = entity.OriginalPrice.HasValue && entity.OriginalPrice > entity.Price
        };
    }

    private class ScoredProduct
    {
        public ProductEntity Product { get; set; } = null!;
        public double Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
