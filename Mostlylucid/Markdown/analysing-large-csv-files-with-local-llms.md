# How to Analyse Large CSV Files with Local LLMs in C#

<!--category-- AI, LLM, Data Analysis, DuckDB, C#, Ollama -->
<datetime class="hidden">2025-12-18T10:00</datetime>

Here's the mistake everyone makes: they try to feed their CSV into an LLM. Don't. **LLMs should generate queries, not consume data.**

You've got a 500MB CSV file and want to ask "What's the average order value by region?" Tools like [Copilot in Excel](https://support.microsoft.com/en-gb/copilot-excel) can do this, but what if your data is too sensitive for cloud services? What if you need to build it yourself?

This article shows you how - locally, privately, in C#.

> **TL;DR**: Use DuckDB to query CSV files directly. Use a local LLM to generate the SQL. The LLM never sees your data - just the schema. Result: sub-100ms queries on million-row files, completely offline.

[TOC]

## The Core Insight

Using an LLM as a data store is the wrong abstraction. LLMs are fundamentally incapable of scanning millions of rows to compute an average - that's not what they're for. Even a 200K token context window fits maybe 50,000 rows. Your 500MB CSV has millions.

The correct pattern: **LLM reasons, database computes.**

```mermaid
flowchart LR
    A[User Question] --> B[LLM]
    B --> C[SQL Query]
    C --> D[DuckDB]
    D --> E[Results]
    
    style B stroke:#333,stroke-width:4px
    style D stroke:#333,stroke-width:4px
```

Notice what's happening: the LLM generates a SQL query based on your question and the schema. DuckDB executes it against the actual data. The LLM never touches your data - it only sees column names and types. This is why it's fast, private, and accurate.

## Why Not Just Load It Into Memory?

The obvious approaches all share the same fatal flaw:

**[CsvHelper](https://joshclose.github.io/CsvHelper/) / DataFrames**: Load entire file into RAM. A 500MB CSV becomes 2-4GB of objects. A 5GB file? OOM crash.

**SQLite / PostgreSQL**: Requires a slow import step (minutes for large files), upfront schema definitions, and database management overhead.

**[PandasAI](https://github.com/Sinaptik-AI/pandas-ai)**: Still loads everything into memory. Plus, executing LLM-generated arbitrary code is a security nightmare - SQL is declarative and sandboxable; Python is not.

## Why DuckDB

[DuckDB](https://duckdb.org/) is different. It queries CSV files *directly* - no import step, no loading into memory:

```csharp
using var connection = new DuckDBConnection("DataSource=:memory:");
connection.Open();
using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Region, SUM(Amount) FROM 'sales.csv' GROUP BY Region";
// Executes directly against the file - no import, no memory explosion
```

The killer feature: **it treats files as tables**. Point it at a CSV, Parquet, or JSON file and query immediately. No CREATE TABLE, no bulk insert, no waiting.

| Factor | CsvHelper | SQLite | DuckDB |
|--------|-----------|--------|--------|
| **Memory** | Loads entire file | Loads during import | Streams from disk |
| **Setup** | None | Schema + import | None |
| **500MB file** | ~2GB RAM | Minutes to import | Instant |
| **5GB file** | OOM crash | Very slow | Works fine |
| **Parquet** | No | No | Yes (10-100x faster) |

DuckDB is what data engineers use in Python for exactly this use case. The [.NET bindings](https://github.com/Giorgi/DuckDB.NET) give you full ADO.NET support - it feels like any other database, except you're querying files.

## The Stack

| Component | Why This One |
|-----------|--------------|
| [DuckDB](https://duckdb.org/) | Queries CSV directly, no import step |
| [DuckDB.NET](https://github.com/Giorgi/DuckDB.NET) | Full ADO.NET support, feels native |
| [Ollama](https://ollama.ai/) | Local inference, no API keys, no cloud |
| [Bogus](https://github.com/bchavez/Bogus) | Realistic test data at any scale |
| [`qwen2.5-coder:7b`](https://ollama.ai/library/qwen2.5-coder) | Best SQL accuracy at 7B size |

> **Note on security**: We're executing LLM-generated SQL. This is safer than arbitrary code, but still requires validation. See the [Security section](#security-considerations) for safeguards.

## Project Setup

Let's create a sample project. Install the NuGet packages:

```bash
dotnet add package DuckDB.NET.Data.Full
dotnet add package OllamaSharp
dotnet add package Bogus
```

Pull a coding-focused model that's good at SQL:

```bash
ollama pull qwen2.5-coder:7b
```

## The Architecture

Here's how the pieces fit together:

```mermaid
flowchart TB
    subgraph Input
        Q[User Question]
        CSV[CSV File]
    end
    
    subgraph Processing
        Schema[Extract Schema]
        Sample[Get Sample Rows]
        Context[Build LLM Context]
        LLM[Generate SQL]
        Validate[Validate SQL]
        Execute[Execute Query]
    end
    
    subgraph Output
        Results[Query Results]
    end
    
    CSV --> Schema
    CSV --> Sample
    Schema --> Context
    Sample --> Context
    Q --> Context
    Context --> LLM
    LLM --> Validate
    Validate -->|Error| LLM
    Validate -->|OK| Execute
    CSV --> Execute
    Execute --> Results
    
    style LLM stroke:#333,stroke-width:4px
    style Execute stroke:#333,stroke-width:4px
```

The key insight: we give the LLM **schema and sample data**, not the actual data. This keeps context small and responses fast.

**Why this matters**: The LLM generates intent (SQL). DuckDB executes it. The validation step catches syntax errors before execution. The retry loop handles the occasional mistake. This separation is what makes the system both safe and accurate.

## Step 1: Generate Test Data with Bogus

> **Already have CSV data?** Skip to [Step 2: Build the Schema Context](#step-2-build-the-schema-context).

Before we can test our LLM-powered CSV analyser, we need data to analyse. For development and testing, synthetic data beats real data:

1. **Scale testing** - Generate 100K, 1M, or 10M rows to verify performance at different sizes
2. **Privacy** - No risk of exposing real customer/business data in demos or screenshots
3. **Reproducibility** - Same seed = same data, making bugs reproducible
4. **Edge cases** - Control the distribution (e.g., force 5% returns, specific date ranges)

### What is Bogus?

[Bogus](https://github.com/bchavez/Bogus) is a .NET port of the popular faker.js library. It generates realistic-looking fake data - names, addresses, emails, dates, numbers - with proper locale support. Instead of hand-crafting test CSV files or using random garbage data, Bogus gives you data that *looks* real:

- `f.Name.FullName()` → "John Smith" (not "asdf1234")
- `f.Internet.Email()` → "john.smith@gmail.com" (properly formatted)
- `f.Date.Between(start, end)` → Realistic date distribution
- `f.Commerce.ProductName()` → "Handcrafted Granite Cheese" (fun, but recognisable)

This matters because realistic data helps you spot issues - weird formatting, unexpected aggregations, edge cases in date handling - that random strings would hide.

### Define the Data Model

```csharp
internal class SaleRecord
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
```

### Configure the Faker

Bogus uses a fluent API to define generation rules:

```csharp
var categories = new[] { "Electronics", "Clothing", "Home & Garden", "Sports", "Books" };
var regions = new[] { "North", "South", "East", "West", "Central" };

var faker = new Faker<SaleRecord>()
    .RuleFor(s => s.OrderId, f => f.Random.Guid().ToString()[..8].ToUpper())
    .RuleFor(s => s.OrderDate, f => f.Date.Between(
        new DateTime(2022, 1, 1), 
        new DateTime(2024, 12, 31)))
    .RuleFor(s => s.CustomerId, f => $"CUST-{f.Random.Number(10000, 99999)}")
    .RuleFor(s => s.CustomerName, f => f.Name.FullName())
    .RuleFor(s => s.Region, f => f.PickRandom(regions))
    .RuleFor(s => s.Category, f => f.PickRandom(categories))
    .RuleFor(s => s.ProductName, (f, s) => GenerateProductName(f, s.Category))
    .RuleFor(s => s.Quantity, f => f.Random.Number(1, 20))
    .RuleFor(s => s.UnitPrice, f => f.Random.Decimal(9.99m, 299.99m))
    .RuleFor(s => s.Discount, f => f.Random.Bool(0.3f) ? f.Random.Decimal(0.05m, 0.25m) : 0m)
    .RuleFor(s => s.IsReturned, f => f.Random.Bool(0.05f));
```

Let's break down what's happening:

- **`f` (Faker)** - The generator instance with access to all the data modules (Name, Date, Random, etc.)
- **`f.Random.Guid().ToString()[..8]`** - Generate a GUID but take only first 8 chars for a readable order ID
- **`f.Date.Between()`** - Random date within a realistic range (not year 9999)
- **`f.PickRandom(array)`** - Select randomly from predefined options (ensures valid categories)
- **`f.Random.Bool(0.3f)`** - 30% chance of true (30% of orders get a discount)
- **`(f, s)` syntax** - Access both faker AND the partially-built record. This lets `ProductName` depend on `Category`

The `(f, s)` pattern is powerful - it means "Electronics" orders get electronics product names, not random items. This coherence makes the generated data much more realistic for testing aggregations like "revenue by category".

### Generate and Write to CSV

```csharp
var records = faker.Generate(100_000); // Adjust for your testing needs

await using var writer = new StreamWriter(csvPath, false, Encoding.UTF8);
await writer.WriteLineAsync("OrderId,OrderDate,CustomerId,CustomerName,Region,Category,...");

foreach (var record in records)
{
    var total = record.Quantity * record.UnitPrice * (1 - record.Discount);
    await writer.WriteLineAsync($"{record.OrderId},{record.OrderDate:yyyy-MM-dd},...");
}
```

100K rows generates about 15MB of CSV - enough to test with, but you can easily scale to millions. The generation is fast (~2 seconds for 100K rows) because Bogus is optimised for bulk generation.

> **Tip**: Set `Randomizer.Seed = new Random(12345)` before generating to get reproducible data. Same seed = same "random" records every time, which is invaluable for debugging.

## Step 2: Build the Schema Context

Before the LLM can generate SQL, it needs to understand the data structure. We extract this from DuckDB:

### The Context Model

```csharp
public class DataContext
{
    public string CsvPath { get; set; } = "";
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<Dictionary<string, string>> SampleRows { get; set; } = new();
    public long RowCount { get; set; }
}

public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";  // VARCHAR, DOUBLE, TIMESTAMP, etc.
}
```

This captures everything the LLM needs: column names, types, and a few sample rows to understand the data format.

### Extracting the Schema

DuckDB can describe any CSV without loading it all:

```csharp
private DataContext BuildContext(DuckDBConnection connection, string csvPath)
{
    var context = new DataContext { CsvPath = csvPath };

    // Get schema - DuckDB infers types from the CSV
    using var cmd = connection.CreateCommand();
    cmd.CommandText = $"DESCRIBE SELECT * FROM '{csvPath}'";
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        context.Columns.Add(new ColumnInfo
        {
            Name = reader.GetString(0),  // Column name
            Type = reader.GetString(1)   // Inferred type
        });
    }

    return context;
}
```

The `DESCRIBE` command reads only the file header plus a few rows for type inference - it's instant even on huge files.

### Getting Sample Data

Sample rows help the LLM understand data formats (dates, IDs, etc.):

```csharp
using var cmd = connection.CreateCommand();
cmd.CommandText = $"SELECT * FROM '{csvPath}' LIMIT 3";
using var reader = cmd.ExecuteReader();

while (reader.Read())
{
    var row = new Dictionary<string, string>();
    for (int i = 0; i < reader.FieldCount; i++)
    {
        var value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
        row[reader.GetName(i)] = value;
    }
    context.SampleRows.Add(row);
}
```

Three rows is usually enough - it shows the LLM what formats to expect without wasting tokens.

## Step 3: Generate SQL with the LLM

**This is the hard part.** The prompt engineering here is non-negotiable - without strict rules, local LLMs will produce creative but broken SQL. The goal is determinism, not creativity.

### Building the Prompt

```csharp
private string BuildPrompt(DataContext context, string question, string? previousError)
{
    var sb = new StringBuilder();

    sb.AppendLine("You are a SQL expert. Generate a DuckDB SQL query to answer the user's question.");
    sb.AppendLine();
    sb.AppendLine("IMPORTANT RULES:");
    sb.AppendLine("1. The table is accessed directly from the CSV file path");
    sb.AppendLine("2. Use single quotes around the file path in FROM clause");
    sb.AppendLine("3. DuckDB syntax - use LIMIT not TOP, use || for string concat");
    sb.AppendLine("4. Return ONLY the SQL query, no explanation, no markdown");
    sb.AppendLine();
    
    sb.AppendLine($"CSV File: '{context.CsvPath}'");
    sb.AppendLine($"Row Count: {context.RowCount:N0}");
    sb.AppendLine();
    
    // Schema
    sb.AppendLine("Schema:");
    foreach (var col in context.Columns)
    {
        sb.AppendLine($"  - {col.Name}: {col.Type}");
    }
```

The rules section is crucial - it tells the LLM exactly how to format the query for DuckDB. Being explicit about syntax (LIMIT vs TOP, string concatenation) prevents common errors.

### Adding Sample Data

```csharp
    if (context.SampleRows.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine("Sample data (first 3 rows):");
        foreach (var row in context.SampleRows)
        {
            var values = row.Select(kv => $"{kv.Key}='{kv.Value}'");
            sb.AppendLine($"  {{ {string.Join(", ", values)} }}");
        }
    }
```

### Error Recovery

If a previous attempt failed, include the error:

```csharp
    if (previousError != null)
    {
        sb.AppendLine();
        sb.AppendLine("YOUR PREVIOUS QUERY HAD AN ERROR:");
        sb.AppendLine(previousError);
        sb.AppendLine("Please fix the query based on this error.");
    }

    sb.AppendLine();
    sb.AppendLine($"Question: {question}");
    sb.AppendLine();
    sb.AppendLine("SQL Query (no markdown, no explanation):");

    return sb.ToString();
}
```

This retry mechanism is important - local LLMs sometimes make syntax mistakes, and giving them the error usually fixes it on the second attempt.

### Calling the LLM

```csharp
var request = new GenerateRequest { Model = _model, Prompt = prompt };
var response = await _ollama.GenerateAsync(request).StreamToEndAsync();
var sql = CleanSqlResponse(response?.Response ?? "");
```

The `StreamToEndAsync()` waits for the complete response. For a better UX, you could stream tokens as they arrive.

### Cleaning the Response

LLMs often wrap SQL in markdown code blocks despite being told not to:

```csharp
private string CleanSqlResponse(string response)
{
    var sql = response.Trim();

    // Remove markdown code blocks if present
    if (sql.StartsWith("```"))
    {
        var lines = sql.Split('\n').ToList();
        lines.RemoveAt(0);  // Remove opening ```sql
        if (lines.Count > 0 && lines[^1].Trim().StartsWith("```"))
        {
            lines.RemoveAt(lines.Count - 1);  // Remove closing ```
        }
        sql = string.Join('\n', lines);
    }

    return sql.Trim('`', ' ', '\n', '\r');
}
```

## Step 4: Validate Before Executing

DuckDB's `EXPLAIN` lets us check SQL syntax without running the query:

```csharp
private string? ValidateSql(DuckDBConnection connection, string sql)
{
    try
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"EXPLAIN {sql}";
        cmd.ExecuteNonQuery();
        return null; // Valid
    }
    catch (Exception ex)
    {
        return ex.Message;
    }
}
```

If validation fails, we feed the error back to the LLM and retry (up to a limit).

## Step 5: Execute and Format Results

Finally, run the query and format the output:

```csharp
private QueryResult ExecuteQuery(DuckDBConnection connection, string sql)
{
    var result = new QueryResult { Sql = sql };

    try
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

        // Capture column names
        for (int i = 0; i < reader.FieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
        }

        // Capture rows
        while (reader.Read())
        {
            var row = new List<object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
            }
            result.Rows.Add(row);
        }

        result.Success = true;
    }
    catch (Exception ex)
    {
        result.Success = false;
        result.Error = ex.Message;
    }

    return result;
}
```

The `QueryResult` class (shown in full in the sample project) includes a `ToString()` method that formats results as a readable table.

## Adding Conversation Context

For interactive analysis, users often want to ask follow-up questions:

```
"What's the total revenue?"
→ "Break that down by region"
→ "Show the top 5 regions"
```

The second and third questions only make sense with context from the first.

### Tracking Conversation History

```csharp
public class ConversationTurn
{
    public string Question { get; set; } = "";
    public string Sql { get; set; } = "";
    public bool Success { get; set; }
    public int RowCount { get; set; }
    public string Summary { get; set; } = "";  // "Single value: 1234567.89"
}
```

### Including History in Prompts

```csharp
if (_history.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("CONVERSATION HISTORY (for context):");
    
    foreach (var turn in _history.TakeLast(5))  // Last 5 turns
    {
        sb.AppendLine($"Q: {turn.Question}");
        sb.AppendLine($"SQL: {turn.Sql}");
        if (turn.Success)
        {
            sb.AppendLine($"Result: {turn.Summary}");
        }
        sb.AppendLine();
    }
}
```

The history gives the LLM context to understand references like "that", "those results", or "break it down further".

## Which Model to Use?

For SQL generation, coding-focused models work best. Here are the options available via [Ollama's model library](https://ollama.ai/library):

| Model | Size | Speed | Quality | Link |
|-------|------|-------|---------|------|
| `qwen2.5-coder:7b` | 4.7GB | Fast | Excellent | [Ollama](https://ollama.ai/library/qwen2.5-coder) |
| `deepseek-coder-v2:16b` | 9GB | Medium | Best | [Ollama](https://ollama.ai/library/deepseek-coder-v2) |
| `codellama:7b` | 4GB | Fast | Good | [Ollama](https://ollama.ai/library/codellama) |
| `llama3.2:3b` | 2GB | Very Fast | Acceptable | [Ollama](https://ollama.ai/library/llama3.2) |

For most use cases, **`qwen2.5-coder:7b`** hits the sweet spot - accurate SQL, good speed, runs on modest hardware (8GB+ RAM).

## Performance

### Real-World Benchmarks

Testing on a 100K row CSV file (14MB) on a standard dev machine (Ryzen 5, NVMe SSD, 32GB RAM):

| Query Type | Time |
|------------|------|
| Simple COUNT | 65ms |
| GROUP BY with SUM | 58-68ms |
| Complex aggregation with FILTER | 63ms |
| Multi-table GROUP BY with HAVING | 71ms |

Sub-100ms for analytical queries on 100K rows - without any import step. Query complexity matters more than row count; DuckDB's columnar engine handles aggregations efficiently regardless of file size.

### Convert Large Files to Parquet

For files over 1GB, [Parquet format](https://parquet.apache.org/) is 10-100x faster:

```csharp
using var cmd = connection.CreateCommand();
cmd.CommandText = $"COPY (SELECT * FROM '{csvPath}') TO '{parquetPath}' (FORMAT PARQUET)";
cmd.ExecuteNonQuery();
```

The compressed Parquet file is also much smaller.

### Security Considerations

When executing LLM-generated SQL:

```csharp
private bool IsSafeQuery(string sql)
{
    var dangerous = new[] { "DROP", "DELETE", "TRUNCATE", "UPDATE", "INSERT", "ALTER", "CREATE" };
    var upperSql = sql.ToUpperInvariant();
    return !dangerous.Any(d => upperSql.Contains(d));
}
```

DuckDB's in-memory mode also provides natural isolation - it can't affect your production databases.

## Complete Example

Here's how it all comes together:

```csharp
// Generate test data
await GenerateSalesCsvAsync("sales.csv", 100_000);

// Simple query
using var service = new CsvQueryService("qwen2.5-coder:7b", verbose: true);
var result = await service.QueryAsync("sales.csv", "What are total sales by region?");
Console.WriteLine(result);

// Conversational analysis
using var analyser = new ConversationalCsvAnalyser("sales.csv", "qwen2.5-coder:7b");

Console.WriteLine(await analyser.AskAsync("What's the total revenue?"));
Console.WriteLine(await analyser.AskAsync("Break that down by category"));
Console.WriteLine(await analyser.AskAsync("Which category has the most returns?"));
```

## Summary

The mental model to keep: **LLMs reason; databases compute.**

Don't feed data to the LLM. Feed it schema, let it generate SQL, execute that SQL against a proper query engine. This separation is why the approach works at scale.

The implementation:

1. **DuckDB** queries CSV directly - no import, streams from disk
2. **Schema + samples** give the LLM enough context without exposing data
3. **Strict prompt rules** force deterministic SQL, not creative prose
4. **Validation via EXPLAIN** catches errors before execution
5. **Retry with error feedback** handles the occasional syntax slip

The result: sub-100ms analytical queries on million-row files, completely offline, with data that never leaves your machine.

The full sample project is available at [Mostlylucid.CsvLlm](https://github.com/scottgal/mostlylucidweb) - includes `CsvQueryService`, `ConversationalCsvAnalyser`, and Bogus-based data generation.

## Resources

### DuckDB
- [DuckDB Documentation](https://duckdb.org/docs/) - Full reference
- [DuckDB CSV Import](https://duckdb.org/docs/data/csv/overview.html) - CSV-specific features
- [DuckDB SQL Reference](https://duckdb.org/docs/sql/introduction) - SQL syntax differences from other databases
- [DuckDB.NET GitHub](https://github.com/Giorgi/DuckDB.NET) - C# bindings
- [DuckDB.NET NuGet](https://www.nuget.org/packages/DuckDB.NET.Data.Full) - Full package with native binaries

### Ollama & LLMs
- [Ollama](https://ollama.ai/) - Local LLM runtime
- [Ollama Model Library](https://ollama.ai/library) - Available models
- [OllamaSharp](https://github.com/awaescher/OllamaSharp) - C# client library
- [OllamaSharp NuGet](https://www.nuget.org/packages/OllamaSharp/)

### Test Data Generation
- [Bogus GitHub](https://github.com/bchavez/Bogus) - Fake data generator
- [Bogus API Reference](https://github.com/bchavez/Bogus#bogus-api-support) - Available data types

### Alternatives Mentioned
- [CsvHelper](https://joshclose.github.io/CsvHelper/) - CSV parsing library
- [Microsoft.Data.Analysis](https://www.nuget.org/packages/Microsoft.Data.Analysis) - DataFrame for .NET
- [PandasAI](https://github.com/Sinaptik-AI/pandas-ai) - Python LLM + pandas integration
