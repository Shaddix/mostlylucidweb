using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(SegmentCommerceDbContext context)
    {
        // Ensure database is created and migrations applied
        await context.Database.MigrateAsync();

        // Seed categories if not present
        if (!await context.Categories.AnyAsync())
        {
            await SeedCategoriesAsync(context);
        }

        // Seed products if not present
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

    private static async Task SeedProductsAsync(SegmentCommerceDbContext context)
    {
        var products = new List<ProductEntity>
        {
            // Tech products
            new()
            {
                Name = "Wireless Noise-Cancelling Headphones",
                Category = "tech",
                Description = "Premium over-ear headphones with active noise cancellation and 30-hour battery life.",
                Price = 249.99m,
                OriginalPrice = 299.99m,
                ImageUrl = "https://picsum.photos/seed/headphones/400/400",
                Tags = ["audio", "wireless", "premium"],
                IsTrending = true
            },
            new()
            {
                Name = "Mechanical Keyboard RGB",
                Category = "tech",
                Description = "Full-size mechanical keyboard with hot-swappable switches and per-key RGB lighting.",
                Price = 129.99m,
                ImageUrl = "https://picsum.photos/seed/keyboard/400/400",
                Tags = ["gaming", "peripherals", "rgb"]
            },
            new()
            {
                Name = "4K Webcam Pro",
                Category = "tech",
                Description = "Ultra HD webcam with autofocus and low-light correction for professional video calls.",
                Price = 179.99m,
                ImageUrl = "https://picsum.photos/seed/webcam/400/400",
                Tags = ["video", "streaming", "work-from-home"]
            },
            new()
            {
                Name = "Portable SSD 2TB",
                Category = "tech",
                Description = "Compact external SSD with USB-C and transfer speeds up to 1050MB/s.",
                Price = 199.99m,
                OriginalPrice = 249.99m,
                ImageUrl = "https://picsum.photos/seed/ssd/400/400",
                Tags = ["storage", "portable", "fast"]
            },

            // Fashion products
            new()
            {
                Name = "Classic Leather Jacket",
                Category = "fashion",
                Description = "Timeless genuine leather jacket with a modern slim fit.",
                Price = 299.99m,
                ImageUrl = "https://picsum.photos/seed/jacket/400/400",
                Tags = ["leather", "outerwear", "classic"],
                IsFeatured = true
            },
            new()
            {
                Name = "Premium Cotton T-Shirt Pack",
                Category = "fashion",
                Description = "Set of 3 essential cotton t-shirts in neutral colours.",
                Price = 49.99m,
                ImageUrl = "https://picsum.photos/seed/tshirt/400/400",
                Tags = ["basics", "cotton", "essentials"]
            },
            new()
            {
                Name = "Designer Sunglasses",
                Category = "fashion",
                Description = "UV400 polarised sunglasses with titanium frames.",
                Price = 189.99m,
                OriginalPrice = 229.99m,
                ImageUrl = "https://picsum.photos/seed/sunglasses/400/400",
                Tags = ["accessories", "summer", "uv-protection"],
                IsTrending = true
            },
            new()
            {
                Name = "Minimalist Watch",
                Category = "fashion",
                Description = "Elegant watch with sapphire crystal and Swiss movement.",
                Price = 349.99m,
                ImageUrl = "https://picsum.photos/seed/watch/400/400",
                Tags = ["accessories", "luxury", "minimalist"]
            },

            // Home products
            new()
            {
                Name = "Smart LED Bulb Kit",
                Category = "home",
                Description = "Set of 4 colour-changing smart bulbs compatible with all major assistants.",
                Price = 79.99m,
                ImageUrl = "https://picsum.photos/seed/bulbs/400/400",
                Tags = ["smart-home", "lighting", "energy-efficient"]
            },
            new()
            {
                Name = "Ergonomic Office Chair",
                Category = "home",
                Description = "Fully adjustable mesh office chair with lumbar support.",
                Price = 449.99m,
                OriginalPrice = 549.99m,
                ImageUrl = "https://picsum.photos/seed/chair/400/400",
                Tags = ["office", "ergonomic", "comfort"],
                IsFeatured = true
            },
            new()
            {
                Name = "Indoor Plant Collection",
                Category = "home",
                Description = "Curated set of 3 low-maintenance indoor plants with ceramic pots.",
                Price = 59.99m,
                ImageUrl = "https://picsum.photos/seed/plants/400/400",
                Tags = ["plants", "decor", "wellness"]
            },
            new()
            {
                Name = "Aromatherapy Diffuser",
                Category = "home",
                Description = "Ultrasonic essential oil diffuser with ambient lighting.",
                Price = 39.99m,
                ImageUrl = "https://picsum.photos/seed/diffuser/400/400",
                Tags = ["wellness", "relaxation", "aromatherapy"]
            },

            // Sport products
            new()
            {
                Name = "Running Shoes Pro",
                Category = "sport",
                Description = "Lightweight running shoes with responsive cushioning and breathable mesh.",
                Price = 159.99m,
                ImageUrl = "https://picsum.photos/seed/shoes/400/400",
                Tags = ["running", "fitness", "performance"],
                IsTrending = true
            },
            new()
            {
                Name = "Yoga Mat Premium",
                Category = "sport",
                Description = "Non-slip yoga mat with alignment lines and carrying strap.",
                Price = 69.99m,
                ImageUrl = "https://picsum.photos/seed/yogamat/400/400",
                Tags = ["yoga", "fitness", "home-workout"]
            },
            new()
            {
                Name = "Fitness Tracker Band",
                Category = "sport",
                Description = "Water-resistant fitness tracker with heart rate and sleep monitoring.",
                Price = 89.99m,
                OriginalPrice = 119.99m,
                ImageUrl = "https://picsum.photos/seed/fitband/400/400",
                Tags = ["wearable", "health", "tracking"]
            },
            new()
            {
                Name = "Resistance Band Set",
                Category = "sport",
                Description = "Complete set of 5 resistance bands with different tension levels.",
                Price = 29.99m,
                ImageUrl = "https://picsum.photos/seed/bands/400/400",
                Tags = ["strength", "home-workout", "portable"]
            },

            // Books products
            new()
            {
                Name = "The Pragmatic Programmer",
                Category = "books",
                Description = "Classic software development book covering best practices and career advice.",
                Price = 39.99m,
                ImageUrl = "https://picsum.photos/seed/pragmatic/400/400",
                Tags = ["programming", "career", "classic"],
                IsFeatured = true
            },
            new()
            {
                Name = "Designing Data-Intensive Applications",
                Category = "books",
                Description = "Comprehensive guide to building reliable and scalable data systems.",
                Price = 44.99m,
                ImageUrl = "https://picsum.photos/seed/dataintensive/400/400",
                Tags = ["programming", "data", "architecture"]
            },
            new()
            {
                Name = "Atomic Habits",
                Category = "books",
                Description = "Practical strategies for building good habits and breaking bad ones.",
                Price = 16.99m,
                ImageUrl = "https://picsum.photos/seed/atomichabits/400/400",
                Tags = ["self-help", "productivity", "bestseller"],
                IsTrending = true
            },

            // Food products
            new()
            {
                Name = "Artisan Coffee Beans",
                Category = "food",
                Description = "Single-origin arabica beans, medium roast, 1kg bag.",
                Price = 24.99m,
                ImageUrl = "https://picsum.photos/seed/coffee/400/400",
                Tags = ["coffee", "organic", "fairtrade"]
            },
            new()
            {
                Name = "Gourmet Chocolate Selection",
                Category = "food",
                Description = "Handcrafted Belgian chocolate assortment, 24 pieces.",
                Price = 34.99m,
                ImageUrl = "https://picsum.photos/seed/chocolate/400/400",
                Tags = ["chocolate", "gift", "luxury"],
                IsFeatured = true
            },
            new()
            {
                Name = "Olive Oil Extra Virgin",
                Category = "food",
                Description = "Cold-pressed extra virgin olive oil from Tuscany, 750ml.",
                Price = 19.99m,
                ImageUrl = "https://picsum.photos/seed/oliveoil/400/400",
                Tags = ["cooking", "italian", "organic"]
            }
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();
    }
}
