using Markdig;
using Markdig.Parsers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Markdig.Extensions;
using Mostlylucid.Shared.Config.Markdown;

namespace Mostlylucid.Services.Markdown;

public class MarkdownBaseService
{
    public const string EnglishLanguage = "en";
    private readonly IServiceProvider? _serviceProvider;

    public MarkdownBaseService()
    {
        // Parameterless constructor for backward compatibility
    }

    public MarkdownBaseService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected MarkdownPipeline Pipeline() => Pipeline(null);

    protected MarkdownPipeline Pipeline(Action<MarkdownPipelineBuilder>? configure)
    {
        var builder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseToc(); // Custom TOC extension - supports [TOC] markers

        // Add ImgExtension with DI if available
        if (_serviceProvider != null)
        {
            var env = _serviceProvider.GetRequiredService<IWebHostEnvironment>();
            var imageConfig = _serviceProvider.GetRequiredService<ImageConfig>();
            builder.Use(new ImgExtension(env, imageConfig));
        }
        else
        {
            // Fallback for when DI is not available (e.g., tests)
            builder.Use<ImgExtension>();
        }

        builder.Use<LinkRewriteExtension>();

        // Allow additional configuration
        configure?.Invoke(builder);

        var pipeline = builder.Build();

        return pipeline;
    }
}