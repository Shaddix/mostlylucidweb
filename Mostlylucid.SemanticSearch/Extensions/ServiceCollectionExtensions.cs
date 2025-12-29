using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Shared.Config;

namespace Mostlylucid.SemanticSearch.Extensions;

/// <summary>
/// Extension methods for registering semantic search services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add semantic search services to the DI container
    /// </summary>
    public static void AddSemanticSearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration using POCO pattern
        services.ConfigurePOCO<SemanticSearchConfig>(
            configuration.GetSection(SemanticSearchConfig.Section));

        // Get config to determine backend
        var config = configuration.GetSection(SemanticSearchConfig.Section).Get<SemanticSearchConfig>() 
            ?? new SemanticSearchConfig();

        // Register services based on configured backend
        if (config.Backend == VectorStoreBackend.DuckDB)
        {
            // DuckDB backend - uses DocSummarizer.Core, zero external dependencies
            services.AddSingleton<IEmbeddingService, DocSummarizerEmbeddingService>();
            services.AddSingleton<IVectorStoreService, DuckDbVectorStoreService>();
        }
        else
        {
            // Qdrant backend - requires external Qdrant server
            services.AddSingleton<IEmbeddingService, OnnxEmbeddingService>();
            services.AddSingleton<IVectorStoreService, QdrantVectorStoreService>();
        }

        services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
    }
}
