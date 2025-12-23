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

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
if (builder.Environment.IsDevelopment())
{
    config.AddUserSecrets<Program>();
}
config.AddEnvironmentVariables();

builder.Services.AddControllersWithViews();
var connectionString = config.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<SegmentCommerceDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseVector();
    }));

builder.Services.AddAuthenticationAndAuthorization(builder.Configuration);

builder.Services.RegisterApplicationServices();

builder.Services.RegisterQueueServices();

// Client fingerprint (zero-cookie session identification)
builder.Services.AddClientFingerprint(builder.Configuration);

var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

if (builder.Configuration.GetValue<bool>("BackgroundWorkers:Enabled", false))
{
    builder.Services.AddHostedService<JobWorkerService>();
    builder.Services.AddHostedService<OutboxWorkerService>();
}

// Distributed cache - Redis in production, in-memory for development
var useRedis = builder.Configuration.GetValue<bool>("Cache:UseRedis", false);
var redisConnection = builder.Configuration.GetConnectionString("Redis");

if (useRedis && !string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = builder.Configuration["Cache:InstanceName"] ?? "SegmentCommerce_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

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
    
    try
    {
        await DbSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
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

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
