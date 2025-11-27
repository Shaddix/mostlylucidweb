# Integration Guide: Mostlylucid.Markdig.FetchExtension

This guide shows how to integrate the FetchExtension into various types of applications.

## Table of Contents

- [ASP.NET Core MVC/Razor Pages](#aspnet-core-mvcrazor-pages)
- [ASP.NET Core Minimal API](#aspnet-core-minimal-api)
- [Console Applications](#console-applications)
- [Blazor Server/WASM](#blazor-serverwasm)
- [Background Processing](#background-processing)

---

## ASP.NET Core MVC/Razor Pages

### Step 1: Install Package

```bash
dotnet add package Mostlylucid.Markdig.FetchExtension
# Choose storage provider:
dotnet add package Mostlylucid.Markdig.FetchExtension.Postgres
# or Sqlite, SqlServer
```

### Step 2: Configure Services (Program.cs)

```csharp
using Mostlylucid.Markdig.FetchExtension;

var builder = WebApplication.CreateBuilder(args);

// Add MVC/Razor Pages
builder.Services.AddControllersWithViews();
// or builder.Services.AddRazorPages();

// Configure storage
builder.Services.AddPostgresMarkdownFetch(
    builder.Configuration.GetConnectionString("DefaultConnection"));

// Register preprocessor as singleton
builder.Services.AddSingleton<MarkdownFetchPreprocessor>();

var app = builder.Build();

// Ensure database is created
app.Services.EnsureMarkdownCacheDatabase();

// Configure fetch extension
FetchMarkdownExtension.ConfigureServiceProvider(app.Services);

app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}");
app.Run();
```

### Step 3: Create Rendering Service

```csharp
public class MarkdownRenderingService
{
    private readonly MarkdownFetchPreprocessor _preprocessor;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderingService(MarkdownFetchPreprocessor preprocessor)
    {
        _preprocessor = preprocessor;

        // Build your pipeline once
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UsePipeTables()
            .UseGenericAttributes()
            .Build();
    }

    public string RenderToHtml(string markdown)
    {
        // Preprocess fetch tags
        var processed = _preprocessor.Preprocess(markdown);

        // Render to HTML
        return Markdown.ToHtml(processed, _pipeline);
    }
}
```

### Step 4: Use in Controllers/Pages

```csharp
public class BlogController : Controller
{
    private readonly MarkdownRenderingService _markdownService;

    public BlogController(MarkdownRenderingService markdownService)
    {
        _markdownService = markdownService;
    }

    public IActionResult Post(string slug)
    {
        var markdown = GetMarkdownContent(slug); // Your storage
        var html = _markdownService.RenderToHtml(markdown);
        return View(new BlogPostViewModel { Html = html });
    }
}
```

---

## ASP.NET Core Minimal API

Perfect for lightweight apps and APIs:

```csharp
using Markdig;
using Mostlylucid.Markdig.FetchExtension;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();
builder.Services.AddFileBasedMarkdownFetch("./cache");

var app = builder.Build();

FetchMarkdownExtension.ConfigureServiceProvider(app.Services);

app.MapPost("/render", async (RenderRequest req, IServiceProvider sp) =>
{
    var preprocessor = new MarkdownFetchPreprocessor(sp);
    var processed = preprocessor.Preprocess(req.Markdown);

    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    var html = Markdown.ToHtml(processed, pipeline);
    return Results.Json(new { html });
});

app.Run();

record RenderRequest(string Markdown);
```

---

## Console Applications

For CLI tools, documentation generators, etc.:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.Markdig.FetchExtension;
using Markdig;

// Setup DI
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddInMemoryMarkdownFetch();
var sp = services.BuildServiceProvider();

// Configure extension
FetchMarkdownExtension.ConfigureServiceProvider(sp);

// Create preprocessor and pipeline
var preprocessor = new MarkdownFetchPreprocessor(sp);
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Build();

// Read markdown file
var markdown = await File.ReadAllTextAsync("input.md");

// Process
var processed = preprocessor.Preprocess(markdown);
var html = Markdown.ToHtml(processed, pipeline);

// Write output
await File.WriteAllTextAsync("output.html", html);
Console.WriteLine("Rendered successfully!");
```

---

## Blazor Server/WASM

### Blazor Server

```csharp
// Program.cs
builder.Services.AddServerSideBlazor();
builder.Services.AddSqliteMarkdownFetch("Data Source=app.db");
builder.Services.AddScoped<MarkdownRenderingService>();

var app = builder.Build();
app.Services.EnsureMarkdownCacheDatabase();
FetchMarkdownExtension.ConfigureServiceProvider(app.Services);
```

```razor
@* Component.razor *@
@inject MarkdownRenderingService MarkdownService

<div>
    @((MarkupString)renderedHtml)
</div>

@code {
    private string renderedHtml = "";

    protected override void OnInitialized()
    {
        var markdown = "# Title\n<fetch markdownurl=\"...\" pollfrequency=\"24\"/>";
        renderedHtml = MarkdownService.RenderToHtml(markdown);
    }
}
```

### Blazor WASM

**Note**: Blazor WASM runs in the browser, so server-side fetching won't work. Instead:

1. Use a backend API to render markdown
2. Call the API from your Blazor WASM app
3. Or use client-side fetch if CORS allows

```csharp
// In WASM app
public class MarkdownApiClient
{
    private readonly HttpClient _http;

    public MarkdownApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> RenderAsync(string markdown)
    {
        var response = await _http.PostAsJsonAsync("/api/render",
            new { markdown });
        var result = await response.Content.ReadFromJsonAsync<RenderResult>();
        return result.Html;
    }
}
```

---

## Background Processing

For scheduled jobs, queue workers, etc.:

```csharp
public class MarkdownProcessingJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarkdownProcessingJob> _logger;

    public MarkdownProcessingJob(
        IServiceProvider serviceProvider,
        ILogger<MarkdownProcessingJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingDocuments();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessPendingDocuments()
    {
        using var scope = _serviceProvider.CreateScope();
        var preprocessor = new MarkdownFetchPreprocessor(scope.ServiceProvider);

        var documents = await GetPendingDocuments(); // Your logic

        foreach (var doc in documents)
        {
            try
            {
                var processed = preprocessor.Preprocess(doc.Markdown);
                var html = Markdown.ToHtml(processed, BuildPipeline());
                await SaveRenderedDocument(doc.Id, html);

                _logger.LogInformation("Processed document {Id}", doc.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document {Id}", doc.Id);
            }
        }
    }

    private MarkdownPipeline BuildPipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }
}
```

Register in Program.cs:

```csharp
builder.Services.AddHostedService<MarkdownProcessingJob>();
```

---

## Best Practices

### 1. Pipeline Reuse

Build your pipeline **once** and reuse it:

```csharp
// GOOD - Build once
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string Render(string md)
    {
        return Markdown.ToHtml(md, _pipeline);
    }
}

// BAD - Build every time
public string Render(string md)
{
    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
    return Markdown.ToHtml(md, pipeline);
}
```

### 2. Preprocessor Singleton

Register as singleton for better performance:

```csharp
builder.Services.AddSingleton<MarkdownFetchPreprocessor>();
```

### 3. Storage Provider Selection

Choose based on your deployment:

| Deployment | Recommended Storage |
|------------|---------------------|
| Single server | File-based or SQLite |
| Multiple servers | PostgreSQL or SQL Server |
| Kubernetes | PostgreSQL (with persistent volume) |
| Serverless | Consider API-based caching |
| Development | In-memory |

### 4. Error Handling

Wrap rendering in try-catch for production:

```csharp
public string SafeRender(string markdown)
{
    try
    {
        var processed = _preprocessor.Preprocess(markdown);
        return Markdown.ToHtml(processed, _pipeline);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Markdown rendering failed");
        return "<p>Error rendering content</p>";
    }
}
```

### 5. Logging

Enable logging to debug fetch issues:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug); // See fetch details
});
```

---

## Troubleshooting

### Content Not Fetching

1. Check your service provider is configured:
   ```csharp
   FetchMarkdownExtension.ConfigureServiceProvider(serviceProvider);
   ```

2. Verify fetch service is registered:
   ```csharp
   services.AddInMemoryMarkdownFetch(); // or other provider
   ```

3. Enable debug logging to see fetch attempts

### Cache Not Working

1. For file-based: Check directory permissions
2. For database: Verify connection string and migrations
3. Check poll frequency isn't 0 (always fetches)

### Slow Performance

1. Use database storage for multi-server deployments
2. Increase poll frequency to reduce fetches
3. Consider background polling to prefetch content
4. Build pipeline once, not per request

---

## Need Help?

- 📖 Full Documentation: [README.md](README.md)
- 🐛 Report Issues: [GitHub Issues](https://github.com/scottgal/mostlylucidweb/issues)
- 💬 Discussions: [GitHub Discussions](https://github.com/scottgal/mostlylucidweb/discussions)
