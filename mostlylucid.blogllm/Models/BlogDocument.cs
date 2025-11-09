namespace Mostlylucid.BlogLLM.Models;

public class BlogDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string[] Categories { get; set; } = Array.Empty<string>();
    public DateTime PublishedDate { get; set; }
    public string Language { get; set; } = "en";
    public string MarkdownContent { get; set; } = string.Empty;
    public string PlainTextContent { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public List<DocumentSection> Sections { get; set; } = new();
}

public class DocumentSection
{
    public string Heading { get; set; } = string.Empty;
    public int Level { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<CodeBlock> CodeBlocks { get; set; } = new();
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
}

public class CodeBlock
{
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}
