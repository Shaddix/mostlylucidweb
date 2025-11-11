using Microsoft.AspNetCore.Mvc;
using Mostlylucid.BlogLLM.Api.Models;
using Mostlylucid.BlogLLM.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Mostlylucid BlogLLM API",
        Version = "v1",
        Description = "RAG-powered blog assistant with local LLM inference"
    });
});

// CORS for blog app integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlogApp", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5000", "https://localhost:5001" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure services from appsettings
var config = builder.Configuration.GetSection("BlogRag");

var embeddingModelPath = config["EmbeddingModel:ModelPath"]
    ?? throw new InvalidOperationException("EmbeddingModel:ModelPath not configured");
var embeddingTokenizerPath = config["EmbeddingModel:TokenizerPath"]
    ?? throw new InvalidOperationException("EmbeddingModel:TokenizerPath not configured");
var embeddingDimensions = int.Parse(config["EmbeddingModel:Dimensions"] ?? "384");
var embeddingUseGpu = bool.Parse(config["EmbeddingModel:UseGpu"] ?? "false");

var llmModelPath = config["LlmModel:ModelPath"]
    ?? throw new InvalidOperationException("LlmModel:ModelPath not configured");
var llmContextSize = int.Parse(config["LlmModel:ContextSize"] ?? "4096");
var llmGpuLayers = int.Parse(config["LlmModel:GpuLayers"] ?? "20");

var qdrantHost = config["VectorStore:Host"] ?? "localhost";
var qdrantPort = int.Parse(config["VectorStore:Port"] ?? "6334");
var collectionName = config["VectorStore:CollectionName"] ?? "blog_knowledge_base";
var qdrantApiKey = config["VectorStore:ApiKey"];

// Register services as singletons (they manage their own resources)
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<EmbeddingService>>();
    logger.LogInformation("Initializing EmbeddingService");
    return new EmbeddingService(embeddingModelPath, embeddingTokenizerPath, embeddingDimensions, embeddingUseGpu);
});

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<VectorStoreService>>();
    logger.LogInformation("Initializing VectorStoreService");
    return new VectorStoreService(qdrantHost, qdrantPort, collectionName, qdrantApiKey);
});

builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LlmInferenceService>>();
    return new LlmInferenceService(llmModelPath, llmContextSize, llmGpuLayers, logger);
});

builder.Services.AddScoped<RagService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
    .AddAsyncCheck("vector_store",  async () =>
    {
        try
        {
            var sp = builder.Services.BuildServiceProvider();
            var vectorStore = sp.GetRequiredService<VectorStoreService>();
            var exists = await vectorStore.CollectionExistsAsync();
            return exists
                ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Vector store is accessible")
                : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Collection does not exist");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Cannot connect to vector store", ex);
        }
    });

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowBlogApp");

// API Endpoints

/// <summary>
/// Health check endpoint
/// </summary>
app.MapGet("/health", async (HttpContext context) =>
{
    var healthCheckService = context.RequestServices.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
    var result = await healthCheckService.CheckHealthAsync();

    return Results.Json(new
    {
        status = result.Status.ToString(),
        checks = result.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description
        })
    });
})
.WithName("HealthCheck")
.WithOpenApi();

/// <summary>
/// RAG-based question answering
/// </summary>
app.MapPost("/api/rag/ask", async (RagRequest request, RagService ragService, CancellationToken ct) =>
{
    try
    {
        var response = await ragService.AskAsync(request, ct);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error processing RAG request",
            detail: ex.Message,
            statusCode: 500
        );
    }
})
.WithName("AskQuestion")
.WithOpenApi()
.Produces<RagResponse>()
.Produces<ProblemDetails>(500);

/// <summary>
/// Semantic search in knowledge base
/// </summary>
app.MapPost("/api/search", async (SearchRequest request, RagService ragService, CancellationToken ct) =>
{
    try
    {
        var response = await ragService.SearchAsync(request, ct);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error processing search request",
            detail: ex.Message,
            statusCode: 500
        );
    }
})
.WithName("SearchKnowledgeBase")
.WithOpenApi()
.Produces<SearchResponse>()
.Produces<ProblemDetails>(500);

/// <summary>
/// Get API information
/// </summary>
app.MapGet("/api/info", (IConfiguration config) =>
{
    return Results.Ok(new
    {
        name = "Mostlylucid BlogLLM API",
        version = "1.0.0",
        description = "RAG-powered blog assistant with local LLM inference",
        endpoints = new[]
        {
            "/health - Health check",
            "/api/rag/ask - RAG question answering (POST)",
            "/api/search - Semantic search (POST)",
            "/api/info - API information"
        },
        models = new
        {
            embedding = config["BlogRag:EmbeddingModel:ModelPath"],
            llm = config["BlogRag:LlmModel:ModelPath"],
            vectorStore = $"{config["BlogRag:VectorStore:Host"]}:{config["BlogRag:VectorStore:Port"]}"
        }
    });
})
.WithName("GetInfo")
.WithOpenApi();

app.Run();
