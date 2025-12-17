using System.Text;
using System.Text.RegularExpressions;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentChunker
{
    // Rough estimate: 1 token ≈ 4 characters for English text
    private const int CharsPerToken = 4;
    private readonly int _maxHeadingLevel;
    private readonly int _minChunkTokens;
    private readonly int _targetChunkTokens;

    /// <summary>
    ///     Creates a new document chunker.
    /// </summary>
    /// <param name="maxHeadingLevel">Maximum heading level to split on (1-6). Default is 2 (H1 and H2 only).</param>
    /// <param name="targetChunkTokens">
    ///     Target chunk size in tokens. Default is 4000 (~16KB).
    ///     Chunks smaller than this will be merged with adjacent sections.
    /// </param>
    /// <param name="minChunkTokens">Minimum chunk size before merging. Default is 500 (~2KB).</param>
    public DocumentChunker(int maxHeadingLevel = 2, int targetChunkTokens = 4000, int minChunkTokens = 500)
    {
        _maxHeadingLevel = Math.Clamp(maxHeadingLevel, 1, 6);
        _targetChunkTokens = targetChunkTokens;
        _minChunkTokens = minChunkTokens;
    }

    public List<DocumentChunk> ChunkByStructure(string markdown)
    {
        // Check if document has markdown headings
        var hasHeadings = HasMarkdownHeadings(markdown);

        // First pass: split by structure (headings) or paragraphs for plain text
        var rawSections = hasHeadings
            ? SplitByHeadings(markdown)
            : SplitByParagraphs(markdown);

        // Second pass: merge small sections to approach target size
        var mergedSections = MergeSections(rawSections);

        // Convert to chunks
        var chunks = new List<DocumentChunk>();
        var index = 0;

        foreach (var section in mergedSections)
            if (!string.IsNullOrWhiteSpace(section.Content))
                chunks.Add(new DocumentChunk(
                    index++,
                    section.Heading,
                    section.Level,
                    section.Content,
                    HashHelper.ComputeHash(section.Content)));

        return chunks;
    }

    /// <summary>
    ///     Check if document contains markdown headings
    /// </summary>
    private bool HasMarkdownHeadings(string text)
    {
        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var level = GetHeadingLevel(line);
            if (level > 0 && level <= _maxHeadingLevel)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Split plain text by paragraphs (double newlines)
    /// </summary>
    private List<RawSection> SplitByParagraphs(string text)
    {
        var sections = new List<RawSection>();

        // Split on double newlines (blank lines) - handles \n\n and \r\n\r\n
        var paragraphs = Regex
            .Split(text, @"\r?\n\s*\r?\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        if (paragraphs.Count == 0)
        {
            // No paragraphs found, treat whole text as one section
            if (!string.IsNullOrWhiteSpace(text)) sections.Add(new RawSection("Document", 1, text.Trim()));
            return sections;
        }

        // Try to extract a title from the first paragraph if it's short
        var firstPara = paragraphs[0];
        var hasTitle = firstPara.Length < 200 && !firstPara.Contains('\n');

        if (hasTitle && paragraphs.Count > 1)
        {
            // First paragraph is likely a title
            var title = firstPara.Length > 80 ? firstPara[..77] + "..." : firstPara;
            var content = string.Join("\n\n", paragraphs.Skip(1));
            sections.Add(new RawSection(title, 1, content));
        }
        else
        {
            // Group paragraphs into logical sections
            // Each section starts with a paragraph number for reference
            var paragraphIndex = 0;
            foreach (var para in paragraphs)
            {
                paragraphIndex++;
                var heading = $"Paragraph {paragraphIndex}";

                // Try to use first sentence or first N chars as heading
                var firstSentenceEnd = para.IndexOfAny(['.', '!', '?']);
                if (firstSentenceEnd > 0 && firstSentenceEnd < 100)
                {
                    heading = para[..(firstSentenceEnd + 1)];
                }
                else if (para.Length < 100)
                {
                    heading = para;
                }
                else
                {
                    // Use first 80 chars
                    var cutoff = para.LastIndexOf(' ', Math.Min(80, para.Length - 1));
                    if (cutoff < 20) cutoff = 80;
                    heading = para[..Math.Min(cutoff, para.Length)] + "...";
                }

                sections.Add(new RawSection(heading, 1, para));
            }
        }

        return sections;
    }

    private List<RawSection> SplitByHeadings(string markdown)
    {
        var sections = new List<RawSection>();
        var lines = markdown.Split('\n');

        var content = new StringBuilder();
        string? heading = null;
        var level = 0;

        foreach (var line in lines)
        {
            var headingLevel = GetHeadingLevel(line);

            // Only split on headings up to the configured max level
            if (headingLevel > 0 && headingLevel <= _maxHeadingLevel)
            {
                // Flush previous section
                if (content.Length > 0 || heading != null)
                {
                    sections.Add(new RawSection(heading ?? "", level, content.ToString().Trim()));
                    content.Clear();
                }

                heading = line.TrimStart('#', ' ');
                level = headingLevel;
            }
            else
            {
                content.AppendLine(line);
            }
        }

        // Flush final section
        if (content.Length > 0 || heading != null)
            sections.Add(new RawSection(heading ?? "", level, content.ToString().Trim()));

        return sections;
    }

    private List<RawSection> MergeSections(List<RawSection> sections)
    {
        if (sections.Count <= 1)
            return sections;

        var merged = new List<RawSection>();
        var currentHeading = "";
        var currentLevel = 0;
        var currentContent = new StringBuilder();
        var currentTokens = 0;

        foreach (var section in sections)
        {
            var sectionTokens = EstimateTokens(section.Content);
            var sectionWithHeading = string.IsNullOrEmpty(section.Heading)
                ? section.Content
                : $"## {section.Heading}\n\n{section.Content}";
            var fullSectionTokens = EstimateTokens(sectionWithHeading);

            // If adding this section would exceed target, flush current and start new
            if (currentContent.Length > 0 && currentTokens + fullSectionTokens > _targetChunkTokens)
            {
                // Only flush if current chunk is big enough, otherwise keep merging
                if (currentTokens >= _minChunkTokens)
                {
                    merged.Add(new RawSection(currentHeading, currentLevel, currentContent.ToString().Trim()));
                    currentHeading = section.Heading;
                    currentLevel = section.Level;
                    currentContent.Clear();
                    currentContent.AppendLine(section.Content);
                    currentTokens = sectionTokens;
                }
                else
                {
                    // Current chunk too small, merge anyway
                    if (currentContent.Length > 0) currentContent.AppendLine();
                    if (!string.IsNullOrEmpty(section.Heading))
                    {
                        currentContent.AppendLine($"## {section.Heading}");
                        currentContent.AppendLine();
                    }

                    currentContent.AppendLine(section.Content);
                    currentTokens += fullSectionTokens;
                }
            }
            else
            {
                // Merge into current chunk
                if (currentContent.Length == 0)
                {
                    currentHeading = section.Heading;
                    currentLevel = section.Level;
                    currentContent.AppendLine(section.Content);
                    currentTokens = sectionTokens;
                }
                else
                {
                    // Append with sub-heading preserved
                    if (currentContent.Length > 0) currentContent.AppendLine();
                    if (!string.IsNullOrEmpty(section.Heading))
                    {
                        currentContent.AppendLine($"## {section.Heading}");
                        currentContent.AppendLine();
                    }

                    currentContent.AppendLine(section.Content);
                    currentTokens += fullSectionTokens;
                }
            }
        }

        // Flush final chunk
        if (currentContent.Length > 0)
            merged.Add(new RawSection(currentHeading, currentLevel, currentContent.ToString().Trim()));

        return merged;
    }

    private int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length / CharsPerToken;
    }

    private static int GetHeadingLevel(string line)
    {
        if (string.IsNullOrEmpty(line) || !line.StartsWith('#'))
            return 0;

        var level = 0;
        foreach (var c in line)
            if (c == '#') level++;
            else break;

        // Must have space after # marks to be a valid heading
        return line.Length > level && line[level] == ' ' ? level : 0;
    }

    private record RawSection(string Heading, int Level, string Content);
}