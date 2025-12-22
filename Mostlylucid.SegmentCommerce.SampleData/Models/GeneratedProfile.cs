using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.SampleData.Models;

public class GeneratedProfile
{
    [JsonPropertyName("profile_key")]
    public string ProfileKey { get; set; } = string.Empty;

    [JsonPropertyName("segments")]
    public List<GeneratedProfileSegment> Segments { get; set; } = [];

    [JsonPropertyName("interests")]
    public Dictionary<string, double> Interests { get; set; } = new();

    [JsonPropertyName("signals")]
    public List<GeneratedSignal> Signals { get; set; } = [];

    [JsonPropertyName("profile_image_path")]
    public string? ProfileImagePath { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("birth_date")]
    public DateTime? BirthDate { get; set; }

    [JsonPropertyName("likes")]
    public List<string> Likes { get; set; } = [];

    [JsonPropertyName("dislikes")]
    public List<string> Dislikes { get; set; } = [];
}

public class GeneratedProfileSegment
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

public class GeneratedSignal
{
    [JsonPropertyName("signal_type")]
    public string SignalType { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("product_id")]
    public string? ProductId { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
