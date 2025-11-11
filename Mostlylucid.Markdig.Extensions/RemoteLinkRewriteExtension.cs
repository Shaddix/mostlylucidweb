using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Mostlylucid.Markdig.Extensions;

/// <summary>
/// Markdig extension that rewrites relative links in fetched markdown to point back to the source
/// Handles GitHub raw -> display page transformations
/// </summary>
public class RemoteLinkRewriteExtension : IMarkdownExtension
{
    private readonly string? _sourceUrl;

    public RemoteLinkRewriteExtension(string? sourceUrl = null)
    {
        _sourceUrl = sourceUrl;
    }

    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!string.IsNullOrEmpty(_sourceUrl))
        {
            pipeline.DocumentProcessed += RewriteRemoteLinks;
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    private void RewriteRemoteLinks(MarkdownDocument document)
    {
        if (string.IsNullOrEmpty(_sourceUrl)) return;

        foreach (var link in document.Descendants<LinkInline>())
        {
            // Skip images (they'll be handled separately)
            if (link.IsImage) continue;

            var url = link.Url;
            if (string.IsNullOrEmpty(url)) continue;

            // Skip absolute URLs (already complete)
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("//", StringComparison.Ordinal))
                continue;

            // Skip anchor links and other special URLs
            if (url.StartsWith("#") || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Handle relative URLs
            var resolvedUrl = ResolveRelativeUrl(_sourceUrl, url);
            if (!string.IsNullOrEmpty(resolvedUrl))
            {
                link.Url = resolvedUrl;
            }
        }
    }

    private string? ResolveRelativeUrl(string sourceUrl, string relativeUrl)
    {
        try
        {
            // Special handling for GitHub raw URLs
            if (sourceUrl.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveGitHubUrl(sourceUrl, relativeUrl);
            }

            // For other sources, use standard URL resolution
            var baseUri = new Uri(sourceUrl);
            var resolvedUri = new Uri(baseUri, relativeUrl);
            return resolvedUri.ToString();
        }
        catch (Exception)
        {
            // If URL resolution fails, return the original
            return null;
        }
    }

    private string ResolveGitHubUrl(string sourceUrl, string relativeUrl)
    {
        try
        {
            // Parse GitHub raw URL
            // Format: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
            var uri = new Uri(sourceUrl);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length < 3)
                return new Uri(uri, relativeUrl).ToString();

            var owner = segments[0];
            var repo = segments[1];
            var branch = segments[2];
            var pathParts = segments.Skip(3).ToArray();

            // Get the directory of the current file
            var directory = pathParts.Length > 0
                ? string.Join("/", pathParts.Take(pathParts.Length - 1))
                : string.Empty;

            // Resolve the relative path
            var resolvedPath = ResolveRelativePath(directory, relativeUrl);

            // Convert to GitHub display URL
            // For .md files: https://github.com/{owner}/{repo}/blob/{branch}/{path}
            // For other files: https://github.com/{owner}/{repo}/blob/{branch}/{path}
            var displayUrl = $"https://github.com/{owner}/{repo}/blob/{branch}/{resolvedPath}";

            return displayUrl;
        }
        catch (Exception)
        {
            // Fallback to standard URL resolution
            var baseUri = new Uri(sourceUrl);
            var resolvedUri = new Uri(baseUri, relativeUrl);
            return resolvedUri.ToString();
        }
    }

    private string ResolveRelativePath(string basePath, string relativePath)
    {
        // Split paths into segments
        var baseSegments = string.IsNullOrEmpty(basePath)
            ? new List<string>()
            : basePath.Split('/').ToList();

        var relativeSegments = relativePath.Split('/');

        foreach (var segment in relativeSegments)
        {
            if (segment == "." || string.IsNullOrEmpty(segment))
            {
                // Current directory, skip
                continue;
            }
            else if (segment == "..")
            {
                // Parent directory, pop last segment
                if (baseSegments.Count > 0)
                    baseSegments.RemoveAt(baseSegments.Count - 1);
            }
            else
            {
                // Add segment
                baseSegments.Add(segment);
            }
        }

        return string.Join("/", baseSegments);
    }
}
