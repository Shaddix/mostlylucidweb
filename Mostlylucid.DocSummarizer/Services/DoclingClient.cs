using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Mostlylucid.DocSummarizer.Services;

public class DoclingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public DoclingClient(string baseUrl = "http://localhost:5001")
    {
        _baseUrl = baseUrl;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<string> ConvertAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Document not found: {filePath}");

        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        content.Add(new StreamContent(stream), "files", Path.GetFileName(filePath));

        var response = await _http.PostAsync($"{_baseUrl}/v1/convert/file", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DoclingResponse>();
        return result?.Document?.MarkdownContent ?? 
            throw new Exception("No markdown content returned from Docling");
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}

public class DoclingResponse
{
    [JsonPropertyName("document")]
    public DoclingDocument? Document { get; set; }
}

public class DoclingDocument
{
    [JsonPropertyName("md_content")]
    public string? MarkdownContent { get; set; }
}
