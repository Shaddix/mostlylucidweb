using Markdig;
using Markdig.Parsers;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Services.Markdown.MarkDigExtensions;

namespace Mostlylucid.Services.Markdown;

public class MarkdownBaseService
{
    public const string EnglishLanguage = "en";

    protected MarkdownPipeline Pipeline() => Pipeline(null);

    protected MarkdownPipeline Pipeline(Action<MarkdownPipelineBuilder>? configure)
    {
        var builder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Use<Mostlylucid.Markdig.FetchExtension.FetchMarkdownExtension>()
            .UseToc() // Custom TOC extension - supports [TOC] markers
            .Use<ImgExtension>()
            .Use<LinkRewriteExtension>();


        // Allow additional configuration
        configure?.Invoke(builder);

        var pipeline = builder.Build();

        return pipeline;
    }
}