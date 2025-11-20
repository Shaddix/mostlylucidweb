using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.BotDetection.Detectors;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Services;

namespace Mostlylucid.BotDetection.Extensions;

/// <summary>
/// Extension methods for registering bot detection services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add bot detection services to the DI container
    /// </summary>
    public static IServiceCollection AddBotDetection(
        this IServiceCollection services,
        Action<BotDetectionOptions>? configure = null)
    {
        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<BotDetectionOptions>(options => { });
        }

        // Register detectors
        services.AddSingleton<IDetector, UserAgentDetector>();
        services.AddSingleton<IDetector, HeaderDetector>();
        services.AddSingleton<IDetector, IpDetector>();
        services.AddSingleton<IDetector, BehavioralDetector>();
        services.AddSingleton<IDetector, LlmDetector>();

        // Register main service
        services.AddSingleton<IBotDetectionService, BotDetectionService>();

        // Add memory cache if not already registered
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Add bot detection with configuration from appsettings
    /// </summary>
    public static IServiceCollection AddBotDetection(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<BotDetectionOptions>(
            configuration.GetSection("BotDetection"));

        return services.AddBotDetection();
    }
}
