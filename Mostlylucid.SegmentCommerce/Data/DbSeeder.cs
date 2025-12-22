using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(SegmentCommerceDbContext context)
    {
        await context.Database.MigrateAsync();

        if (!await context.Categories.AnyAsync())
        {
            await SeedCategoriesAsync(context);
        }

        if (!await context.Sellers.AnyAsync())
        {
            await SeedSellersAsync(context);
        }

        if (!await context.Products.AnyAsync())
        {
            await SeedProductsAsync(context);
        }
    }

    private static async Task SeedCategoriesAsync(SegmentCommerceDbContext context)
    {
        var categories = new List<CategoryEntity>
        {
            new() { Slug = "tech", DisplayName = "Technology", CssClass = "interest-tech", SortOrder = 1 },
            new() { Slug = "fashion", DisplayName = "Fashion", CssClass = "interest-fashion", SortOrder = 2 },
            new() { Slug = "home", DisplayName = "Home & Garden", CssClass = "interest-home", SortOrder = 3 },
            new() { Slug = "sport", DisplayName = "Sports", CssClass = "interest-sport", SortOrder = 4 },
            new() { Slug = "books", DisplayName = "Books", CssClass = "interest-books", SortOrder = 5 },
            new() { Slug = "food", DisplayName = "Food & Drink", CssClass = "interest-food", SortOrder = 6 }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }

    private static async Task SeedSellersAsync(SegmentCommerceDbContext context)
    {
        var sellers = new List<SellerEntity>
        {
            new()
            {
                Name = "SegmentCommerce",
                Email = "hello@segmentcommerce.test",
                Rating = 4.8,
                ReviewCount = 120,
                IsVerified = true
            }
        };

        await context.Sellers.AddRangeAsync(sellers);
        await context.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(SegmentCommerceDbContext context)
    {
        var sellerId = await context.Sellers.Select(s => s.Id).FirstAsync();
        var now = DateTime.UtcNow;

        var products = new List<ProductEntity>
        {
            // Tech
            NewProduct("Wireless Noise-Cancelling Headphones", "tech", 249.99m, 299.99m, "Premium over-ear headphones with active noise cancellation and 30-hour battery life.", "https://picsum.photos/seed/headphones/400/400", ["audio", "wireless", "premium"], sellerId, true, true, now),
            NewProduct("Mechanical Keyboard RGB", "tech", 129.99m, null, "Full-size mechanical keyboard with hot-swappable switches and per-key RGB lighting.", "https://picsum.photos/seed/keyboard/400/400", ["gaming", "peripherals", "rgb"], sellerId, false, false, now),
            NewProduct("4K Webcam Pro", "tech", 179.99m, null, "Ultra HD webcam with autofocus and low-light correction for professional video calls.", "https://picsum.photos/seed/webcam/400/400", ["video", "streaming", "work-from-home"], sellerId, false, false, now),
            NewProduct("Portable SSD 2TB", "tech", 199.99m, 249.99m, "Compact external SSD with USB-C and transfer speeds up to 1050MB/s.", "https://picsum.photos/seed/ssd/400/400", ["storage", "portable", "fast"], sellerId, false, false, now),

            // Fashion
            NewProduct("Classic Leather Jacket", "fashion", 299.99m, null, "Timeless genuine leather jacket with a modern slim fit.", "https://picsum.photos/seed/jacket/400/400", ["leather", "outerwear", "classic"], sellerId, false, true, now),
            NewProduct("Premium Cotton T-Shirt Pack", "fashion", 49.99m, null, "Set of 3 essential cotton t-shirts in neutral colours.", "https://picsum.photos/seed/tshirt/400/400", ["basics", "cotton", "essentials"], sellerId, false, false, now),
            NewProduct("Designer Sunglasses", "fashion", 189.99m, 229.99m, "UV400 polarised sunglasses with titanium frames.", "https://picsum.photos/seed/sunglasses/400/400", ["accessories", "summer", "uv-protection"], sellerId, true, false, now),
            NewProduct("Minimalist Watch", "fashion", 349.99m, null, "Elegant watch with sapphire crystal and Swiss movement.", "https://picsum.photos/seed/watch/400/400", ["accessories", "luxury", "minimalist"], sellerId, false, false, now),

            // Home
            NewProduct("Smart LED Bulb Kit", "home", 79.99m, null, "Set of 4 colour-changing smart bulbs compatible with all major assistants.", "https://picsum.photos/seed/bulbs/400/400", ["smart-home", "lighting", "energy-efficient"], sellerId, false, false, now),
            NewProduct("Ergonomic Office Chair", "home", 449.99m, 549.99m, "Fully adjustable mesh office chair with lumbar support.", "https://picsum.photos/seed/chair/400/400", ["office", "ergonomic", "comfort"], sellerId, false, true, now),
            NewProduct("Indoor Plant Collection", "home", 59.99m, null, "Curated set of 3 low-maintenance indoor plants with ceramic pots.", "https://picsum.photos/seed/plants/400/400", ["plants", "decor", "wellness"], sellerId, false, false, now),
            NewProduct("Aromatherapy Diffuser", "home", 39.99m, null, "Ultrasonic essential oil diffuser with ambient lighting.", "https://picsum.photos/seed/diffuser/400/400", ["wellness", "relaxation", "aromatherapy"], sellerId, false, false, now),

            // Sport
            NewProduct("Running Shoes Pro", "sport", 159.99m, null, "Lightweight running shoes with responsive cushioning and breathable mesh.", "https://picsum.photos/seed/shoes/400/400", ["running", "fitness", "performance"], sellerId, true, false, now),
            NewProduct("Yoga Mat Premium", "sport", 69.99m, null, "Non-slip yoga mat with alignment lines and carrying strap.", "https://picsum.photos/seed/yogamat/400/400", ["yoga", "fitness", "home-workout"], sellerId, false, false, now),
            NewProduct("Fitness Tracker Band", "sport", 89.99m, 119.99m, "Water-resistant fitness tracker with heart rate and sleep monitoring.", "https://picsum.photos/seed/fitband/400/400", ["wearable", "health", "tracking"], sellerId, false, false, now),
            NewProduct("Resistance Band Set", "sport", 29.99m, null, "Complete set of 5 resistance bands with different tension levels.", "https://picsum.photos/seed/bands/400/400", ["strength", "home-workout", "portable"], sellerId, false, false, now),

            // Books
            NewProduct("The Pragmatic Programmer", "books", 39.99m, null, "Classic software development book covering best practices and career advice.", "https://picsum.photos/seed/pragmatic/400/400", ["programming", "career", "classic"], sellerId, false, true, now),
            NewProduct("Designing Data-Intensive Applications", "books", 44.99m, null, "Comprehensive guide to building reliable and scalable data systems.", "https://picsum.photos/seed/dataintensive/400/400", ["programming", "data", "architecture"], sellerId, false, false, now),
            NewProduct("Atomic Habits", "books", 16.99m, null, "Practical strategies for building good habits and breaking bad ones.", "https://picsum.photos/seed/atomichabits/400/400", ["self-help", "productivity", "bestseller"], sellerId, true, false, now),
            NewProduct("Clean Code", "books", 29.99m, null, "Essential guide to writing maintainable and readable code.", "https://picsum.photos/seed/cleancode/400/400", ["programming", "software-development", "reference"], sellerId, false, false, now),

            // Food
            NewProduct("Artisan Coffee Beans", "food", 24.99m, null, "Single-origin arabica beans, medium roast, 1kg bag.", "https://picsum.photos/seed/coffee/400/400", ["coffee", "organic", "fairtrade"], sellerId, false, false, now),
            NewProduct("Matcha Green Tea", "food", 19.99m, null, "Premium organic green tea from Japanese gardens.", "https://picsum.photos/seed/tea/400/400", ["tea", "organic", "premium"], sellerId, false, false, now),
            NewProduct("Dark Chocolate Bar", "food", 14.99m, null, "Single-origin dark chocolate bar with 70% cacao.", "https://picsum.photos/seed/chocolate/400/400", ["chocolate", "snacks", "dark", "vegan"], sellerId, false, false, now),
            NewProduct("Organic Honey Jar", "food", 12.99m, null, "Raw wildflower honey from sustainable beekeepers.", "https://picsum.photos/seed/honey/400/400", ["pantry", "organic", "natural-sweetener"], sellerId, false, false, now),
            NewProduct("Extra Virgin Olive Oil", "food", 24.99m, null, "Cold-pressed extra virgin olive oil from Tuscany, 750ml.", "https://picsum.photos/seed/oliveoil/400/400", ["cooking", "italian", "organic", "premium"], sellerId, false, false, now),
            NewProduct("Gourmet Trail Mix", "food", 16.99m, null, "Energy-dense mix of nuts, seeds, and dried fruits.", "https://picsum.photos/seed/trailmix/400/400", ["snacks", "healthy", "energy"], sellerId, false, true, now)
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();
    }

    private static ProductEntity NewProduct(
        string name,
        string category,
        decimal price,
        decimal? originalPrice,
        string description,
        string imageUrl,
        List<string> tags,
        int sellerId,
        bool isTrending,
        bool isFeatured,
        DateTime now)
    {
        return new ProductEntity
        {
            Name = name,
            Handle = Slugify(name),
            Category = category,
            CategoryPath = category,
            Price = price,
            OriginalPrice = originalPrice,
            Description = description,
            ImageUrl = imageUrl,
            Tags = tags,
            SellerId = sellerId,
            IsTrending = isTrending,
            IsFeatured = isFeatured,
            PublishedAt = now.AddDays(-7),
            UpdatedAt = now,
            CreatedAt = now
        };
    }

    private static string Slugify(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "")
            .Replace("&", "and")
            .Replace("--", "-");
    }
}
