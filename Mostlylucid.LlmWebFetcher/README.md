# Mostlylucid.LlmWebFetcher

A sample project demonstrating how to fetch, clean, and analyze web content using local LLMs in C#.

This accompanies the blog post: [Fetching and Analysing Web Content with LLMs in C#](https://mostlylucid.net/blog/fetching-and-analysing-web-content-with-llms)

## Overview

**LLMs don't browse the web. They reason over carefully selected fragments.**

This project demonstrates the pattern:
1. **Fetch** - Get the raw HTML from a URL
2. **Clean** - Strip scripts, styles, navigation, and extract main content
3. **Chunk** - Split content into LLM-sized pieces
4. **Select** - Pick the most relevant chunks for the query
5. **Analyze** - Use a local LLM to reason over the content

## Prerequisites

1. **Install Ollama**: https://ollama.ai
2. **Pull a model**:
   ```bash
   ollama pull llama3.2:3b
   ```
3. **Ensure Ollama is running**:
   ```bash
   ollama serve
   ```

## Quick Start

```bash
cd Mostlylucid.LlmWebFetcher
dotnet run
```

Or with a specific URL:

```bash
dotnet run https://example.com/some-article
```

## Project Structure

```
Mostlylucid.LlmWebFetcher/
├── Models/
│   ├── WebPage.cs           # Fetched page data
│   ├── ContentChunk.cs      # Chunked content
│   └── AnalysisResult.cs    # LLM analysis results
├── Services/
│   ├── WebFetcher.cs        # HTTP fetching with proper headers
│   ├── HtmlCleaner.cs       # HTML cleaning and extraction
│   ├── ContentChunker.cs    # Text chunking strategies
│   ├── WebContentAnalyzer.cs # Question answering
│   └── BlogAnalyzer.cs      # Blog post analysis
└── Program.cs               # Demo application
```

## Usage Examples

### Basic Fetching and Cleaning

```csharp
using var fetcher = new WebFetcher();
var cleaner = new HtmlCleaner();

var page = await fetcher.FetchAsync("https://example.com/article");
var cleanText = cleaner.Clean(page.Html);

Console.WriteLine($"Extracted {cleanText.Length} characters");
```

### Question Answering

```csharp
using var analyzer = new WebContentAnalyzer(model: "llama3.2:3b");

var result = await analyzer.AnalyzeAsync(
    "https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/overview",
    "What are the new features in .NET 9?"
);

Console.WriteLine(result.Answer);
```

### Blog Post Analysis

```csharp
using var analyzer = new BlogAnalyzer(model: "llama3.2:3b", verbose: true);

var analysis = await analyzer.AnalyzeAsync(
    "https://devblogs.microsoft.com/dotnet/announcing-dotnet-9/"
);

Console.WriteLine($"Title: {analysis.Title}");
Console.WriteLine($"Category: {analysis.Category}");
Console.WriteLine($"Summary: {analysis.Summary}");
Console.WriteLine("Key Points:");
foreach (var point in analysis.KeyPoints)
{
    Console.WriteLine($"  - {point}");
}
```

### Content Summarization

```csharp
using var analyzer = new WebContentAnalyzer();

var summary = await analyzer.SummarizeAsync(
    "https://example.com/long-article",
    sentences: 3
);

Console.WriteLine(summary);
```

### Fact Extraction

```csharp
using var analyzer = new WebContentAnalyzer();

var facts = await analyzer.ExtractFactsAsync("https://example.com/product-page");

foreach (var fact in facts)
{
    Console.WriteLine($"• {fact}");
}
```

### Content Classification

```csharp
using var analyzer = new WebContentAnalyzer();

var category = await analyzer.ClassifyAsync(
    "https://example.com/article",
    new[] { "Technical", "Business", "News", "Tutorial", "Opinion" }
);

Console.WriteLine($"Category: {category}");
```

## Key Concepts

### HTML Cleaning

The `HtmlCleaner` removes noise elements and extracts main content:

- Removes: `<script>`, `<style>`, `<nav>`, `<footer>`, `<aside>`, `<iframe>`
- Filters by class/id patterns: `sidebar`, `advertisement`, `social`, `comment`
- Finds main content using semantic selectors: `<main>`, `<article>`, `[role='main']`
- Normalizes whitespace

### Chunking Strategies

The `ContentChunker` provides multiple approaches:

- **By Size**: Simple word-count based chunking with overlap
- **By Sentence**: Keeps complete sentences together
- **By Section**: Uses document structure (headings)
- **By Relevance**: Filters chunks using keyword matching

### Prompt Engineering

The analyzers use structured prompts that:

- Provide clear instructions
- Include source attribution
- Enforce grounded answers ("answer only from provided content")
- Allow "not enough information" responses

## Dependencies

- [AngleSharp](https://anglesharp.github.io/) - HTML parsing
- [OllamaSharp](https://github.com/awaescher/OllamaSharp) - Ollama client for .NET

## Recommended Models

| Model | Size | Speed | Use Case |
|-------|------|-------|----------|
| `llama3.2:3b` | 2GB | Fast | General use, quick responses |
| `llama3.2:7b` | 4GB | Medium | Better quality |
| `qwen2.5-coder:7b` | 4.7GB | Medium | Technical content |
| `mistral:7b` | 4GB | Medium | Good all-rounder |

## Limitations

This approach works best for:
- Static HTML pages
- Blogs and articles
- Documentation
- News sites

It does **not** work for:
- JavaScript-rendered content (SPAs)
- Pages requiring authentication
- Interactive content
- Very large documents (100K+ tokens)

For JS-heavy sites, consider [Playwright](https://playwright.dev/) for headless browser rendering.

## Related Articles

- [Analysing Large CSV Files with Local LLMs in C#](https://mostlylucid.net/blog/analysing-large-csv-files-with-local-llms) - Similar pattern: LLM reasons, database computes
- [Why I Don't Use LangChain](https://mostlylucid.net/blog/why-i-dont-use-langchain) - Framework-less agent design

## External Resources

- [Ollama](https://ollama.ai/) - Local LLM inference
- [AngleSharp Documentation](https://anglesharp.github.io/)
- [OllamaSharp GitHub](https://github.com/awaescher/OllamaSharp)
- [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)

## License

MIT - See the main repository license.
