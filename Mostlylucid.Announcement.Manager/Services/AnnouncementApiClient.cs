using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Announcement.Manager.Services;

public class AnnouncementApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _disposeHttpClient;

    public string BaseUrl { get; set; } = "https://localhost:7240";
    public string ApiToken { get; set; } = string.Empty;

    public AnnouncementApiClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true // Dev only
        };
        _httpClient = new HttpClient(handler);
        _disposeHttpClient = true;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    // Constructor for testing with injected HttpClient
    public AnnouncementApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _disposeHttpClient = false;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    private void SetAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Api-Token");
        if (!string.IsNullOrEmpty(ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Token", ApiToken);
        }
    }

    public async Task<List<AnnouncementDto>> GetAllAnnouncementsAsync()
    {
        SetAuthHeader();
        var response = await _httpClient.GetAsync($"{BaseUrl}/api/announcement/all");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AnnouncementDto>>(_jsonOptions) ?? new List<AnnouncementDto>();
    }

    public async Task<AnnouncementDto?> GetActiveAnnouncementAsync(string language = "en")
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}/api/announcement/active?language={language}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content) || content == "null")
            return null;
        return JsonSerializer.Deserialize<AnnouncementDto>(content, _jsonOptions);
    }

    public async Task<AnnouncementDto?> GetAnnouncementAsync(string key, string language = "en")
    {
        SetAuthHeader();
        var response = await _httpClient.GetAsync($"{BaseUrl}/api/announcement/{Uri.EscapeDataString(key)}?language={language}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AnnouncementDto?>(_jsonOptions);
    }

    public async Task<AnnouncementDto> UpsertAnnouncementAsync(CreateAnnouncementRequest request)
    {
        SetAuthHeader();
        var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/announcement", content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AnnouncementDto>(_jsonOptions) ?? throw new Exception("Failed to parse response");
    }

    public async Task<bool> DeleteAnnouncementAsync(string key, string language = "en")
    {
        SetAuthHeader();
        var response = await _httpClient.DeleteAsync($"{BaseUrl}/api/announcement/{Uri.EscapeDataString(key)}?language={language}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeactivateAnnouncementAsync(string key, string language = "en")
    {
        SetAuthHeader();
        var response = await _httpClient.PostAsync($"{BaseUrl}/api/announcement/{Uri.EscapeDataString(key)}/deactivate?language={language}", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<string> UploadImageAsync(string filePath)
    {
        SetAuthHeader();
        using var fileStream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetMimeType(filePath));
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var response = await _httpClient.PostAsync($"{BaseUrl}/api/announcement/upload-image", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImageUploadResult>(_jsonOptions);
        return result?.Url ?? throw new Exception("Failed to get image URL");
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

public class ImageUploadResult
{
    public string Url { get; set; } = string.Empty;
}
