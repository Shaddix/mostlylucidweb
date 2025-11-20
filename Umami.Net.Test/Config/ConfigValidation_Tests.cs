using Umami.Net.Config;

namespace Umami.Net.Test.Config;

/// <summary>
/// Tests for configuration validation to ensure early detection of configuration errors.
/// </summary>
public class ConfigValidation_Tests
{
    /// <summary>
    /// Verifies that ValidateSettings throws ArgumentNullException when UmamiPath is null.
    /// This ensures configuration errors are caught at startup.
    /// </summary>
    [Fact]
    public void ValidateSettings_NullUmamiPath_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = null!,
            WebsiteId = "12345678-1234-1234-1234-123456789abc"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Contains("UmamiUrl is required", exception.Message);
    }

    /// <summary>
    /// Verifies that ValidateSettings throws ArgumentNullException when UmamiPath is empty.
    /// </summary>
    [Fact]
    public void ValidateSettings_EmptyUmamiPath_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = "",
            WebsiteId = "12345678-1234-1234-1234-123456789abc"
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Contains("UmamiUrl is required", exception.Message);
    }

    /// <summary>
    /// Verifies that ValidateSettings throws FormatException when UmamiPath is not a valid URI.
    /// This catches common configuration mistakes like missing protocol or malformed URLs.
    /// </summary>
    [Fact]
    public void ValidateSettings_InvalidUmamiPath_ThrowsFormatException()
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = "not-a-valid-url",
            WebsiteId = "12345678-1234-1234-1234-123456789abc"
        };

        // Act & Assert
        var exception = Assert.Throws<FormatException>(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Contains("UmamiUrl must be a valid Uri", exception.Message);
    }

    /// <summary>
    /// Verifies that ValidateSettings throws ArgumentNullException when WebsiteId is null.
    /// </summary>
    [Fact]
    public void ValidateSettings_NullWebsiteId_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = "https://analytics.example.com",
            WebsiteId = null!
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Contains("WebsiteId is required", exception.Message);
    }

    /// <summary>
    /// Verifies that ValidateSettings throws ArgumentNullException when WebsiteId is empty.
    /// </summary>
    [Fact]
    public void ValidateSettings_EmptyWebsiteId_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = "https://analytics.example.com",
            WebsiteId = ""
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Contains("WebsiteId is required", exception.Message);
    }

    /// <summary>
    /// Verifies that ValidateSettings throws FormatException when WebsiteId is not a valid GUID.
    /// This catches common mistakes like using names instead of GUIDs.
    /// </summary>
    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("my-website")]
    [InlineData("{12345678-1234-1234-1234-123456789abc}")] // Braces not allowed
    public void ValidateSettings_InvalidWebsiteId_ThrowsFormatException(string invalidGuid)
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = "https://analytics.example.com",
            WebsiteId = invalidGuid
        };

        // Act & Assert
        var exception = Assert.Throws<FormatException>(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Contains("WebSiteId must be a valid Guid", exception.Message);
    }

    /// <summary>
    /// Verifies that ValidateSettings succeeds with valid configuration.
    /// </summary>
    [Fact]
    public void ValidateSettings_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = "https://analytics.example.com",
            WebsiteId = "12345678-1234-1234-1234-123456789abc"
        };

        // Act & Assert - should not throw
        var exception = Record.Exception(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that ValidateSettings accepts various valid URL formats.
    /// </summary>
    [Theory]
    [InlineData("https://analytics.example.com")]
    [InlineData("https://analytics.example.com/")]
    [InlineData("https://analytics.example.com:8080")]
    [InlineData("http://localhost:3000")]
    public void ValidateSettings_VariousValidUrls_DoesNotThrow(string validUrl)
    {
        // Arrange
        var settings = new UmamiClientSettings
        {
            UmamiPath = validUrl,
            WebsiteId = "12345678-1234-1234-1234-123456789abc"
        };

        // Act & Assert
        var exception = Record.Exception(() =>
            UmamiClientSettings.ValidateSettings(settings));

        Assert.Null(exception);
    }
}
