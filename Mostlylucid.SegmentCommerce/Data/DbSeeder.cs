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
        var categories = await context.Categories.Select(c => c.Slug).ToListAsync();
        var products = new List<ProductEntity>();

        // Tech products (12 products)
        products.AddRange(CreateTechProducts("tech"));
        products.AddRange(CreateTechProducts("audio"));
        products.AddRange(CreateTechProducts("gaming"));

        // Fashion products (10 products)
        products.AddRange(CreateFashionProducts("clothing"));
        products.AddRange(CreateFashionProducts("accessories"));

        // Home products (10 products)
        products.AddRange(CreateHomeProducts("furniture"));
        products.AddRange(CreateHomeProducts("lighting"));
        products.AddRange(CreateHomeProducts("kitchen"));
        products.AddRange(CreateHomeProducts("decor"));

        // Sport products (10 products)
        products.AddRange(CreateSportProducts("equipment"));
        products.AddRange(CreateSportProducts("apparel"));

        // Books products (8 products)
        products.AddRange(CreateBooksProducts("development"));
        products.AddRange(CreateBooksProducts("fiction"));

        // Food products (10 products)
        products.AddRange(CreateFoodProducts("beverages"));
        products.AddRange(CreateFoodProducts("snacks"));
        products.AddRange(CreateFoodProducts("ingredients"));

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();
    }

    private static List<ProductEntity> CreateTechProducts(string subcategory)
    {
        var basePrice = 29.99m;
        var originalPrice = subcategory == "audio" ? 59.99m : 34.99m;

        return new List<ProductEntity>
        {
            new() { Name = "Wireless Noise-Cancelling Headphones", Category = "tech", Subcategory = subcategory, Price = basePrice, OriginalPrice = originalPrice, Description = "Premium over-ear headphones with active noise cancellation and 30-hour battery life.", ImageUrl = "https://picsum.photos/seed/headphones/400/400", Tags = ["audio", "wireless", "premium"], IsTrending = true },
            new() { Name = "Mechanical Keyboard RGB", Category = "tech", Subcategory = "gaming", Price = 129.99m, Description = "Full-size mechanical keyboard with hot-swappable switches and per-key RGB lighting.", ImageUrl = "https://picsum.photos/seed/keyboard/400/400", Tags = ["gaming", "peripherals", "rgb"] },
            new() { Name = "4K Webcam Pro", Category = "tech", Subcategory = "streaming", Price = 179.99m, Description = "Ultra HD webcam with autofocus and low-light correction for professional video calls.", ImageUrl = "https://picsum.photos/seed/webcam/400/400", Tags = ["video", "streaming", "work-from-home"] },
            new() { Name = "Portable SSD 2TB", Category = "tech", Subcategory = "storage", Price = 199.99m, OriginalPrice = 249.99m, Description = "Compact external SSD with USB-C and transfer speeds up to 1050MB/s.", ImageUrl = "https://picsum.photos/seed/ssd/400/400", Tags = ["storage", "portable", "fast"] }
        };
    }

    private static List<ProductEntity> CreateFashionProducts(string subcategory)
    {
        return new List<ProductEntity>
        {
            new() { Name = "Classic Leather Jacket", Category = "fashion", Subcategory = subcategory, Price = 299.99m, Description = "Timeless genuine leather jacket with a modern slim fit.", ImageUrl = "https://picsum.photos/seed/jacket/400/400", Tags = ["leather", "outerwear", "classic"], IsFeatured = true },
            new() { Name = "Premium Cotton T-Shirt Pack", Category = "fashion", Subcategory = subcategory, Price = 49.99m, Description = "Set of 3 essential cotton t-shirts in neutral colours.", ImageUrl = "https://picsum.photos/seed/tshirt/400/400", Tags = ["basics", "cotton", "essentials"] },
            new() { Name = "Designer Sunglasses", Category = "fashion", Subcategory = "accessories", Price = 189.99m, OriginalPrice = 229.99m, Description = "UV400 polarised sunglasses with titanium frames.", ImageUrl = "https://picsum.photos/seed/sunglasses/400/400", Tags = ["accessories", "summer", "uv-protection"], IsTrending = true },
            new() { Name = "Minimalist Watch", Category = "fashion", Subcategory = "accessories", Price = 349.99m, Description = "Elegant watch with sapphire crystal and Swiss movement.", ImageUrl = "https://picsum.photos/seed/watch/400/400", Tags = ["accessories", "luxury", "minimalist"] },
            new() { Name = "Slim Fit Chino Pants", Category = "fashion", Subcategory = subcategory, Price = 89.99m, Description = "Modern slim-fit chinos with stretch comfort.", ImageUrl = "https://picsum.photos/seed/pants/400/400", Tags = ["casual", "chinos", "slim-fit"] },
            new() { Name = "Wool Blend Sweater", Category = "fashion", Subcategory = subcategory, Price = 129.99m, Description = "Soft merino wool blend sweater perfect for layering.", ImageUrl = "https://picsum.photos/seed/sweater/400/400", Tags = ["knitwear", "winter", "warm"] },
            new() { Name = "Linen Summer Dress", Category = "fashion", Subcategory = subcategory, Price = 179.99m, Description = "Lightweight linen dress with breathable fabric and elegant cut.", ImageUrl = "https://picsum.photos/seed/dress/400/400", Tags = ["summer", "dresses", "linen"] },
            new() { Name = "Canvas Tote Bag", Category = "fashion", Subcategory = subcategory, Price = 59.99m, Description = "Durable canvas tote with reinforced handles and inner pocket.", ImageUrl = "https://picsum.photos/seed/tote/400/400", Tags = ["bags", "accessories", "everyday"] },
            new() { Name = "Leather Belt", Category = "fashion", Subcategory = subcategory, Price = 49.99m, Description = "Genuine leather belt with brushed metal buckle.", ImageUrl = "https://picsum.photos/seed/belt/400/400", Tags = ["accessories", "leather", "classic"] }
        };
    }

    private static List<ProductEntity> CreateHomeProducts(string subcategory)
    {
        return new List<ProductEntity>
        {
            new() { Name = "Smart LED Bulb Kit", Category = "home", Subcategory = subcategory, Price = 79.99m, Description = "Set of 4 colour-changing smart bulbs compatible with all major assistants.", ImageUrl = "https://picsum.photos/seed/bulbs/400/400", Tags = ["smart-home", "lighting", "energy-efficient"] },
            new() { Name = "Ergonomic Office Chair", Category = "home", Subcategory = subcategory, Price = 449.99m, OriginalPrice = 549.99m, Description = "Fully adjustable mesh office chair with lumbar support.", ImageUrl = "https://picsum.photos/seed/chair/400/400", Tags = ["office", "ergonomic", "comfort"], IsFeatured = true },
            new() { Name = "Indoor Plant Collection", Category = "home", Subcategory = subcategory, Price = 59.99m, Description = "Curated set of 3 low-maintenance indoor plants with ceramic pots.", ImageUrl = "https://picsum.photos/seed/plants/400/400", Tags = ["plants", "decor", "wellness"] },
            new() { Name = "Aromatherapy Diffuser", Category = "home", Subcategory = subcategory, Price = 39.99m, Description = "Ultrasonic essential oil diffuser with ambient lighting.", ImageUrl = "https://picsum.photos/seed/diffuser/400/400", Tags = ["wellness", "relaxation", "aromatherapy"] },
            new() { Name = "Modern Pendant Light", Category = "home", Subcategory = subcategory, Price = 149.99m, Description = "Minimalist geometric pendant with warm LED glow.", ImageUrl = "https://picsum.photos/seed/pendant/400/400", Tags = ["lighting", "modern", "decor"] },
            new() { Name = "Bamboo Storage Baskets", Category = "home", Subcategory = subcategory, Price = 39.99m, Description = "Set of 3 sustainable bamboo storage baskets.", ImageUrl = "https://picsum.photos/seed/baskets/400/400", Tags = ["storage", "organizing", "bamboo", "sustainable"] },
            new() { Name = "Silk Pillow Set", Category = "home", Subcategory = subcategory, Price = 89.99m, Description = "100% silk pillowcases with hypoallergenic fill.", ImageUrl = "https://picsum.photos/seed/pillows/400/400", Tags = ["bedroom", "textiles", "comfort"] },
            new() { Name = "Cast Iron Skillet", Category = "home", Subcategory = subcategory, Price = 69.99m, Description = "Pre-seasoned cast iron skillet with excellent heat retention.", ImageUrl = "https://picsum.photos/seed/skillet/400/400", Tags = ["kitchen", "cookware", "cast-iron"] },
            new() { Name = "Robot Vacuum", Category = "home", Subcategory = subcategory, Price = 349.99m, OriginalPrice = 449.99m, Description = "Smart robot vacuum with mapping and self-emptying base.", ImageUrl = "https://picsum.photos/seed/vacuum/400/400", Tags = ["smart-home", "cleaning", "robot"], IsFeatured = true }
        };
    }

    private static List<ProductEntity> CreateSportProducts(string subcategory)
    {
        return new List<ProductEntity>
        {
            new() { Name = "Running Shoes Pro", Category = "sport", Subcategory = subcategory, Price = 159.99m, Description = "Lightweight running shoes with responsive cushioning and breathable mesh.", ImageUrl = "https://picsum.photos/seed/shoes/400/400", Tags = ["running", "fitness", "performance"], IsTrending = true },
            new() { Name = "Yoga Mat Premium", Category = "sport", Subcategory = subcategory, Price = 69.99m, Description = "Non-slip yoga mat with alignment lines and carrying strap.", ImageUrl = "https://picsum.photos/seed/yogamat/400/400", Tags = ["yoga", "fitness", "home-workout"] },
            new() { Name = "Fitness Tracker Band", Category = "sport", Subcategory = subcategory, Price = 89.99m, OriginalPrice = 119.99m, Description = "Water-resistant fitness tracker with heart rate and sleep monitoring.", ImageUrl = "https://picsum.photos/seed/fitband/400/400", Tags = ["wearable", "health", "tracking"] },
            new() { Name = "Resistance Band Set", Category = "sport", Subcategory = subcategory, Price = 29.99m, Description = "Complete set of 5 resistance bands with different tension levels.", ImageUrl = "https://picsum.photos/seed/bands/400/400", Tags = ["strength", "home-workout", "portable"] },
            new() { Name = "Training Gloves", Category = "sport", Subcategory = subcategory, Price = 49.99m, Description = "Breathable training gloves with padded palms.", ImageUrl = "https://picsum.photos/seed/gloves/400/400", Tags = ["fitness", "training", "gloves"] },
            new() { Name = "Adjustable Dumbbells", Category = "sport", Subcategory = subcategory, Price = 199.99m, Description = "Set of 5 quick-adjust dumbbells from 5kg to 25kg.", ImageUrl = "https://picsum.photos/seed/dumbbells/400/400", Tags = ["strength", "weights", "adjustable"] },
            new() { Name = "Pro Skipping Rope", Category = "sport", Subcategory = subcategory, Price = 34.99m, Description = "Professional speed rope with weighted handles.", ImageUrl = "https://picsum.photos/seed/rope/400/400", Tags = ["cardio", "fitness", "crossfit"] },
            new() { Name = "Compression Shorts", Category = "sport", Subcategory = subcategory, Price = 59.99m, Description = "Moisture-wicking compression shorts with elastic waistband.", ImageUrl = "https://picsum.photos/seed/shorts/400/400", Tags = ["activewear", "running", "compression"] }
        };
    }

    private static List<ProductEntity> CreateBooksProducts(string subcategory)
    {
        return new List<ProductEntity>
        {
            new() { Name = "The Pragmatic Programmer", Category = "books", Subcategory = subcategory, Price = 39.99m, Description = "Classic software development book covering best practices and career advice.", ImageUrl = "https://picsum.photos/seed/pragmatic/400/400", Tags = ["programming", "career", "classic"], IsFeatured = true },
            new() { Name = "Designing Data-Intensive Applications", Category = "books", Subcategory = subcategory, Price = 44.99m, Description = "Comprehensive guide to building reliable and scalable data systems.", ImageUrl = "https://picsum.photos/seed/dataintensive/400/400", Tags = ["programming", "data", "architecture"] },
            new() { Name = "Atomic Habits", Category = "books", Subcategory = subcategory, Price = 16.99m, Description = "Practical strategies for building good habits and breaking bad ones.", ImageUrl = "https://picsum.photos/seed/atomichabits/400/400", Tags = ["self-help", "productivity", "bestseller"], IsTrending = true },
            new() { Name = "Clean Code", Category = "books", Subcategory = subcategory, Price = 29.99m, Description = "Essential guide to writing maintainable and readable code.", ImageUrl = "https://picsum.photos/seed/cleancode/400/400", Tags = ["programming", "software-development", "reference"] },
            new() { Name = "The Algorithm Design Manual", Category = "books", Subcategory = subcategory, Price = 49.99m, Description = "Beautifully illustrated guide to algorithmic thinking and problem-solving.", ImageUrl = "https://picsum.photos/seed/algorithms/400/400", Tags = ["computer-science", "algorithms", "education"] },
            new() { Name = "Refactoring UI Patterns", Category = "books", Subcategory = subcategory, Price = 54.99m, Description = "Modern guide to restructuring legacy codebases for better maintainability.", ImageUrl = "https://picsum.photos/seed/refactoring/400/400", Tags = ["programming", "software-design", "clean-code"] },
            new() { Name = "Domain-Driven Design", Category = "books", Subcategory = subcategory, Price = 39.99m, Description = "Strategic approach to software design based on business domain.", ImageUrl = "https://picsum.photos/seed/ddd/400/400", Tags = ["software-architecture", "design", "enterprise"] },
            new() { Name = "API Design Best Practices", Category = "books", Subcategory = subcategory, Price = 59.99m, Description = "Comprehensive patterns for building robust and scalable APIs.", ImageUrl = "https://picsum.photos/seed/apidesign/400/400", Tags = ["programming", "api", "rest", "backend"] },
            new() { Name = "Microservices Patterns", Category = "books", Subcategory = subcategory, Price = 64.99m, Description = "Design patterns for distributed systems architecture.", ImageUrl = "https://picsum.photos/seed/microservices/400/400", Tags = ["software-architecture", "distributed-systems", "devops"] },
            new() { Name = "System Design Interview", Category = "books", Subcategory = subcategory, Price = 34.99m, Description = "Essential preparation guide for system design job interviews.", ImageUrl = "https://picsum.photos/seed/sdinterview/400/400", Tags = ["career", "interviews", "education"] }
        };
    }

    private static List<ProductEntity> CreateFoodProducts(string subcategory)
    {
        return new List<ProductEntity>
        {
            new() { Name = "Artisan Coffee Beans", Category = "food", Subcategory = subcategory, Price = 24.99m, Description = "Single-origin arabica beans, medium roast, 1kg bag.", ImageUrl = "https://picsum.photos/seed/coffee/400/400", Tags = ["coffee", "organic", "fairtrade"] },
            new() { Name = "Matcha Green Tea", Category = "food", Subcategory = subcategory, Price = 19.99m, Description = "Premium organic green tea from Japanese gardens.", ImageUrl = "https://picsum.photos/seed/tea/400/400", Tags = ["tea", "organic", "premium"] },
            new() { Name = "Dark Chocolate Bar", Category = "food", Subcategory = subcategory, Price = 14.99m, Description = "Single-origin dark chocolate bar with 70% cacao.", ImageUrl = "https://picsum.photos/seed/chocolate/400/400", Tags = ["chocolate", "snacks", "dark", "vegan"] },
            new() { Name = "Organic Honey Jar", Category = "food", Subcategory = subcategory, Price = 12.99m, Description = "Raw wildflower honey from sustainable beekeepers.", ImageUrl = "https://picsum.photos/seed/honey/400/400", Tags = ["pantry", "organic", "natural-sweetener"] },
            new() { Name = "Artisan Pasta", Category = "food", Subcategory = subcategory, Price = 8.99m, Description = "Bronze-die cut pasta from Italian durum wheat.", ImageUrl = "https://picsum.photos/seed/pasta/400/400", Tags = ["pasta", "italian", "artisan"] },
            new() { Name = "Extra Virgin Olive Oil", Category = "food", Subcategory = subcategory, Price = 24.99m, Description = "Cold-pressed extra virgin olive oil from Tuscany, 750ml.", ImageUrl = "https://picsum.photos/seed/oliveoil/400/400", Tags = ["cooking", "italian", "organic", "premium"] },
            new() { Name = "Gourmet Trail Mix", Category = "food", Subcategory = subcategory, Price = 16.99m, Description = "Energy-dense mix of nuts, seeds, and dried fruits.", ImageUrl = "https://picsum.photos/seed/trailmix/400/400", Tags = ["snacks", "healthy", "energy"], IsFeatured = true },
            new() { Name = "Protein Powder", Category = "food", Subcategory = subcategory, Price = 29.99m, Description = "Whey protein isolate for post-workout recovery.", ImageUrl = "https://picsum.photos/seed/protein/400/400", Tags = ["supplements", "protein", "fitness"] },
            new() { Name = "Bone Broth", Category = "food", Subcategory = subcategory, Price = 9.99m, Description = "Slow-simmered nutrient-rich bone broth in convenient jars.", ImageUrl = "https://picsum.photos/seed/broth/400/400", Tags = ["soup", "wellness", "paleo"], IsTrending = true },
            new() { Name = "Sourdough Starter Kit", Category = "food", Subcategory = subcategory, Price = 29.99m, Description = "Complete kit with dehydrated starter, rye flour, and proofing basket.", ImageUrl = "https://picsum.photos/seed/sourdough/400/400", Tags = ["baking", "fermentation", "artisan"], IsFeatured = true },
            new() { Name = "Herbal Tea Collection", Category = "food", Subcategory = subcategory, Price = 19.99m, Description = "Assortment of caffeine-free herbal blends.", ImageUrl = "https://picsum.photos/seed/herbaltea/400/400", Tags = ["tea", "herbal", "caffeine-free"] },
            new() { Name = "Quinoa Trio Pack", Category = "food", Subcategory = subcategory, Price = 14.99m, Description = "White, red, and black quinoa in convenient packs.", ImageUrl = "https://picsum.photos/seed/quinoa/400/400", Tags = ["grains", "healthy", "gluten-free"] }
        };
    }
}
