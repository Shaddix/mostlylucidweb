using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Mostlylucid.LlmWebFetcher.Services;

/// <summary>
/// Cleans and extracts main content from HTML pages.
/// Removes scripts, styles, navigation, and other non-content elements.
/// </summary>
public partial class HtmlCleaner
{
    private readonly HtmlParser _parser;
    
    // Elements that are always noise
    private static readonly string[] NoiseElements = 
    {
        "script", "style", "noscript", "iframe", "svg", "canvas",
        "nav", "footer", "header", "aside", "form"
    };
    
    // CSS selectors for noise elements (ads, social, comments, etc.)
    private static readonly string[] NoiseSelectors =
    {
        "[class*='advertisement']", "[class*='social']", "[class*='share']",
        "[class*='comment']", "[class*='sidebar']", "[class*='cookie']",
        "[class*='popup']", "[class*='modal']", "[class*='banner']",
        "[class*='promo']", "[class*='newsletter']", "[class*='subscribe']",
        "[id*='sidebar']", "[id*='nav']", "[id*='footer']", "[id*='header']",
        "[role='navigation']", "[role='banner']", "[role='complementary']",
        "[aria-hidden='true']"
    };
    
    // Selectors for main content
    private static readonly string[] ContentSelectors =
    {
        "main", "article", "[role='main']", ".content", ".post-content",
        ".article-content", ".entry-content", ".post-body", "#content",
        ".markdown-body", ".prose"
    };
    
    public HtmlCleaner()
    {
        _parser = new HtmlParser();
    }
    
    /// <summary>
    /// Cleans HTML and extracts main text content.
    /// </summary>
    /// <param name="html">Raw HTML string.</param>
    /// <returns>Clean text content.</returns>
    public string Clean(string html)
    {
        var document = _parser.ParseDocument(html);
        
        // Remove noise elements
        RemoveElements(document, NoiseElements);
        
        // Remove by CSS selectors
        foreach (var selector in NoiseSelectors)
        {
            try
            {
                var elements = document.QuerySelectorAll(selector);
                foreach (var el in elements.ToList())
                    el.Remove();
            }
            catch
            {
                // Ignore invalid selectors
            }
        }
        
        // Find main content container
        IElement? mainContent = null;
        foreach (var selector in ContentSelectors)
        {
            mainContent = document.QuerySelector(selector);
            if (mainContent != null)
                break;
        }
        
        mainContent ??= document.Body;
        
        if (mainContent == null)
            return "";
        
        // Extract text
        var text = mainContent.TextContent;
        
        // Normalize whitespace
        text = NormalizeWhitespace(text);
        
        return text;
    }
    
    /// <summary>
    /// Extracts main content using a Readability-style scoring algorithm.
    /// Better for pages without clear semantic structure.
    /// </summary>
    /// <param name="html">Raw HTML string.</param>
    /// <returns>Clean text content from the highest-scoring container.</returns>
    public string ExtractWithScoring(string html)
    {
        var document = _parser.ParseDocument(html);
        
        // Remove obvious noise first
        RemoveElements(document, NoiseElements);
        
        // Score potential content containers
        var candidates = document.QuerySelectorAll("p, div, article, section, td")
            .Select(el => new ScoredElement(el, ScoreElement(el)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();
        
        if (candidates.Count == 0)
            return document.Body?.TextContent ?? "";
        
        // Get top candidate and its text
        var topCandidate = candidates.First();
        var text = topCandidate.Element.TextContent;
        
        return NormalizeWhitespace(text);
    }
    
    /// <summary>
    /// Extracts the page title.
    /// </summary>
    public string ExtractTitle(string html)
    {
        var document = _parser.ParseDocument(html);
        
        // Try various title sources
        var title = document.QuerySelector("h1")?.TextContent
                    ?? document.QuerySelector("title")?.TextContent
                    ?? document.QuerySelector("[class*='title']")?.TextContent
                    ?? "";
        
        return NormalizeWhitespace(title);
    }
    
    /// <summary>
    /// Extracts structured content sections based on headings.
    /// </summary>
    /// <param name="html">Raw HTML string.</param>
    /// <returns>List of sections with headings and content.</returns>
    public List<ContentSection> ExtractSections(string html)
    {
        var document = _parser.ParseDocument(html);
        RemoveElements(document, NoiseElements);
        
        var sections = new List<ContentSection>();
        var headings = document.QuerySelectorAll("h1, h2, h3, h4, h5, h6");
        
        foreach (var heading in headings)
        {
            var content = new System.Text.StringBuilder();
            
            // Collect all content until next heading
            var sibling = heading.NextElementSibling;
            while (sibling != null && !sibling.TagName.StartsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                content.AppendLine(sibling.TextContent);
                sibling = sibling.NextElementSibling;
            }
            
            var sectionText = NormalizeWhitespace(content.ToString());
            if (!string.IsNullOrWhiteSpace(sectionText))
            {
                sections.Add(new ContentSection
                {
                    Heading = NormalizeWhitespace(heading.TextContent),
                    HeadingLevel = int.Parse(heading.TagName[1..]),
                    Content = sectionText
                });
            }
        }
        
        return sections;
    }
    
    private void RemoveElements(IDocument document, params string[] tagNames)
    {
        foreach (var tag in tagNames)
        {
            var elements = document.QuerySelectorAll(tag);
            foreach (var el in elements.ToList())
                el.Remove();
        }
    }
    
    private int ScoreElement(IElement element)
    {
        var score = 0;
        var text = element.TextContent;
        
        // Length: longer text is more likely to be content
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        score += Math.Min(wordCount / 10, 50); // Cap at 50 points
        
        // Paragraph count
        var paragraphCount = element.QuerySelectorAll("p").Length;
        score += paragraphCount * 3;
        
        // Tag bonuses
        if (element.TagName is "ARTICLE" or "MAIN")
            score += 25;
        else if (element.TagName == "P")
            score += 10;
        else if (element.TagName == "DIV")
            score += 5;
        
        // Check class/id names
        var className = element.ClassName?.ToLowerInvariant() ?? "";
        var id = element.Id?.ToLowerInvariant() ?? "";
        
        // Positive signals
        if (className.Contains("content") || className.Contains("article") || 
            className.Contains("post") || className.Contains("entry") ||
            id.Contains("content") || id.Contains("main") || id.Contains("article"))
            score += 15;
        
        // Negative signals
        if (className.Contains("sidebar") || className.Contains("ad") || 
            className.Contains("comment") || className.Contains("footer") ||
            className.Contains("nav") || className.Contains("menu") ||
            className.Contains("social") || className.Contains("share"))
            score -= 50;
        
        // Text density: ratio of text length to HTML length
        var htmlLength = element.OuterHtml.Length;
        var textLength = text.Length;
        if (htmlLength > 0)
        {
            var density = (double)textLength / htmlLength;
            score += (int)(density * 20);
        }
        
        // Link density penalty: lots of links = probably navigation
        var linkCount = element.QuerySelectorAll("a").Length;
        if (wordCount > 0)
        {
            var linkRatio = (double)linkCount / wordCount;
            if (linkRatio > 0.3)
                score -= 30;
        }
        
        return score;
    }
    
    private string NormalizeWhitespace(string text)
    {
        // Collapse multiple spaces/tabs to single space
        text = MultipleSpaces().Replace(text, " ");
        
        // Collapse multiple newlines to double newline (paragraph break)
        text = MultipleNewlines().Replace(text, "\n\n");
        
        // Remove leading/trailing whitespace from lines
        text = TrailingWhitespace().Replace(text, "");
        
        return text.Trim();
    }
    
    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultipleSpaces();
    
    [GeneratedRegex(@"\n\s*\n\s*\n+")]
    private static partial Regex MultipleNewlines();
    
    [GeneratedRegex(@"[ \t]+$", RegexOptions.Multiline)]
    private static partial Regex TrailingWhitespace();
    
    private record ScoredElement(IElement Element, int Score);
}

/// <summary>
/// Represents a content section with heading and body text.
/// </summary>
public class ContentSection
{
    public string Heading { get; set; } = "";
    public int HeadingLevel { get; set; }
    public string Content { get; set; } = "";
}
