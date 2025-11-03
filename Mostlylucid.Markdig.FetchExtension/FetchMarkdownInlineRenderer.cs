using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Renderer for the fetch markdown inline element
/// </summary>
public class FetchMarkdownInlineRenderer : HtmlObjectRenderer<FetchMarkdownInline>
{
    protected override void Write(HtmlRenderer renderer, FetchMarkdownInline obj)
    {
        if (obj.FetchSuccessful && !string.IsNullOrWhiteSpace(obj.FetchedContent))
        {
            // Parse the fetched markdown content and convert to HTML
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            var html = global::Markdig.Markdown.ToHtml(obj.FetchedContent, pipeline);

            // Write the HTML directly to the writer (not escaped)
            renderer.Writer.Write(html);
        }
        else
        {
            // Render comment or fallback (already HTML comment)
            renderer.Write(obj.FetchedContent);
        }
    }
}