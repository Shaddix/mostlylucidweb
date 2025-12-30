using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class ProductServiceTests
{
    private static SegmentCommerceDbContext CreateContext() => TestDbContextBase.Create();

    private static async Task SeedProducts(SegmentCommerceDbContext context)
    {
        // Create a user with seller profile
        var sellerUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            DisplayName = "Test Seller",
            SellerProfile = new SellerProfileEntity { BusinessName = "Test Business" }
        };
        context.Users.Add(sellerUser);
        await context.SaveChangesAsync();

        var products = new[]
        {
            new ProductEntity { Name = "Tech Product 1", Category = "tech", Price = 100, IsTrending = true, IsFeatured = false, SellerId = sellerUser.Id, Handle = "tech-1", Tags = ["gadget"] },
            new ProductEntity { Name = "Tech Product 2", Category = "tech", Price = 200, OriginalPrice = 250, IsTrending = false, IsFeatured = true, SellerId = sellerUser.Id, Handle = "tech-2", Tags = ["laptop"] },
            new ProductEntity { Name = "Fashion Item 1", Category = "fashion", Price = 50, IsTrending = true, IsFeatured = false, SellerId = sellerUser.Id, Handle = "fashion-1", Tags = ["shirt"] },
            new ProductEntity { Name = "Fashion Item 2", Category = "fashion", Price = 75, IsTrending = false, IsFeatured = true, SellerId = sellerUser.Id, Handle = "fashion-2", Tags = ["pants"] },
            new ProductEntity { Name = "Home Product", Category = "home", Price = 150, OriginalPrice = 200, IsTrending = false, IsFeatured = false, SellerId = sellerUser.Id, Handle = "home-1", Tags = ["decor"] },
        };

        context.Products.AddRange(products);

        var categories = new[]
        {
            new CategoryEntity { Slug = "tech", DisplayName = "Technology", SortOrder = 1 },
            new CategoryEntity { Slug = "fashion", DisplayName = "Fashion", SortOrder = 2 },
            new CategoryEntity { Slug = "home", DisplayName = "Home", SortOrder = 3 },
        };
        context.Categories.AddRange(categories);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProducts()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var products = await service.GetAllAsync();

        Assert.Equal(5, products.Count());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProduct_WhenExists()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);
        var firstProduct = await context.Products.FirstAsync();

        var product = await service.GetByIdAsync(firstProduct.Id);

        Assert.NotNull(product);
        Assert.Equal(firstProduct.Name, product.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        using var context = CreateContext();
        var service = new ProductService(context);

        var product = await service.GetByIdAsync(99999);

        Assert.Null(product);
    }

    [Fact]
    public async Task GetByCategoryAsync_ReturnsOnlyMatchingCategory()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var products = await service.GetByCategoryAsync("tech");

        Assert.Equal(2, products.Count());
        Assert.All(products, p => Assert.Equal("tech", p.Category));
    }

    [Fact]
    public async Task GetTrendingAsync_ReturnsTrendingProducts()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var products = await service.GetTrendingAsync();

        Assert.Equal(2, products.Count());
        Assert.All(products, p => Assert.True(p.IsTrending));
    }

    [Fact]
    public async Task GetFeaturedAsync_ReturnsFeaturedProducts()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var products = await service.GetFeaturedAsync();

        Assert.Equal(2, products.Count());
    }

    [Fact]
    public async Task GetOnSaleAsync_ReturnsDiscountedProducts()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var products = await service.GetOnSaleAsync();

        Assert.Equal(2, products.Count());
        Assert.All(products, p => Assert.True(p.OriginalPrice > p.Price));
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsAllCategories()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var categories = await service.GetCategoriesAsync();

        Assert.Equal(3, categories.Count());
        Assert.Contains("tech", categories);
        Assert.Contains("fashion", categories);
        Assert.Contains("home", categories);
    }

    [Fact]
    public async Task GetPersonalisedAsync_ReturnsTrending_WhenNoInterests()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);
        var emptySignature = new InterestSignature();

        var products = await service.GetPersonalisedAsync(emptySignature);

        // Should fall back to trending
        Assert.All(products, p => Assert.True(p.IsTrending));
    }

    [Fact]
    public async Task GetPersonalisedAsync_PrioritizesInterests()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);
        var signature = new InterestSignature
        {
            Interests = new Dictionary<string, InterestWeight>
            {
                ["tech"] = new InterestWeight { Category = "tech", Weight = 0.9, ReinforcementCount = 5 }
            }
        };

        var products = (await service.GetPersonalisedAsync(signature, 5)).ToList();

        // Tech products should be prioritised
        Assert.True(products.Any(p => p.Category == "tech"));
    }

    [Fact]
    public async Task GetTrendingAsync_RespectsCount()
    {
        using var context = CreateContext();
        await SeedProducts(context);
        var service = new ProductService(context);

        var products = await service.GetTrendingAsync(1);

        Assert.Single(products);
    }

}
