namespace TinyLLM.Models;

public class RagEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Content { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class RagDatabase
{
    public List<RagEntry> Entries { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
