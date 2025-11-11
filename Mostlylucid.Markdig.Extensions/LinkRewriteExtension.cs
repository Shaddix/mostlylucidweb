using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Mostlylucid.Markdig.Extensions;

/// <summary>
/// Markdig extension that rewrites markdown links (.md files) to proper blog URLs
/// Converts TITLE_NAME.md -> /blog/title-name
/// </summary>
public class LinkRewriteExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.DocumentProcessed += RewriteLinks;
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }

    private void RewriteLinks(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            // Skip images and external links
            if (link.IsImage) continue;

            var url = link.Url;
            if (string.IsNullOrEmpty(url)) continue;

            // Skip absolute URLs
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("//", StringComparison.Ordinal))
                continue;

            // Skip anchor links and other special URLs
            if (url.StartsWith("#") || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Process .md file links
            if (url.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                // Remove .md extension
                var path = url[..^3];

                // Split path into parts
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // Process only the last part (the slug)
                if (parts.Length > 0)
                {
                    var slug = parts[^1];

                    // Normalize slug: convert underscores/spaces to hyphens and lowercase
                    slug = slug.Replace('_', '-')
                              .Replace(' ', '-')
                              .ToLowerInvariant();

                    // Replace last part with normalized slug
                    parts[^1] = slug;

                    // Rebuild path
                    var normalizedPath = string.Join("/", parts);

                    // Ensure it starts with /blog/ if it doesn't already have a path prefix
                    if (!normalizedPath.StartsWith("blog/", StringComparison.OrdinalIgnoreCase))
                    {
                        link.Url = $"/blog/{normalizedPath}";
                    }
                    else
                    {
                        link.Url = $"/{normalizedPath}";
                    }
                }
            }
        }
    }
}
