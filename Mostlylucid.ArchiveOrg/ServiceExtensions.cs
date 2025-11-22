using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.ArchiveOrg.Config;
using Mostlylucid.ArchiveOrg.Services;
using Polly;
using Polly.Extensions.Http;

namespace Mostlylucid.ArchiveOrg;

public static class ServiceExtensions
{
    /// <summary>
    /// Add all Archive.org downloader services
    /// </summary>
    public static IServiceCollection AddArchiveOrgServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<ArchiveOrgOptions>(configuration.GetSection(ArchiveOrgOptions.SectionName));
        services.Configure<MarkdownConversionOptions>(configuration.GetSection(MarkdownConversionOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));

        // HTTP clients with retry policies
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        services.AddHttpClient<ICdxApiClient, CdxApiClient>()
            .AddPolicyHandler(retryPolicy);

        services.AddHttpClient<IArchiveDownloader, ArchiveDownloader>()
            .AddPolicyHandler(retryPolicy)
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mostlylucid-ArchiveOrg-Downloader/1.0 (https://github.com/scottgal/mostlylucidweb)");
            });

        services.AddHttpClient<IOllamaTagGenerator, OllamaTagGenerator>();

        services.AddHttpClient<IHtmlToMarkdownConverter, HtmlToMarkdownConverter>()
            .AddPolicyHandler(retryPolicy);

        return services;
    }
}
