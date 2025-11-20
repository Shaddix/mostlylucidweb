using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Mostlylucid.SentimentAnalysis.Config;
using Mostlylucid.SentimentAnalysis.Services;

namespace Mostlylucid.SentimentAnalysis.Extensions;

/// <summary>
/// Extension methods for registering sentiment analysis services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add sentiment analysis services to the DI container
    /// </summary>
    public static IServiceCollection AddSentimentAnalysis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<SentimentAnalysisConfig>(
            configuration.GetSection("SentimentAnalysis"));

        // Register services
        services.AddSingleton<ISentimentAnalysisService, SentimentAnalysisService>();

        return services;
    }
}
