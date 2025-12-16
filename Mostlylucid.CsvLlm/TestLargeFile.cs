using System.Diagnostics;
using DuckDB.NET.Data;

namespace Mostlylucid.CsvLlm;

/// <summary>
/// Test queries on the larger generated file
/// </summary>
public static class TestLargeFile
{
    public static void Run(string csvPath = "SampleData/sales.csv")
    {
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"File not found: {csvPath}");
            Console.WriteLine("Run 'dotnet run generate' first");
            return;
        }

        var fileInfo = new FileInfo(csvPath);
        Console.WriteLine($"=== Testing Large File: {csvPath} ({fileInfo.Length / 1024.0 / 1024.0:F2} MB) ===\n");

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        // Test various queries and time them
        var queries = new (string Name, string Sql)[]
        {
            ("Row count", $"SELECT COUNT(*) FROM '{csvPath}'"),
            ("Sum by Region", $"SELECT Region, SUM(TotalAmount) as revenue FROM '{csvPath}' GROUP BY Region ORDER BY revenue DESC"),
            ("Sum by Category", $"SELECT Category, SUM(TotalAmount) as revenue, COUNT(*) as orders FROM '{csvPath}' GROUP BY Category ORDER BY revenue DESC"),
            ("Monthly trend", $"SELECT DATE_TRUNC('month', OrderDate) as month, SUM(TotalAmount) as revenue FROM '{csvPath}' GROUP BY month ORDER BY month"),
            ("Top 10 customers", $"SELECT CustomerName, SUM(TotalAmount) as total_spend FROM '{csvPath}' GROUP BY CustomerName ORDER BY total_spend DESC LIMIT 10"),
            ("Return rate by category", $"SELECT Category, COUNT(*) FILTER (WHERE IsReturned = true) * 100.0 / COUNT(*) as return_rate FROM '{csvPath}' GROUP BY Category"),
            ("Complex: High-value non-returned", $@"
                SELECT Region, Category, AVG(TotalAmount) as avg_order 
                FROM '{csvPath}' 
                WHERE TotalAmount > 500 AND IsReturned = false 
                GROUP BY Region, Category 
                HAVING COUNT(*) > 10
                ORDER BY avg_order DESC 
                LIMIT 10"),
        };

        foreach (var (name, sql) in queries)
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                using var reader = cmd.ExecuteReader();
                
                var rowCount = 0;
                var firstRow = new List<string>();
                
                while (reader.Read())
                {
                    if (rowCount == 0)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
                            if (val.Length > 30) val = val[..27] + "...";
                            firstRow.Add(val);
                        }
                    }
                    rowCount++;
                }
                
                sw.Stop();
                Console.WriteLine($"{name}: {rowCount} rows in {sw.ElapsedMilliseconds}ms");
                if (firstRow.Count > 0)
                {
                    Console.WriteLine($"  First row: {string.Join(" | ", firstRow)}");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"{name}: ERROR in {sw.ElapsedMilliseconds}ms - {ex.Message}\n");
            }
        }

        Console.WriteLine("=== Large File Tests Complete ===");
    }
}
