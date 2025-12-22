using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.SegmentCommerce.SampleData.Models;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

public class PersonaGenerator
{
    private readonly HttpClient _httpClient;
    private readonly GenerationConfig _config;
    private readonly Random _random = new();

    public PersonaGenerator(HttpClient httpClient, GenerationConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(config.OllamaBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.OllamaTimeoutSeconds);
    }

    public async Task EnrichAsync(List<GeneratedProfile> profiles, CancellationToken cancellationToken = default)
    {
        if (!await IsOllamaAvailableAsync(cancellationToken))
        {
            foreach (var p in profiles)
            {
                FallbackPersona(p);
            }
            return;
        }

        var batch = profiles.Select(p => new
        {
            profile_key = p.ProfileKey,
            interests = p.Interests,
            signals = p.Signals.Select(s => new { s.SignalType, s.Category, s.Weight }).ToList()
        }).ToList();

        var prompt = BuildPrompt(batch);
        var response = await CallOllamaAsync(prompt, cancellationToken);
        var parsed = ParseResponse(response);

        foreach (var p in profiles)
        {
            if (parsed.TryGetValue(p.ProfileKey, out var persona))
            {
                ApplyPersona(p, persona);
            }
            else
            {
                FallbackPersona(p);
            }
        }
    }

    private void ApplyPersona(GeneratedProfile profile, Persona persona)
    {
        profile.DisplayName = persona.Name;
        profile.Bio = persona.Bio;
        profile.Age = persona.Age;
        profile.BirthDate = persona.BirthDate;
        profile.Likes = persona.Likes ?? [];
        profile.Dislikes = persona.Dislikes ?? [];
    }

    private void FallbackPersona(GeneratedProfile profile)
    {
        var names = new[] { "Alex", "Jamie", "Taylor", "Jordan", "Morgan", "Casey", "Riley", "Cameron" };
        profile.DisplayName = profile.DisplayName ?? $"{names[_random.Next(names.Length)]} {(_random.Next(2)==0 ? "Smith" : "Lee" )}";
        profile.Age ??= _random.Next(21, 60);
        profile.BirthDate ??= DateTime.UtcNow.AddYears(-profile.Age!.Value).AddDays(_random.Next(-180, 180));
        profile.Bio ??= "Privacy-first shopper exploring curated picks.";
        if (profile.Likes.Count == 0)
        {
            profile.Likes = profile.Interests.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key.Replace("-", " ")).ToList();
        }
        if (profile.Dislikes.Count == 0)
        {
            profile.Dislikes = new List<string> { "pushy upsells", "slow shipping" };
        }
    }

    private async Task<bool> IsOllamaAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resp = await _httpClient.GetAsync("/api/tags", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string BuildPrompt(object batch)
    {
        var json = JsonSerializer.Serialize(batch, new JsonSerializerOptions { WriteIndented = true });
        return """
You are generating concise ecommerce shopper personas. For each profile, return a JSON object keyed by profile_key with fields: name, bio, age, birth_date (ISO), likes (array), dislikes (array).
- Make bios short (1-2 sentences), realistic, no PII.
- Likes/dislikes should align with interests provided.
- Age 21-65, birth_date consistent with age.
- Keep it clean and neutral.

Input profiles:
{json}

Respond ONLY with JSON of shape:
{
  "profile_key": {
    "name": "...",
    "bio": "...",
    "age": 34,
    "birth_date": "1990-05-20",
    "likes": ["..."],
    "dislikes": ["..."]
  }
}
""";
    }


    private async Task<string> CallOllamaAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new
        {
            model = _config.OllamaModel,
            prompt,
            stream = false,
            options = new { temperature = 0.6, top_p = 0.9 }
        };

        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }

    private Dictionary<string, Persona> ParseResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var dict = JsonSerializer.Deserialize<Dictionary<string, Persona>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });
                return dict ?? new();
            }
        }
        catch
        {
            // ignore
        }
        return new();
    }

    private sealed class Persona
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("birth_date")]
        public DateTime? BirthDate { get; set; }

        [JsonPropertyName("likes")]
        public List<string>? Likes { get; set; }

        [JsonPropertyName("dislikes")]
        public List<string>? Dislikes { get; set; }
    }
}
