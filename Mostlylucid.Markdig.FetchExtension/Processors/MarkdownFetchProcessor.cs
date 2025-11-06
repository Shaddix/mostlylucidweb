using Markdig;

namespace Mostlylucid.Markdig.FetchExtension.Processors;

using Mostlylucid.Markdig.FetchExtension.Models;
using Mostlylucid.Markdig.FetchExtension.Services;
using Mostlylucid.Markdig.FetchExtension.Utilities;

/// <summary>
/// Standalone processor for markdown with fetch tags.
/// Provides simple methods to process fetch tags and optionally render to HTML.
/// </summary>
public static class MarkdownFetchProcessor
{
    /// <summary>
    /// Processes fetch tags in markdown and returns the markdown with fetched content inserted.
    /// Use this when you want to process fetch tags but keep the result as markdown.
    /// </summary>
    /// <param name="markdown">Markdown text containing fetch tags</param>
    /// <param name="serviceProvider">Service provider with IMarkdownFetchService registered</param>
    /// <returns>Processed markdown with fetch tags replaced by fetched content</returns>
    /// <example>
    /// var markdown = "# My Doc\n&lt;fetch markdownurl=\"https://example.com/README.md\" pollfrequency=\"24\"/&gt;";
    /// var processed = MarkdownFetchProcessor.ProcessFetchTags(markdown, serviceProvider);
    /// // processed now contains the fetched content instead of the fetch tag
    /// </example>
    public static string ProcessFetchTags(string markdown, IServiceProvider serviceProvider)
    {
        var preprocessor = new MarkdownFetchPreprocessor(serviceProvider);
        return preprocessor.Preprocess(markdown);
    }

    /// <summary>
    /// Processes fetch tags and renders to HTML in one step.
    /// Use this when you want to go directly from markdown with fetch tags to final HTML.
    /// </summary>
    /// <param name="markdown">Markdown text containing fetch tags</param>
    /// <param name="serviceProvider">Service provider with IMarkdownFetchService registered</param>
    /// <param name="pipeline">Optional Markdig pipeline. If null, uses default with advanced extensions.</param>
    /// <returns>Rendered HTML with all fetch tags processed</returns>
    /// <example>
    /// var markdown = "# My Doc\n&lt;fetch markdownurl=\"https://example.com/README.md\" pollfrequency=\"24\"/&gt;";
    /// var html = MarkdownFetchProcessor.ProcessAndRender(markdown, serviceProvider);
    /// // html contains the fully rendered output
    ///
    /// // Or with custom pipeline:
    /// var pipeline = new MarkdownPipelineBuilder()
    ///     .UseAdvancedExtensions()
    ///     .UsePipeTables()
    ///     .Build();
    /// var html = MarkdownFetchProcessor.ProcessAndRender(markdown, serviceProvider, pipeline);
    /// </example>
    public static string ProcessAndRender(
        string markdown,
        IServiceProvider serviceProvider,
        MarkdownPipeline? pipeline = null)
    {
        var preprocessor = new MarkdownFetchPreprocessor(serviceProvider);
        var processedMarkdown = preprocessor.Preprocess(markdown);

        var pipelineToUse = pipeline ?? new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        return Markdown.ToHtml(processedMarkdown, pipelineToUse);
    }

    /// <summary>
    /// Processes fetch tags asynchronously for better performance with multiple fetches.
    /// Note: The actual processing is still synchronous, but this allows for async/await patterns.
    /// </summary>
    /// <param name="markdown">Markdown text containing fetch tags</param>
    /// <param name="serviceProvider">Service provider with IMarkdownFetchService registered</param>
    /// <returns>Processed markdown with fetch tags replaced by fetched content</returns>
    public static Task<string> ProcessFetchTagsAsync(string markdown, IServiceProvider serviceProvider)
    {
        return Task.Run(() => ProcessFetchTags(markdown, serviceProvider));
    }

    /// <summary>
    /// Processes fetch tags and renders to HTML asynchronously.
    /// </summary>
    /// <param name="markdown">Markdown text containing fetch tags</param>
    /// <param name="serviceProvider">Service provider with IMarkdownFetchService registered</param>
    /// <param name="pipeline">Optional Markdig pipeline. If null, uses default with advanced extensions.</param>
    /// <returns>Rendered HTML with all fetch tags processed</returns>
    public static Task<string> ProcessAndRenderAsync(
        string markdown,
        IServiceProvider serviceProvider,
        MarkdownPipeline? pipeline = null)
    {
        return Task.Run(() => ProcessAndRender(markdown, serviceProvider, pipeline));
    }
}
