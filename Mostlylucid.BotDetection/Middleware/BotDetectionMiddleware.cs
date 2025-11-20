using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mostlylucid.BotDetection.Services;

namespace Mostlylucid.BotDetection.Middleware;

/// <summary>
/// Middleware that detects bots and adds detection result to HttpContext
/// </summary>
public class BotDetectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BotDetectionMiddleware> _logger;

    public const string BotDetectionResultKey = "BotDetectionResult";

    public BotDetectionMiddleware(
        RequestDelegate next,
        ILogger<BotDetectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IBotDetectionService botDetectionService)
    {
        // Run bot detection
        var result = await botDetectionService.DetectAsync(context, context.RequestAborted);

        // Store result in HttpContext for access by controllers/views
        context.Items[BotDetectionResultKey] = result;

        // Add custom header with bot detection result (for debugging)
        if (result.IsBot)
        {
            context.Response.Headers.TryAdd("X-Bot-Detected", "true");
            context.Response.Headers.TryAdd("X-Bot-Confidence",
                result.ConfidenceScore.ToString("F2"));

            _logger.LogInformation(
                "Bot detected: {BotType}, Confidence: {Confidence:F2}, IP: {IP}",
                result.BotType, result.ConfidenceScore, context.Connection.RemoteIpAddress);
        }

        // Continue pipeline
        await _next(context);
    }
}

/// <summary>
/// Extension methods for adding bot detection middleware
/// </summary>
public static class BotDetectionMiddlewareExtensions
{
    /// <summary>
    /// Add bot detection middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseBotDetection(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<BotDetectionMiddleware>();
    }
}
