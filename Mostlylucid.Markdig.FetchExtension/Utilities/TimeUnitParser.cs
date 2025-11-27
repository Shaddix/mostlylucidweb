using System.Text.RegularExpressions;

namespace Mostlylucid.Markdig.FetchExtension.Utilities;

/// <summary>
/// Parses time duration strings with units (s, m, h, d) and converts them to hours
/// Examples: "30s", "5m", "12h", "7d", "24" (defaults to hours)
/// </summary>
public static partial class TimeUnitParser
{
    [GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*([smhd])?$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TimeUnitRegex();

    /// <summary>
    /// Parses a time string and converts it to hours
    /// </summary>
    /// <param name="timeString">Time string like "30s", "5m", "12h", "7d", or "24"</param>
    /// <returns>The duration in hours</returns>
    /// <exception cref="ArgumentException">Thrown when the time string format is invalid</exception>
    public static int ParseToHours(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            throw new ArgumentException("Time string cannot be null or empty", nameof(timeString));

        var match = TimeUnitRegex().Match(timeString.Trim());
        if (!match.Success)
            throw new ArgumentException($"Invalid time format: '{timeString}'. Expected format: number followed by optional unit (s/m/h/d)", nameof(timeString));

        var value = double.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Success ? match.Groups[2].Value.ToLowerInvariant() : "h";

        var hours = unit switch
        {
            "s" => value / 3600.0,           // seconds to hours
            "m" => value / 60.0,             // minutes to hours
            "h" => value,                     // hours to hours
            "d" => value * 24.0,             // days to hours
            _ => throw new ArgumentException($"Unknown time unit: '{unit}'. Valid units: s, m, h, d", nameof(timeString))
        };

        // Round to nearest integer, with a minimum of 0
        // Use MidpointRounding.AwayFromZero so 0.5 rounds up to 1
        return Math.Max(0, (int)Math.Round(hours, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Tries to parse a time string and convert it to hours
    /// </summary>
    /// <param name="timeString">Time string like "30s", "5m", "12h", "7d", or "24"</param>
    /// <param name="hours">The parsed duration in hours</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParseToHours(string timeString, out int hours)
    {
        hours = 0;

        if (string.IsNullOrWhiteSpace(timeString))
            return false;

        try
        {
            hours = ParseToHours(timeString);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Formats hours into a human-readable string with appropriate unit
    /// </summary>
    /// <param name="hours">Duration in hours</param>
    /// <returns>Formatted string like "30s", "5m", "12h", or "7d"</returns>
    public static string FormatFromHours(int hours)
    {
        if (hours == 0)
            return "0s";

        // Convert to most appropriate unit
        if (hours < 1)
        {
            var minutes = hours * 60;
            if (minutes < 1)
            {
                var seconds = hours * 3600;
                return $"{seconds}s";
            }
            return $"{minutes}m";
        }

        if (hours < 24)
            return $"{hours}h";

        if (hours % 24 == 0)
            return $"{hours / 24}d";

        return $"{hours}h";
    }
}
