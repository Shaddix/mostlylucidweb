using Markdig;
using Markdig.Renderers;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Markdig extension for fetching remote markdown content
/// Syntax: <fetch markdownurl="url" pollfrequency="12h"/>
///
/// This extension processes fetch tags BEFORE rendering by:
/// 1. Parsing the document normally (fetch tags become HTML blocks)
/// 2. After parsing, replacing fetch blocks with parsed fetched markdown
/// 3. Everything flows through the same pipeline once
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

    public void Setup(MarkdownPipelineBuilder pipelineBuilder)
    {
        // We need to preprocess markdown BEFORE parsing to inject fetched content
        // This is handled by the MarkdownRenderingService calling the preprocessor
        // No document processing needed here
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        // No custom renderers needed - everything is handled in document processing
    }
}