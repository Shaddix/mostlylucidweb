using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.LlmAltText.Models;
using Mostlylucid.LlmAltText.Services;

namespace Mostlylucid.LlmAltText.Extensions;

/// <summary>
/// Extension methods for registering alt text services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add AI-powered alt text generation services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>Service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // Basic usage with defaults
    /// services.AddAltTextGeneration();
    ///
    /// // With custom configuration
    /// services.AddAltTextGeneration(options =>
    /// {
    ///     options.ModelPath = "./my-models";
    ///     options.EnableDiagnosticLogging = true;
    ///     options.MaxWords = 100;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAltTextGeneration(
        this IServiceCollection services,
        Action<AltTextOptions>? configure = null)
    {
        // Configure options
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<AltTextOptions>(options => { });
        }

        // Register the image analysis service as a singleton
        // (model initialization is expensive, so we share one instance)
        services.AddSingleton<IImageAnalysisService, Florence2ImageAnalysisService>();

        return services;
    }

    /// <summary>
    /// Update alt text generation options after initial registration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection ConfigureAltTextGeneration(
        this IServiceCollection services,
        Action<AltTextOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}
