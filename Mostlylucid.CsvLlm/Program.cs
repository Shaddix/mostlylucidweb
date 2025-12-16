using System.Globalization;
using System.Text;
using Bogus;
using Mostlylucid.CsvLlm;
using Mostlylucid.CsvLlm.Services;

// Configuration
const int rowCount = 100_000; // Adjust for testing (100k, 1M, etc.)
const string csvPath = "SampleData/sales.csv";
const string ollamaModel = "qwen2.5-coder:7b";

// Check command line args
var runMode = args.Length > 0 ? args[0].ToLower() : "test";

switch (runMode)
{
    case "test":
        // Run basic tests (no LLM required)
        await TestBasics.RunAsync();
        break;
        
    case "bench":
        // Benchmark queries on the large file
        TestLargeFile.Run(csvPath);
        break;
        
    case "demo":
        // Full demo with LLM (requires Ollama running)
        Console.WriteLine("=== CSV + LLM Query Demo ===\n");
        await GenerateSalesCsvAsync(csvPath, rowCount);
        await RunSimpleQueryDemo(csvPath, ollamaModel);
        await RunConversationalDemo(csvPath, ollamaModel);
        Console.WriteLine("\n=== Demo Complete ===");
        break;
        
    case "generate":
        // Just generate CSV
        await GenerateSalesCsvAsync(csvPath, rowCount);
        break;
        
    default:
        Console.WriteLine("Usage: dotnet run [test|demo|generate|bench]");
        Console.WriteLine("  test     - Run basic tests (no LLM required)");
        Console.WriteLine("  bench    - Benchmark queries on generated CSV");
        Console.WriteLine("  demo     - Full demo with LLM queries");
        Console.WriteLine("  generate - Just generate the CSV file");
        break;
}

/// <summary>
/// Generate a realistic sales dataset using Bogus
/// </summary>
static async Task GenerateSalesCsvAsync(string path, int count)
{
    Console.WriteLine($"Generating {count:N0} row sales dataset...");
    
    // Ensure directory exists
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
    {
        Directory.CreateDirectory(dir);
    }

    // Define realistic product categories and regions
    var categories = new[] { "Electronics", "Clothing", "Home & Garden", "Sports", "Books", "Toys", "Food & Beverage" };
    var regions = new[] { "North", "South", "East", "West", "Central" };
    var paymentMethods = new[] { "Credit Card", "Debit Card", "PayPal", "Bank Transfer", "Cash" };

    // Configure Bogus faker
    var faker = new Faker<SaleRecord>()
        .RuleFor(s => s.OrderId, f => f.Random.Guid().ToString()[..8].ToUpper())
        .RuleFor(s => s.OrderDate, f => f.Date.Between(new DateTime(2022, 1, 1), new DateTime(2024, 12, 31)))
        .RuleFor(s => s.CustomerId, f => $"CUST-{f.Random.Number(10000, 99999)}")
        .RuleFor(s => s.CustomerName, f => f.Name.FullName())
        .RuleFor(s => s.Email, (f, s) => f.Internet.Email(s.CustomerName))
        .RuleFor(s => s.Region, f => f.PickRandom(regions))
        .RuleFor(s => s.Category, f => f.PickRandom(categories))
        .RuleFor(s => s.ProductName, (f, s) => GenerateProductName(f, s.Category))
        .RuleFor(s => s.Quantity, f => f.Random.Number(1, 20))
        .RuleFor(s => s.UnitPrice, (f, s) => GeneratePrice(f, s.Category))
        .RuleFor(s => s.Discount, f => f.Random.Bool(0.3f) ? f.Random.Decimal(0.05m, 0.25m) : 0m)
        .RuleFor(s => s.PaymentMethod, f => f.PickRandom(paymentMethods))
        .RuleFor(s => s.IsReturned, f => f.Random.Bool(0.05f));

    // Generate and write to CSV
    var records = faker.Generate(count);
    
    await using var writer = new StreamWriter(path, false, Encoding.UTF8);
    
    // Write header
    await writer.WriteLineAsync("OrderId,OrderDate,CustomerId,CustomerName,Email,Region,Category,ProductName,Quantity,UnitPrice,Discount,TotalAmount,PaymentMethod,IsReturned");
    
    // Write rows
    foreach (var record in records)
    {
        var total = record.Quantity * record.UnitPrice * (1 - record.Discount);
        await writer.WriteLineAsync(
            $"{record.OrderId}," +
            $"{record.OrderDate:yyyy-MM-dd}," +
            $"{record.CustomerId}," +
            $"\"{record.CustomerName}\"," +
            $"{record.Email}," +
            $"{record.Region}," +
            $"{record.Category}," +
            $"\"{record.ProductName}\"," +
            $"{record.Quantity}," +
            $"{record.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)}," +
            $"{record.Discount.ToString("F2", CultureInfo.InvariantCulture)}," +
            $"{total.ToString("F2", CultureInfo.InvariantCulture)}," +
            $"{record.PaymentMethod}," +
            $"{record.IsReturned}");
    }

    var fileInfo = new FileInfo(path);
    Console.WriteLine($"Created: {path} ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)\n");
}

static string GenerateProductName(Faker f, string category)
{
    return category switch
    {
        "Electronics" => f.PickRandom(new[] { 
            "Wireless Headphones", "Smart Watch", "Bluetooth Speaker", "USB-C Hub", 
            "Mechanical Keyboard", "Gaming Mouse", "Webcam HD", "Portable Charger" }),
        "Clothing" => f.PickRandom(new[] { 
            "Cotton T-Shirt", "Denim Jeans", "Running Shoes", "Winter Jacket", 
            "Casual Shorts", "Wool Sweater", "Sports Socks", "Baseball Cap" }),
        "Home & Garden" => f.PickRandom(new[] { 
            "LED Desk Lamp", "Plant Pot Set", "Kitchen Knife Set", "Throw Pillow", 
            "Wall Clock", "Storage Basket", "Door Mat", "Picture Frame" }),
        "Sports" => f.PickRandom(new[] { 
            "Yoga Mat", "Dumbbell Set", "Tennis Racket", "Basketball", 
            "Running Belt", "Resistance Bands", "Jump Rope", "Water Bottle" }),
        "Books" => f.PickRandom(new[] { 
            "Programming Guide", "Novel Bestseller", "Cookbook", "Self-Help Book", 
            "Science Fiction", "Biography", "Children's Book", "Art Book" }),
        "Toys" => f.PickRandom(new[] { 
            "Building Blocks", "Board Game", "Remote Control Car", "Puzzle 1000pc", 
            "Action Figure", "Plush Toy", "Card Game", "Science Kit" }),
        "Food & Beverage" => f.PickRandom(new[] { 
            "Coffee Beans 1kg", "Organic Tea Set", "Protein Bars", "Gourmet Chocolate", 
            "Olive Oil Premium", "Mixed Nuts", "Energy Drinks Pack", "Specialty Sauce" }),
        _ => f.Commerce.ProductName()
    };
}

static decimal GeneratePrice(Faker f, string category)
{
    return category switch
    {
        "Electronics" => f.Random.Decimal(29.99m, 299.99m),
        "Clothing" => f.Random.Decimal(19.99m, 149.99m),
        "Home & Garden" => f.Random.Decimal(9.99m, 99.99m),
        "Sports" => f.Random.Decimal(14.99m, 199.99m),
        "Books" => f.Random.Decimal(9.99m, 49.99m),
        "Toys" => f.Random.Decimal(9.99m, 79.99m),
        "Food & Beverage" => f.Random.Decimal(4.99m, 49.99m),
        _ => f.Random.Decimal(9.99m, 99.99m)
    };
}

/// <summary>
/// Demo: Simple one-off queries
/// </summary>
static async Task RunSimpleQueryDemo(string csvPath, string model)
{
    Console.WriteLine("--- Simple Query Demo ---\n");
    
    using var service = new CsvQueryService(model, verbose: true);
    
    // Show schema first
    var schema = service.GetSchema(csvPath);
    Console.WriteLine("Schema detected:");
    Console.WriteLine(schema);
    Console.WriteLine();

    // Sample questions
    var questions = new[]
    {
        "What are the total sales by region?",
        "Which category has the highest average order value?",
        "How many orders were returned?"
    };

    foreach (var question in questions)
    {
        Console.WriteLine($"Q: {question}");
        var result = await service.QueryAsync(csvPath, question);
        Console.WriteLine(result);
        Console.WriteLine();
    }
}

/// <summary>
/// Demo: Conversational follow-up questions
/// </summary>
static async Task RunConversationalDemo(string csvPath, string model)
{
    Console.WriteLine("--- Conversational Demo ---\n");
    
    using var analyser = new ConversationalCsvAnalyser(csvPath, model, verbose: true);
    
    Console.WriteLine("Starting conversation about sales data...\n");

    // Series of related questions that build on each other
    var questions = new[]
    {
        "What's the total revenue?",
        "Break that down by category",
        "Which category has the most returns?",
        "Show the top 5 customers by total spend"
    };

    foreach (var question in questions)
    {
        Console.WriteLine($"You: {question}");
        var result = await analyser.AskAsync(question);
        Console.WriteLine($"\nResult:\n{result}\n");
        Console.WriteLine(new string('-', 60));
    }

    Console.WriteLine($"\nConversation had {analyser.History.Count} turns");
}

/// <summary>
/// Record type for Bogus to generate
/// </summary>
internal class SaleRecord
{
    public string OrderId { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Region { get; set; } = "";
    public string Category { get; set; } = "";
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public bool IsReturned { get; set; }
}
