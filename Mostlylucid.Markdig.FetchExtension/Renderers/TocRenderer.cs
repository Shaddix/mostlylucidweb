using System.Text;
using System.Text.RegularExpressions;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mostlylucid.Markdig.FetchExtension.Models;

namespace Mostlylucid.Markdig.FetchExtension.Renderers;

/// <summary>
/// HTML renderer for Table of Contents blocks
/// Generates nested ul/li structure with anchor links
/// </summary>
public partial class TocRenderer : HtmlObjectRenderer<TocBlock>
{
    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.IgnoreCase)]
    private static partial Regex InvalidCharsRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    protected override void Write(HtmlRenderer renderer, TocBlock obj)
    {
        Console.WriteLine("[TocRenderer] Write() called");
        Console.WriteLine($"[TocRenderer] CSS Class: '{obj.CssClass}'");

        // Get the document - walk up from the TOC block
        var document = GetDocument(obj);
        if (document == null)
        {
            Console.WriteLine("[TocRenderer] ERROR: Could not find document");
            return;
        }

        Console.WriteLine("[TocRenderer] Document found");

        // Find all headings in the document
        var headings = CollectHeadings(document, obj.MinLevel, obj.MaxLevel);

        Console.WriteLine($"[TocRenderer] Found {headings.Count} headings (levels {obj.MinLevel}-{obj.MaxLevel})");

        if (headings.Count == 0)
        {
            // No headings found, render nothing
            Console.WriteLine("[TocRenderer] No headings, rendering nothing");
            return;
        }

        // Use provided CSS class or default to "ml_toc"
        var cssClass = !string.IsNullOrEmpty(obj.CssClass) ? obj.CssClass : "ml_toc";

        Console.WriteLine($"[TocRenderer] Rendering TOC with class '{cssClass}'");

        // Start TOC container nav
        renderer.WriteLine($"<nav class=\"{cssClass}\" aria-label=\"Table of Contents\">");

        if (!string.IsNullOrEmpty(obj.Title))
        {
            renderer.WriteLine($"<div class=\"toc-title\">{EscapeHtml(obj.Title)}</div>");
        }

        // Build nested list
        BuildNestedList(renderer, headings, 0, obj.MinLevel);

        renderer.WriteLine("</nav>");

        Console.WriteLine("[TocRenderer] TOC rendering complete");
    }

    /// <summary>
    /// Get the document from a block
    /// </summary>
    private MarkdownDocument? GetDocument(TocBlock block)
    {
        var current = block.Parent;
        while (current != null)
        {
            if (current is MarkdownDocument doc)
            {
                return doc;
            }
            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Collect all headings from the document
    /// </summary>
    private List<HeadingInfo> CollectHeadings(MarkdownObject root, int minLevel, int maxLevel)
    {
        var headings = new List<HeadingInfo>();

        foreach (var descendant in root.Descendants())
        {
            if (descendant is HeadingBlock heading &&
                heading.Level >= minLevel &&
                heading.Level <= maxLevel)
            {
                var text = ExtractHeadingText(heading);
                var id = GenerateId(text);

                headings.Add(new HeadingInfo
                {
                    Level = heading.Level,
                    Text = text,
                    Id = id,
                    Heading = heading
                });

                // Ensure the heading has an ID for linking
                EnsureHeadingHasId(heading, id);
            }
        }

        return headings;
    }

    /// <summary>
    /// Extract plain text from a heading block
    /// </summary>
    private string ExtractHeadingText(HeadingBlock heading)
    {
        if (heading.Inline == null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in heading.Inline.Descendants())
        {
            if (inline is LiteralInline literal)
            {
                sb.Append(literal.Content.ToString());
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Generate a URL-safe ID from heading text
    /// </summary>
    private string GenerateId(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "heading";

        // Convert to lowercase
        var id = text.ToLowerInvariant();

        // Remove invalid characters
        id = InvalidCharsRegex().Replace(id, "");

        // Replace whitespace with hyphens
        id = WhitespaceRegex().Replace(id, "-");

        // Remove leading/trailing hyphens
        id = id.Trim('-');

        // Ensure it's not empty
        if (string.IsNullOrEmpty(id))
            id = "heading";

        return id;
    }

    /// <summary>
    /// Ensure the heading has an ID attribute for anchor linking
    /// </summary>
    private void EnsureHeadingHasId(HeadingBlock heading, string id)
    {
        // Check if heading already has an ID
        var attributes = heading.GetAttributes();
        if (attributes.Id == null)
        {
            attributes.Id = id;
        }
    }

    /// <summary>
    /// Build nested ul/li structure recursively
    /// </summary>
    private void BuildNestedList(HtmlRenderer renderer, List<HeadingInfo> headings, int startIndex, int currentLevel)
    {
        if (startIndex >= headings.Count)
            return;

        renderer.WriteLine("<ul>");

        for (int i = startIndex; i < headings.Count; i++)
        {
            var heading = headings[i];

            if (heading.Level < currentLevel)
            {
                // Parent level - stop here
                renderer.WriteLine("</ul>");
                return;
            }

            if (heading.Level == currentLevel)
            {
                // Same level - render item
                renderer.Write("<li>");
                renderer.Write($"<a href=\"#{heading.Id}\">{EscapeHtml(heading.Text)}</a>");

                // Check if next item is a child
                if (i + 1 < headings.Count && headings[i + 1].Level > currentLevel)
                {
                    // Render children
                    var childrenEnd = FindChildrenEnd(headings, i + 1, currentLevel);
                    BuildNestedList(renderer, headings, i + 1, currentLevel + 1);
                    i = childrenEnd - 1; // Skip processed children
                }

                renderer.WriteLine("</li>");
            }
            else if (heading.Level > currentLevel)
            {
                // Deeper level - should not happen in well-formed iteration
                continue;
            }
        }

        renderer.WriteLine("</ul>");
    }

    /// <summary>
    /// Find where children at the next level end
    /// </summary>
    private int FindChildrenEnd(List<HeadingInfo> headings, int startIndex, int parentLevel)
    {
        for (int i = startIndex; i < headings.Count; i++)
        {
            if (headings[i].Level <= parentLevel)
            {
                return i;
            }
        }
        return headings.Count;
    }

    /// <summary>
    /// Escape HTML special characters
    /// </summary>
    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Heading information for TOC generation
    /// </summary>
    private class HeadingInfo
    {
        public int Level { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public HeadingBlock Heading { get; set; } = null!;
    }
}
