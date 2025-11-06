using Markdig;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
///     Factory for creating markdown pipelines for fetched content
///     Ensures fetched content uses the same extensions as the parent document
/// </summary>
public static class FetchMarkdownPipelineFactory
{
    /// <summary>
    ///     Creates a pipeline for rendering fetched markdown
    ///     Should match the configuration of the main pipeline
    /// </summary>
    public static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseTableOfContent()
            .Build();
    }
}