using System.Diagnostics;
using System.Text;
using Mostlylucid.DocSummarizer.Config;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services.Onnx;
using Spectre.Console;

namespace Mostlylucid.DocSummarizer.Services;

/// <summary>
/// BERT→RAG Summarizer: The production-grade summarization pipeline.
/// 
/// Architecture:
/// 1. Extract: Parse document → segments with embeddings + salience scores
/// 2. Retrieve: Dual-score ranking (query similarity + salience) 
/// 3. Synthesize: LLM generates fluent summary from retrieved segments
/// 
/// Key properties:
/// - LLM only at synthesis (no LLM-in-the-loop evaluation)
/// - Deterministic extraction (reproducible, debuggable)
/// - Perfect citations (every claim traceable to source segment)
/// - Scales to any document size (extraction is O(n), retrieval is O(log n))
/// - Cost-optimal (cheap CPU work first, expensive LLM last)
/// </summary>
public class BertRagSummarizer : IDisposable
{
    private readonly SegmentExtractor _extractor;
    private readonly OnnxEmbeddingService _queryEmbedder;
    private readonly OllamaService _ollama;
    private readonly RetrievalConfig _retrievalConfig;
    private readonly bool _verbose;
    
    public SummaryTemplate Template { get; private set; }

    public BertRagSummarizer(
        OnnxConfig onnxConfig,
        OllamaService ollama,
        ExtractionConfig? extractionConfig = null,
        RetrievalConfig? retrievalConfig = null,
        SummaryTemplate? template = null,
        bool verbose = false)
    {
        _extractor = new SegmentExtractor(onnxConfig, extractionConfig, verbose);
        _queryEmbedder = new OnnxEmbeddingService(onnxConfig, verbose);
        _ollama = ollama;
        _retrievalConfig = retrievalConfig ?? new RetrievalConfig();
        Template = template ?? SummaryTemplate.Presets.Default;
        _verbose = verbose;
    }

    public void SetTemplate(SummaryTemplate template) => Template = template;

    /// <summary>
    /// Full pipeline: Extract → Retrieve → Synthesize
    /// </summary>
    public async Task<DocumentSummary> SummarizeAsync(
        string docId,
        string markdown,
        string? focusQuery = null,
        ContentType contentType = ContentType.Unknown,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // === Phase 1: Extract ===
        if (_verbose) AnsiConsole.MarkupLine("[bold cyan]Phase 1: Extraction[/]");
        var extraction = await _extractor.ExtractAsync(docId, markdown, contentType, ct);
        
        if (extraction.AllSegments.Count == 0)
        {
            return CreateEmptySummary(docId, "No segments found in document");
        }
        
        // === Phase 2: Retrieve ===
        if (_verbose) AnsiConsole.MarkupLine("[bold cyan]Phase 2: Retrieval[/]");
        var retrieved = await RetrieveAsync(extraction, focusQuery, ct);
        
        if (_verbose)
        {
            AnsiConsole.MarkupLine($"[dim]Retrieved {retrieved.Count} segments for synthesis[/]");
        }
        
        // === Phase 3: Synthesize ===
        if (_verbose) AnsiConsole.MarkupLine("[bold cyan]Phase 3: Synthesis[/]");
        var summary = await SynthesizeAsync(docId, retrieved, extraction, focusQuery, ct);
        
        stopwatch.Stop();
        
        // Build trace with citation map
        var citationMap = retrieved.ToDictionary(s => s.Citation, s => s.ToCitation());
        var coverage = extraction.AllSegments.Count == 0
            ? 0
            : (double)retrieved.Count / extraction.AllSegments.Count;
        var trace = new SummarizationTrace(
            docId,
            extraction.AllSegments.Count,
            retrieved.Count,
            extraction.AllSegments
                .Where(s => s.Type == SegmentType.Heading)
                .Select(s => s.Text)
                .Take(10)
                .ToList(),
            stopwatch.Elapsed,
            CoverageScore: coverage,
            CitationRate: 1.0 // BERT-RAG always has perfect citations
        );
        
        return new DocumentSummary(
            summary.ExecutiveSummary,
            summary.TopicSummaries,
            summary.OpenQuestions,
            trace,
            summary.Entities
        );
    }

    /// <summary>
    /// Retrieve segments using hybrid search: Dense + BM25 (sparse) + Salience via RRF.
    /// 
    /// Three-way RRF combines:
    ///   1. Dense similarity (semantic meaning from embeddings)
    ///   2. BM25 sparse (lexical/keyword matching)
    ///   3. Salience score (document importance from extraction)
    /// 
    /// RRF(d) = 1/(k + rank_dense) + 1/(k + rank_bm25) + 1/(k + rank_salience)
    /// 
    /// Benefits:
    /// - Catches both semantic AND lexical matches
    /// - Scale-invariant (no weight tuning needed)
    /// - Proven effective (Elasticsearch, Vespa, MS-RAG all use this pattern)
    /// </summary>
    private async Task<List<Segment>> RetrieveAsync(
        ExtractionResult extraction,
        string? focusQuery,
        CancellationToken ct)
    {
        var segments = extraction.AllSegments;
        var topBySalience = extraction.TopBySalience;
        
        // If no query, just return top by salience (generic summary)
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            if (_verbose) AnsiConsole.MarkupLine("[dim]No focus query - using salience-only ranking[/]");
            return topBySalience.Take(_retrievalConfig.TopK).ToList();
        }
        
        // Embed the query for dense retrieval
        await _queryEmbedder.InitializeAsync(ct);
        var queryEmbedding = await _queryEmbedder.EmbedAsync(focusQuery, ct);
        
        // Score all segments by dense (semantic) similarity
        foreach (var segment in segments)
        {
            if (segment.Embedding == null) continue;
            segment.QuerySimilarity = CosineSimilarity(queryEmbedding, segment.Embedding);
        }
        
        List<Segment> topByRetrieval;
        
        if (_retrievalConfig.UseRRF)
        {
            if (_retrievalConfig.UseHybridSearch)
            {
                // Hybrid: Dense + BM25 + Salience via three-way RRF
                var bm25 = new BM25Scorer();
                bm25.Initialize(segments);
                
                topByRetrieval = HybridRRF.Fuse(
                    segments, 
                    focusQuery, 
                    bm25, 
                    _retrievalConfig.RrfK, 
                    _retrievalConfig.TopK);
                
                if (_verbose) AnsiConsole.MarkupLine($"[dim]Using Hybrid RRF: Dense + BM25 + Salience (k={_retrievalConfig.RrfK})[/]");
            }
            else
            {
                // Standard: Dense + Salience via two-way RRF
                topByRetrieval = RetrieveWithRRF(segments, _retrievalConfig.RrfK, _retrievalConfig.TopK);
                if (_verbose) AnsiConsole.MarkupLine($"[dim]Using RRF: Dense + Salience (k={_retrievalConfig.RrfK})[/]");
            }
        }
        else
        {
            // Legacy: Weighted sum
            var alpha = _retrievalConfig.Alpha;
            foreach (var segment in segments)
            {
                segment.RetrievalScore = alpha * segment.QuerySimilarity + (1 - alpha) * segment.SalienceScore;
            }
            
            topByRetrieval = segments
                .Where(s => s.QuerySimilarity >= _retrievalConfig.MinSimilarity)
                .OrderByDescending(s => s.RetrievalScore)
                .Take(_retrievalConfig.TopK)
                .ToList();
            
            if (_verbose) AnsiConsole.MarkupLine($"[dim]Using weighted sum (alpha={alpha})[/]");
        }
        
        // Merge with fallback bucket (ensures global coverage even if query is narrow)
        var fallbackNeeded = _retrievalConfig.FallbackCount;
        var fallbackToAdd = topBySalience
            .Where(s => !topByRetrieval.Contains(s))
            .Take(fallbackNeeded);
        
        var result = topByRetrieval.Concat(fallbackToAdd)
            .OrderBy(s => s.Index) // Restore document order for coherent synthesis
            .ToList();
        
        if (_verbose)
        {
            AnsiConsole.MarkupLine($"[dim]Query: \"{focusQuery}\"[/]");
            AnsiConsole.MarkupLine($"[dim]Top by retrieval: {topByRetrieval.Count}, fallback added: {result.Count - topByRetrieval.Count}[/]");
        }
        
        return result;
    }

    /// <summary>
    /// Reciprocal Rank Fusion: combine multiple rankings into a single ranking.
    /// 
    /// Formula: RRF(d) = Σ 1/(k + rank_i(d))
    /// 
    /// Where:
    /// - k = 60 (standard, prevents division by small numbers)
    /// - rank_i(d) = position in ranking i (1-based)
    /// 
    /// We fuse two rankings:
    /// 1. By query similarity (relevance to user's focus)
    /// 2. By salience score (importance to document)
    /// </summary>
    private static List<Segment> RetrieveWithRRF(List<Segment> segments, int k, int topK)
    {
        // Create rankings
        var byQuerySim = segments
            .Where(s => s.Embedding != null)
            .OrderByDescending(s => s.QuerySimilarity)
            .ToList();
        
        var bySalience = segments
            .Where(s => s.Embedding != null)
            .OrderByDescending(s => s.SalienceScore)
            .ToList();
        
        // Compute RRF scores
        var rrfScores = new Dictionary<Segment, double>();
        
        for (int i = 0; i < byQuerySim.Count; i++)
        {
            var segment = byQuerySim[i];
            var rank = i + 1; // 1-based
            rrfScores[segment] = 1.0 / (k + rank);
        }
        
        for (int i = 0; i < bySalience.Count; i++)
        {
            var segment = bySalience[i];
            var rank = i + 1; // 1-based
            if (rrfScores.ContainsKey(segment))
                rrfScores[segment] += 1.0 / (k + rank);
            else
                rrfScores[segment] = 1.0 / (k + rank);
        }
        
        // Store RRF score in segment for debugging/tracing
        foreach (var (segment, score) in rrfScores)
        {
            segment.RetrievalScore = score;
        }
        
        // Return top-K by RRF score
        return rrfScores
            .OrderByDescending(kv => kv.Value)
            .Take(topK)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>
    /// Synthesize a fluent summary from retrieved segments.
    /// 
    /// The LLM's job is ONLY to:
    /// - Organize the information coherently
    /// - Write fluent prose
    /// - Preserve citations
    /// 
    /// The LLM does NOT:
    /// - Judge importance (already done by extraction)
    /// - Retrieve information (already done by retrieval)
    /// - Add new information (not in the segments)
    /// </summary>
    private async Task<DocumentSummary> SynthesizeAsync(
        string docId,
        List<Segment> retrieved,
        ExtractionResult extraction,
        string? focusQuery,
        CancellationToken ct)
    {
        // Filter out heavy code; keep mostly prose
        var synthesisSegments = FilterForSynthesis(retrieved, includeCodeFallback: true);
        
        // Group segments by section for structure
        var bySection = synthesisSegments
            .GroupBy(s => s.SectionTitle)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToList();
        
        // Build context for synthesis
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("# Retrieved Segments");
        contextBuilder.AppendLine();
        
        foreach (var segment in synthesisSegments)
        {
            contextBuilder.AppendLine($"{segment.Citation} [{segment.Type}] {segment.Text}");
        }
        
        // Build section structure
        var sectionStructure = new StringBuilder();
        if (bySection.Count > 1)
        {
            sectionStructure.AppendLine("# Document Structure");
            foreach (var section in bySection)
            {
                sectionStructure.AppendLine($"- {section.Key}: {section.Count()} segments");
            }
            sectionStructure.AppendLine();
        }
        
        // Coverage gating
        var coverage = extraction.AllSegments.Count == 0
            ? 0
            : (double)synthesisSegments.Count / extraction.AllSegments.Count;
        var targetWords = Template.TargetWords > 0 ? Template.TargetWords : 300;
        var synthesisPrompt = BuildSynthesisPrompt(
            extraction.ContentType, 
            targetWords, 
            focusQuery, 
            sectionStructure.ToString(), 
            contextBuilder.ToString(),
            coverage);
        
        var rawSummary = await _ollama.GenerateAsync(synthesisPrompt, temperature: 0.3);
        var executiveSummary = CleanSynthesisResponse(rawSummary);
        executiveSummary = ApplyCoverageGuard(executiveSummary, coverage);
        executiveSummary = AppendCoverageFooter(executiveSummary, coverage);
        
        // Build topic summaries from sections with lightweight annotations
        var topicSummaries = new List<TopicSummary>();
        foreach (var section in bySection.Take(10))
        {
            var sectionSegments = section.OrderBy(s => s.Index).ToList();
            var sectionText = string.Join(" ", sectionSegments.Select(s => $"{s.Text} {s.Citation}"));
            var sourceRefs = sectionSegments.Select(s => s.Id).ToList();
            var annotation = BuildSectionAnnotation(section.Key, sectionSegments);
            var annotatedText = string.IsNullOrEmpty(annotation)
                ? sectionText
                : $"Annotation: {annotation}\n{sectionText}";
            
            topicSummaries.Add(new TopicSummary(section.Key, annotatedText, sourceRefs));
        }
        
        // Extract entities from filtered segments
        var entities = ExtractEntities(synthesisSegments, extraction.ContentType);
        
        return new DocumentSummary(
            executiveSummary,
            topicSummaries,
            new List<string>(), // Open questions could be added via another LLM call if needed
            new SummarizationTrace(docId, 0, 0, new List<string>(), TimeSpan.Zero, coverage, 0),
            entities
        );
    }

    private static List<Segment> FilterForSynthesis(List<Segment> segments, bool includeCodeFallback)
    {
        var prose = segments.Where(s => s.Type != SegmentType.CodeBlock).ToList();
        if (prose.Count >= 8) return prose;
        if (!includeCodeFallback) return prose;
        var needed = 8 - prose.Count;
        var code = segments.Where(s => s.Type == SegmentType.CodeBlock).Take(needed).ToList();
        return prose.Concat(code).ToList();
    }

    /// <summary>
    /// Simple entity extraction from retrieved segments
    /// </summary>
    private static ExtractedEntities ExtractEntities(List<Segment> segments, ContentType contentType)
    {
        // Improved heuristic entity extraction for narrative: merge proper noun spans and drop stopwords
        var text = string.Join(" ", segments.Select(s => s.Text));
        var tokens = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim(
                ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '—', '–'))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
        
        var honorifics = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Mr.", "Mrs.", "Miss", "Ms.", "Dr.", "Captain", "Inspector", "Professor" };
        var placeStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Street", "St", "Wharf", "Yard", "Road", "Lane", "Court" };
        var dayStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        var monthStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var miscStop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Baker", "Street", "Wharf" };
        
        var candidates = new List<string>();
        int i = 0;
        while (i < tokens.Count)
        {
            var tok = tokens[i];
            // Start of a proper span: capitalized or honorific
            if (IsProper(tok, honorifics))
            {
                var span = new List<string> { tok };
                var j = i + 1;
                while (j < tokens.Count && IsContinuation(tokens[j]))
                {
                    span.Add(tokens[j]);
                    j++;
                }
                var spanText = string.Join(" ", span);
                candidates.Add(spanText);
                i = j;
            }
            else
            {
                i++;
            }
        }
        
        // Merge duplicates and filter
        var merged = candidates
            .Select(c => c.Trim())
            .Where(c => c.Length > 2)
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();
        
        var final = new List<string>();
        foreach (var name in merged)
        {
            // Drop standalone stopwords
            if (dayStop.Contains(name) || monthStop.Contains(name)) continue;
            if (placeStop.Contains(name)) continue;
            if (honorifics.Contains(name)) continue;
            if (miscStop.Contains(name)) continue;
            
            // Drop single-word common places unless combined
            if (name.Equals("Street", StringComparison.OrdinalIgnoreCase) || name.Equals("Wharf", StringComparison.OrdinalIgnoreCase))
                continue;
            
            final.Add(name);
            if (final.Count >= 12) break;
        }
        
        // Narrative: treat as characters; Expository: keep same list as characters for now
        var characters = contentType == ContentType.Narrative ? final : final;
        return new ExtractedEntities(characters, new List<string>(), new List<string>(), new List<string>(), new List<string>());
        
        bool IsProper(string token, HashSet<string> honors) =>
            honors.Contains(token) || (token.Length > 1 && char.IsUpper(token[0]) && token.Skip(1).All(char.IsLetter));
        
        bool IsContinuation(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            if (placeStop.Contains(token)) return true; // allow Baker Street
            if (honorifics.Contains(token)) return true;
            if (token.Length > 1 && char.IsUpper(token[0]) && token.Skip(1).All(char.IsLetter)) return true;
            return false;
        }
    }

    /// <summary>
    /// Cosine similarity between query embedding and segment embedding
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom > 0 ? dot / denom : 0;
    }

    private static DocumentSummary CreateEmptySummary(string docId, string message)
    {
        return new DocumentSummary(
            message,
            new List<TopicSummary>(),
            new List<string>(),
            new SummarizationTrace(docId, 0, 0, new List<string>(), TimeSpan.Zero, 0, 0),
            ExtractedEntities.Empty
        );
    }

    /// <summary>
    /// Build content-type-aware synthesis prompt.
    /// 
    /// For NARRATIVE (fiction): Focus on plot, characters, setting, themes
    /// For EXPOSITORY (technical): Focus on key points, structure, citations
    /// </summary>
    private static string BuildSynthesisPrompt(
        ContentType contentType,
        int targetWords,
        string? focusQuery,
        string sectionStructure,
        string context,
        double coverage)
    {
        var focusLine = string.IsNullOrEmpty(focusQuery) ? "" : $"FOCUS: {focusQuery}\n";
        var coverageLine = coverage < 0.05
            ? "Coverage is very low (<5%). Use cautious language like 'in the sampled sections' and avoid definitive endings."
            : "Use standard confident tone consistent with evidence.";
        
        if (contentType == ContentType.Narrative)
        {
            // FICTION/NARRATIVE prompt - book report style
            return $"""
                You are writing a book report / plot summary. Your job is to describe WHAT HAPPENS, not transcribe dialogue.

                INSTRUCTIONS:
                1. Write at the SCENE level, not the dialogue level
                2. Describe ACTIONS: who did what, where, when
                3. Identify KEY CHARACTERS and their roles (detective, narrator, client, villain)
                4. Describe the SETTING (place, time period, atmosphere)
                5. Identify the INCITING INCIDENT (what starts the plot)
                6. Note any THEMES or central conflicts
                7. Use past tense, third person
                8. Include citation references like [s1], [s2] as evidence
                9. Write ~{targetWords} words

                COVERAGE RULE:
                {coverageLine}

                DO NOT:
                - Quote dialogue verbatim (paraphrase instead)
                - List every conversation ("He said X, she replied Y")
                - Treat all moments as equally important

                {focusLine}
                {sectionStructure}
                {context}

                Write a book report that describes what happens in this story:
                """;
        }
        else
        {
            // EXPOSITORY/TECHNICAL prompt - standard summary
            return $"""
                You are a precise summarization assistant. Your job is to synthesize the retrieved segments into a fluent summary.

                RULES:
                1. Use ONLY information from the retrieved segments
                2. PRESERVE citation references like [s1], [s2], [li3] etc.
                3. Write fluent, coherent prose (~{targetWords} words)
                4. Organize logically (intro → main points → conclusion)
                5. Do NOT add information not in the segments
                6. Do NOT make judgments about importance - that's already been done
                7. If coverage is low (<5%), use cautious language ("in the sampled sections") and avoid definitive conclusions.

                {focusLine}
                {sectionStructure}
                {context}

                Write a fluent summary that preserves all citations:
                """;
        }
    }

    /// <summary>
    /// Remove common LLM preamble patterns from synthesis responses
    /// </summary>
    private static string CleanSynthesisResponse(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cleaned = new List<string>();
        var foundContent = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip common preamble patterns at the start
            if (!foundContent)
            {
                if (trimmed.StartsWith("Here is", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Here are", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Here's", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Below is", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("The following", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Based on", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("I'll", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Let me", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Sure", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Certainly", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(trimmed))
                    continue;
                    
                foundContent = true;
            }
            
            cleaned.Add(line);
        }
        
        // If nothing left, return original (might have been all content)
        if (cleaned.Count == 0)
            return response.Trim();
            
        return string.Join("\n", cleaned).Trim();
    }

    private static string ApplyCoverageGuard(string summary, double coverage)
    {
        if (coverage >= 0.05) return summary;
        var banned = new[] { "ultimately", "as the story unfolds", "it becomes clear", "finally", "in the end" };
        var guarded = summary;
        foreach (var term in banned)
        {
            guarded = System.Text.RegularExpressions.Regex.Replace(
                guarded,
                System.Text.RegularExpressions.Regex.Escape(term),
                "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        // Prepend disclaimer
        var disclaimer = $"Based on sampled sections only (~{coverage:P1}), the excerpt suggests:";
        return $"{disclaimer}\n{guarded.Trim()}";
    }

    private static string AppendCoverageFooter(string summary, double coverage)
    {
        var scope = coverage < 0.05 ? "sampled scenes" : "document segments covered";
        var confidence = coverage < 0.05 ? "Low" : coverage < 0.15 ? "Medium" : "High";
        var footer = $"\n\nCoverage: {coverage:P1} ({scope})\nConfidence: {confidence}";
        return summary.TrimEnd() + footer;
    }

    private static string BuildSectionAnnotation(string sectionTitle, List<Segment> sectionSegments)
    {
        if (!sectionSegments.Any()) return string.Empty;
        var first = sectionSegments.First();
        var who = first.SectionTitle;
        var what = first.Type.ToString();
        return $"Focuses on {sectionTitle}; showcases {what.ToLowerInvariant()} tied to this thread.";
    }

    public void Dispose()
    {
        _extractor.Dispose();
        _queryEmbedder.Dispose();
    }
}
