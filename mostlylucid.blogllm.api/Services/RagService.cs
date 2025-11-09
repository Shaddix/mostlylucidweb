using Mostlylucid.BlogLLM.Api.Models;
using System.Diagnostics;

namespace Mostlylucid.BlogLLM.Api.Services;

public class RagService
{
    private readonly EmbeddingService _embeddingService;
    private readonly VectorStoreService _vectorStoreService;
    private readonly LlmInferenceService _llmService;
    private readonly ILogger<RagService> _logger;

    private const string DefaultSystemPrompt = @"You are a helpful AI assistant that answers questions based on the provided context from blog posts about software development, .NET, and web technologies.

Guidelines:
- Answer questions accurately based on the provided context
- If you're unsure or the context doesn't contain the answer, say so
- Be concise but thorough
- Use technical terminology appropriately
- Cite specific blog posts when relevant";

    public RagService(
        EmbeddingService embeddingService,
        VectorStoreService vectorStoreService,
        LlmInferenceService llmService,
        ILogger<RagService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<RagResponse> AskAsync(RagRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing RAG request: {Question}", request.Question);

            // 1. Generate embedding for the question
            var questionEmbedding = _embeddingService.GenerateEmbedding(request.Question);
            _logger.LogDebug("Generated question embedding");

            // 2. Search vector store for relevant context
            var searchResults = await _vectorStoreService.SearchAsync(
                queryEmbedding: questionEmbedding,
                limit: request.MaxContextChunks,
                scoreThreshold: request.ScoreThreshold,
                languageFilter: request.Language
            );

            _logger.LogInformation("Found {ResultCount} relevant chunks", searchResults.Count);

            if (searchResults.Count == 0)
            {
                _logger.LogWarning("No relevant context found for question");
                return new RagResponse
                {
                    Answer = "I don't have enough information to answer this question based on the available content.",
                    Context = new List<ContextChunk>(),
                    ProcessingTimeMs = sw.ElapsedMilliseconds,
                    TokensGenerated = 0
                };
            }

            // 3. Prepare context chunks
            var contextChunks = searchResults.Select(r => r.Text).ToList();
            var contextMetadata = searchResults.Select(r => new ContextChunk
            {
                Text = r.Text,
                DocumentTitle = r.DocumentTitle,
                SectionHeading = r.SectionHeading,
                Score = r.Score,
                Categories = r.Categories.ToList(),
                Language = r.Language
            }).ToList();

            // 4. Generate answer using LLM
            _logger.LogDebug("Generating answer with LLM");
            var (answer, tokensGenerated) = await _llmService.GenerateWithContextAsync(
                systemPrompt: DefaultSystemPrompt,
                userQuestion: request.Question,
                contextChunks: contextChunks,
                maxTokens: request.MaxTokens,
                temperature: request.Temperature,
                cancellationToken: cancellationToken
            );

            sw.Stop();

            _logger.LogInformation(
                "RAG request completed in {ElapsedMs}ms, generated {TokenCount} tokens",
                sw.ElapsedMilliseconds,
                tokensGenerated);

            return new RagResponse
            {
                Answer = answer,
                Context = contextMetadata,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                TokensGenerated = tokensGenerated
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing RAG request");
            throw;
        }
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Processing search request: {Query}", request.Query);

            // Generate embedding for the search query
            var queryEmbedding = _embeddingService.GenerateEmbedding(request.Query);

            // Search vector store
            var searchResults = await _vectorStoreService.SearchAsync(
                queryEmbedding: queryEmbedding,
                limit: request.Limit,
                scoreThreshold: request.ScoreThreshold,
                languageFilter: request.Language
            );

            sw.Stop();

            var results = searchResults.Select(r => new ContextChunk
            {
                Text = r.Text,
                DocumentTitle = r.DocumentTitle,
                SectionHeading = r.SectionHeading,
                Score = r.Score,
                Categories = r.Categories.ToList(),
                Language = r.Language
            }).ToList();

            _logger.LogInformation(
                "Search completed in {ElapsedMs}ms, found {ResultCount} results",
                sw.ElapsedMilliseconds,
                results.Count);

            return new SearchResponse
            {
                Results = results,
                TotalResults = results.Count,
                SearchTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing search request");
            throw;
        }
    }
}
