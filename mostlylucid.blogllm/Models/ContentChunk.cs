namespace Mostlylucid.BlogLLM.Models;

public class ContentChunk
{
    public string ChunkId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentSlug { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }

    // Content
    public string Text { get; set; } = string.Empty;
    public string[] Headings { get; set; } = Array.Empty<string>();
    public string SectionHeading { get; set; } = string.Empty;

    // Metadata
    public string[] Categories { get; set; } = Array.Empty<string>();
    public DateTime PublishedDate { get; set; }
    public string Language { get; set; } = "en";
    public int TokenCount { get; set; }

    // For vector search
    public float[]? Embedding { get; set; }
}

public class SearchResult
{
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentSlug { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string SectionHeading { get; set; } = string.Empty;
    public float Score { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
}
