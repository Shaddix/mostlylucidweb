using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using mostlylucid.llmslidetranslator.Models;

namespace mostlylucid.llmslidetranslator.Services;

/// <summary>
/// Chunks markdown documents into translatable blocks
/// </summary>
public class MarkdownChunker : IMarkdownChunker
{
    private readonly ILogger<MarkdownChunker> _logger;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownChunker(ILogger<MarkdownChunker> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public Task<List<TranslationBlock>> ChunkAsync(
        string markdown,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Chunking markdown document {DocumentId}", documentId);

        var document = Markdown.Parse(markdown, _pipeline);
        var blocks = new List<TranslationBlock>();
        var index = 0;

        foreach (var block in document)
        {
            var translationBlock = CreateTranslationBlock(
                block,
                documentId,
                sourceLanguage,
                targetLanguage,
                index);

            if (translationBlock != null)
            {
                blocks.Add(translationBlock);
                index++;
            }
        }

        _logger.LogInformation("Created {Count} translation blocks from document {DocumentId}",
            blocks.Count, documentId);

        return Task.FromResult(blocks);
    }

    private TranslationBlock? CreateTranslationBlock(
        Block block,
        string documentId,
        string sourceLanguage,
        string targetLanguage,
        int index)
    {
        var text = ExtractText(block);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var blockType = DetermineBlockType(block);
        var shouldTranslate = ShouldTranslateBlock(block, blockType);

        return new TranslationBlock
        {
            BlockId = $"{documentId}_{index}",
            Index = index,
            DocumentId = documentId,
            Text = text.Trim(),
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            BlockType = blockType,
            ShouldTranslate = shouldTranslate
        };
    }

    private string ExtractText(Block block)
    {
        return block switch
        {
            LeafBlock leafBlock => leafBlock.Lines.ToString(),
            ContainerBlock containerBlock => string.Join("\n",
                containerBlock.SelectMany(b => b is LeafBlock lb
                    ? new[] { lb.Lines.ToString() }
                    : Array.Empty<string>())),
            _ => string.Empty
        };
    }

    private string DetermineBlockType(Block block)
    {
        return block switch
        {
            HeadingBlock => "heading",
            FencedCodeBlock => "code",  // More specific type must come before CodeBlock
            CodeBlock => "code",
            QuoteBlock => "quote",
            ListBlock => "list",
            ParagraphBlock => "paragraph",
            _ => "other"
        };
    }

    private bool ShouldTranslateBlock(Block block, string blockType)
    {
        // Don't translate code blocks
        if (blockType == "code")
        {
            return false;
        }

        // Don't translate blocks that are primarily URLs or code
        if (block is ParagraphBlock paragraph)
        {
            var text = ExtractText(block);

            // Check if it's mostly a URL
            if (text.StartsWith("http://") || text.StartsWith("https://"))
            {
                return false;
            }

            // Check if it contains inline code
            var hasInlineCode = paragraph.Inline?.Any(inline => inline is CodeInline) ?? false;
            if (hasInlineCode && text.Length < 100)
            {
                // Short blocks with code snippets might be better left untranslated
                _logger.LogDebug("Skipping translation of short code-heavy block");
            }
        }

        return true;
    }
}
