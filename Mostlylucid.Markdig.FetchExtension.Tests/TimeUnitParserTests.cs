using Mostlylucid.Markdig.FetchExtension.Utilities;
using Xunit;

namespace Mostlylucid.Markdig.FetchExtension.Tests;

public class TimeUnitParserTests
{
    [Theory]
    [InlineData("30s", 0)]       // 30 seconds = 0 hours (rounds to 0)
    [InlineData("3600s", 1)]     // 3600 seconds = 1 hour
    [InlineData("7200s", 2)]     // 7200 seconds = 2 hours
    [InlineData("90s", 0)]       // 90 seconds = 0.025 hours (rounds to 0)
    [InlineData("1800s", 1)]     // 1800 seconds = 0.5 hours (rounds to 1)
    public void ParseToHours_Seconds_ConvertsCorrectly(string input, int expected)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5m", 0)]        // 5 minutes = 0.083 hours (rounds to 0)
    [InlineData("30m", 1)]       // 30 minutes = 0.5 hours (rounds to 1)
    [InlineData("60m", 1)]       // 60 minutes = 1 hour
    [InlineData("120m", 2)]      // 120 minutes = 2 hours
    [InlineData("90m", 2)]       // 90 minutes = 1.5 hours (rounds to 2)
    public void ParseToHours_Minutes_ConvertsCorrectly(string input, int expected)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1h", 1)]
    [InlineData("12h", 12)]
    [InlineData("24h", 24)]
    [InlineData("0h", 0)]
    [InlineData("48h", 48)]
    public void ParseToHours_Hours_ConvertsCorrectly(string input, int expected)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1d", 24)]
    [InlineData("7d", 168)]
    [InlineData("0d", 0)]
    [InlineData("30d", 720)]
    public void ParseToHours_Days_ConvertsCorrectly(string input, int expected)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("24", 24)]       // No unit defaults to hours
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("168", 168)]
    public void ParseToHours_NoUnit_DefaultsToHours(string input, int expected)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.5h", 2)]      // 1.5 hours rounds to 2
    [InlineData("0.5h", 1)]      // 0.5 hours rounds to 1
    [InlineData("2.4h", 2)]      // 2.4 hours rounds to 2
    [InlineData("2.6h", 3)]      // 2.6 hours rounds to 3
    public void ParseToHours_DecimalValues_RoundsCorrectly(string input, int expected)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  12h  ")]      // Whitespace around value
    [InlineData("12 h")]         // Space before unit
    [InlineData("12H")]          // Uppercase unit
    [InlineData("12 H")]         // Space and uppercase
    public void ParseToHours_HandlesWhitespaceAndCase(string input)
    {
        var result = TimeUnitParser.ParseToHours(input);
        Assert.Equal(12, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void ParseToHours_EmptyOrNull_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => TimeUnitParser.ParseToHours(input));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12x")]
    [InlineData("h12")]
    [InlineData("12 hours")]
    [InlineData("-5h")]
    public void ParseToHours_InvalidFormat_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => TimeUnitParser.ParseToHours(input));
    }

    [Theory]
    [InlineData("24h", true, 24)]
    [InlineData("7d", true, 168)]
    [InlineData("invalid", false, 0)]
    [InlineData("", false, 0)]
    public void TryParseToHours_ReturnsExpectedResults(string input, bool expectedSuccess, int expectedHours)
    {
        var success = TimeUnitParser.TryParseToHours(input, out var hours);
        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedHours, hours);
    }

    [Theory]
    [InlineData(0, "0s")]
    [InlineData(1, "1h")]
    [InlineData(12, "12h")]
    [InlineData(23, "23h")]
    [InlineData(24, "1d")]
    [InlineData(48, "2d")]
    [InlineData(168, "7d")]
    [InlineData(169, "169h")]    // Not divisible by 24
    public void FormatFromHours_ReturnsExpectedFormat(int hours, string expected)
    {
        var result = TimeUnitParser.FormatFromHours(hours);
        Assert.Equal(expected, result);
    }
}
