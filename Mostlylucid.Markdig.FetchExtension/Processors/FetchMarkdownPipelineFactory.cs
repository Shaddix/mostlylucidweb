using Markdig;

namespace Mostlylucid.Markdig.FetchExtension.Processors;

using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;
using Mostlylucid.Markdig.FetchExtension.Utilities;

/// <summary>
///     Factory for creating markdown pipelines for fetched content
///     Ensures fetched content uses the same extensions as the parent document
/// </summary>
public static class FetchMarkdownPipelineFactory
{
    /// <summary>
    ///     Creates a pipeline for rendering fetched markdown
    ///     Should match the configuration of the main pipeline
    ///     NOTE: Add your own extensions as needed (e.g., ToC, custom parsers, etc.)
    /// </summary>
    public static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }
}