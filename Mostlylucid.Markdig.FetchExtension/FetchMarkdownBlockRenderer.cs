using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Mostlylucid.Markdig.FetchExtension;

/// <summary>
/// Renderer for block-level fetch element
/// </summary>
public class FetchMarkdownBlockRenderer : HtmlObjectRenderer<FetchMarkdownBlock>
{
    protected override void Write(HtmlRenderer renderer, FetchMarkdownBlock obj)
    {
        if (obj.FetchSuccessful && !string.IsNullOrWhiteSpace(obj.FetchedContent))
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            var html = global::Markdig.Markdown.ToHtml(obj.FetchedContent, pipeline);
            renderer.Writer.Write(html);
        }
        else
        {
            renderer.Write(obj.FetchedContent);
        }
    }
}
