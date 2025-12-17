using System.Text;
using Mostlylucid.DocSummarizer.Models;

namespace Mostlylucid.DocSummarizer.Services;

public class DocumentChunker
{
    public List<DocumentChunk> ChunkByStructure(string markdown)
    {
        var chunks = new List<DocumentChunk>();
        var lines = markdown.Split('\n');
        
        var section = new StringBuilder();
        string? heading = null;
        var level = 0;
        var index = 0;

        foreach (var line in lines)
        {
            var headingLevel = GetHeadingLevel(line);
            
            if (headingLevel > 0 && headingLevel <= 3)
            {
                // Flush previous chunk
                if (section.Length > 0)
                {
                    var content = section.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        chunks.Add(new DocumentChunk(
                            index++, heading ?? string.Empty, level, content, HashHelper.ComputeHash(content)));
                    }
                    section.Clear();
                }
                
                heading = line.TrimStart('#', ' ');
                level = headingLevel;
            }
            else
            {
                section.AppendLine(line);
            }
        }

        // Flush final chunk
        if (section.Length > 0)
        {
            var content = section.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new DocumentChunk(
                    index, heading ?? string.Empty, level, content, HashHelper.ComputeHash(content)));
            }
        }

        return chunks;
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
}
