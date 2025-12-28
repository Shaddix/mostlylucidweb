using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Mostlylucid.DocSummarizer.Models;

/// <summary>
/// Document metadata extracted from filename, embedded PDF metadata, or external APIs.
/// Provides a "sanity banner" to confirm you're working with the right document.
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Source file path
    /// </summary>
    public string FilePath { get; init; } = "";

    /// <summary>
    /// Document title (from PDF metadata or extracted)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Author(s) of the document
    /// </summary>
    public string? Authors { get; set; }

    /// <summary>
    /// Publication or creation date
    /// </summary>
    public DateTimeOffset? Date { get; set; }

    /// <summary>
    /// Abstract or description
    /// </summary>
    public string? Abstract { get; set; }

    /// <summary>
    /// External identifier (arXiv ID, DOI, etc.)
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Type of external identifier (arXiv, DOI, ISBN, etc.)
    /// </summary>
    public ExternalIdType ExternalIdType { get; set; } = ExternalIdType.None;

    /// <summary>
    /// URL to the source (e.g., arXiv page)
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Keywords or categories
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Page count if available
    /// </summary>
    public int? PageCount { get; set; }

    /// <summary>
    /// Source of the metadata (Filename, PDF, ArXiv, etc.)
    /// </summary>
    public MetadataSource Source { get; set; } = MetadataSource.Unknown;

    /// <summary>
    /// Whether we successfully fetched external metadata
    /// </summary>
    public bool HasExternalMetadata => ExternalIdType != ExternalIdType.None && !string.IsNullOrEmpty(Title);

    /// <summary>
    /// Format a short sanity banner for display
    /// </summary>
    public string ToSanityBanner()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(Title))
            parts.Add($"Title: {Title}");

        if (!string.IsNullOrEmpty(Authors))
            parts.Add($"Authors: {Authors}");

        if (Date.HasValue)
            parts.Add($"Year: {Date.Value.Year}");

        if (ExternalIdType != ExternalIdType.None && !string.IsNullOrEmpty(ExternalId))
            parts.Add($"{ExternalIdType}: {ExternalId}");

        if (PageCount.HasValue)
            parts.Add($"Pages: {PageCount}");

        return parts.Count > 0
            ? string.Join(" | ", parts)
            : $"File: {Path.GetFileName(FilePath)}";
    }

    /// <summary>
    /// Format as markdown for inclusion in output
    /// </summary>
    public string ToMarkdown()
    {
        var lines = new List<string>();
        lines.Add("## Document Information");
        lines.Add("");

        if (!string.IsNullOrEmpty(Title))
            lines.Add($"**Title:** {Title}");

        if (!string.IsNullOrEmpty(Authors))
            lines.Add($"**Authors:** {Authors}");

        if (Date.HasValue)
            lines.Add($"**Date:** {Date.Value:yyyy-MM-dd}");

        if (ExternalIdType != ExternalIdType.None && !string.IsNullOrEmpty(ExternalId))
        {
            var url = ExternalIdType == ExternalIdType.ArXiv
                ? $"https://arxiv.org/abs/{ExternalId}"
                : SourceUrl;
            lines.Add(url != null
                ? $"**{ExternalIdType}:** [{ExternalId}]({url})"
                : $"**{ExternalIdType}:** {ExternalId}");
        }

        if (!string.IsNullOrEmpty(Abstract))
        {
            lines.Add("");
            lines.Add($"**Abstract:** {Abstract}");
        }

        if (Categories.Count > 0)
            lines.Add($"**Categories:** {string.Join(", ", Categories)}");

        return string.Join("\n", lines);
    }
}

public enum ExternalIdType
{
    None,
    ArXiv,
    DOI,
    ISBN,
    PubMed
}

public enum MetadataSource
{
    Unknown,
    Filename,
    PdfMetadata,
    ArXivApi,
    CrossRef,
    Manual
}

/// <summary>
/// Extracts document metadata from various sources
/// </summary>
public static class MetadataExtractor
{
    // arXiv ID pattern: YYMM.NNNNN or old format like hep-ph/9901234
    private static readonly Regex ArxivNewFormat = new(@"(\d{4}\.\d{4,5})(v\d+)?", RegexOptions.Compiled);
    private static readonly Regex ArxivOldFormat = new(@"([a-z-]+/\d{7})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // DOI pattern
    private static readonly Regex DoiPattern = new(@"(10\.\d{4,}/[^\s]+)", RegexOptions.Compiled);

    /// <summary>
    /// Extract metadata from a file, optionally fetching from external APIs
    /// </summary>
    public static async Task<DocumentMetadata> ExtractAsync(
        string filePath,
        bool fetchExternal = true,
        CancellationToken ct = default)
    {
        var metadata = new DocumentMetadata { FilePath = filePath };
        var filename = Path.GetFileNameWithoutExtension(filePath);

        // Try to detect external ID from filename
        DetectExternalId(filename, metadata);

        // Extract PDF metadata if applicable
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".pdf" && File.Exists(filePath))
        {
            ExtractPdfMetadata(filePath, metadata);
        }

        // Fetch external metadata if we have an ID
        if (fetchExternal && metadata.ExternalIdType == ExternalIdType.ArXiv)
        {
            await FetchArxivMetadataAsync(metadata, ct);
        }

        return metadata;
    }

    /// <summary>
    /// Quick extraction without external API calls
    /// </summary>
    public static DocumentMetadata ExtractQuick(string filePath)
    {
        var metadata = new DocumentMetadata { FilePath = filePath };
        var filename = Path.GetFileNameWithoutExtension(filePath);

        DetectExternalId(filename, metadata);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".pdf" && File.Exists(filePath))
        {
            ExtractPdfMetadata(filePath, metadata);
        }

        return metadata;
    }

    private static void DetectExternalId(string filename, DocumentMetadata metadata)
    {
        // Check for arXiv ID
        var arxivMatch = ArxivNewFormat.Match(filename);
        if (arxivMatch.Success)
        {
            metadata.ExternalId = arxivMatch.Groups[1].Value;
            metadata.ExternalIdType = ExternalIdType.ArXiv;
            metadata.SourceUrl = $"https://arxiv.org/abs/{metadata.ExternalId}";
            metadata.Source = MetadataSource.Filename;
            return;
        }

        arxivMatch = ArxivOldFormat.Match(filename);
        if (arxivMatch.Success)
        {
            metadata.ExternalId = arxivMatch.Groups[1].Value;
            metadata.ExternalIdType = ExternalIdType.ArXiv;
            metadata.SourceUrl = $"https://arxiv.org/abs/{metadata.ExternalId}";
            metadata.Source = MetadataSource.Filename;
            return;
        }

        // Check for DOI
        var doiMatch = DoiPattern.Match(filename);
        if (doiMatch.Success)
        {
            metadata.ExternalId = doiMatch.Groups[1].Value;
            metadata.ExternalIdType = ExternalIdType.DOI;
            metadata.SourceUrl = $"https://doi.org/{metadata.ExternalId}";
            metadata.Source = MetadataSource.Filename;
        }
    }

    private static void ExtractPdfMetadata(string filePath, DocumentMetadata metadata)
    {
        try
        {
            using var document = PdfDocument.Open(filePath);
            var info = document.Information;

            if (!string.IsNullOrEmpty(info.Title))
                metadata.Title ??= info.Title;

            if (!string.IsNullOrEmpty(info.Author))
                metadata.Authors ??= info.Author;

            // Parse creation date (PdfPig returns string, not DateTimeOffset)
            if (!string.IsNullOrEmpty(info.CreationDate))
            {
                if (DateTimeOffset.TryParse(info.CreationDate, out var creationDate))
                    metadata.Date ??= creationDate;
            }

            metadata.PageCount = document.NumberOfPages;

            if (metadata.Source == MetadataSource.Unknown)
                metadata.Source = MetadataSource.PdfMetadata;
        }
        catch
        {
            // PDF metadata extraction failed - continue without it
        }
    }

    private static async Task FetchArxivMetadataAsync(DocumentMetadata metadata, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(metadata.ExternalId))
            return;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"http://export.arxiv.org/api/query?id_list={metadata.ExternalId}";

            var response = await http.GetStringAsync(url, ct);
            var doc = XDocument.Parse(response);

            XNamespace atom = "http://www.w3.org/2005/Atom";
            XNamespace arxiv = "http://arxiv.org/schemas/atom";

            var entry = doc.Descendants(atom + "entry").FirstOrDefault();
            if (entry == null) return;

            // Title
            var title = entry.Element(atom + "title")?.Value;
            if (!string.IsNullOrEmpty(title))
            {
                // Clean up whitespace
                metadata.Title = Regex.Replace(title, @"\s+", " ").Trim();
            }

            // Authors
            var authors = entry.Elements(atom + "author")
                .Select(a => a.Element(atom + "name")?.Value)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
            if (authors.Count > 0)
            {
                metadata.Authors = authors.Count <= 3
                    ? string.Join(", ", authors)
                    : $"{authors[0]} et al.";
            }

            // Abstract
            var summary = entry.Element(atom + "summary")?.Value;
            if (!string.IsNullOrEmpty(summary))
            {
                metadata.Abstract = Regex.Replace(summary, @"\s+", " ").Trim();
            }

            // Published date
            var published = entry.Element(atom + "published")?.Value;
            if (DateTimeOffset.TryParse(published, out var date))
            {
                metadata.Date = date;
            }

            // Categories
            var categories = entry.Elements(atom + "category")
                .Select(c => c.Attribute("term")?.Value)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            if (categories.Count > 0)
            {
                metadata.Categories = categories!;
            }

            metadata.Source = MetadataSource.ArXivApi;
        }
        catch
        {
            // arXiv API failed - keep what we have from filename/PDF
        }
    }
}
