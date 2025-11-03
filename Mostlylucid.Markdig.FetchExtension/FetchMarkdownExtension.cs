using Markdig;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Parsers;
using Markdig.Renderers.Html;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Markdig extension for fetching remote markdown content
/// Syntax: <fetch markdownurl="url" pollfrequency="12h"/>
/// </summary>
public class FetchMarkdownExtension : IMarkdownExtension
{
    // Static service provider for dependency resolution
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Configure the service provider for the extension
    /// Call this during application startup
    /// </summary>
    public static void ConfigureServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        // Insert our block parser before default HTML block parser
        if (!pipeline.BlockParsers.Contains<FetchMarkdownBlockParser>())
        {
            pipeline.BlockParsers.Insert(0, new FetchMarkdownBlockParser(_serviceProvider));
        }

        // Insert our inline parser before HTML inline parser
        if (!pipeline.InlineParsers.Contains<FetchMarkdownInlineParser>())
        {
            pipeline.InlineParsers.Insert(0, new FetchMarkdownInlineParser(_serviceProvider));
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
        {
            // Register block renderer
            if (!htmlRenderer.ObjectRenderers.Contains<FetchMarkdownBlockRenderer>())
            {
                htmlRenderer.ObjectRenderers.InsertBefore<CodeBlockRenderer>(new FetchMarkdownBlockRenderer());
            }

            // Register inline renderer
            if (!htmlRenderer.ObjectRenderers.Contains<FetchMarkdownInlineRenderer>())
            {
                htmlRenderer.ObjectRenderers.InsertBefore<LinkInlineRenderer>(new FetchMarkdownInlineRenderer());
            }
        }
    }
}