using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;

namespace Mostlylucid.SegmentCommerce.Services;

/// <summary>
/// Product service using EF Core with PostgreSQL.
/// </summary>
public class ProductService
{
    private readonly SegmentCommerceDbContext _context;

    public ProductService(SegmentCommerceDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        var entities = await _context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        var entity = await _context.Products.FindAsync(id);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        var entities = await _context.Products
            .Where(p => p.Category == category.ToLowerInvariant())
            .OrderBy(p => p.Name)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<IEnumerable<Product>> GetTrendingAsync(int count = 10)
    {
        var entities = await _context.Products
            .Where(p => p.IsTrending)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<IEnumerable<Product>> GetFeaturedAsync(int count = 10)
    {
        var entities = await _context.Products
            .Where(p => p.IsFeatured)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<IEnumerable<Product>> GetOnSaleAsync(int count = 10)
    {
        var entities = await _context.Products
            .Where(p => p.OriginalPrice.HasValue && p.OriginalPrice > p.Price)
            .OrderByDescending(p => p.OriginalPrice - p.Price)
            .Take(count)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        return await _context.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Slug)
            .ToListAsync();
    }

    public async Task<CategoryEntity?> GetCategoryAsync(string slug)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Slug == slug.ToLowerInvariant());
    }

    /// <summary>
    /// Get products personalised based on the user's interest signature.
    /// </summary>
    public async Task<IEnumerable<Product>> GetPersonalisedAsync(InterestSignature signature, int count = 8)
    {
        if (!signature.Interests.Any())
        {
            // No interests yet - return trending items
            return await GetTrendingAsync(count);
        }

        // Get all products
        var allProducts = await _context.Products.ToListAsync();

        // Score each product based on interest match
        var scored = allProducts.Select(p =>
        {
            var score = 0.0;
            if (signature.Interests.TryGetValue(p.Category, out var interest))
            {
                score = interest.EffectiveWeight;
            }

            return new { Entity = p, Score = score };
        });

        // Return products sorted by relevance score
        return scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Entity.IsTrending)
            .Take(count)
            .Select(s =>
            {
                var product = MapToModel(s.Entity);
                product.RelevanceScore = s.Score;
                product.IsRecommended = s.Score > 0.5;
                return product;
            });
    }

    /// <summary>
    /// Search products by name, description, or tags.
    /// </summary>
    public async Task<IEnumerable<Product>> SearchAsync(string query, int count = 20)
    {
        var lowerQuery = query.ToLowerInvariant();

        var entities = await _context.Products
            .Where(p => 
                EF.Functions.ILike(p.Name, $"%{query}%") ||
                EF.Functions.ILike(p.Description, $"%{query}%") ||
                p.Tags.Any(t => EF.Functions.ILike(t, $"%{query}%")))
            .Take(count)
            .ToListAsync();

        return entities.Select(MapToModel);
    }

    private static Product MapToModel(ProductEntity entity)
    {
        return new Product
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            OriginalPrice = entity.OriginalPrice,
            ImageUrl = entity.ImageUrl,
            Category = entity.Category,
            Tags = entity.Tags,
            IsTrending = entity.IsTrending,
            IsRecommended = entity.IsFeatured
        };
    }
}
