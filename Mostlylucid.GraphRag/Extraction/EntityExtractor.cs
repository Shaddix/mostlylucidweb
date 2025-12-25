using System.Text.RegularExpressions;
using Mostlylucid.GraphRag.Services;
using Mostlylucid.GraphRag.Storage;

namespace Mostlylucid.GraphRag.Extraction;

/// <summary>
/// Hybrid entity/relationship extraction using heuristics + embeddings + links.
/// Links in markdown provide explicit relationships that don't require LLM inference.
/// </summary>
public sealed class EntityExtractor
{
    private readonly GraphRagDb _db;
    private readonly EmbeddingService _embedder;
    private readonly OllamaClient? _llm;

    // Extraction patterns
    private static readonly Regex InternalLinkRx = new(@"\[([^\]]+)\]\(/blog/([^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex ExternalLinkRx = new(@"\[([^\]]+)\]\((https?://[^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex TechPascalRx = new(@"\b([A-Z][a-z]+(?:[A-Z][a-z]+)+)\b", RegexOptions.Compiled);
    private static readonly Regex TechDottedRx = new(@"\b([A-Z][a-zA-Z]*(?:\.[A-Z][a-zA-Z]*)+)\b", RegexOptions.Compiled);
    private static readonly Regex TechAcronymRx = new(@"\b([A-Z]{2,6})\b", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRx = new(@"`([^`]{2,40})`", RegexOptions.Compiled);

    private static readonly HashSet<string> KnownTech = new(StringComparer.OrdinalIgnoreCase)
    {
        "ASP.NET", "Entity Framework", "Docker", "Kubernetes", "PostgreSQL", "Redis", "Nginx", "Caddy",
        "HTMX", "Alpine.js", "Tailwind", "Blazor", "SignalR", "gRPC", "REST", "GraphQL", "YARP",
        "ONNX", "BERT", "LLM", "RAG", "Qdrant", "Ollama", "OpenAI", "Anthropic", "DuckDB",
        "C#", "JavaScript", "TypeScript", "Python", "SQL", "JSON", "YAML", "Markdown",
        "GitHub", "Azure", "AWS", "Linux", "Windows", "Umami", "Seq", "Prometheus"
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "This", "That", "These", "Those", "Here", "There", "When", "Where", "What", "Which",
        "While", "With", "Without", "Within", "About", "After", "Before", "Some", "Each",
        "TODO", "NOTE", "FIXME", "README", "API", "URL", "HTTP", "HTTPS", "HTML", "CSS"
    };

    public EntityExtractor(GraphRagDb db, EmbeddingService embedder, OllamaClient? llm = null)
    {
        _db = db;
        _embedder = embedder;
        _llm = llm;
    }

    public async Task<ExtractionStats> ExtractAsync(IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var chunks = await _db.GetAllChunksAsync();
        var stats = new ExtractionStats();

        // Phase 1: Extract candidates + links from all chunks
        var entities = new Dictionary<string, EntityCandidate>(StringComparer.OrdinalIgnoreCase);
        var coOccur = new Dictionary<(string, string), int>();
        var linkRels = new List<(string Source, string Target, string Type, string[] ChunkIds)>();

        for (int i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = chunks[i];

            // Extract entities
            foreach (var c in ExtractCandidates(chunk.Text))
            {
                if (entities.TryGetValue(c.Name, out var existing))
                {
                    existing.ChunkIds.Add(chunk.Id);
                    existing.MentionCount++;
                }
                else
                {
                    c.ChunkIds.Add(chunk.Id);
                    entities[c.Name] = c;
                }
            }

            // Extract links (explicit relationships!)
            foreach (var link in ExtractLinks(chunk.Text, chunk.Id))
                linkRels.Add(link);

            // Track co-occurrences
            var names = entities.Keys.Where(k => entities[k].ChunkIds.Contains(chunk.Id)).ToList();
            for (int j = 0; j < names.Count; j++)
                for (int k = j + 1; k < names.Count; k++)
                {
                    var pair = string.Compare(names[j], names[k], StringComparison.OrdinalIgnoreCase) < 0
                        ? (names[j], names[k]) : (names[k], names[j]);
                    coOccur[pair] = coOccur.GetValueOrDefault(pair) + 1;
                }

            if (i % 100 == 0)
                progress?.Report(new ProgressInfo(i, chunks.Count, $"Extracting: {entities.Count} entities, {linkRels.Count} links"));
        }

        stats.CandidatesFound = entities.Count;
        stats.LinksFound = linkRels.Count;

        // Phase 2: Deduplicate via embeddings
        progress?.Report(new ProgressInfo(0, 1, "Deduplicating entities..."));
        var filtered = await DeduplicateAsync(entities.Values.ToList(), ct);
        stats.EntitiesAfterDedup = filtered.Count;

        // Phase 3: Classify (LLM or heuristic)
        if (_llm != null && await _llm.IsAvailableAsync(ct))
            await ClassifyWithLlmAsync(filtered, ct);
        else
            ClassifyHeuristic(filtered);

        // Phase 4: Store entities
        progress?.Report(new ProgressInfo(0, filtered.Count, "Storing entities..."));
        foreach (var e in filtered)
            await _db.UpsertEntityAsync(EntityId(e.Name), e.Name, e.Type, e.Description, e.ChunkIds.ToArray());
        stats.EntitiesStored = filtered.Count;

        // Phase 5: Store link-based relationships (high quality!)
        progress?.Report(new ProgressInfo(0, 1, "Storing link relationships..."));
        var storedLinks = await StoreLinkRelationshipsAsync(linkRels, filtered);
        stats.LinkRelationshipsStored = storedLinks;

        // Phase 6: Store co-occurrence relationships
        var coOccurRels = await StoreCoOccurrenceRelationshipsAsync(coOccur, filtered);
        stats.CoOccurrenceRelationshipsStored = coOccurRels;

        return stats;
    }

    private List<EntityCandidate> ExtractCandidates(string text)
    {
        var candidates = new Dictionary<string, EntityCandidate>(StringComparer.OrdinalIgnoreCase);

        // Known tech terms (highest confidence)
        foreach (var term in KnownTech)
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                Add(candidates, term, "technology", 1.0);

        // PascalCase (e.g., EntityFramework)
        foreach (Match m in TechPascalRx.Matches(text))
            if (!StopWords.Contains(m.Value) && m.Value.Length >= 5)
                Add(candidates, m.Value, "technology", 0.7);

        // Dotted (e.g., ASP.NET)
        foreach (Match m in TechDottedRx.Matches(text))
            Add(candidates, m.Value, "technology", 0.9);

        // Acronyms (e.g., API, REST)
        foreach (Match m in TechAcronymRx.Matches(text))
            if (!StopWords.Contains(m.Value))
                Add(candidates, m.Value, "concept", 0.5);

        // Inline code (e.g., `DbContext`)
        foreach (Match m in InlineCodeRx.Matches(text))
        {
            var code = m.Groups[1].Value;
            if (!code.Contains('(') && !code.Contains('{') && !code.Contains('=') && !code.Contains(' '))
                Add(candidates, code, "code", 0.4);
        }

        return candidates.Values.ToList();
    }

    private IEnumerable<(string Source, string Target, string Type, string[] ChunkIds)> ExtractLinks(string text, string chunkId)
    {
        // Internal blog links -> "references" relationship
        foreach (Match m in InternalLinkRx.Matches(text))
        {
            var linkText = m.Groups[1].Value;
            var slug = m.Groups[2].Value;
            // Link text often names a concept (e.g., "semantic search", "Docker")
            if (linkText.Length >= 3 && linkText.Length <= 50)
                yield return (linkText, $"blog:{slug}", "references", new[] { chunkId });
        }

        // External links -> "links_to" relationship with domain as entity
        foreach (Match m in ExternalLinkRx.Matches(text))
        {
            var linkText = m.Groups[1].Value;
            var url = m.Groups[2].Value;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var domain = uri.Host.Replace("www.", "");
                // GitHub repos are entities
                if (domain == "github.com" && uri.Segments.Length >= 3)
                {
                    var repo = $"{uri.Segments[1].TrimEnd('/')}/{uri.Segments[2].TrimEnd('/')}";
                    yield return (linkText, $"github:{repo}", "links_to", new[] { chunkId });
                }
                else
                    yield return (linkText, $"site:{domain}", "links_to", new[] { chunkId });
            }
        }
    }

    private async Task<List<EntityCandidate>> DeduplicateAsync(List<EntityCandidate> candidates, CancellationToken ct)
    {
        var significant = candidates.Where(c => c.MentionCount >= 2 || c.Confidence >= 0.8)
            .OrderByDescending(c => c.MentionCount * c.Confidence).ToList();

        if (significant.Count <= 1) return significant;

        var embeddings = await _embedder.EmbedBatchAsync(significant.Select(c => c.Name), ct);
        var merged = new List<EntityCandidate>();
        var used = new HashSet<int>();

        for (int i = 0; i < significant.Count; i++)
        {
            if (used.Contains(i)) continue;
            var canonical = significant[i];
            used.Add(i);

            for (int j = i + 1; j < significant.Count; j++)
            {
                if (used.Contains(j)) continue;
                if (CosineSim(embeddings[i], embeddings[j]) > 0.85 || StringSim(canonical.Name, significant[j].Name) > 0.8)
                {
                    canonical.MentionCount += significant[j].MentionCount;
                    canonical.ChunkIds.UnionWith(significant[j].ChunkIds);
                    used.Add(j);
                }
            }
            merged.Add(canonical);
        }
        return merged;
    }

    private async Task ClassifyWithLlmAsync(List<EntityCandidate> entities, CancellationToken ct)
    {
        var list = string.Join("\n", entities.Take(80).Select(e => $"- {e.Name} ({e.MentionCount}x)"));
        var prompt = $"""
            Classify these technical entities. Return: Name|type|description (one per line)
            Types: technology, framework, library, language, tool, service, concept, pattern
            
            Entities:
            {list}
            """;
        var response = await _llm!.GenerateAsync(prompt, 0.3, ct);
        ParseClassification(entities, response);
    }

    private static void ClassifyHeuristic(List<EntityCandidate> entities)
    {
        foreach (var e in entities)
            e.Type = InferType(e.Name);
    }

    private static string InferType(string name)
    {
        var l = name.ToLowerInvariant();
        if (l.EndsWith(".js") || l.EndsWith("js")) return "library";
        if (l.Contains(".net") || l.Contains("framework")) return "framework";
        if (l.EndsWith("db") || l == "postgresql" || l == "redis" || l == "qdrant") return "database";
        if (l == "docker" || l == "kubernetes" || l == "nginx" || l == "caddy") return "tool";
        return "technology";
    }

    private static void ParseClassification(List<EntityCandidate> entities, string response)
    {
        var lookup = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length >= 2 && lookup.TryGetValue(parts[0].Trim().TrimStart('-', ' '), out var e))
            {
                e.Type = parts[1].Trim().ToLowerInvariant();
                if (parts.Length >= 3) e.Description = parts[2].Trim();
            }
        }
    }

    private async Task<int> StoreLinkRelationshipsAsync(
        List<(string Source, string Target, string Type, string[] ChunkIds)> links,
        List<EntityCandidate> entities)
    {
        var entityNames = entities.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var count = 0;

        // Group by source-target pair
        var grouped = links.GroupBy(l => (l.Source, l.Target, l.Type));
        foreach (var g in grouped)
        {
            var (source, target, relType) = g.Key;
            var chunkIds = g.SelectMany(x => x.ChunkIds).Distinct().ToArray();

            // Create target entity if it's a blog/github reference
            string targetId;
            if (target.StartsWith("blog:") || target.StartsWith("github:") || target.StartsWith("site:"))
            {
                targetId = EntityId(target);
                var targetType = target.StartsWith("blog:") ? "document" : target.StartsWith("github:") ? "repository" : "website";
                await _db.UpsertEntityAsync(targetId, target, targetType, null, chunkIds);
            }
            else if (entityNames.Contains(target))
                targetId = EntityId(target);
            else
                continue;

            // Source must be an entity or we create it from link text
            var sourceId = EntityId(source);
            if (!entityNames.Contains(source))
                await _db.UpsertEntityAsync(sourceId, source, "concept", null, chunkIds);

            await _db.UpsertRelationshipAsync($"r_{sourceId}_{targetId}_{relType}", sourceId, targetId, relType, null, chunkIds);
            count++;
        }
        return count;
    }

    private async Task<int> StoreCoOccurrenceRelationshipsAsync(
        Dictionary<(string, string), int> coOccur,
        List<EntityCandidate> entities)
    {
        var entityNames = entities.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
        var count = 0;

        var significant = coOccur.Where(kv => kv.Value >= 2).OrderByDescending(kv => kv.Value).Take(300);
        foreach (var ((a, b), occ) in significant)
        {
            if (!entityNames.TryGetValue(a, out var ea) || !entityNames.TryGetValue(b, out var eb))
                continue;

            var srcId = EntityId(a);
            var tgtId = EntityId(b);
            var relType = InferRelType(ea.Type, eb.Type);
            var chunkIds = ea.ChunkIds.Intersect(eb.ChunkIds).ToArray();

            await _db.UpsertRelationshipAsync($"r_{srcId}_{tgtId}_{relType}", srcId, tgtId, relType, null, chunkIds);
            count++;
        }
        return count;
    }

    private static string InferRelType(string srcType, string tgtType) =>
        (srcType, tgtType) switch
        {
            ("framework", "library") => "uses",
            ("library", "framework") => "part_of",
            _ when srcType == tgtType => "related_to",
            _ => "associated_with"
        };

    private static void Add(Dictionary<string, EntityCandidate> d, string name, string type, double conf)
    {
        if (d.TryGetValue(name, out var e)) { if (conf > e.Confidence) { e.Type = type; e.Confidence = conf; } }
        else d[name] = new EntityCandidate { Name = name, Type = type, Confidence = conf };
    }

    private static string EntityId(string name) => $"e_{name.ToLowerInvariant().Replace(" ", "_").Replace(".", "_").Replace("-", "_").Replace(":", "_").Replace("/", "_")}";
    private static float CosineSim(float[] a, float[] b) { float dot = 0, na = 0, nb = 0; for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; } return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb)); }
    private static double StringSim(string a, string b) { var d = Levenshtein(a.ToLower(), b.ToLower()); return 1.0 - (double)d / Math.Max(a.Length, b.Length); }
    private static int Levenshtein(string a, string b) { var m = a.Length; var n = b.Length; var d = new int[m + 1, n + 1]; for (int i = 0; i <= m; i++) d[i, 0] = i; for (int j = 0; j <= n; j++) d[0, j] = j; for (int i = 1; i <= m; i++) for (int j = 1; j <= n; j++) d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1)); return d[m, n]; }
}

public class EntityCandidate
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "concept";
    public string? Description { get; set; }
    public double Confidence { get; set; }
    public int MentionCount { get; set; } = 1;
    public HashSet<string> ChunkIds { get; set; } = [];
}

public record ExtractionStats
{
    public int CandidatesFound { get; set; }
    public int LinksFound { get; set; }
    public int EntitiesAfterDedup { get; set; }
    public int EntitiesStored { get; set; }
    public int LinkRelationshipsStored { get; set; }
    public int CoOccurrenceRelationshipsStored { get; set; }
    public int TotalRelationships => LinkRelationshipsStored + CoOccurrenceRelationshipsStored;
}
