using Markdig;
using Mostlylucid.Services.Markdown.MarkDigExtensions;

namespace Mostlylucid.Services.Markdown;

public class MarkdownBaseService
{
    public const string EnglishLanguage = "en";

    protected MarkdownPipeline Pipeline() => Pipeline(null);

    protected MarkdownPipeline Pipeline(Action<MarkdownPipelineBuilder>? configure)
    {
        var builder = new MarkdownPipelineBuilder()
            .Use<FetchMarkdownExtension>()
            .UseAdvancedExtensions()
            .UseTableOfContent()

            .Use<ImgExtension>();

        // Allow additional configuration
        configure?.Invoke(builder);

        return builder.Build();
    }
}