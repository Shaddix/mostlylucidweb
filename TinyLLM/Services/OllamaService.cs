using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TinyLLM.Services;

public class OllamaModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("modified_at")]
    public DateTime ModifiedAt { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = "";

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
}

public class OllamaModelDetails
{
    [JsonPropertyName("parent_model")]
    public string ParentModel { get; set; } = "";

    [JsonPropertyName("format")]
    public string Format { get; set; } = "";

    [JsonPropertyName("family")]
    public string Family { get; set; } = "";

    [JsonPropertyName("families")]
    public List<string> Families { get; set; } = new();

    [JsonPropertyName("parameter_size")]
    public string ParameterSize { get; set; } = "";

    [JsonPropertyName("quantization_level")]
    public string QuantizationLevel { get; set; } = "";
}

public class OllamaListResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel> Models { get; set; } = new();
}

public class OllamaPullRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;
}

public class OllamaPullProgress
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = "";

    [JsonPropertyName("total")]
    public long Total { get; set; }

    [JsonPropertyName("completed")]
    public long Completed { get; set; }
}

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public OllamaService(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(30)
        };
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OllamaModel>> ListModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<OllamaListResponse>("/api/tags");
            return response?.Models ?? new List<OllamaModel>();
        }
        catch
        {
            return new List<OllamaModel>();
        }
    }

    public async Task<bool> PullModelAsync(
        string modelName,
        IProgress<(string status, long completed, long total, double percentage)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new OllamaPullRequest
            {
                Name = modelName,
                Stream = true
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("/api/pull", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var progressUpdate = JsonSerializer.Deserialize<OllamaPullProgress>(line);
                    if (progressUpdate != null && progress != null)
                    {
                        var percentage = progressUpdate.Total > 0
                            ? (double)progressUpdate.Completed / progressUpdate.Total * 100
                            : 0;

                        progress.Report((
                            progressUpdate.Status,
                            progressUpdate.Completed,
                            progressUpdate.Total,
                            percentage
                        ));
                    }
                }
                catch
                {
                    // Ignore JSON parsing errors for individual progress lines
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> HasModelAsync(string modelName)
    {
        var models = await ListModelsAsync();
        return models.Any(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
    }

    public string GetModelPath(string modelName)
    {
        // Ollama stores models in its own directory structure
        // We return a special path that indicates it's an Ollama model
        return $"ollama://{modelName}";
    }
}
