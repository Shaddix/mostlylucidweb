using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Umami.Net.Helpers;

namespace Umami.Net.Test.Helpers;

/// <summary>
/// Tests for ResponseDecoder to ensure correct handling of Umami API responses.
/// Validates JWT decoding, bot detection, and error handling.
/// </summary>
public class ResponseDecoder_Tests
{
    private readonly ResponseDecoder _decoder = new(NullLogger<ResponseDecoder>.Instance);

    /// <summary>
    /// Verifies that DecodeResponse correctly identifies "beep boop" bot detection response.
    /// Umami returns this plain text response when it detects a bot.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_BeepBoop_ReturnsBotDetected()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("beep boop")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.BotDetected, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles case-insensitive "beep boop" variants.
    /// </summary>
    [Theory]
    [InlineData("BEEP BOOP")]
    [InlineData("Beep Boop")]
    [InlineData("beep    boop")]
    [InlineData("  beep boop  ")]
    public async Task DecodeResponse_BeepBoopVariants_ReturnsBotDetected(string content)
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.BotDetected, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeResponse successfully decodes valid JWT token.
    /// Umami returns JWT tokens containing visitor information.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_ValidJwt_ReturnsSuccess()
    {
        // Arrange
        // Valid JWT token with minimal payload
        var validJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ2aXNpdG9ySWQiOiIxMjM0NSJ9.dummysignature";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"\"{validJwt}\"")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.Success, result.Status);
        Assert.NotNull(result.VisitorId);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles invalid JWT gracefully.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_InvalidJwt_ReturnsFailed()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("\"not-a-valid-jwt\"")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.Failed, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles empty response.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_EmptyContent_ReturnsFailed()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.Failed, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles null content.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_NullContent_ReturnsFailed()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.Failed, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles malformed JSON.
    /// </summary>
    [Fact]
    public async Task DecodeResponse_MalformedJson_ReturnsFailed()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{invalid json}")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.Failed, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeResponse handles error status codes.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task DecodeResponse_ErrorStatusCodes_ReturnsFailed(HttpStatusCode statusCode)
    {
        // Arrange
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("Error")
        };

        // Act
        var result = await _decoder.DecodeResponse(response);

        // Assert
        Assert.Equal(ResponseStatus.Failed, result.Status);
    }

    /// <summary>
    /// Verifies that DecodeJwt extracts visitor ID from JWT payload.
    /// </summary>
    [Fact]
    public void DecodeJwt_ValidToken_ExtractsVisitorId()
    {
        // Arrange - Create a JWT with known payload
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"visitorId\":\"12345-67890\"}"));
        var signature = "dummysignature";
        var jwt = $"{header}.{payload}.{signature}";

        // Act
        var visitorId = ResponseDecoder.DecodeJwt(jwt);

        // Assert
        Assert.Equal("12345-67890", visitorId);
    }

    /// <summary>
    /// Verifies that DecodeJwt handles JWT without visitorId field.
    /// </summary>
    [Fact]
    public void DecodeJwt_NoVisitorId_ReturnsEmptyString()
    {
        // Arrange
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"HS256\"}"));
        var payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"someOtherField\":\"value\"}"));
        var signature = "dummysignature";
        var jwt = $"{header}.{payload}.{signature}";

        // Act
        var visitorId = ResponseDecoder.DecodeJwt(jwt);

        // Assert
        Assert.Equal(string.Empty, visitorId);
    }

    /// <summary>
    /// Verifies that DecodeJwt handles malformed JWT gracefully.
    /// </summary>
    [Theory]
    [InlineData("not.a.jwt")]
    [InlineData("only.one")]
    [InlineData("")]
    [InlineData("no-dots-here")]
    public void DecodeJwt_MalformedToken_ThrowsException(string malformedJwt)
    {
        // Act & Assert
        Assert.Throws<Exception>(() => ResponseDecoder.DecodeJwt(malformedJwt));
    }

    /// <summary>
    /// Verifies that DecodeJwt handles invalid Base64 encoding.
    /// </summary>
    [Fact]
    public void DecodeJwt_InvalidBase64_ThrowsException()
    {
        // Arrange
        var jwt = "invalid!base64.invalid!base64.signature";

        // Act & Assert
        Assert.Throws<Exception>(() => ResponseDecoder.DecodeJwt(jwt));
    }
}
