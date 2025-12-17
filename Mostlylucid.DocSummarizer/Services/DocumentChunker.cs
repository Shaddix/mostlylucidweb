using System.Text;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentChunker
{
    private readonly int _maxHeadingLevel;
    private readonly int _targetChunkTokens;
    private readonly int _minChunkTokens;
    
    // Rough estimate: 1 token ≈ 4 characters for English text
    private const int CharsPerToken = 4;
    
    /// <summary>
    /// Creates a new document chunker.
    /// </summary>
    /// <param name="maxHeadingLevel">Maximum heading level to split on (1-6). Default is 2 (H1 and H2 only).</param>
    /// <param name="targetChunkTokens">Target chunk size in tokens. Default is 4000 (~16KB). 
    /// Chunks smaller than this will be merged with adjacent sections.</param>
    /// <param name="minChunkTokens">Minimum chunk size before merging. Default is 500 (~2KB).</param>
    public DocumentChunker(int maxHeadingLevel = 2, int targetChunkTokens = 4000, int minChunkTokens = 500)
    {
        _maxHeadingLevel = Math.Clamp(maxHeadingLevel, 1, 6);
        _targetChunkTokens = targetChunkTokens;
        _minChunkTokens = minChunkTokens;
    }
    
    public List<DocumentChunk> ChunkByStructure(string markdown)
    {
        // First pass: split by structure (headings)
        var rawSections = SplitByHeadings(markdown);
        
        // Second pass: merge small sections to approach target size
        var mergedSections = MergeSections(rawSections);
        
        // Convert to chunks
        var chunks = new List<DocumentChunk>();
        var index = 0;
        
        foreach (var section in mergedSections)
        {
            if (!string.IsNullOrWhiteSpace(section.Content))
            {
                chunks.Add(new DocumentChunk(
                    index++, 
                    section.Heading, 
                    section.Level, 
                    section.Content, 
                    HashHelper.ComputeHash(section.Content)));
            }
        }
        
        return chunks;
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
        {
            sections.Add(new RawSection(heading ?? "", level, content.ToString().Trim()));
        }

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
        {
            merged.Add(new RawSection(currentHeading, currentLevel, currentContent.ToString().Trim()));
        }

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
        {
            if (c == '#') level++;
            else break;
        }

        // Must have space after # marks to be a valid heading
        return line.Length > level && line[level] == ' ' ? level : 0;
    }

    private record RawSection(string Heading, int Level, string Content);
}
