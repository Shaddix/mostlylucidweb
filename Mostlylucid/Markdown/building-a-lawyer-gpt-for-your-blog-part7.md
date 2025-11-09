# Building a "Lawyer GPT" for Your Blog - Part 7: Content Generation & Prompt Engineering

<!--category-- AI, LLM, Prompt Engineering, RAG, C#, AI-Article, mostlylucid.blogllm -->
<datetime class="hidden">1973-02-08T23:00</datetime>

## Introduction

Welcome to Part 7! We've built everything needed for a working writing assistant - ingestion (Part 4), UI (Part 5), and local LLM integration (Part 6). Now it's time to bring it all together and focus on what matters most: generating actually useful writing suggestions.

> NOTE: This is part of my experiments with AI (assisted drafting) + my own editing. Same voice, same pragmatism; just faster fingers.

This is where the rubber meets the road. We'll explore advanced prompt engineering techniques, context management strategies, and how to make the LLM generate suggestions that actually match your writing style.

[TOC]

## The Complete RAG Pipeline

Here's the full flow we're implementing:

```mermaid
sequenceDiagram
    participant User
    participant Editor
    participant Embedder
    participant VectorDB
    participant ContextBuilder
    participant PromptEngine
    participant LLM
    participant UI

    User->>Editor: Types text
    Editor->>Embedder: Generate embedding for context
    Embedder->>VectorDB: Search similar content
    VectorDB-->>ContextBuilder: Return top K chunks
    ContextBuilder->>ContextBuilder: Rank & filter
    ContextBuilder->>PromptEngine: Provide context
    PromptEngine->>PromptEngine: Build structured prompt
    PromptEngine->>LLM: Generate suggestion
    LLM-->>UI: Stream response
    UI->>User: Display suggestion

    style Embedder fill:#f9f,stroke:#333,stroke-width:2px
    style VectorDB fill:#bbf,stroke:#333,stroke-width:2px
    style PromptEngine fill:#9f9,stroke:#333,stroke-width:4px
    style LLM fill:#f9f,stroke:#333,stroke-width:2px
```

## Context Builder Service

First, let's build a sophisticated context builder that selects and ranks retrieved chunks.

```csharp
namespace Mostlylucid.BlogLLM.Core.Services
{
    public interface IContextBuilder
    {
        Task<ContextResult> BuildContextAsync(string currentText, int maxTokens = 2000);
    }

    public class ContextResult
    {
        public List<ContextChunk> Chunks { get; set; } = new();
        public int TotalTokens { get; set; }
        public string CombinedContext { get; set; } = string.Empty;
    }

    public class ContextChunk
    {
        public string Text { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public float RelevanceScore { get; set; }
        public int TokenCount { get; set; }
    }

    public class ContextBuilder : IContextBuilder
    {
        private readonly BatchEmbeddingService _embedder;
        private readonly QdrantVectorStore _vectorStore;
        private readonly ILogger<ContextBuilder> _logger;

        public ContextBuilder(
            BatchEmbeddingService embedder,
            QdrantVectorStore vectorStore,
            ILogger<ContextBuilder> logger)
        {
            _embedder = embedder;
            _vectorStore = vectorStore;
            _logger = logger;
        }

        public async Task<ContextResult> BuildContextAsync(string currentText, int maxTokens = 2000)
        {
            // Extract meaningful context from current text
            var searchText = ExtractSearchContext(currentText);

            // Generate embedding
            var embedding = _embedder.GenerateEmbedding(searchText);

            // Retrieve similar chunks
            var searchResults = await _vectorStore.SearchAsync(
                queryEmbedding: embedding,
                limit: 20,  // Get more than we need
                languageFilter: "en"
            );

            // Re-rank results
            var rankedChunks = ReRankResults(searchResults, currentText);

            // Select chunks that fit in token budget
            var selectedChunks = SelectChunks(rankedChunks, maxTokens);

            // Build combined context
            var combinedContext = BuildCombinedContext(selectedChunks);

            return new ContextResult
            {
                Chunks = selectedChunks,
                TotalTokens = selectedChunks.Sum(c => c.TokenCount),
                CombinedContext = combinedContext
            };
        }

        private string ExtractSearchContext(string currentText)
        {
            // Get last 3 paragraphs or 500 characters
            var paragraphs = currentText.Split("\n\n");
            var lastParagraphs = paragraphs.TakeLast(3);
            var searchText = string.Join("\n\n", lastParagraphs);

            if (searchText.Length > 500)
            {
                searchText = searchText.Substring(searchText.Length - 500);
            }

            return searchText;
        }

        private List<ContextChunk> ReRankResults(List<SearchResult> results, string currentText)
        {
            var chunks = new List<ContextChunk>();

            foreach (var result in results)
            {
                var chunk = new ContextChunk
                {
                    Text = result.Text,
                    Source = $"{result.BlogPostTitle} - {result.SectionHeading}",
                    RelevanceScore = result.Score,
                    TokenCount = EstimateTokens(result.Text)
                };

                // Boost score if from same category
                if (SharesCategories(result, currentText))
                {
                    chunk.RelevanceScore *= 1.2f;
                }

                // Reduce score if very recent (might be too similar)
                if (IsVeryRecent(result))
                {
                    chunk.RelevanceScore *= 0.9f;
                }

                chunks.Add(chunk);
            }

            return chunks.OrderByDescending(c => c.RelevanceScore).ToList();
        }

        private List<ContextChunk> SelectChunks(List<ContextChunk> rankedChunks, int maxTokens)
        {
            var selected = new List<ContextChunk>();
            var totalTokens = 0;

            foreach (var chunk in rankedChunks)
            {
                if (totalTokens + chunk.TokenCount > maxTokens)
                {
                    break;
                }

                selected.Add(chunk);
                totalTokens += chunk.TokenCount;

                _logger.LogDebug("Selected chunk from {Source} ({Tokens} tokens, score: {Score:F3})",
                    chunk.Source, chunk.TokenCount, chunk.RelevanceScore);
            }

            _logger.LogInformation("Selected {Count} chunks totaling {Tokens} tokens",
                selected.Count, totalTokens);

            return selected;
        }

        private string BuildCombinedContext(List<ContextChunk> chunks)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < chunks.Count; i++)
            {
                sb.AppendLine($"=== Reference {i + 1}: {chunks[i].Source} ===");
                sb.AppendLine(chunks[i].Text);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private int EstimateTokens(string text)
        {
            // Rough estimate: 1 token ≈ 4 characters
            return text.Length / 4;
        }

        private bool SharesCategories(SearchResult result, string currentText)
        {
            // Simple heuristic - check if category names appear in current text
            // In production, extract actual categories from metadata
            return false;
        }

        private bool IsVeryRecent(SearchResult result)
        {
            // Reduce score for posts from last 7 days to encourage diversity
            // In production, parse actual publish dates
            return false;
        }
    }
}
```

## Advanced Prompt Engineering

The key to good output is good prompts. Let's build a sophisticated prompt engineering system.

### Prompt Templates

```csharp
namespace Mostlylucid.BlogLLM.Core.Services
{
    public enum PromptType
    {
        ContinueWriting,
        SuggestStructure,
        GenerateCode,
        ImproveSection,
        AddExample
    }

    public class PromptTemplate
    {
        public PromptType Type { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPromptTemplate { get; set; } = string.Empty;
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 300;
    }

    public static class PromptTemplates
    {
        public static readonly Dictionary<PromptType, PromptTemplate> Templates = new()
        {
            [PromptType.ContinueWriting] = new PromptTemplate
            {
                Type = PromptType.ContinueWriting,
                SystemPrompt = @"You are a helpful writing assistant for a technical blog about .NET, ASP.NET Core, and related technologies.

Your writing style is:
- Conversational but professional
- Technical and detailed
- Includes code examples when relevant
- Uses practical, real-world examples
- Occasionally uses dry humor
- Avoids marketing fluff

When continuing text, maintain consistency with the existing tone and technical depth.",

                UserPromptTemplate = @"Based on these excerpts from similar blog posts:

{context}

---

Current draft:
{current_text}

Task: Suggest 2-3 sentences to naturally continue the current thought. Match the technical depth and conversational tone.

Continuation:",
                Temperature = 0.75f,
                MaxTokens = 200
            },

            [PromptType.SuggestStructure] = new PromptTemplate
            {
                Type = PromptType.SuggestStructure,
                SystemPrompt = @"You are a technical blog editor helping to organize content effectively.

Your suggestions should:
- Create logical flow from simple to complex
- Group related concepts together
- Include practical examples sections
- Ensure each section has clear purpose",

                UserPromptTemplate = @"Based on how similar topics were structured:

{context}

---

Proposed section: {section_title}

Task: Suggest 5-7 subsections or bullet points for what this section should cover.

Structure (as markdown list):",
                Temperature = 0.6f,  // Lower for more structured output
                MaxTokens = 300
            },

            [PromptType.GenerateCode] = new PromptTemplate
            {
                Type = PromptType.GenerateCode,
                SystemPrompt = @"You are a C# coding expert specializing in ASP.NET Core, Entity Framework, and modern .NET development.

Your code should:
- Follow C# conventions and best practices
- Include XML doc comments for public members
- Handle edge cases and errors
- Be production-ready, not just proof-of-concept
- Include explanatory comments for complex logic",

                UserPromptTemplate = @"Relevant code patterns from past posts:

{context}

---

Task: {code_request}

Generate a complete, working C# code example with comments.

Code:",
                Temperature = 0.4f,  // Lower for more consistent code
                MaxTokens = 500
            },

            [PromptType.ImproveSection] = new PromptTemplate
            {
                Type = PromptType.ImproveSection,
                SystemPrompt = @"You are an editor helping to improve technical blog content.

Focus on:
- Clarity and readability
- Technical accuracy
- Adding concrete examples
- Removing redundancy",

                UserPromptTemplate = @"Examples of well-written content:

{context}

---

Current version:
{current_section}

Task: Suggest improvements to make this clearer and more engaging while maintaining technical depth.

Improved version:",
                Temperature = 0.7f,
                MaxTokens = 400
            },

            [PromptType.AddExample] = new PromptTemplate
            {
                Type = PromptType.AddExample,
                SystemPrompt = @"You are a technical writer who excels at creating practical, real-world examples.

Your examples should:
- Be realistic and relatable
- Demonstrate the concept clearly
- Include enough detail to be useful
- Reference common scenarios developers face",

                UserPromptTemplate = @"Similar examples from past posts:

{context}

---

Concept to illustrate: {concept}

Task: Create a practical example that demonstrates this concept in a real-world scenario.

Example:",
                Temperature = 0.8f,  // Higher for more creative examples
                MaxTokens = 350
            }
        };
    }
}
```

### Prompt Builder Service

```csharp
namespace Mostlylucid.BlogLLM.Core.Services
{
    public interface IPromptBuilder
    {
        string BuildPrompt(PromptType type, Dictionary<string, string> variables, string context);
        PromptTemplate GetTemplate(PromptType type);
    }

    public class PromptBuilder : IPromptBuilder
    {
        private readonly ILogger<PromptBuilder> _logger;

        public PromptBuilder(ILogger<PromptBuilder> logger)
        {
            _logger = logger;
        }

        public string BuildPrompt(
            PromptType type,
            Dictionary<string, string> variables,
            string context)
        {
            var template = PromptTemplates.Templates[type];

            // Build full prompt with system message
            var sb = new StringBuilder();

            // System prompt
            sb.AppendLine("<|system|>");
            sb.AppendLine(template.SystemPrompt);
            sb.AppendLine("</|system|>");
            sb.AppendLine();

            // User prompt with variables substituted
            sb.AppendLine("<|user|>");

            var userPrompt = template.UserPromptTemplate;
            userPrompt = userPrompt.Replace("{context}", context);

            foreach (var kvp in variables)
            {
                userPrompt = userPrompt.Replace($"{{{kvp.Key}}}", kvp.Value);
            }

            sb.AppendLine(userPrompt);
            sb.AppendLine("</|user|>");
            sb.AppendLine();

            // Assistant prompt (model fills this in)
            sb.AppendLine("<|assistant|>");

            var fullPrompt = sb.ToString();

            _logger.LogDebug("Built {Type} prompt ({Length} chars)",
                type, fullPrompt.Length);

            return fullPrompt;
        }

        public PromptTemplate GetTemplate(PromptType type)
        {
            return PromptTemplates.Templates[type];
        }
    }
}
```

## Content Generation Service

Now let's tie it all together with a high-level content generation service.

```csharp
namespace Mostlylucid.BlogLLM.Core.Services
{
    public interface IContentGenerationService
    {
        Task<GenerationResult> GenerateAsync(
            GenerationRequest request,
            CancellationToken cancellationToken = default);

        IAsyncEnumerable<string> GenerateStreamAsync(
            GenerationRequest request,
            CancellationToken cancellationToken = default);
    }

    public class GenerationRequest
    {
        public string CurrentText { get; set; } = string.Empty;
        public PromptType Type { get; set; } = PromptType.ContinueWriting;
        public Dictionary<string, string> Variables { get; set; } = new();
        public int MaxContextTokens { get; set; } = 2000;
    }

    public class GenerationResult
    {
        public string GeneratedText { get; set; } = string.Empty;
        public List<string> SourcePosts { get; set; } = new();
        public int ContextTokensUsed { get; set; }
        public int GeneratedTokens { get; set; }
        public TimeSpan GenerationTime { get; set; }
    }

    public class ContentGenerationService : IContentGenerationService
    {
        private readonly IContextBuilder _contextBuilder;
        private readonly IPromptBuilder _promptBuilder;
        private readonly ILlmService _llmService;
        private readonly ILogger<ContentGenerationService> _logger;

        public ContentGenerationService(
            IContextBuilder contextBuilder,
            IPromptBuilder promptBuilder,
            ILlmService llmService,
            ILogger<ContentGenerationService> logger)
        {
            _contextBuilder = contextBuilder;
            _promptBuilder = promptBuilder;
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<GenerationResult> GenerateAsync(
            GenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Starting content generation for {Type}", request.Type);

            // Build context from similar posts
            var contextResult = await _contextBuilder.BuildContextAsync(
                request.CurrentText,
                request.MaxContextTokens
            );

            _logger.LogInformation("Built context from {Count} chunks ({Tokens} tokens)",
                contextResult.Chunks.Count, contextResult.TotalTokens);

            // Build prompt
            var prompt = _promptBuilder.BuildPrompt(
                request.Type,
                request.Variables,
                contextResult.CombinedContext
            );

            // Get template for parameters
            var template = _promptBuilder.GetTemplate(request.Type);

            // Generate with LLM
            var generated = await _llmService.GenerateAsync(prompt, cancellationToken);

            var result = new GenerationResult
            {
                GeneratedText = CleanGeneratedText(generated),
                SourcePosts = contextResult.Chunks
                    .Select(c => c.Source)
                    .Distinct()
                    .ToList(),
                ContextTokensUsed = contextResult.TotalTokens,
                GeneratedTokens = EstimateTokens(generated),
                GenerationTime = DateTime.UtcNow - startTime
            };

            _logger.LogInformation("Generated {Tokens} tokens in {Ms}ms",
                result.GeneratedTokens, result.GenerationTime.TotalMilliseconds);

            return result;
        }

        public async IAsyncEnumerable<string> GenerateStreamAsync(
            GenerationRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Build context
            var contextResult = await _contextBuilder.BuildContextAsync(
                request.CurrentText,
                request.MaxContextTokens
            );

            // Build prompt
            var prompt = _promptBuilder.BuildPrompt(
                request.Type,
                request.Variables,
                contextResult.CombinedContext
            );

            // Stream from LLM
            await foreach (var token in _llmService.GenerateStreamAsync(prompt, cancellationToken))
            {
                yield return token;
            }
        }

        private string CleanGeneratedText(string text)
        {
            // Remove common artifacts
            text = text.Trim();

            // Remove model's own tags if it added them
            text = text.Replace("<|assistant|>", "");
            text = text.Replace("</|assistant|>", "");

            // Remove incomplete sentences at end
            if (!text.EndsWith(".") && !text.EndsWith("!") && !text.EndsWith("?"))
            {
                var lastPeriod = text.LastIndexOf('.');
                if (lastPeriod > text.Length / 2)  // Only if more than halfway through
                {
                    text = text.Substring(0, lastPeriod + 1);
                }
            }

            return text.Trim();
        }

        private int EstimateTokens(string text)
        {
            return text.Length / 4;
        }
    }
}
```

## Integration with UI

Update the SuggestionsViewModel to use the new service:

```csharp
public partial class SuggestionsViewModel : ViewModelBase
{
    private readonly IContentGenerationService _generationService;

    [RelayCommand]
    private async Task GenerateContinuation()
    {
        IsGenerating = true;

        try
        {
            var request = new GenerationRequest
            {
                CurrentText = GetCurrentText(),
                Type = PromptType.ContinueWriting,
                MaxContextTokens = 2000
            };

            var result = await _generationService.GenerateAsync(request);

            AiSuggestion = result.GeneratedText;
            SourcePosts.Clear();
            foreach (var source in result.SourcePosts)
            {
                SourcePosts.Add(source);
            }

            StatusMessage = $"Generated in {result.GenerationTime.TotalSeconds:F1}s using {result.ContextTokensUsed} context tokens";
        }
        catch (Exception ex)
        {
            AiSuggestion = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateStructure()
    {
        var sectionTitle = PromptForSectionTitle();  // Show dialog

        var request = new GenerationRequest
        {
            CurrentText = GetCurrentText(),
            Type = PromptType.SuggestStructure,
            Variables = new Dictionary<string, string>
            {
                ["section_title"] = sectionTitle
            }
        };

        // ... similar to above
    }
}
```

## Real-Time Streaming

For better UX, stream tokens as they're generated:

```csharp
[RelayCommand]
private async Task GenerateWithStreaming()
{
    IsGenerating = true;
    AiSuggestion = "";

    try
    {
        var request = new GenerationRequest
        {
            CurrentText = GetCurrentText(),
            Type = PromptType.ContinueWriting
        };

        await foreach (var token in _generationService.GenerateStreamAsync(request))
        {
            // Update UI in real-time
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AiSuggestion += token;
            });
        }
    }
    finally
    {
        IsGenerating = false;
    }
}
```

## Quality Filtering

Not all generations are good. Let's add quality checks:

```csharp
public class QualityFilter
{
    public bool IsAcceptableQuality(string generatedText, string currentText)
    {
        // Too short
        if (generatedText.Length < 50)
        {
            return false;
        }

        // Just repeats the prompt
        if (generatedText.StartsWith(currentText.Substring(Math.Max(0, currentText.Length - 100))))
        {
            return false;
        }

        // Contains common failure modes
        var badPatterns = new[]
        {
            "I cannot",
            "I don't have",
            "As an AI",
            "I apologize"
        };

        if (badPatterns.Any(p => generatedText.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Contains markdown artifacts
        if (generatedText.Contains("```") && !generatedText.Contains("```\n"))
        {
            return false;  // Incomplete code block
        }

        return true;
    }

    public string SuggestImprovement(string generatedText)
    {
        if (generatedText.Length < 50)
            return "Too short - try increasing max_tokens";

        if (!generatedText.Contains("."))
            return "No complete sentences - check for truncation";

        return "Quality seems acceptable";
    }
}
```

## Usage Examples

### Continue Writing

```csharp
var request = new GenerationRequest
{
    CurrentText = @"# Using Docker Compose for Development

Docker Compose makes it incredibly easy to manage multiple containers. In this post, I'll show you how to set up a complete development environment with",
    Type = PromptType.ContinueWriting
};

var result = await _generationService.GenerateAsync(request);
// Result: "databases, caches, and message queues all defined in a single YAML file. We'll start with PostgreSQL and Redis, then add RabbitMQ for asynchronous messaging."
```

### Suggest Structure

```csharp
var request = new GenerationRequest
{
    CurrentText = "# Understanding Entity Framework Migrations",
    Type = PromptType.SuggestStructure,
    Variables = new Dictionary<string, string>
    {
        ["section_title"] = "Best Practices for Production"
    }
};

var result = await _generationService.GenerateAsync(request);
// Result:
// - Always review generated migrations before applying
// - Test migrations on staging environment first
// - Keep migration history in version control
// - Plan for rollback scenarios
// - Monitor migration performance on large tables
```

### Generate Code

```csharp
var request = new GenerationRequest
{
    CurrentText = "We need to configure Entity Framework with PostgreSQL",
    Type = PromptType.GenerateCode,
    Variables = new Dictionary<string, string>
    {
        ["code_request"] = "Create a DbContext setup with PostgreSQL connection"
    }
};

var result = await _generationService.GenerateAsync(request);
// Result: Complete C# code example
```

## Performance Monitoring

Track generation performance:

```csharp
public class GenerationMetrics
{
    private readonly List<GenerationResult> _recentResults = new();
    private readonly object _lock = new();

    public void RecordResult(GenerationResult result)
    {
        lock (_lock)
        {
            _recentResults.Add(result);
            if (_recentResults.Count > 100)
            {
                _recentResults.RemoveAt(0);
            }
        }
    }

    public GenerationStats GetStats()
    {
        lock (_lock)
        {
            if (_recentResults.Count == 0)
                return new GenerationStats();

            return new GenerationStats
            {
                AverageGenerationTimeMs = _recentResults.Average(r => r.GenerationTime.TotalMilliseconds),
                AverageContextTokens = _recentResults.Average(r => r.ContextTokensUsed),
                AverageGeneratedTokens = _recentResults.Average(r => r.GeneratedTokens),
                TotalGenerations = _recentResults.Count
            };
        }
    }
}

public class GenerationStats
{
    public double AverageGenerationTimeMs { get; set; }
    public double AverageContextTokens { get; set; }
    public double AverageGeneratedTokens { get; set; }
    public int TotalGenerations { get; set; }
}
```

## Summary

We've built a complete content generation system:

1. ✅ Context builder with intelligent chunk selection
2. ✅ Advanced prompt templates for different scenarios
3. ✅ Prompt builder with variable substitution
4. ✅ Content generation service orchestrating everything
5. ✅ Real-time streaming for better UX
6. ✅ Quality filtering to reject bad outputs
7. ✅ Performance monitoring
8. ✅ Integration with Windows client

## What's Next?

In **Part 8** (final part!), we'll cover:

- Auto-linking to related posts
- Deployment strategies
- Configuration management
- Continuous improvement
- Real-world usage patterns
- Troubleshooting guide
- Future enhancements

We'll wrap up the series with deployment and making this production-ready!

## Resources

- [Prompt Engineering Guide](https://www.promptingguide.ai/)
- [OpenAI Best Practices](https://platform.openai.com/docs/guides/prompt-engineering)
- [LangChain Prompts](https://python.langchain.com/docs/modules/model_io/prompts/)

See you in the final part!
