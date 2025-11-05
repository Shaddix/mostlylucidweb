using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Mostlylucid.Services.Markdown.MarkDigExtensions;

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
                var slug = url[..^3];

                // Remove any leading path separators
                slug = slug.TrimStart('/', '\\');

                // Normalize slug: convert underscores/spaces to hyphens and lowercase
                slug = slug.Replace('_', '-')
                          .Replace(' ', '-')
                          .ToLowerInvariant();

                // Ensure it starts with /blog/
                if (!slug.StartsWith("blog/", StringComparison.OrdinalIgnoreCase))
                {
                    link.Url = $"/blog/{slug}";
                }
                else
                {
                    link.Url = $"/{slug}";
                }
            }
        }
    }
}
