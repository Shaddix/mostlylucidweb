using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Preprocesses markdown text to fetch remote content before parsing
/// This ensures everything goes through the same pipeline once
/// </summary>
public class MarkdownFetchPreprocessor
{
    private static readonly Regex FetchTagRegex = new(
        @"<fetch\s+[^>]*?markdownurl\s*=\s*[""']([^""']+)[""'][^>]*?pollfrequency\s*=\s*[""'](\d+)h?[""'](?:[^>]*?transformlinks\s*=\s*[""'](true|false)[""'])?[^>]*?/\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<MarkdownFetchPreprocessor>? _logger;

    public MarkdownFetchPreprocessor(IServiceProvider? serviceProvider, ILogger<MarkdownFetchPreprocessor>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Preprocesses markdown text by fetching remote content and replacing fetch tags
    /// </summary>
    public string Preprocess(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return markdown;

        // Find all fetch tags
        var matches = FetchTagRegex.Matches(markdown);
        if (matches.Count == 0)
            return markdown;

        var result = markdown;

        // Process each fetch tag in reverse order to preserve string positions
        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var url = match.Groups[1].Value;
            var pollFrequencyHours = int.Parse(match.Groups[2].Value);
            var transformLinks = match.Groups.Count > 3 &&
                                match.Groups[3].Success &&
                                match.Groups[3].Value.Equals("true", StringComparison.OrdinalIgnoreCase);

            string replacement = string.Empty;

            if (_serviceProvider != null)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var fetchService = scope.ServiceProvider.GetRequiredService<IMarkdownFetchService>();

                    var fetchResult = fetchService.FetchMarkdownAsync(url, pollFrequencyHours, blogPostId: 0)
                        .GetAwaiter()
                        .GetResult();

                    if (fetchResult.Success)
                    {
                        replacement = fetchResult.Content;

                        // Transform relative links to absolute URLs pointing back to source
                        if (transformLinks)
                        {
                            replacement = MarkdownLinkRewriter.RewriteLinks(replacement, url);
                            _logger?.LogDebug("Transformed links in fetched markdown from {Url}", url);
                        }

                        _logger?.LogInformation("Successfully fetched markdown from {Url}", url);
                    }
                    else
                    {
                        _logger?.LogWarning("Failed to fetch markdown from {Url}: {Error}", url, fetchResult.ErrorMessage);
                        replacement = $"<!-- Failed to fetch content from {url}: {fetchResult.ErrorMessage} -->";
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error fetching markdown from {Url}", url);
                    replacement = $"<!-- Error fetching content from {url}: {ex.Message} -->";
                }
            }
            else
            {
                replacement = "<!-- Markdown fetch service not configured -->";
            }

            // Replace the fetch tag with the fetched content
            result = result.Substring(0, match.Index) + replacement + result.Substring(match.Index + match.Length);
        }

        return result;
    }
}
