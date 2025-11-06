using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mostlylucid.Markdig.FetchExtension.Storage;
using Mostlylucid.Markdig.FetchExtension.Events;
using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;

namespace Mostlylucid.Markdig.FetchExtension;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the optional in-memory fetch polling service for the Markdig Fetch extension.
    ///     Polling is disabled by default (options.Enabled=false). Hosts can set Enabled=true to start polling.
    /// </summary>
    public static IServiceCollection AddMarkdownFetchPolling(this IServiceCollection services,
        Action<FetchPollingOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.TryAddEnumerable(ServiceDescriptor
                .Singleton<IConfigureOptions<FetchPollingOptions>, DefaultOptions>());

        // Register IHttpClientFactory named client if not otherwise customized by host
        services.AddHttpClient("Mostlylucid.Markdig.FetchExtension");

        // Register the update service as singleton (stateful in-memory) and hosted background service
        services.TryAddSingleton<IMarkdownFetchUpdateService, MarkdownFetchUpdateService>();
        services.AddHostedService(sp =>
            (MarkdownFetchUpdateService)sp.GetRequiredService<IMarkdownFetchUpdateService>());

        return services;
    }

    /// <summary>
    ///     Registers an in-memory implementation of IMarkdownFetchService.
    ///     Cached markdown is stored in memory and will be lost on restart.
    ///     Perfect for demos, testing, or applications that don't need persistence.
    /// </summary>
    public static IServiceCollection AddInMemoryMarkdownFetch(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.TryAddScoped<IMarkdownFetchService, InMemoryMarkdownFetchService>();
        services.TryAddSingleton<IMarkdownFetchEventPublisher, MarkdownFetchEventPublisher>();
        return services;
    }

    /// <summary>
    ///     Registers a file-based implementation of IMarkdownFetchService.
    ///     Cached markdown is persisted to disk and survives restarts.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="cacheDirectory">Optional directory for cache files. Defaults to temp directory.</param>
    public static IServiceCollection AddFileBasedMarkdownFetch(this IServiceCollection services,
        string? cacheDirectory = null)
    {
        services.AddHttpClient();
        services.TryAddScoped<IMarkdownFetchService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<FileBasedMarkdownFetchService>>();
            var eventPublisher = sp.GetService<IMarkdownFetchEventPublisher>();
            return new FileBasedMarkdownFetchService(httpClientFactory, logger, eventPublisher, cacheDirectory);
        });
        services.TryAddSingleton<IMarkdownFetchEventPublisher, MarkdownFetchEventPublisher>();
        return services;
    }

    private sealed class DefaultOptions : IConfigureOptions<FetchPollingOptions>
    {
        public void Configure(FetchPollingOptions options)
        {
            // Keep defaults; Enabled=false ensures optional behavior with a warning in logs.
        }
    }
}