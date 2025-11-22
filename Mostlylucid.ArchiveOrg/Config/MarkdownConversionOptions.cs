namespace Mostlylucid.ArchiveOrg.Config;

public class MarkdownConversionOptions
{
    public const string SectionName = "MarkdownConversion";

    /// <summary>
    /// Input directory containing HTML files to convert
    /// </summary>
    public string InputDirectory { get; set; } = "./archive-output";

    /// <summary>
    /// Output directory for converted markdown files
    /// </summary>
    public string OutputDirectory { get; set; } = "./markdown-output";

    /// <summary>
    /// CSS selector for the main content area to extract
    /// e.g., "article", ".post-content", "#main-content"
    /// If empty, attempts to auto-detect main content
    /// </summary>
    public string ContentSelector { get; set; } = string.Empty;

    /// <summary>
    /// CSS selectors to remove from the content before conversion
    /// e.g., navigation, ads, footers, sidebars
    /// </summary>
    public List<string> RemoveSelectors { get; set; } =
    [
        "nav",
        "header",
        "footer",
        ".sidebar",
        ".advertisement",
        ".ads",
        ".comments",
        ".social-share",
        "script",
        "style",
        "noscript",
        "iframe"
    ];

    /// <summary>
    /// Whether to generate tags using LLM
    /// </summary>
    public bool GenerateTags { get; set; } = true;

    /// <summary>
    /// Whether to extract/infer publish date from content
    /// </summary>
    public bool ExtractDates { get; set; } = true;

    /// <summary>
    /// File extension pattern to process
    /// </summary>
    public string FilePattern { get; set; } = "*.html";

    /// <summary>
    /// Whether to preserve images (download and update paths)
    /// </summary>
    public bool PreserveImages { get; set; } = true;

    /// <summary>
    /// Directory for downloaded images (relative to markdown output)
    /// </summary>
    public string ImagesDirectory { get; set; } = "images";
}
