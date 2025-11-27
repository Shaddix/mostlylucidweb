namespace Mostlylucid.Chat.Server.Middleware;

/// <summary>
/// Simple API key authentication middleware
/// Validates admin connections using a shared API key
/// For production, consider OAuth2 or JWT tokens
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly string _apiKey;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _apiKey = configuration["Chat:AdminApiKey"] ?? throw new InvalidOperationException("Chat:AdminApiKey not configured");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check API key for admin-related endpoints
        // SignalR connections will validate in the hub
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            if (!ValidateApiKey(context))
            {
                _logger.LogWarning("Unauthorized admin access attempt from {IpAddress}",
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }

        await _next(context);
    }

    private bool ValidateApiKey(HttpContext context)
    {
        // Check header first
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var headerKey))
        {
            return headerKey.ToString() == _apiKey;
        }

        // Check query string (for SignalR connections)
        if (context.Request.Query.TryGetValue("apiKey", out var queryKey))
        {
            return queryKey.ToString() == _apiKey;
        }

        return false;
    }
}

/// <summary>
/// Extension methods for registering the API key middleware
/// </summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
