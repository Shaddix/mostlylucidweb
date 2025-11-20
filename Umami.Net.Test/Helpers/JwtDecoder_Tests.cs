using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging.Abstractions;
using Umami.Net.Helpers;

namespace Umami.Net.Test.Helpers;

/// <summary>
/// Tests for JwtDecoder to ensure correct handling of JWT tokens from Umami API.
/// Validates JWT decoding and error handling.
/// </summary>
public class JwtDecoder_Tests
{
    private readonly JwtDecoder _decoder = new(NullLogger<JwtDecoder>.Instance);

    /// <summary>
    /// Verifies that DecodeResponse successfully decodes valid JWT token.
    /// Umami returns JWT tokens containing visitor information.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_ValidJwt_ReturnsPayload()
    {
        // Arrange - Create a valid JWT with known payload
        var handler = new JwtSecurityTokenHandler();
        var payload = new JwtPayload
        {
            { "visitId", "12345-67890" },
            { "iat", 1234567890 }
        };
        var header = new JwtHeader();
        var token = new JwtSecurityToken(header, payload);
        var jwt = handler.WriteToken(token);

        // Act
        var result = await _decoder.DecodeResponse(jwt);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("visitId"));
        Assert.Equal("12345-67890", result["visitId"].ToString());
    }

    /// <summary>
    /// Verifies that DecodeResponse handles invalid JWT gracefully.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_InvalidJwt_ReturnsNull()
    {
        // Arrange
        var invalidJwt = "not-a-valid-jwt";

        // Act
        var result = await _decoder.DecodeResponse(invalidJwt);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles empty response.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_EmptyContent_ReturnsNull()
    {
        // Arrange
        var emptyContent = "";

        // Act
        var result = await _decoder.DecodeResponse(emptyContent);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles malformed JWT.
    /// </summary>
    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("only.one")]
    [InlineData("no-dots-here")]
    public async Task DecodeResponse_MalformedJwt_ReturnsNull(string malformedJwt)
    {
        // Act
        var result = await _decoder.DecodeResponse(malformedJwt);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that DecodeResponse extracts all fields from a complete JWT payload.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_CompleteJwt_ExtractsAllFields()
    {
        // Arrange
        var handler = new JwtSecurityTokenHandler();
        var payload = new JwtPayload
        {
            { "id", Guid.NewGuid().ToString() },
            { "websiteId", Guid.NewGuid().ToString() },
            { "hostname", "example.com" },
            { "browser", "Chrome" },
            { "os", "Windows" },
            { "device", "desktop" },
            { "screen", "1920x1080" },
            { "language", "en-US" },
            { "country", "US" },
            { "visitId", Guid.NewGuid().ToString() },
            { "iat", 1234567890 }
        };
        var header = new JwtHeader();
        var token = new JwtSecurityToken(header, payload);
        var jwt = handler.WriteToken(token);

        // Act
        var result = await _decoder.DecodeResponse(jwt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(11, result.Count); // All fields present
        Assert.True(result.ContainsKey("hostname"));
        Assert.Equal("example.com", result["hostname"].ToString());
        Assert.True(result.ContainsKey("browser"));
        Assert.Equal("Chrome", result["browser"].ToString());
    }

    /// <summary>
    /// Verifies that DecodeResponse handles minimal JWT with only required fields.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_MinimalJwt_ReturnsPayload()
    {
        // Arrange
        var handler = new JwtSecurityTokenHandler();
        var payload = new JwtPayload
        {
            { "visitId", Guid.NewGuid().ToString() },
            { "iat", 1234567890 }
        };
        var header = new JwtHeader();
        var token = new JwtSecurityToken(header, payload);
        var jwt = handler.WriteToken(token);

        // Act
        var result = await _decoder.DecodeResponse(jwt);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("visitId"));
        Assert.True(result.ContainsKey("iat"));
    }
}