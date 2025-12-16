using System.Text;
using Mostlylucid.LlmWebFetcher.Models;

namespace Mostlylucid.LlmWebFetcher.Services;

/// <summary>
/// Chunks text content for LLM consumption.
/// Provides multiple chunking strategies: fixed-size, sentence-based, and semantic.
/// </summary>
public class ContentChunker
{
    /// <summary>
    /// Estimates the number of tokens in a text string.
    /// Uses a rough approximation of 1.3 tokens per word.
    /// </summary>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)(wordCount * 1.3);
    }
    
    /// <summary>
    /// Chunks text by fixed size (word-based).
    /// Simple but may break mid-sentence.
    /// </summary>
    /// <param name="text">Text to chunk.</param>
    /// <param name="maxTokens">Maximum tokens per chunk.</param>
    /// <param name="overlap">Number of tokens to overlap between chunks.</param>
    /// <returns>List of text chunks.</returns>
    public List<ContentChunk> ChunkBySize(string text, int maxTokens = 2000, int overlap = 100)
    {
        var chunks = new List<ContentChunk>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var wordsPerChunk = (int)(maxTokens / 1.3);
        var overlapWords = (int)(overlap / 1.3);
        
        for (int i = 0; i < words.Length; i += wordsPerChunk - overlapWords)
        {
            var chunkWords = words.Skip(i).Take(wordsPerChunk).ToArray();
            var chunkText = string.Join(" ", chunkWords);
            
            chunks.Add(new ContentChunk
            {
                Content = chunkText,
                EstimatedTokens = EstimateTokens(chunkText)
            });
            
            if (i + wordsPerChunk >= words.Length)
                break;
        }
        
        return chunks;
    }
    
    /// <summary>
    /// Chunks text by sentences, keeping complete sentences together.
    /// Better for maintaining coherence.
    /// </summary>
    /// <param name="text">Text to chunk.</param>
    /// <param name="maxTokens">Maximum tokens per chunk.</param>
    /// <returns>List of text chunks.</returns>
    public List<ContentChunk> ChunkBySentence(string text, int maxTokens = 2000)
    {
        var chunks = new List<ContentChunk>();
        
        // Split on sentence endings
        var sentences = SplitIntoSentences(text);
        
        var currentChunk = new StringBuilder();
        var currentTokens = 0;
        
        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokens(sentence);
            
            // If single sentence exceeds limit, add it anyway
            if (sentenceTokens > maxTokens)
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(new ContentChunk
                    {
                        Content = currentChunk.ToString().Trim(),
                        EstimatedTokens = currentTokens
                    });
                    currentChunk.Clear();
                    currentTokens = 0;
                }
                
                chunks.Add(new ContentChunk
                {
                    Content = sentence,
                    EstimatedTokens = sentenceTokens
                });
                continue;
            }
            
            // If adding this sentence would exceed limit, start new chunk
            if (currentTokens + sentenceTokens > maxTokens && currentChunk.Length > 0)
            {
                chunks.Add(new ContentChunk
                {
                    Content = currentChunk.ToString().Trim(),
                    EstimatedTokens = currentTokens
                });
                currentChunk.Clear();
                currentTokens = 0;
            }
            
            currentChunk.Append(sentence).Append(' ');
            currentTokens += sentenceTokens;
        }
        
        // Add remaining content
        if (currentChunk.Length > 0)
        {
            chunks.Add(new ContentChunk
            {
                Content = currentChunk.ToString().Trim(),
                EstimatedTokens = currentTokens
            });
        }
        
        return chunks;
    }
    
    /// <summary>
    /// Chunks content by structural sections (from HtmlCleaner.ExtractSections).
    /// Maintains semantic coherence based on document structure.
    /// </summary>
    /// <param name="sections">Content sections extracted from HTML.</param>
    /// <param name="maxTokens">Maximum tokens per chunk.</param>
    /// <returns>List of content chunks.</returns>
    public List<ContentChunk> ChunkBySections(List<ContentSection> sections, int maxTokens = 2000)
    {
        var chunks = new List<ContentChunk>();
        
        foreach (var section in sections)
        {
            var sectionText = $"{section.Heading}\n{section.Content}";
            var tokens = EstimateTokens(sectionText);
            
            if (tokens <= maxTokens)
            {
                chunks.Add(new ContentChunk
                {
                    Heading = section.Heading,
                    Content = sectionText,
                    EstimatedTokens = tokens
                });
            }
            else
            {
                // Section too large, chunk by sentence
                var subChunks = ChunkBySentence(sectionText, maxTokens);
                foreach (var subChunk in subChunks)
                {
                    subChunk.Heading = section.Heading;
                    chunks.Add(subChunk);
                }
            }
        }
        
        return chunks;
    }
    
    /// <summary>
    /// Filters and ranks chunks by keyword relevance.
    /// </summary>
    /// <param name="chunks">Chunks to filter.</param>
    /// <param name="query">Query to match against.</param>
    /// <param name="topK">Number of top chunks to return.</param>
    /// <returns>Top K most relevant chunks.</returns>
    public List<ContentChunk> FilterByKeywords(List<ContentChunk> chunks, string query, int topK = 5)
    {
        // Extract keywords (words > 3 chars, excluding common stop words)
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "are", "but", "not", "you", "all", "can", "had",
            "her", "was", "one", "our", "out", "has", "have", "been", "being",
            "with", "this", "that", "from", "they", "will", "would", "there",
            "their", "what", "about", "which", "when", "make", "like", "time",
            "just", "know", "take", "into", "year", "your", "some", "could",
            "them", "than", "then", "look", "only", "come", "over", "such",
            "also", "back", "after", "use", "how", "because", "any", "these"
        };
        
        var keywords = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .Distinct()
            .ToList();
        
        if (keywords.Count == 0)
            return chunks.Take(topK).ToList();
        
        // Score chunks by keyword matches
        foreach (var chunk in chunks)
        {
            var lowerContent = chunk.Content.ToLowerInvariant();
            chunk.RelevanceScore = keywords.Count(kw => lowerContent.Contains(kw));
        }
        
        return chunks
            .OrderByDescending(c => c.RelevanceScore)
            .Take(topK)
            .ToList();
    }
    
    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var currentSentence = new StringBuilder();
        
        for (int i = 0; i < text.Length; i++)
        {
            currentSentence.Append(text[i]);
            
            // Check for sentence endings
            if (text[i] is '.' or '!' or '?')
            {
                // Check if it's really end of sentence (not abbreviation, decimal, etc.)
                var isEndOfSentence = true;
                
                // Look ahead for space and capital letter or end of text
                if (i + 1 < text.Length)
                {
                    var nextChar = text[i + 1];
                    if (!char.IsWhiteSpace(nextChar))
                    {
                        isEndOfSentence = false;
                    }
                    else if (i + 2 < text.Length)
                    {
                        var charAfterSpace = text[i + 2];
                        // Check for capital letter or number (likely new sentence)
                        isEndOfSentence = char.IsUpper(charAfterSpace) || char.IsDigit(charAfterSpace);
                    }
                }
                
                if (isEndOfSentence)
                {
                    var sentence = currentSentence.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    currentSentence.Clear();
                }
            }
        }
        
        // Add any remaining text
        var remaining = currentSentence.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            sentences.Add(remaining);
        }
        
        return sentences;
    }
}
