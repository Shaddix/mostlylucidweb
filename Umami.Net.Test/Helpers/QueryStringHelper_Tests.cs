using Umami.Net.UmamiData.Helpers;
using Umami.Net.UmamiData.Models.RequestObjects;

namespace Umami.Net.Test.Helpers;

/// <summary>
/// Tests for QueryStringHelper to ensure correct query string generation and validation.
/// Validates parameter serialization, required field checking, and proper encoding.
/// </summary>
public class QueryStringHelper_Tests
{
    /// <summary>
    /// Verifies that ToQueryString throws ArgumentNullException for null object.
    /// This catches programming errors early with a helpful message.
    /// </summary>
    [Fact]
    public void ToQueryString_NullObject_ThrowsArgumentNullException()
    {
        // Arrange
        object? nullObject = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            nullObject!.ToQueryString());

        Assert.Contains("Cannot convert null object to query string", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
    }

    /// <summary>
    /// Verifies that ToQueryString works with all required parameters set.
    /// MetricType is an enum (value type), so it always has a value and cannot be null.
    /// </summary>
    [Fact]
    public void ToQueryString_RequiredParametersSet_Success()
    {
        // Arrange
        var request = new MetricsRequest
        {
            Type = MetricType.url, // Required enum parameter
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.Contains("type=url", queryString);
        Assert.Contains("startAt=", queryString);
        Assert.Contains("endAt=", queryString);
    }

    /// <summary>
    /// Verifies that ToQueryString throws for required string parameters that are empty.
    /// </summary>
    [Fact]
    public void ToQueryString_RequiredParameterEmpty_ThrowsArgumentException()
    {
        // Arrange - Create a request where we can test empty string validation
        // Note: Most request models use value types or nullable types for required params
        // This test ensures the validation logic works for string parameters

        var request = new TestRequestWithRequiredString
        {
            RequiredField = "" // Empty string
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            request.ToQueryString());

        Assert.Contains("cannot be empty or whitespace", exception.Message);
        Assert.Contains("Suggestion:", exception.Message);
    }

    /// <summary>
    /// Verifies that ToQueryString correctly serializes all parameters.
    /// </summary>
    [Fact]
    public void ToQueryString_ValidRequest_GeneratesCorrectQueryString()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndAtDate = new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            Type = MetricType.path,
            Unit = Unit.day,
            Limit = 10,
            Timezone = "UTC"
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.Contains("startAt=1735689600000", queryString);
        Assert.Contains("endAt=1736294400000", queryString);
        Assert.Contains("type=path", queryString);
        Assert.Contains("unit=day", queryString);
        Assert.Contains("limit=10", queryString);
        Assert.Contains("timezone=UTC", queryString);
    }

    /// <summary>
    /// Verifies that ToQueryString skips null optional parameters.
    /// </summary>
    [Fact]
    public void ToQueryString_NullOptionalParameters_Skipped()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Path = null, // Optional, should be skipped
            Country = null // Optional, should be skipped
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.DoesNotContain("path=", queryString);
        Assert.DoesNotContain("country=", queryString);
        Assert.Contains("startAt=", queryString);
        Assert.Contains("endAt=", queryString);
    }

    /// <summary>
    /// Verifies that ToQueryString includes non-null optional parameters.
    /// </summary>
    [Fact]
    public void ToQueryString_NonNullOptionalParameters_Included()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Path = "/blog/my-post",
            Country = "US",
            Browser = "Chrome"
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.Contains("path=%2Fblog%2Fmy-post", queryString); // URL encoded
        Assert.Contains("country=US", queryString);
        Assert.Contains("browser=Chrome", queryString);
    }

    /// <summary>
    /// Verifies that ToQueryString properly URL-encodes special characters.
    /// </summary>
    [Fact]
    public void ToQueryString_SpecialCharacters_ProperlyEncoded()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Path = "/blog/post?query=test&other=value",
            Title = "Test Post: Special & Characters"
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        // Verify URL encoding for special characters
        Assert.Contains("path=", queryString);
        Assert.Contains("title=", queryString);
        // Should be properly encoded (exact encoding depends on implementation)
        Assert.DoesNotContain("&other=value", queryString); // Should be encoded
    }

    /// <summary>
    /// Verifies that enum values are serialized as strings.
    /// </summary>
    [Fact]
    public void ToQueryString_EnumValues_SerializedAsStrings()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.browser,
            Unit = Unit.hour
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.Contains("type=browser", queryString);
        Assert.Contains("unit=hour", queryString);
    }

    /// <summary>
    /// Verifies that numeric values are serialized correctly.
    /// </summary>
    [Fact]
    public void ToQueryString_NumericValues_SerializedCorrectly()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.path,
            Limit = 42
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.Contains("limit=42", queryString);
    }

    /// <summary>
    /// Verifies that query string starts with '?' and uses '&' for separators.
    /// </summary>
    [Fact]
    public void ToQueryString_Format_CorrectDelimiters()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.path,
            Limit = 10
        };

        // Act
        var queryString = request.ToQueryString();

        // Assert
        Assert.StartsWith("?", queryString);
        Assert.Contains("&", queryString); // Multiple parameters separated by &
    }

    /// <summary>
    /// Test class with required string field for validation testing.
    /// </summary>
    private class TestRequestWithRequiredString
    {
        [QueryStringParameter("requiredField", isRequired: true)]
        public string RequiredField { get; set; } = "";
    }
}
