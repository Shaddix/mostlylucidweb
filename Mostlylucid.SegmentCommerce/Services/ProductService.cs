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

        return entities.Select(e => MapToModel(e));
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

        return entities.Select(e => MapToModel(e));
    }

    public async Task<IEnumerable<Product>> GetTrendingAsync(int count = 10)
    {
        var entities = await _context.Products
            .Where(p => p.IsTrending)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToListAsync();

        return entities.Select(e => MapToModel(e));
    }

    public async Task<IEnumerable<Product>> GetFeaturedAsync(int count = 10)
    {
        var entities = await _context.Products
            .Where(p => p.IsFeatured)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToListAsync();

        return entities.Select(e => MapToModel(e));
    }

    public async Task<IEnumerable<Product>> GetOnSaleAsync(int count = 10)
    {
        var entities = await _context.Products
            .Where(p => p.OriginalPrice.HasValue && p.OriginalPrice > p.Price)
            .OrderByDescending(p => p.OriginalPrice - p.Price)
            .Take(count)
            .ToListAsync();

        return entities.Select(e => MapToModel(e));
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
            return await GetTrendingAsync(count);
        }

        var categories = signature.Interests.Keys.ToList();
        
        // Fetch products from database first
        var products = await _context.Products
            .Where(p => categories.Contains(p.Category))
            .OrderByDescending(p => p.IsTrending)
            .Take(count * 3) // Fetch more to allow for scoring/filtering
            .ToListAsync();

        // Score in memory (cannot be translated to SQL)
        return products
            .Select(p => new { Entity = p, Score = GetScore(signature, p.Category) })
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Entity.IsTrending)
            .Take(count)
            .Select(s => MapToModel(s.Entity, s.Score, s.Score > 0.5));
    }

    /// <summary>
    /// Search products by name, description, or tags.
    /// </summary>
    public async Task<IEnumerable<Product>> SearchAsync(string query, int count = 20)
    {
        var entities = await _context.Products
            .Where(p => 
                EF.Functions.ILike(p.Name, $"%{query}%") ||
                EF.Functions.ILike(p.Description, $"%{query}%") ||
                p.Tags.Any(t => EF.Functions.ILike(t, $"%{query}%")))
            .Take(count)
            .ToListAsync();

        return entities.Select(e => MapToModel(e));
    }

    private static double GetScore(InterestSignature signature, string category)
    {
        return signature.Interests.TryGetValue(category, out var interest) ? interest.EffectiveWeight : 0.0;
    }

    private static Product MapToModel(ProductEntity entity, double relevanceScore = 0, bool isRecommended = false)
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
            IsRecommended = isRecommended,
            RelevanceScore = relevanceScore
        };
    }
}
