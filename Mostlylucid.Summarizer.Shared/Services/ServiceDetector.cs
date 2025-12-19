namespace Mostlylucid.Summarizer.Shared.Services;

/// <summary>
/// Detect available services (Ollama, etc.)
/// </summary>
public static class ServiceDetector
{
    /// <summary>
    /// Check if Ollama is running
    /// </summary>
    public static async Task<bool> IsOllamaAvailableAsync(
        string baseUrl = "http://localhost:11434",
        CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{baseUrl}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if Qdrant is running
    /// </summary>
    public static async Task<bool> IsQdrantAvailableAsync(
        string host = "localhost",
        int port = 6333,
        CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"http://{host}:{port}/collections", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if Docling is running
    /// </summary>
    public static async Task<bool> IsDoclingAvailableAsync(
        string baseUrl = "http://localhost:5001",
        CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{baseUrl}/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect all services and return status
    /// </summary>
    public static async Task<ServiceStatus> DetectAllAsync(CancellationToken ct = default)
    {
        var ollamaTask = IsOllamaAvailableAsync(ct: ct);
        var qdrantTask = IsQdrantAvailableAsync(ct: ct);
        var doclingTask = IsDoclingAvailableAsync(ct: ct);

        await Task.WhenAll(ollamaTask, qdrantTask, doclingTask);

        return new ServiceStatus
        {
            OllamaAvailable = await ollamaTask,
            QdrantAvailable = await qdrantTask,
            DoclingAvailable = await doclingTask
        };
    }
}

public class ServiceStatus
{
    public bool OllamaAvailable { get; set; }
    public bool QdrantAvailable { get; set; }
    public bool DoclingAvailable { get; set; }

    public override string ToString()
    {
        var services = new List<string>();
        if (OllamaAvailable) services.Add("Ollama✓");
        if (QdrantAvailable) services.Add("Qdrant✓");
        if (DoclingAvailable) services.Add("Docling✓");
        
        return services.Count > 0 
            ? string.Join(" ", services) 
            : "No services detected";
    }
}
