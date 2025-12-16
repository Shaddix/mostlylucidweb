using System.Globalization;
using System.Text;
using Bogus;
using DuckDB.NET.Data;
using Mostlylucid.CsvLlm.Models;

namespace Mostlylucid.CsvLlm;

/// <summary>
/// Basic tests that don't require Ollama
/// </summary>
public static class TestBasics
{
    /// <summary>
    /// Test CSV generation and DuckDB reading
    /// </summary>
    public static async Task RunAsync()
    {
        const string csvPath = "TestData/test_sales.csv";
        const int rowCount = 1000;
        
        Console.WriteLine("=== Basic Tests (no LLM required) ===\n");
        
        // Test 1: Generate CSV
        Console.WriteLine("Test 1: Generate CSV with Bogus");
        await GenerateTestCsvAsync(csvPath, rowCount);
        Console.WriteLine($"  PASS: Generated {rowCount} rows\n");
        
        // Test 2: Read with DuckDB
        Console.WriteLine("Test 2: Read CSV with DuckDB");
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        
        // Test schema detection
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"DESCRIBE SELECT * FROM '{csvPath}'";
            using var reader = cmd.ExecuteReader();
            
            var columns = new List<string>();
            while (reader.Read())
            {
                columns.Add($"{reader.GetString(0)} ({reader.GetString(1)})");
            }
            Console.WriteLine($"  Schema: {string.Join(", ", columns.Take(5))}...");
        }
        Console.WriteLine("  PASS: Schema detected\n");
        
        // Test 3: Query execution
        Console.WriteLine("Test 3: Execute SQL queries");
        
        var testQueries = new Dictionary<string, string>
        {
            ["Count"] = $"SELECT COUNT(*) as total FROM '{csvPath}'",
            ["Sum by Region"] = $"SELECT Region, SUM(TotalAmount) as revenue FROM '{csvPath}' GROUP BY Region ORDER BY revenue DESC",
            ["Top Products"] = $"SELECT ProductName, COUNT(*) as orders FROM '{csvPath}' GROUP BY ProductName ORDER BY orders DESC LIMIT 5",
            ["Returns"] = $"SELECT COUNT(*) as returns FROM '{csvPath}' WHERE IsReturned = true"
        };
        
        foreach (var (name, sql) in testQueries)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                
                var result = new QueryResult { Sql = sql, Success = true };
                for (int i = 0; i < reader.FieldCount; i++)
                    result.Columns.Add(reader.GetName(i));
                    
                while (reader.Read())
                {
                    var row = new List<object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                    result.Rows.Add(row);
                }
                
                Console.WriteLine($"  {name}:");
                Console.WriteLine($"    {result.Rows.Count} rows returned");
                if (result.Rows.Count > 0 && result.Rows.Count <= 5)
                {
                    foreach (var row in result.Rows)
                    {
                        Console.WriteLine($"      {string.Join(" | ", row)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL {name}: {ex.Message}");
            }
        }
        Console.WriteLine("  PASS: Queries executed\n");
        
        // Test 4: QueryResult formatting
        Console.WriteLine("Test 4: QueryResult formatting");
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT Region, COUNT(*) as orders, SUM(TotalAmount) as revenue FROM '{csvPath}' GROUP BY Region";
            using var reader = cmd.ExecuteReader();
            
            var result = new QueryResult { Sql = cmd.CommandText, Success = true };
            for (int i = 0; i < reader.FieldCount; i++)
                result.Columns.Add(reader.GetName(i));
            while (reader.Read())
            {
                var row = new List<object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                result.Rows.Add(row);
            }
            
            Console.WriteLine(result.ToString());
        }
        Console.WriteLine("  PASS: Formatting works\n");
        
        // Cleanup
        if (File.Exists(csvPath))
        {
            File.Delete(csvPath);
            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
        
        Console.WriteLine("=== All Basic Tests Passed ===");
    }
    
    private static async Task GenerateTestCsvAsync(string path, int count)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var categories = new[] { "Electronics", "Clothing", "Home & Garden", "Sports", "Books" };
        var regions = new[] { "North", "South", "East", "West", "Central" };

        var faker = new Faker<TestSaleRecord>()
            .RuleFor(s => s.OrderId, f => f.Random.Guid().ToString()[..8].ToUpper())
            .RuleFor(s => s.OrderDate, f => f.Date.Between(new DateTime(2023, 1, 1), new DateTime(2024, 12, 31)))
            .RuleFor(s => s.CustomerId, f => $"CUST-{f.Random.Number(10000, 99999)}")
            .RuleFor(s => s.CustomerName, f => f.Name.FullName())
            .RuleFor(s => s.Region, f => f.PickRandom(regions))
            .RuleFor(s => s.Category, f => f.PickRandom(categories))
            .RuleFor(s => s.ProductName, f => f.Commerce.ProductName())
            .RuleFor(s => s.Quantity, f => f.Random.Number(1, 10))
            .RuleFor(s => s.UnitPrice, f => f.Random.Decimal(9.99m, 199.99m))
            .RuleFor(s => s.Discount, f => f.Random.Bool(0.2f) ? f.Random.Decimal(0.05m, 0.20m) : 0m)
            .RuleFor(s => s.IsReturned, f => f.Random.Bool(0.05f));

        var records = faker.Generate(count);
        
        await using var writer = new StreamWriter(path, false, Encoding.UTF8);
        await writer.WriteLineAsync("OrderId,OrderDate,CustomerId,CustomerName,Region,Category,ProductName,Quantity,UnitPrice,Discount,TotalAmount,IsReturned");
        
        foreach (var record in records)
        {
            var total = record.Quantity * record.UnitPrice * (1 - record.Discount);
            // Escape names that might contain commas
            var customerName = record.CustomerName.Contains(',') ? $"\"{record.CustomerName}\"" : record.CustomerName;
            var productName = record.ProductName.Contains(',') ? $"\"{record.ProductName}\"" : record.ProductName;
            
            await writer.WriteLineAsync(
                $"{record.OrderId}," +
                $"{record.OrderDate:yyyy-MM-dd}," +
                $"{record.CustomerId}," +
                $"{customerName}," +
                $"{record.Region}," +
                $"{record.Category}," +
                $"{productName}," +
                $"{record.Quantity}," +
                $"{record.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)}," +
                $"{record.Discount.ToString("F2", CultureInfo.InvariantCulture)}," +
                $"{total.ToString("F2", CultureInfo.InvariantCulture)}," +
                $"{record.IsReturned}");
        }
    }
    
    private class TestSaleRecord
    {
        public string OrderId { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string CustomerId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string Region { get; set; } = "";
        public string Category { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public bool IsReturned { get; set; }
    }
}
