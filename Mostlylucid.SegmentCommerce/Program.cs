using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Services;
using Mostlylucid.SegmentCommerce.Services.Embeddings;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Mostlylucid.SegmentCommerce.Middleware;
using Mostlylucid.SegmentCommerce.Services.Profiles;
using Mostlylucid.SegmentCommerce.Services.Queue;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Configure PostgreSQL with EF Core + pgvector
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=segmentcommerce;Username=postgres;Password=postgres";

builder.Services.AddDbContext<SegmentCommerceDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseVector(); // Enable pgvector
    }));

// Authentication / Authorization (Bearer/JWT)
var signingKey = builder.Configuration["Jwt:SigningKey"];
var audience = builder.Configuration["Jwt:Audience"];
var authority = builder.Configuration["Jwt:Authority"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;

        if (!string.IsNullOrEmpty(signingKey))
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrEmpty(authority),
                ValidateAudience = !string.IsNullOrEmpty(audience),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("admin");
    });
});

// Register application services
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<InteractionService>();
builder.Services.AddScoped<IProfileKeyService, ProfileKeyService>();
builder.Services.AddScoped<ISessionCollector, SessionCollector>();
builder.Services.AddScoped<IProfilePromoter, ProfilePromoter>();
builder.Services.AddScoped<IProfileMerger, ProfileMerger>();

// Register queue services
builder.Services.AddScoped<IJobQueue, PostgresJobQueue>();
builder.Services.AddScoped<IOutbox, PostgresOutbox>();
builder.Services.AddScoped<IOutboxProcessor, OutboxProcessor>();

// Register embedding service
var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Background workers (only enable in production or explicitly)
if (builder.Configuration.GetValue<bool>("BackgroundWorkers:Enabled", false))
{
    builder.Services.AddHostedService<JobWorkerService>();
    builder.Services.AddHostedService<OutboxWorkerService>();
}

// Add session support for interest signatures
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".SegmentCommerce.Session";
});

var app = builder.Build();

// Seed database on startup (for demo purposes)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SegmentCommerceDbContext>();
    
    // Apply migrations and seed data
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

// Configure the HTTP request pipeline
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
