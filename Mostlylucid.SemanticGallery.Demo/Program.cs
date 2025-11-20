using Mostlylucid.SemanticGallery.Demo.Services;
using Mostlylucid.SemanticSearch.Extensions;
using Mostlylucid.SemanticSearch.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add semantic search services (optional - will gracefully degrade if models not available)
try
{
    builder.Services.AddSemanticSearch(builder.Configuration);
    Console.WriteLine("✓ Semantic search services registered");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Semantic search not available: {ex.Message}");
}

// Register gallery services
builder.Services.AddSingleton<InMemoryGalleryService>();
builder.Services.AddSingleton<SimplifiedImageAnalysisService>();

// Enable CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure middleware
app.UseStaticFiles();
app.UseCors();
app.UseRouting();
app.MapControllers();

// Serve the gallery UI at root
app.MapGet("/", () => Results.Redirect("/index.html"));

// Health check endpoint
app.MapGet("/health", () => new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
});

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          Semantic Gallery Demo - Starting Up...              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("📸 Gallery API: https://localhost:5001/api/gallery");
Console.WriteLine("🔍 Search API: https://localhost:5001/api/gallery/search");
Console.WriteLine("🌐 Web UI: https://localhost:5001");
Console.WriteLine();

app.Run();
