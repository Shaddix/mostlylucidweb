using Markdig;
using Markdig.Renderers;
using Mostlylucid.Markdig.FetchExtension.Parsers;
using Mostlylucid.Markdig.FetchExtension.Renderers;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Extension to add Table of Contents support to Markdig
/// Usage: [TOC], [TOC:2-4], [TOC:2-4:my-class]
/// </summary>
public class TocExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        // Add the TOC block parser
        if (!pipeline.BlockParsers.Contains<TocBlockParser>())
        {
            // Insert before paragraph parser to ensure TOC is parsed first
            pipeline.BlockParsers.Insert(0, new TocBlockParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
        {
            if (!htmlRenderer.ObjectRenderers.Contains<TocRenderer>())
            {
                // Add the TOC renderer
                htmlRenderer.ObjectRenderers.Add(new TocRenderer());
            }
        }
    }
}

/// <summary>
/// Extension method to easily add TOC support to a pipeline
/// </summary>
public static class TocExtensionExtensions
{
    public static MarkdownPipelineBuilder UseToc(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.Add(new TocExtension());
        return pipeline;
    }
}
