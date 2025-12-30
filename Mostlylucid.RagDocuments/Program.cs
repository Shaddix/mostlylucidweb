using Microsoft.EntityFrameworkCore;
using Mostlylucid.DocSummarizer.Extensions;
using Mostlylucid.RagDocuments.Config;
using Mostlylucid.RagDocuments.Data;
using Mostlylucid.RagDocuments.Services;
using Mostlylucid.RagDocuments.Services.Background;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

// Configuration
builder.Services.Configure<RagDocumentsConfig>(
    builder.Configuration.GetSection(RagDocumentsConfig.SectionName));
builder.Services.Configure<PromptsConfig>(
    builder.Configuration.GetSection(PromptsConfig.SectionName));

var ragConfig = builder.Configuration
    .GetSection(RagDocumentsConfig.SectionName)
    .Get<RagDocumentsConfig>() ?? new();

// Database
builder.Services.AddDbContext<RagDocumentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// DocSummarizer.Core
builder.Services.AddDocSummarizer(builder.Configuration.GetSection("DocSummarizer"));

// Application services
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IAgenticSearchService, AgenticSearchService>();
builder.Services.AddSingleton<DocumentProcessingQueue>();
builder.Services.AddHostedService<DocumentQueueProcessor>();

// MVC + Razor
builder.Services.AddControllersWithViews();

// OpenAPI
builder.Services.AddOpenApi();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();

// Serilog request logging
app.UseSerilogRequestLogging();

// Development tools
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Static files
app.UseStaticFiles();

// Routing
app.UseRouting();

// Health check
app.MapHealthChecks("/healthz");

// Controllers
app.MapControllers();

// Default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RagDocumentsDbContext>();
    await db.Database.MigrateAsync();
}

// Ensure upload directory exists
Directory.CreateDirectory(ragConfig.UploadPath);

app.Run();
