using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using Mostlylucid.BlogLLM.Models;
using Mostlylucid.Markdig.FetchExtension.Processors;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mostlylucid.BlogLLM.Services;

public class MarkdownParserService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<MarkdownParserService>? _logger;

    public MarkdownParserService(IServiceProvider? serviceProvider = null, ILogger<MarkdownParserService>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public BlogDocument ParseFile(string filePath)
    {
        var markdown = File.ReadAllText(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Preprocess markdown to fetch remote content if there are <fetch> tags
        if (_serviceProvider != null)
        {
            var preprocessor = new MarkdownFetchPreprocessor(_serviceProvider, _logger);
            markdown = preprocessor.Preprocess(markdown);
            _logger?.LogInformation("Preprocessed markdown file {FilePath} for fetch tags", filePath);
        }

        var document = Markdown.Parse(markdown, _pipeline);

        return new BlogDocument
        {
            FilePath = filePath,
            Slug = ExtractSlug(fileName),
            Title = ExtractTitle(document),
            Categories = ExtractCategories(markdown),
            PublishedDate = ExtractPublishedDate(markdown),
            Language = ExtractLanguage(fileName),
            MarkdownContent = markdown,
            PlainTextContent = ConvertToPlainText(document),
            ContentHash = ComputeHash(markdown),
            Sections = ExtractSections(document, markdown),
            WordCount = CountWords(ConvertToPlainText(document))
        };
    }

    private string ExtractSlug(string fileName)
    {
        // Handle translated files: "slug.es.md" -> "slug"
        var parts = fileName.Split('.');
        return parts[0];
    }

    private string ExtractTitle(MarkdownDocument document)
    {
        var heading = document.Descendants<HeadingBlock>()
            .FirstOrDefault(h => h.Level == 1);

        if (heading?.Inline?.FirstChild != null)
        {
            return ExtractInlineText(heading.Inline);
        }

        return "Untitled";
    }

    private string[] ExtractCategories(string markdown)
    {
        var match = Regex.Match(markdown,
            @"<!--\s*category--\s*(.+?)\s*-->",
            RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups[1].Value
                .Split(',')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private DateTime ExtractPublishedDate(string markdown)
    {
        var match = Regex.Match(markdown,
            @"<datetime[^>]*>(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})</datetime>",
            RegexOptions.IgnoreCase);

        if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
        {
            return date;
        }

        return DateTime.Now;
    }

    private string ExtractLanguage(string fileName)
    {
        var parts = fileName.Split('.');
        if (parts.Length == 3 && parts[1].Length == 2)
        {
            return parts[1];
        }
        return "en";
    }

    private string ConvertToPlainText(MarkdownDocument document)
    {
        var sb = new StringBuilder();

        foreach (var block in document)
        {
            if (block is ParagraphBlock paragraph)
            {
                sb.AppendLine(ExtractInlineText(paragraph.Inline!));
            }
            else if (block is HeadingBlock heading)
            {
                sb.AppendLine(ExtractInlineText(heading.Inline!));
            }
        }

        return sb.ToString();
    }

    private string ExtractInlineText(ContainerInline inline)
    {
        var sb = new StringBuilder();
        foreach (var child in inline)
        {
            if (child is LiteralInline literal)
            {
                sb.Append(literal.Content.ToString());
            }
            else if (child is CodeInline code)
            {
                sb.Append(code.Content);
            }
        }
        return sb.ToString();
    }

    private List<DocumentSection> ExtractSections(MarkdownDocument document, string markdown)
    {
        var sections = new List<DocumentSection>();
        DocumentSection? currentSection = null;
        var lines = markdown.Split('\n');

        int position = 0;
        foreach (var block in document)
        {
            if (block is HeadingBlock heading && heading.Level <= 3)
            {
                if (currentSection != null)
                {
                    currentSection.EndPosition = position;
                    sections.Add(currentSection);
                }

                currentSection = new DocumentSection
                {
                    Heading = ExtractInlineText(heading.Inline!),
                    Level = heading.Level,
                    StartPosition = position,
                    Content = string.Empty
                };
            }
            else if (currentSection != null)
            {
                if (block is FencedCodeBlock codeBlock)
                {
                    currentSection.CodeBlocks.Add(new CodeBlock
                    {
                        Language = codeBlock.Info ?? "text",
                        Code = ExtractCodeContent(codeBlock, lines),
                        LineNumber = codeBlock.Line
                    });
                }
                else if (block is ParagraphBlock paragraph)
                {
                    currentSection.Content += ExtractInlineText(paragraph.Inline!) + "\n\n";
                }
            }

            position += block.ToString()?.Length ?? 0;
        }

        if (currentSection != null)
        {
            currentSection.EndPosition = position;
            sections.Add(currentSection);
        }

        return sections;
    }

    private string ExtractCodeContent(FencedCodeBlock codeBlock, string[] lines)
    {
        var startLine = codeBlock.Line + 1;
        return string.Join("\n", lines.Skip(startLine).Take(codeBlock.Lines.Count));
    }

    private int CountWords(string text)
    {
        return text.Split(new[] { ' ', '\n', '\r', '\t' },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(bytes);
    }
}
