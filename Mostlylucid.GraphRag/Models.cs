using System.Text.Json.Serialization;

namespace Mostlylucid.GraphRag;

// ═══════════════════════════════════════════════════════════════════════════
// Database Result Types
// ═══════════════════════════════════════════════════════════════════════════

public record ChunkResult(string Id, string DocumentId, string Text, int ChunkIndex, float Similarity = 0);

public record EntityResult(string Id, string Name, string Type, string? Description, int MentionCount);

public record RelationshipResult(
    string Id, string SourceEntityId, string TargetEntityId, 
    string RelationshipType, string? Description, float Weight,
    string SourceName, string TargetName);

public record CommunityResult(string Id, int Level, string[] EntityIds, string? Summary);

public record DbStats(long DocumentCount, long ChunkCount, long EntityCount, long RelationshipCount, long CommunityCount);

// ═══════════════════════════════════════════════════════════════════════════
// Query Types
// ═══════════════════════════════════════════════════════════════════════════

public enum QueryMode { Local, Global, Drift }

public record QueryResult(QueryMode Mode, string Query, string Answer)
{
    public List<SourceRef> Sources { get; init; } = [];
    public List<string> Entities { get; init; } = [];
    public int CommunitiesUsed { get; init; }
}

public record SourceRef(string ChunkId, string DocumentId, string Text, double Score);

// ═══════════════════════════════════════════════════════════════════════════
// Graph Types
// ═══════════════════════════════════════════════════════════════════════════

public record Community(string Id, int Level, List<EntityResult> Entities, string? Summary = null)
{
    public string? Summary { get; set; } = Summary;
}

public record CommunityLevel(int Level, List<Community> Communities);

public record HierarchicalCommunities(List<CommunityLevel> Levels);

// ═══════════════════════════════════════════════════════════════════════════
// Pipeline Types  
// ═══════════════════════════════════════════════════════════════════════════

public enum PipelinePhase { Indexing, EntityExtraction, CommunityDetection, Summarization, Complete }

public record PipelineProgress(PipelinePhase Phase, double Percentage, string Message);

public record ProgressInfo(int Current, int Total, string Message)
{
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

public class GraphRagConfig
{
    public string DatabasePath { get; set; } = "graphrag.duckdb";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2:3b";
    public int EmbeddingDimension { get; set; } = 384;
}

// ═══════════════════════════════════════════════════════════════════════════
// Ollama API Types (shared)
// ═══════════════════════════════════════════════════════════════════════════

internal class OllamaRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";
    [JsonPropertyName("stream")] public bool Stream { get; set; }
    [JsonPropertyName("format")] public string? Format { get; set; }
    [JsonPropertyName("options")] public OllamaOptions? Options { get; set; }
}

internal class OllamaOptions
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.7;
    [JsonPropertyName("num_predict")] public int NumPredict { get; set; } = 512;
}

internal class OllamaResponse
{
    [JsonPropertyName("response")] public string Response { get; set; } = "";
}
