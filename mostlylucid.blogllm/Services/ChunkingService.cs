using Mostlylucid.BlogLLM.Models;
using System.Text;
using TiktokenSharp;

namespace Mostlylucid.BlogLLM.Services;

public class ChunkingService
{
    private readonly TikToken _tokenizer;
    private readonly int _maxChunkTokens;
    private readonly int _minChunkTokens;
    private readonly int _overlapTokens;

    public ChunkingService(string tokenizerPath, int maxChunkTokens = 512, int minChunkTokens = 100, int overlapTokens = 50)
    {
        // Load tokenizer - using TikToken for BGE models
        _tokenizer = TikToken.GetEncoding("cl100k_base");
        _maxChunkTokens = maxChunkTokens;
        _minChunkTokens = minChunkTokens;
        _overlapTokens = overlapTokens;
    }

    public List<ContentChunk> ChunkDocument(BlogDocument document)
    {
        var chunks = new List<ContentChunk>();
        int chunkIndex = 0;

        var headingStack = new Stack<string>();
        headingStack.Push(document.Title);

        foreach (var section in document.Sections)
        {
            UpdateHeadingStack(headingStack, section.Level, section.Heading);

            var sectionText = BuildSectionText(section);
            var tokenCount = CountTokens(sectionText);

            if (tokenCount <= _maxChunkTokens)
            {
                chunks.Add(CreateChunk(document, section, sectionText, headingStack, chunkIndex++));
            }
            else
            {
                var subChunks = SplitSection(document, section, headingStack, ref chunkIndex);
                chunks.AddRange(subChunks);
            }
        }

        return chunks;
    }

    private void UpdateHeadingStack(Stack<string> stack, int level, string heading)
    {
        while (stack.Count > level)
        {
            stack.Pop();
        }
        stack.Push(heading);
    }

    private string BuildSectionText(DocumentSection section)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {section.Heading}");
        sb.AppendLine();
        sb.AppendLine(section.Content);

        foreach (var code in section.CodeBlocks)
        {
            sb.AppendLine($"```{code.Language}");
            sb.AppendLine(code.Code);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private ContentChunk CreateChunk(
        BlogDocument document,
        DocumentSection section,
        string text,
        Stack<string> headingStack,
        int chunkIndex)
    {
        return new ContentChunk
        {
            DocumentSlug = document.Slug,
            DocumentTitle = document.Title,
            ChunkIndex = chunkIndex,
            Text = text.Trim(),
            Headings = headingStack.Reverse().ToArray(),
            SectionHeading = section.Heading,
            Categories = document.Categories,
            PublishedDate = document.PublishedDate,
            Language = document.Language,
            TokenCount = CountTokens(text)
        };
    }

    private List<ContentChunk> SplitSection(
        BlogDocument document,
        DocumentSection section,
        Stack<string> headingStack,
        ref int chunkIndex)
    {
        var chunks = new List<ContentChunk>();
        var paragraphs = section.Content.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        var currentText = new StringBuilder();
        var currentTokens = 0;
        string? previousText = null;

        foreach (var paragraph in paragraphs)
        {
            var paragraphTokens = CountTokens(paragraph);

            if (currentTokens + paragraphTokens > _maxChunkTokens && currentTokens > _minChunkTokens)
            {
                var chunkText = $"## {section.Heading}\n\n" + currentText.ToString();

                chunks.Add(new ContentChunk
                {
                    DocumentSlug = document.Slug,
                    DocumentTitle = document.Title,
                    ChunkIndex = chunkIndex++,
                    Text = chunkText.Trim(),
                    Headings = headingStack.Reverse().ToArray(),
                    SectionHeading = section.Heading,
                    Categories = document.Categories,
                    PublishedDate = document.PublishedDate,
                    Language = document.Language,
                    TokenCount = currentTokens
                });

                previousText = GetLastSentences(currentText.ToString(), _overlapTokens);
                currentText.Clear();
                if (!string.IsNullOrWhiteSpace(previousText))
                {
                    currentText.AppendLine(previousText);
                    currentTokens = CountTokens(previousText);
                }
                else
                {
                    currentTokens = 0;
                }
            }

            currentText.AppendLine(paragraph);
            currentText.AppendLine();
            currentTokens += paragraphTokens;
        }

        if (currentTokens > 0)
        {
            var chunkText = $"## {section.Heading}\n\n" + currentText.ToString();

            chunks.Add(new ContentChunk
            {
                DocumentSlug = document.Slug,
                DocumentTitle = document.Title,
                ChunkIndex = chunkIndex++,
                Text = chunkText.Trim(),
                Headings = headingStack.Reverse().ToArray(),
                SectionHeading = section.Heading,
                Categories = document.Categories,
                PublishedDate = document.PublishedDate,
                Language = document.Language,
                TokenCount = currentTokens
            });
        }

        return chunks;
    }

    private string GetLastSentences(string text, int maxTokens)
    {
        var sentences = text.Split('.', StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray();
        var overlap = new StringBuilder();
        int tokens = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = CountTokens(sentence);
            if (tokens + sentenceTokens > maxTokens) break;

            overlap.Insert(0, sentence + ".");
            tokens += sentenceTokens;
        }

        return overlap.ToString().Trim();
    }

    public int CountTokens(string text)
    {
        var encoded = _tokenizer.Encode(text);
        return encoded.Count;
    }
}
