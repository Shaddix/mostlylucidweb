using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.BotDetection.Extensions;
using Mostlylucid.Referrers.Config;
using Mostlylucid.Referrers.Services;

namespace Mostlylucid.Referrers.Extensions;

/// <summary>
/// Extension methods for registering referrer services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds referrer tracking services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddReferrerTracking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure referrer settings
        services.Configure<ReferrerConfig>(
            configuration.GetSection(ReferrerConfig.SectionName));

        // Add bot detection if not already registered
        services.AddBotDetection();

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register referrer service
        services.AddScoped<IReferrerService, ReferrerService>();

        return services;
    }

    /// <summary>
    /// Adds referrer tracking services with custom configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure referrer options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddReferrerTracking(
        this IServiceCollection services,
        Action<ReferrerConfig> configureOptions)
    {
        // Configure referrer settings
        services.Configure(configureOptions);

        // Add bot detection if not already registered
        services.AddBotDetection();

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register referrer service
        services.AddScoped<IReferrerService, ReferrerService>();

        return services;
    }
}
