using Microsoft.AspNetCore.SignalR;

namespace Mostlylucid.Chat.Server.Hubs;

/// <summary>
/// SignalR authorization filter for API key validation
/// Validates admin connections before allowing hub method invocations
/// </summary>
public class ApiKeyAuthorizationFilter : IHubFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyAuthorizationFilter> _logger;

    public ApiKeyAuthorizationFilter(IConfiguration configuration, ILogger<ApiKeyAuthorizationFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        // Only validate admin methods
        var methodName = invocationContext.HubMethodName;
        var requiresAuth = methodName == "RegisterAdmin";

        if (requiresAuth)
        {
            var httpContext = invocationContext.Context.GetHttpContext();
            if (httpContext == null || !ValidateApiKey(httpContext))
            {
                _logger.LogWarning("Unauthorized admin method call attempt: {MethodName} from {ConnectionId}",
                    methodName, invocationContext.Context.ConnectionId);

                throw new HubException("Unauthorized. Valid API key required for admin operations.");
            }
        }

        return await next(invocationContext);
    }

    private bool ValidateApiKey(HttpContext httpContext)
    {
        var configuredApiKey = _configuration["Chat:AdminApiKey"];
        if (string.IsNullOrEmpty(configuredApiKey))
        {
            _logger.LogError("Chat:AdminApiKey not configured");
            return false;
        }

        // Check query string (added by client during connection)
        if (httpContext.Request.Query.TryGetValue("apiKey", out var queryKey))
        {
            return queryKey.ToString() == configuredApiKey;
        }

        // Check header
        if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var headerKey))
        {
            return headerKey.ToString() == configuredApiKey;
        }

        return false;
    }
}
