using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Services;

namespace Mostlylucid.DocSummarizer.Extensions;

/// <summary>
/// Extension methods for registering DocSummarizer services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DocSummarizer services to the service collection with default configuration.
    /// Uses ONNX embeddings (local, no external services required) and BertRag mode.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // In Program.cs or Startup.cs
    /// builder.Services.AddDocSummarizer();
    /// 
    /// // Then inject IDocumentSummarizer in your services
    /// public class MyService(IDocumentSummarizer summarizer)
    /// {
    ///     public async Task&lt;string&gt; SummarizeAsync(string markdown)
    ///     {
    ///         var result = await summarizer.SummarizeMarkdownAsync(markdown);
    ///         return result.ExecutiveSummary;
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddDocSummarizer(this IServiceCollection services)
    {
        return services.AddDocSummarizer(_ => { });
    }

    /// <summary>
    /// Adds DocSummarizer services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddDocSummarizer(options =>
    /// {
    ///     // Use Ollama for embeddings instead of local ONNX
    ///     options.EmbeddingBackend = EmbeddingBackend.Ollama;
    ///     options.Ollama.BaseUrl = "http://localhost:11434";
    ///     options.Ollama.Model = "llama3.2:3b";
    ///     
    ///     // Configure vector storage
    ///     options.BertRag.VectorStore = VectorStoreBackend.DuckDB;
    ///     options.BertRag.ReindexOnStartup = false; // Production setting
    ///     
    ///     // Verbose logging during development
    ///     options.Output.Verbose = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDocSummarizer(
        this IServiceCollection services,
        Action<DocSummarizerConfig> configure)
    {
        // Register configuration
        services.Configure(configure);
        
        // Register core services
        services.TryAddSingleton<IEmbeddingService>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<DocSummarizerConfig>>().Value;
            var logger = sp.GetService<ILogger<IEmbeddingService>>();
            
            return config.EmbeddingBackend == EmbeddingBackend.Onnx
                ? CreateOnnxEmbeddingService(config.Onnx, config.Output.Verbose)
                : CreateOllamaEmbeddingService(config.Ollama);
        });
        
        services.TryAddSingleton<IVectorStore>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<DocSummarizerConfig>>().Value;
            return CreateVectorStore(config);
        });
        
        services.TryAddSingleton<IDocumentSummarizer, DocumentSummarizerService>();
        
        // Register the startup initializer as a hosted service
        services.AddHostedService<DocSummarizerInitializer>();
        
        return services;
    }

    /// <summary>
    /// Adds DocSummarizer services bound to a configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationSection">The configuration section containing DocSummarizer settings.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // In appsettings.json:
    /// // {
    /// //   "DocSummarizer": {
    /// //     "EmbeddingBackend": "Onnx",
    /// //     "BertRag": {
    /// //       "ReindexOnStartup": false
    /// //     }
    /// //   }
    /// // }
    /// 
    /// builder.Services.AddDocSummarizer(
    ///     builder.Configuration.GetSection("DocSummarizer"));
    /// </code>
    /// </example>
    public static IServiceCollection AddDocSummarizer(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfigurationSection configurationSection)
    {
        services.Configure<DocSummarizerConfig>(configurationSection);
        
        // Register core services (same as above)
        services.TryAddSingleton<IEmbeddingService>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<DocSummarizerConfig>>().Value;
            return config.EmbeddingBackend == EmbeddingBackend.Onnx
                ? CreateOnnxEmbeddingService(config.Onnx, config.Output.Verbose)
                : CreateOllamaEmbeddingService(config.Ollama);
        });
        
        services.TryAddSingleton<IVectorStore>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<DocSummarizerConfig>>().Value;
            return CreateVectorStore(config);
        });
        
        services.TryAddSingleton<IDocumentSummarizer, DocumentSummarizerService>();
        services.AddHostedService<DocSummarizerInitializer>();
        
        return services;
    }

    private static IEmbeddingService CreateOnnxEmbeddingService(OnnxConfig config, bool verbose)
    {
        // Will be implemented when we copy services to Core
        throw new NotImplementedException("ONNX embedding service - copy from DocSummarizer");
    }

    private static IEmbeddingService CreateOllamaEmbeddingService(OllamaConfig config)
    {
        // Will be implemented when we copy services to Core
        throw new NotImplementedException("Ollama embedding service - copy from DocSummarizer");
    }

    private static IVectorStore CreateVectorStore(DocSummarizerConfig config)
    {
        return config.BertRag.VectorStore switch
        {
            VectorStoreBackend.InMemory => new InMemoryVectorStore(),
            VectorStoreBackend.DuckDB => CreateDuckDbStore(config),
            VectorStoreBackend.Qdrant => CreateQdrantStore(config),
            _ => new InMemoryVectorStore()
        };
    }

    private static IVectorStore CreateDuckDbStore(DocSummarizerConfig config)
    {
        // Will be implemented when we copy services to Core
        throw new NotImplementedException("DuckDB vector store - copy from DocSummarizer");
    }

    private static IVectorStore CreateQdrantStore(DocSummarizerConfig config)
    {
        // Will be implemented when we copy services to Core
        throw new NotImplementedException("Qdrant vector store - copy from DocSummarizer");
    }
}
