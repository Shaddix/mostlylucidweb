using Htmx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Extensions;
using Mostlylucid.SegmentCommerce.Middleware;
using Mostlylucid.SegmentCommerce.Services;
using Mostlylucid.SegmentCommerce.Services.Attributes;
using Mostlylucid.SegmentCommerce.Services.Embeddings;
using Mostlylucid.SegmentCommerce.Services.Profiles;
using Mostlylucid.SegmentCommerce.Services.Queue;
using Mostlylucid.SegmentCommerce.Services.Segments;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
if (builder.Environment.IsDevelopment())
{
    config.AddUserSecrets<Program>();
}
config.AddEnvironmentVariables();

builder.Services.AddControllersWithViews();

// Add antiforgery with HTMX header support
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.SameSite = SameSiteMode.Strict;
});
var connectionString = config.GetConnectionString("DefaultConnection");

// Configure NpgsqlDataSource with dynamic JSON support for JSONB columns
var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<SegmentCommerceDbContext>(options =>
    options.UseNpgsql(dataSource, npgsqlOptions =>
    {
        npgsqlOptions.UseVector();
    }));

builder.Services.AddAuthenticationAndAuthorization(builder.Configuration);

builder.Services.RegisterApplicationServices();

builder.Services.RegisterQueueServices();

// Client fingerprint (zero-cookie session identification)
builder.Services.AddClientFingerprint(builder.Configuration);

// Embedding service - prefer ONNX for local inference, fallback to Ollama
var useOnnxEmbeddings = builder.Configuration.GetValue("Embeddings:UseOnnx", true);
if (useOnnxEmbeddings)
{
    builder.Services.AddScoped<IEmbeddingService, OnnxEmbeddingService>();
}
else
{
    var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
    {
        client.BaseAddress = new Uri(ollamaUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
    });
}

if (builder.Configuration.GetValue<bool>("BackgroundWorkers:Enabled", false))
{
    builder.Services.AddHostedService<JobWorkerService>();
    builder.Services.AddHostedService<OutboxWorkerService>();
}

// In-memory distributed cache (sufficient for single-instance demo)
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".SegmentCommerce.Session";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SegmentCommerceDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        await DbSeeder.SeedAsync(context);
        
        // Seed dynamic segments (LLM-named from data patterns)
        var segmentGenerator = scope.ServiceProvider.GetRequiredService<ISegmentGeneratorService>();
        await segmentGenerator.SeedDefaultSegmentsAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.UseProfileIdentification();

// Health check endpoint for Docker/Kubernetes
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
