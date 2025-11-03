using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Mostlylucid.Markdig.FetchExtension;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the optional in-memory fetch polling service for the Markdig Fetch extension.
    /// Polling is disabled by default (options.Enabled=false). Hosts can set Enabled=true to start polling.
    /// </summary>
    public static IServiceCollection AddMarkdownFetchPolling(this IServiceCollection services, Action<FetchPollingOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<FetchPollingOptions>, DefaultOptions>());
        }

        // Register IHttpClientFactory named client if not otherwise customized by host
        services.AddHttpClient("Mostlylucid.Markdig.FetchExtension");

        // Register the update service as singleton (stateful in-memory) and hosted background service
        services.TryAddSingleton<IMarkdownFetchUpdateService, MarkdownFetchUpdateService>();
        services.AddHostedService(sp => (MarkdownFetchUpdateService)sp.GetRequiredService<IMarkdownFetchUpdateService>());

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
