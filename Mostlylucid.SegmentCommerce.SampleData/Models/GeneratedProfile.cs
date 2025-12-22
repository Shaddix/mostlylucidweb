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

    [JsonPropertyName("weight")]
    public double Weight { get; set; }
}
