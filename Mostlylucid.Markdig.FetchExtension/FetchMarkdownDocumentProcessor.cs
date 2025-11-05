using System.Text.RegularExpressions;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Document processor that replaces fetch tags with fetched markdown content
/// This runs after initial parsing but before rendering, allowing fetched content
/// to flow through the same pipeline
/// </summary>
public class FetchMarkdownDocumentProcessor
{
    private static readonly Regex FetchTagRegex = new(
        @"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'](?:[^>]*?transformlinks\s*=\s*[""'](true|false)[""'])?[^>]*?/\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IServiceProvider? _serviceProvider;
    private readonly MarkdownPipeline _pipeline;

    public FetchMarkdownDocumentProcessor(IServiceProvider? serviceProvider, MarkdownPipeline pipeline)
    {
        _serviceProvider = serviceProvider;
        _pipeline = pipeline;
    }

    /// <summary>
    /// Processes the document to replace fetch tags with fetched content
    /// </summary>
    public void ProcessDocument(MarkdownDocument document)
    {
        if (_serviceProvider == null)
            return;

        // Find all HTML blocks that might contain fetch tags
        var blocksToReplace = new List<(Block original, List<Block> replacements)>();

        foreach (var block in document.Descendants<HtmlBlock>())
        {
            var html = block.Lines.ToString();
            var match = FetchTagRegex.Match(html);

            if (match.Success)
            {
                var url = match.Groups[1].Value;
                var pollFrequencyHours = int.Parse(match.Groups[2].Value);
                var transformLinks = match.Groups.Count > 3 &&
                                    match.Groups[3].Success &&
                                    match.Groups[3].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

                string fetchedMarkdown = FetchMarkdown(url, pollFrequencyHours, transformLinks);

                if (!string.IsNullOrWhiteSpace(fetchedMarkdown))
                {
                    // Parse the fetched markdown with the same pipeline
                    var fetchedDocument = Markdown.Parse(fetchedMarkdown, _pipeline);

                    // Collect all blocks from the fetched document
                    var replacementBlocks = new List<Block>();
                    foreach (var fetchedBlock in fetchedDocument)
                    {
                        replacementBlocks.Add(fetchedBlock);
                    }

                    // Detach blocks from fetched document
                    foreach (var fetchedBlock in replacementBlocks)
                    {
                        fetchedDocument.Remove(fetchedBlock);
                    }

                    blocksToReplace.Add((block, replacementBlocks));
                }
            }
        }

        // Now replace the blocks
        foreach (var (original, replacements) in blocksToReplace)
        {
            var parent = original.Parent;
            var index = parent?.IndexOf(original) ?? -1;

            if (parent != null && index >= 0)
            {
                // Remove the original fetch block
                parent.Remove(original);

                // Insert all replacement blocks at the same position
                for (int i = 0; i < replacements.Count; i++)
                {
                    parent.Insert(index + i, replacements[i]);
                }
            }
        }
    }

    private string FetchMarkdown(string url, int pollFrequencyHours, bool transformLinks)
    {
        try
        {
            using var scope = _serviceProvider!.CreateScope();
            var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();
            var logger = scope.ServiceProvider.GetService<ILogger<FetchMarkdownDocumentProcessor>>();

            var result = fetchService.FetchMarkdownAsync(url, pollFrequencyHours, blogPostId: 0)
                .GetAwaiter()
                .GetResult();

            if (result.Success)
            {
                var content = result.Content;

                if (transformLinks)
                {
                    content = MarkdownLinkRewriter.RewriteLinks(content, url);
                    logger?.LogDebug("Transformed links in fetched markdown from {Url}", url);
                }

                logger?.LogInformation("Successfully fetched markdown from {Url}", url);
                return content;
            }
            else
            {
                logger?.LogWarning("Failed to fetch markdown from {Url}: {Error}", url, result.ErrorMessage);
                return $"<!-- Failed to fetch content from {url}: {result.ErrorMessage} -->";
            }
        }
        catch (Exception ex)
        {
            return $"<!-- Error fetching content from {url}: {ex.Message} -->";
        }
    }
}
