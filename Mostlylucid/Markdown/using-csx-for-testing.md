# Using CSX Scripts for Quick C# Testing

Need to test a snippet of C# code without spinning up a full project? C# Script files (`.csx`) let you write and run C# code like a scripting language. No `Program.cs`, no `.csproj`, no build step - just write and run. Perfect for testing APIs, validating logic, or prototyping before committing to a full implementation.

Befor I roll out the full semanti c search functionality I thought I'd share how I use `.csx` files for ad hoc testing in this and other projects.

<datetime class="hidden">2025-11-26T20:00</datetime>
<!--category-- C#, Testing, Scripting, dotnet-script -->

[TOC]

## CSX vs .NET 10 File-Based Apps

Before diving in, let's address the elephant in the room: .NET 10 now has native "file-based apps" that let you run `.cs` files directly with `dotnet run app.cs`. How does this compare to CSX?

### .NET 10 File-Based Apps (Now Available!)

With .NET 10, you can run single-file C# directly:

```bash
# .NET 10 - available now!
dotnet run app.cs
```

**Features:**
- Native to the SDK - no extra tools needed
- Uses familiar `.cs` extension
- NuGet references via `#:package` directive
- Full debugger support from day one
- Same compiler as regular projects

```csharp
// app.cs - .NET 10 style
#:package Newtonsoft.Json@13.0.3

using Newtonsoft.Json;

var obj = new { Name = "Test", Value = 42 };
Console.WriteLine(JsonConvert.SerializeObject(obj));
```

### CSX Scripts (Available Now)

CSX via `dotnet-script` has been around since 2017:

```bash
# Available today
dotnet script app.csx
```

**Features:**
- Works with .NET 6, 7, 8, 9 , 10
- Rich ecosystem and tooling
- REPL mode for interactive exploration
- Proven and battle-tested

```csharp
// app.csx - CSX style
#r "nuget: Newtonsoft.Json, 13.0.3"

using Newtonsoft.Json;

var obj = new { Name = "Test", Value = 42 };
Console.WriteLine(JsonConvert.SerializeObject(obj));
```

### Which Should You Use?

| Feature | CSX (dotnet-script) | .NET 10 File Apps |
|---------|---------------------|-------------------|
| **Availability** | .NET 6+ | .NET 10  |
| **Installation** | `dotnet tool install -g dotnet-script` | Built into SDK |
| **File Extension** | `.csx` | `.cs` |
| **NuGet Syntax** | `#r "nuget: Pkg, Ver"` | `#:package Pkg@Ver` |
| **REPL Mode** | Yes | Not yet |
| **IDE Support** | Good (VS Code, Rider) | Improving |
| **Debugging** | Yes | Yes (native) |

**My recommendation:**
- **Try .NET 10 file apps first** - native support means less tooling overhead
- **Fall back to CSX** if you need REPL mode or are on an older .NET version
- The concepts are nearly identical - knowledge transfers easily between both

The rest of this article covers CSX which still works great and has some features (like REPL) that .NET 10 file apps don't have yet.

## What is CSX?

CSX (C# Script) files are C# code files that can be executed directly without compilation into a project. Think of it as "Python-style" C# - you write code, you run it, you see results.

```csharp
// hello.csx
Console.WriteLine("Hello from C# Script!");
```

Run it:
```bash
dotnet script hello.csx
```

That's it. No `Main()` method, no namespace, no class wrapper required.

## Installing dotnet-script

The most popular way to run CSX files is via [dotnet-script](https://github.com/dotnet-script/dotnet-script):

```bash
dotnet tool install -g dotnet-script
```

Verify installation:
```bash
dotnet script --version
```

## Where CSX Fits in the Testing Cycle

Before diving into the "how", let's understand the "when". CSX scripts occupy a unique spot in the testing pyramid:

```
                    ┌─────────────────┐
                    │   E2E Tests     │  ← Full system, slow, expensive
                    │   (Playwright)  │
                   ─┼─────────────────┼─
                   │ Integration Tests │  ← Multiple components, database
                   │    (xUnit + DB)   │
                  ─┼───────────────────┼─
                 │   CSX Scripts        │  ← Quick validation, exploration
                 │   (Ad-hoc testing)   │     ★ YOU ARE HERE ★
                ─┼─────────────────────┼─
               │      Unit Tests         │  ← Single class, mocked deps
               │   (xUnit, NUnit, etc)   │
              ─┴─────────────────────────┴─
```

### CSX Scripts as "Mini Integration Tests"

CSX scripts aren't a replacement for formal tests - they're a **complement**. Think of them as:

- **Pre-implementation spikes** - Verify an API works before building a service around it
- **Debugging aids** - Isolate and reproduce issues without rebuilding your entire app
- **Exploratory tests** - Understand how a library behaves before writing unit tests
- **Smoke tests** - Quick sanity checks against real services (database, APIs, queues)

### The Development Workflow

Here's how CSX fits into a typical feature development cycle:

```
1. EXPLORE (CSX Script)
   └─→ "Does this API even work? What's the response format?"
   └─→ Write a quick script to call the API and see the output

2. PROTOTYPE (CSX Script)
   └─→ "How should I structure this service?"
   └─→ Test different approaches without project scaffolding

3. IMPLEMENT (Production Code)
   └─→ Build the actual service with proper error handling, DI, etc.
   └─→ You already know the API works from step 1!

4. TEST (xUnit/NUnit)
   └─→ Write formal unit tests with mocks
   └─→ Write integration tests against test database

5. DEBUG (CSX Script)
   └─→ Production issue? Write a script to reproduce it
   └─→ Faster than adding logging, rebuilding, deploying
```

### Real Example: Building the Umami Integration

When I built the Umami analytics integration for this blog, my workflow was:

1. **CSX: Test the raw API** - Does authentication work? What do responses look like?
2. **CSX: Test timestamp conversion** - Found a bug here before writing any production code!
3. **Implement: Build UmamiClient** - With confidence because I'd already validated the API
4. **xUnit: Write unit tests** - Mock HttpClient, test serialization logic
5. **CSX: Debug production issue** - Metrics returning empty? Script to isolate the problem

The CSX scripts didn't replace my unit tests - they **prevented me from writing code that wouldn't work** and helped me **debug issues faster** when they occurred.

## Why Use CSX for Testing?

### 1. Zero Ceremony

Traditional approach to test an API call:
1. Create new console project
2. Add NuGet packages
3. Write `Program.cs`
4. Build
5. Run
6. Delete project when done

CSX approach:
1. Write script
2. Run script

### 2. Inline NuGet References

Need a package? Reference it directly in your script:

```csharp
#r "nuget: Newtonsoft.Json, 13.0.3"
#r "nuget: RestSharp, 110.2.0"

using Newtonsoft.Json;
using RestSharp;

var client = new RestClient("https://api.github.com");
var request = new RestRequest("users/scottgal", Method.Get);
request.AddHeader("User-Agent", "CSX-Test");

var response = await client.ExecuteAsync(request);
Console.WriteLine(JsonConvert.SerializeObject(
    JsonConvert.DeserializeObject(response.Content),
    Formatting.Indented));
```

First run downloads packages. Subsequent runs use cache.

### 3. Reference Local DLLs

Testing your own library? Reference it directly:

```csharp
#r "bin/Debug/net9.0/MyLibrary.dll"

using MyLibrary;

var result = MyClass.DoSomething();
Console.WriteLine(result);
```

### 4. Reference Other Scripts

Split complex scripts into reusable parts:

```csharp
#load "helpers.csx"
#load "config.csx"

// Use functions/classes from loaded scripts
var config = LoadConfig();
var result = ProcessData(config);
```

## Real Examples from This Project

These aren't contrived examples - they're actual scripts I use to debug and test this blog's codebase. Each one solved a real problem I encountered during development.

### Testing API Timestamps

**The Problem:** My Umami analytics integration was returning empty data. After hours of debugging, I suspected the timestamp conversion was wrong - the Umami API expects Unix timestamps in milliseconds, but I wasn't sure if my .NET code was producing the correct format.

**Why CSX?** I could have added logging to the production code, rebuilt, deployed, and checked logs. Or I could write a quick script to verify my hypothesis in 30 seconds.

```csharp
#!/usr/bin/env dotnet-script

// This script helped debug an issue where the Umami API was returning empty data.
// The API expects Unix timestamps in milliseconds, and I suspected my conversion was wrong.

// Start with known values we can verify
var now = DateTime.UtcNow;
var yesterday = now.AddHours(-24);

// The "O" format specifier gives us ISO 8601 format - precise and unambiguous
// Example output: "2025-11-24T10:30:45.1234567Z"
Console.WriteLine($"Now: {now:O}");
Console.WriteLine($"Yesterday: {yesterday:O}");

// The Umami API expects Unix timestamps in MILLISECONDS (not seconds!)
// DateTimeOffset is the safest way to convert - it handles time zones correctly.
// Always use ToUniversalTime() first to ensure we're working with UTC.
var nowOffset = new DateTimeOffset(now.ToUniversalTime());
var yesterdayOffset = new DateTimeOffset(yesterday.ToUniversalTime());

// ToUnixTimeMilliseconds() returns milliseconds since 1970-01-01 00:00:00 UTC
var nowMs = nowOffset.ToUnixTimeMilliseconds();
var yesterdayMs = yesterdayOffset.ToUnixTimeMilliseconds();

Console.WriteLine($"\nNow in milliseconds: {nowMs}");
Console.WriteLine($"Yesterday in milliseconds: {yesterdayMs}");

// IMPORTANT: Verify the conversion is reversible!
// This catches off-by-one errors and timezone issues
var nowConverted = DateTimeOffset.FromUnixTimeMilliseconds(nowMs);
var yesterdayConverted = DateTimeOffset.FromUnixTimeMilliseconds(yesterdayMs);

Console.WriteLine($"\nConverted back (should match above):");
Console.WriteLine($"Now: {nowConverted:O}");
Console.WriteLine($"Yesterday: {yesterdayConverted:O}");

// THE ACTUAL BUG: I found this timestamp in my application logs
// Let's see what date it actually represents...
var suspiciousTimestamp = 1763440087664L;
var suspiciousDate = DateTimeOffset.FromUnixTimeMilliseconds(suspiciousTimestamp);
Console.WriteLine($"\nSuspicious timestamp {suspiciousTimestamp} = {suspiciousDate:O}");

// Output showed this timestamp was in the year 2025... but it should have been in 2024!
// Tracing back, I found I was using DateTime.Now instead of DateTime.UtcNow,
// causing the local timezone offset to be applied incorrectly.
```

**The Outcome:** This script proved the timestamp was 1 year in the future. I traced the bug back to using `DateTime.Now` instead of `DateTime.UtcNow` in the production code. Fixed in 5 minutes instead of potentially 5 hours of debugging.

### Testing Query String Generation

**The Problem:** I needed to verify that ASP.NET's `QueryHelpers` class generates query strings in the exact format the Umami API expects. Does it URL-encode special characters? What order are the parameters in?

**Why CSX?** Reading documentation is one thing, but seeing the actual output tells you exactly what your code will produce.

```csharp
#!/usr/bin/env dotnet-script

// Pull in ASP.NET's WebUtilities package - this is the same package
// that ASP.NET Core uses internally for query string manipulation
#r "nuget: Microsoft.AspNetCore.WebUtilities, 9.0.0"

using Microsoft.AspNetCore.WebUtilities;

// These are the exact parameters I need to send to the Umami metrics API
// Using a Dictionary makes it easy to see all parameters at once
var queryParams = new Dictionary<string, string>
{
    {"startAt", "1730000000000"},   // Unix timestamp in milliseconds
    {"endAt", "1730086400000"},     // 24 hours later
    {"type", "url"},                // Type of metric to fetch
    {"unit", "day"},                // Aggregation unit
    {"limit", "500"}                // Maximum results to return
};

// QueryHelpers.AddQueryString builds a properly formatted query string
// First parameter: base URL (empty string = just the query string portion)
// Second parameter: dictionary of key-value pairs
var queryString = QueryHelpers.AddQueryString(string.Empty, queryParams);

Console.WriteLine($"Generated query string:");
Console.WriteLine(queryString);
// Output: ?startAt=1730000000000&endAt=1730086400000&type=url&unit=day&limit=500

// Now let's verify we can parse it back - this catches encoding issues
// that might not be obvious in the generated string
Console.WriteLine($"\nParsed back (verifying round-trip):");
var parsed = QueryHelpers.ParseQuery(queryString);
foreach (var kvp in parsed)
{
    // Note: parsed values are StringValues, not string
    // StringValues can hold multiple values for the same key (e.g., ?tag=a&tag=b)
    Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
}

// What I learned: QueryHelpers properly handles URL encoding for special characters
// This became important when I later added search terms with spaces and unicode
```

### Testing Raw HTTP API Calls

**The Problem:** Before building a full service class with dependency injection, error handling, retry logic, and unit tests, I wanted to verify the API actually works and understand its response format.

**Why CSX?** It's faster to write 50 lines of exploratory code than to build proper service infrastructure. If the API doesn't work how I expect, I've wasted 5 minutes instead of 5 hours.

```csharp
#!/usr/bin/env dotnet-script

// System.Net.Http.Json provides extension methods like PostAsJsonAsync and GetFromJsonAsync
// This is the same package ASP.NET Core uses internally
#r "nuget: System.Net.Http.Json, 9.0.0"

using System.Net.Http.Json;
using System.Text.Json;

// Configuration - in a real app these would come from appsettings.json
var websiteId = "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee";
var umamiPath = "https://umami.mostlylucid.net";
var username = "admin";

// SECURITY: Never hardcode passwords! Use environment variables instead.
// Set before running: $env:UMAMI_PASSWORD = "your-password" (PowerShell)
//               or:   export UMAMI_PASSWORD="your-password" (bash)
var password = Environment.GetEnvironmentVariable("UMAMI_PASSWORD") ?? "";

if (string.IsNullOrEmpty(password))
{
    // Provide helpful instructions when the password is missing
    Console.WriteLine("ERROR: Set UMAMI_PASSWORD environment variable");
    Console.WriteLine("  PowerShell: $env:UMAMI_PASSWORD = 'your-password'");
    Console.WriteLine("  Bash:       export UMAMI_PASSWORD='your-password'");
    return;  // In CSX, 'return' at top level exits the script
}

// Create a single HttpClient instance - never create multiple instances in a loop!
// BaseAddress means all subsequent requests can use relative URLs
var httpClient = new HttpClient { BaseAddress = new Uri(umamiPath) };

// === STEP 1: Authenticate ===
// PostAsJsonAsync automatically serializes our anonymous object to JSON
// and sets the Content-Type header to application/json
Console.WriteLine("Step 1: Logging in...");
var loginPayload = new { username, password };
var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", loginPayload);

// Always check for errors before trying to read the response body
if (!loginResponse.IsSuccessStatusCode)
{
    Console.WriteLine($"Login failed: {loginResponse.StatusCode}");
    var error = await loginResponse.Content.ReadAsStringAsync();
    Console.WriteLine($"Error body: {error}");
    return;
}

Console.WriteLine("Login successful!");

// === STEP 2: Extract JWT Token ===
// Use JsonDocument for one-off JSON parsing without creating dedicated DTOs
// This is perfect for exploratory testing when we don't know the exact schema
var loginContent = await loginResponse.Content.ReadAsStringAsync();
var loginJson = JsonDocument.Parse(loginContent);
var token = loginJson.RootElement.GetProperty("token").GetString();

// Add the JWT token to all future requests via the Authorization header
httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

// === STEP 3: Build the API Request ===
// Always use UTC for API calls to avoid timezone confusion
var now = DateTime.UtcNow;
var yesterday = now.AddHours(-24);
var nowMs = ((DateTimeOffset)now).ToUnixTimeMilliseconds();
var yesterdayMs = ((DateTimeOffset)yesterday).ToUnixTimeMilliseconds();

var testUrl = $"/api/websites/{websiteId}/metrics?startAt={yesterdayMs}&endAt={nowMs}&type=url&unit=day&limit=10";

Console.WriteLine($"\nStep 2: Testing metrics endpoint...");
Console.WriteLine($"URL: {testUrl}");

// === STEP 4: Make the Request ===
var response = await httpClient.GetAsync(testUrl);
Console.WriteLine($"Status: {response.StatusCode}");

// Pretty-print the JSON response so we can understand the structure
var responseBody = await response.Content.ReadAsStringAsync();
try
{
    var formatted = JsonSerializer.Serialize(
        JsonSerializer.Deserialize<JsonElement>(responseBody),
        new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine($"Response:\n{formatted}");
}
catch
{
    // If it's not valid JSON, just print raw
    Console.WriteLine($"Response (raw):\n{responseBody}");
}

// What I learned from this script:
// 1. The API returns an array of objects with 'x' (url) and 'y' (count) properties
// 2. Empty results return [] not null
// 3. The JWT token expires after 24 hours
```

### Testing with Dependency Injection

**The Problem:** I've published a NuGet package (Umami.Net) and want to test it exactly as a consumer would use it - with proper dependency injection setup, not by instantiating classes directly.

**Why CSX?** Creating a test console project, adding my NuGet reference, writing all the DI boilerplate - that's 15+ minutes of ceremony. With CSX, I can verify the consumer experience in under 2 minutes.

```csharp
#!/usr/bin/env dotnet-script

// Reference my published NuGet package - this tests the ACTUAL PUBLISHED VERSION,
// not my local source code. This is crucial for verifying releases work correctly!
#r "nuget: Umami.Net, 0.1.0"

// Standard Microsoft DI packages - the same ones ASP.NET Core uses
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"

using Umami.Net;
using Umami.Net.UmamiData;
using Umami.Net.UmamiData.Models.RequestObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Configuration
var websiteId = "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee";
var umamiPath = "https://umami.mostlylucid.net";
var password = Environment.GetEnvironmentVariable("UMAMI_PASSWORD") ?? "";

if (string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Set UMAMI_PASSWORD environment variable");
    return;
}

// === BUILD THE DI CONTAINER ===
// This mimics exactly what happens in a real ASP.NET Core app's Program.cs

var services = new ServiceCollection();

// Add logging so we can see what the library is doing internally
// Debug level will show HTTP requests, retries, token refreshes, etc.
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);  // Show everything
});

// This is my library's extension method - this is the public API that users call
// I want to verify this works correctly without any hidden dependencies
services.AddUmamiData(umamiPath, websiteId);

// Build the container and resolve our service
var serviceProvider = services.BuildServiceProvider();
var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();

Console.WriteLine("=== Testing Umami.Net Package via DI ===\n");

// === TEST THE LOGIN FLOW ===
Console.WriteLine("Testing login...");
var loginSuccess = await umamiDataService.LoginAsync("admin", password);
if (!loginSuccess)
{
    Console.WriteLine("ERROR: Login failed - check credentials");
    return;
}
Console.WriteLine("Login successful!\n");

// === TEST THE METRICS API ===
Console.WriteLine("Testing metrics API...");
var metricsResult = await umamiDataService.GetMetrics(new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddHours(-24),
    EndAtDate = DateTime.UtcNow,
    Type = MetricType.url,   // Get URL metrics (most visited pages)
    Unit = Unit.day,
    Limit = 10
});

// Display results
Console.WriteLine($"API returned status: {metricsResult?.Status}");
if (metricsResult?.Data?.Length > 0)
{
    Console.WriteLine($"\nTop {Math.Min(5, metricsResult.Data.Length)} URLs in the last 24 hours:");
    foreach (var metric in metricsResult.Data.Take(5))
    {
        // metric.x = the URL path, metric.y = the view count
        Console.WriteLine($"  {metric.y,5} views - {metric.x}");
    }
}
else
{
    Console.WriteLine("No data returned - check date range or website ID");
}

// What I verified with this script:
// 1. The NuGet package installs correctly
// 2. The DI registration extension method works
// 3. The service can be resolved from the container
// 4. Login and API calls work as expected
```

### Testing Qdrant Vector Database

**The Problem:** I'm integrating a Qdrant vector database for semantic search. Before writing the production service, I need to understand how the gRPC client works, what the API looks like, and verify my local Qdrant instance is running correctly.

**Why CSX?** Vector databases are new territory for many developers. CSX lets me experiment interactively, trying different operations and seeing immediate results before committing to an architecture.

```csharp
#!/usr/bin/env dotnet-script

// Qdrant.Client is the official .NET client for the Qdrant vector database
#r "nuget: Qdrant.Client, 1.12.0"

using Qdrant.Client;
using Qdrant.Client.Grpc;

// === CRITICAL: Windows gRPC HTTP/2 Fix ===
// By default, .NET on Windows doesn't allow unencrypted HTTP/2 connections (used by gRPC)
// Without this line, you'll get cryptic "Protocol error" exceptions
// This must be called BEFORE creating the QdrantClient!
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Connect to Qdrant running locally
// Note: Port 6334 is gRPC (faster), port 6333 is REST API
// The .NET client uses gRPC for better performance
var client = new QdrantClient("localhost", 6334);

Console.WriteLine("=== Qdrant Vector Database Testing ===\n");

// === STEP 1: List Existing Collections ===
// A "collection" in Qdrant is like a table - it holds vectors with the same dimensionality
Console.WriteLine("Step 1: Checking existing collections...");
var collections = await client.ListCollectionsAsync();

if (!collections.Any())
{
    Console.WriteLine("No collections found. This is a fresh Qdrant instance.\n");
}
else
{
    foreach (var collection in collections)
    {
        var info = await client.GetCollectionInfoAsync(collection);
        Console.WriteLine($"  Collection: {collection}");
        Console.WriteLine($"    Points (vectors): {info.PointsCount}");
        Console.WriteLine($"    Status: {info.Status}");
    }
    Console.WriteLine();
}

// === STEP 2: Create a Test Collection ===
// Vector databases store "points" - each point has a vector and optional metadata (payload)
var testCollection = "csx_demo";

Console.WriteLine($"Step 2: Creating test collection '{testCollection}'...");
try
{
    await client.CreateCollectionAsync(
        collectionName: testCollection,
        vectorsConfig: new VectorParams
        {
            // Vector size MUST match your embedding model!
            // all-MiniLM-L6-v2 produces 384-dimensional vectors
            // text-embedding-ada-002 produces 1536-dimensional vectors
            Size = 384,

            // Cosine similarity is standard for text embeddings
            // Alternatives: Distance.Dot (dot product), Distance.Euclid (euclidean)
            Distance = Distance.Cosine
        });
    Console.WriteLine("Collection created successfully!\n");
}
catch (Exception ex) when (ex.Message.Contains("already exists"))
{
    Console.WriteLine("Collection already exists, continuing...\n");
}

// === STEP 3: Insert Test Data ===
// In production, vectors come from an embedding model (BERT, OpenAI, etc.)
// For testing, we'll use random vectors
Console.WriteLine("Step 3: Inserting test point...");

var testVector = Enumerable.Range(0, 384)
    .Select(_ => (float)Random.Shared.NextDouble())
    .ToArray();

// Payload = metadata attached to the vector
// This is what you filter on and return in search results
var payload = new Dictionary<string, Value>
{
    ["title"] = "Understanding Vector Databases",
    ["slug"] = "understanding-vector-databases",
    ["language"] = "en",
    ["created"] = DateTime.UtcNow.ToString("O")
};

await client.UpsertAsync(
    collectionName: testCollection,
    points: new[]
    {
        new PointStruct
        {
            Id = Guid.NewGuid(),  // Unique identifier for this point
            Vectors = testVector,
            Payload = { payload }
        }
    });
Console.WriteLine("Point inserted!\n");

// === STEP 4: Search for Similar Vectors ===
// In production, you'd embed a search query and find similar documents
Console.WriteLine("Step 4: Searching for similar vectors...");

var searchVector = Enumerable.Range(0, 384)
    .Select(_ => (float)Random.Shared.NextDouble())
    .ToArray();

var results = await client.SearchAsync(
    collectionName: testCollection,
    vector: searchVector,
    limit: 5,
    scoreThreshold: 0.0f  // Return all results (random vectors won't have high similarity)
);

Console.WriteLine($"Found {results.Count} results:");
foreach (var result in results)
{
    // Score: 0 to 1 for cosine similarity (higher = more similar)
    Console.WriteLine($"  Score: {result.Score:F4}");
    Console.WriteLine($"    Title: {result.Payload["title"].StringValue}");
    Console.WriteLine($"    Slug: {result.Payload["slug"].StringValue}");
}

// === STEP 5: Clean Up ===
Console.WriteLine($"\nStep 5: Deleting test collection...");
await client.DeleteCollectionAsync(testCollection);
Console.WriteLine("Done! Test collection cleaned up.");

// What I learned from this script:
// 1. The gRPC client is fast but needs the HTTP/2 switch on Windows
// 2. Collection creation requires specifying vector dimensions upfront
// 3. Payloads can be arbitrary key-value pairs
// 4. Search returns results sorted by similarity score
```

## More Practical Examples

### Testing an HTTP Endpoint

```csharp
#r "nuget: System.Net.Http.Json, 9.0.0"

using System.Net.Http.Json;

var http = new HttpClient();
http.DefaultRequestHeaders.Add("User-Agent", "CSX-Test");

// Test a GET endpoint
var response = await http.GetFromJsonAsync<JsonElement>(
    "https://api.github.com/repos/dotnet/runtime");

Console.WriteLine($"Stars: {response.GetProperty("stargazers_count")}");
Console.WriteLine($"Forks: {response.GetProperty("forks_count")}");
```

### Testing JSON Serialization

```csharp
#r "nuget: System.Text.Json, 8.0.0"

using System.Text.Json;
using System.Text.Json.Serialization;

public record Person(
    string Name,
    int Age,
    [property: JsonPropertyName("email_address")] string Email);

var person = new Person("Scott", 50, "scott@example.com");

var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var json = JsonSerializer.Serialize(person, options);
Console.WriteLine(json);

// Deserialize back
var parsed = JsonSerializer.Deserialize<Person>(json, options);
Console.WriteLine($"Parsed: {parsed}");
```

### Testing Database Queries

```csharp
#r "nuget: Npgsql, 8.0.0"
#r "nuget: Dapper, 2.1.24"

using Npgsql;
using Dapper;

var connectionString = "Host=localhost;Database=test;Username=postgres;Password=secret";

await using var conn = new NpgsqlConnection(connectionString);

// Quick query test
var results = await conn.QueryAsync<dynamic>(
    "SELECT * FROM users WHERE created_at > @date",
    new { date = DateTime.UtcNow.AddDays(-7) });

foreach (var row in results)
{
    Console.WriteLine($"{row.id}: {row.name}");
}
```

### Testing Regex Patterns

```csharp
using System.Text.RegularExpressions;

var patterns = new[]
{
    @"^\d{4}-\d{2}-\d{2}$",           // Date
    @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$", // Email
    @"^https?://[\w\-]+(\.[\w\-]+)+", // URL
};

var testCases = new[]
{
    "2025-11-24",
    "test@example.com",
    "https://mostlylucid.net",
    "not-a-date",
    "invalid-email",
};

foreach (var test in testCases)
{
    Console.WriteLine($"\n{test}:");
    foreach (var pattern in patterns)
    {
        var match = Regex.IsMatch(test, pattern);
        if (match) Console.WriteLine($"  ✓ Matches: {pattern}");
    }
}
```

### Testing LINQ Queries

```csharp
var data = new[]
{
    new { Name = "Alice", Age = 30, Department = "Engineering" },
    new { Name = "Bob", Age = 25, Department = "Marketing" },
    new { Name = "Charlie", Age = 35, Department = "Engineering" },
    new { Name = "Diana", Age = 28, Department = "Engineering" },
};

// Test complex LINQ query
var result = data
    .Where(x => x.Department == "Engineering")
    .GroupBy(x => x.Age >= 30)
    .Select(g => new
    {
        Senior = g.Key,
        Count = g.Count(),
        Names = string.Join(", ", g.Select(x => x.Name))
    });

foreach (var group in result)
{
    Console.WriteLine($"Senior: {group.Senior}, Count: {group.Count}, Names: {group.Names}");
}
```

### Testing Qdrant Vector Search

```csharp
#r "nuget: Qdrant.Client, 1.12.0"

using Qdrant.Client;
using Qdrant.Client.Grpc;

var client = new QdrantClient("localhost", 6334);

// Test collection exists
var collections = await client.ListCollectionsAsync();
Console.WriteLine("Collections:");
foreach (var collection in collections)
{
    Console.WriteLine($"  - {collection}");
}

// Test a search (assuming you have embeddings)
var testVector = Enumerable.Range(0, 384).Select(_ => (float)Random.Shared.NextDouble()).ToArray();

try
{
    var results = await client.SearchAsync(
        collectionName: "blog_posts",
        vector: testVector,
        limit: 5);

    foreach (var result in results)
    {
        Console.WriteLine($"Score: {result.Score}, Id: {result.Id}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Search failed: {ex.Message}");
}
```

## IDE Support

### Visual Studio Code

Install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension. You get:
- Syntax highlighting
- IntelliSense
- Run/Debug via CodeLens

Create `.vscode/launch.json`:
```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Run CSX",
            "type": "coreclr",
            "request": "launch",
            "program": "dotnet",
            "args": ["script", "${file}"],
            "cwd": "${workspaceFolder}"
        }
    ]
}
```

### JetBrains Rider

Rider has built-in CSX support. Right-click any `.csx` file and select "Run".

## Tips and Tricks

### Use the Shebang

Add a shebang to make scripts directly executable on Linux/Mac:

```csharp
#!/usr/bin/env dotnet-script

Console.WriteLine("Runs directly with ./script.csx");
```

### Arguments with Defaults

Access command-line arguments via the global `Args` variable:

```csharp
// run: dotnet script test.csx -- arg1 arg2 "arg with spaces"
Console.WriteLine($"Arguments: {Args.Count}");
foreach (var (arg, index) in Args.Select((a, i) => (a, i)))
{
    Console.WriteLine($"  [{index}]: {arg}");
}

// Common pattern: use args with defaults
var environment = Args.ElementAtOrDefault(0) ?? "development";
var verbose = Args.Contains("--verbose");

Console.WriteLine($"Environment: {environment}, Verbose: {verbose}");
```

### Environment Variables for Secrets

Never hardcode secrets - use environment variables:

```csharp
var apiKey = Environment.GetEnvironmentVariable("API_KEY");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("ERROR: API_KEY not set");
    Console.Error.WriteLine("Run: $env:API_KEY='your-key' (PowerShell)");
    Console.Error.WriteLine(" or: export API_KEY='your-key' (bash)");
    Environment.Exit(1);
}

// Safely log partial key for debugging
Console.WriteLine($"Using API key: {apiKey[..4]}...{apiKey[^4..]}");
```

### Interactive Mode (REPL)

Start an interactive session for exploration:

```bash
dotnet script
```

You get a C# REPL:

```
> var x = 42;
> x * 2
84
> #r "nuget: Newtonsoft.Json, 13.0.3"
> using Newtonsoft.Json;
> JsonConvert.SerializeObject(new { foo = "bar" })
"{"foo":"bar"}"
```

### Debugging

Debug with VS Code by adding a breakpoint and running with F5, or:

```bash
dotnet script test.csx --debug
```

### Use Records for Quick DTOs

No class files needed - define inline:

```csharp
// Records are perfect for CSX - single line definitions
public record Person(string Name, int Age, string Email);
public record ApiResponse<T>(bool Success, T? Data, string? Error);
public record SearchResult(string Title, string Slug, float Score);

var person = new Person("Scott", 50, "scott@example.com");
var response = new ApiResponse<Person>(true, person, null);
```

### Pretty Print with Dumpify

```csharp
#r "nuget: Dumpify, 0.6.5"

using Dumpify;

var data = new
{
    Name = "Test",
    Items = new[] { 1, 2, 3 },
    Nested = new { Foo = "bar" }
};

data.Dump();  // Pretty console output with colors
```

## Common Issues & Gotchas

### Issue: "NuGet package not found"

First run is slow - packages download in the background:

```csharp
#r "nuget: SomePackage, 1.0.0"  // First run: downloads
                                  // Second run: uses cache
```

**Fix**: Wait for first run to complete, or pre-download:

```bash
dotnet script init  # Creates omnisharp.json
dotnet script       # Downloads packages in REPL
```

### Issue: "Type or namespace not found"

Package version might be wrong or incompatible:

```csharp
// Bad - version doesn't have the type you need
#r "nuget: Microsoft.Extensions.Http, 6.0.0"

// Good - use matching version for your .NET SDK
#r "nuget: Microsoft.Extensions.Http, 9.0.0"
```

### Issue: gRPC on Windows

Qdrant and other gRPC services fail with HTTP/2 errors:

```csharp
// Add this BEFORE creating gRPC clients
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var client = new QdrantClient("localhost", 6334);  // Now works
```

### Issue: HttpClient Socket Exhaustion

Don't create multiple HttpClient instances in a loop:

```csharp
// Bad - creates socket exhaustion
foreach (var url in urls)
{
    using var client = new HttpClient();  // DON'T do this
    await client.GetAsync(url);
}

// Good - reuse HttpClient
using var client = new HttpClient();
foreach (var url in urls)
{
    await client.GetAsync(url);
}
```

### Issue: Async at Top Level

Top-level async just works in CSX - no async Main needed:

```csharp
// This works - no async Main needed
var response = await httpClient.GetAsync("https://example.com");
var content = await response.Content.ReadAsStringAsync();
Console.WriteLine(content);
```

### Issue: Assembly Load Conflicts

When referencing local DLLs that have dependencies:

```csharp
// Order matters - load dependencies first
#r "Mostlylucid.Shared/bin/Debug/net9.0/Mostlylucid.Shared.dll"
#r "Mostlylucid.Services/bin/Debug/net9.0/Mostlylucid.Services.dll"

// Or use NuGet for dependencies, local for your code
#r "nuget: Microsoft.Extensions.Logging, 9.0.0"
#r "MyLibrary/bin/Debug/net9.0/MyLibrary.dll"
```

### Issue: Script Won't Run After Edit

IntelliSense cache can get stale:

```bash
# Clear the cache
rm -rf ~/.dotnet-script/          # Linux/Mac
rd /s /q %USERPROFILE%\.dotnet-script\  # Windows
```

### Issue: Nullable Reference Types

CSX uses different defaults - enable explicitly if needed:

```csharp
#nullable enable

string? nullableString = null;  // OK
string nonNullable = null;      // Warning
```

## When to Use CSX vs Full Project

**Use CSX when:**
- Quick one-off tests
- API exploration
- Prototyping algorithms
- Testing NuGet packages before adding to project
- Validating regex, LINQ, JSON serialization
- Database query testing
- Learning/experimenting

**Use a full project when:**
- Multiple files with complex dependencies
- Unit testing (use xUnit/NUnit)
- Production code
- Team collaboration
- CI/CD pipelines

## Real-World Example: Testing My Blog API

Here's a script I use to test the Mostlylucid search endpoint:

```csharp
#r "nuget: System.Net.Http.Json, 8.0.0"

using System.Net.Http.Json;

var baseUrl = Args.Length > 0 ? Args[0] : "https://www.mostlylucid.net";
var searchTerm = Args.Length > 1 ? Args[1] : "docker";

var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

Console.WriteLine($"Searching {baseUrl} for '{searchTerm}'...\n");

var results = await http.GetFromJsonAsync<JsonElement>(
    $"/api/search?term={Uri.EscapeDataString(searchTerm)}");

if (results.TryGetProperty("results", out var items))
{
    foreach (var item in items.EnumerateArray().Take(5))
    {
        var title = item.GetProperty("title").GetString();
        var slug = item.GetProperty("slug").GetString();
        Console.WriteLine($"- {title}");
        Console.WriteLine($"  /{slug}\n");
    }
}
```

Run it:
```bash
dotnet script search-test.csx -- https://localhost:5001 "entity framework"
```

## Summary

CSX scripts are the perfect middle ground between the C# REPL and a full project. They're ideal for:

- **Speed**: Write and run in seconds
- **Simplicity**: No project ceremony
- **Power**: Full C# with NuGet support
- **Portability**: Share a single file

Next time you need to test something quick in C#, skip `dotnet new console` and reach for `dotnet script` instead.

**Resources:**
- [dotnet-script GitHub](https://github.com/dotnet-script/dotnet-script)
- [C# Scripting Documentation](https://docs.microsoft.com/en-us/archive/msdn-magazine/2016/january/essential-net-csharp-scripting)
