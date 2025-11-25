using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Mostlylucid.Shared.Config;

namespace Mostlylucid.Filters;

/// <summary>
/// Attribute to require API token authentication for announcement API endpoints
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiTokenAuthAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string ApiTokenHeaderName = "X-Api-Token";
    private const string ApiTokenQueryName = "api_token";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<AnnouncementConfig>();

        if (string.IsNullOrEmpty(config.ApiToken))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "API token not configured" });
            return;
        }

        // Check header first, then query string
        string? providedToken = context.HttpContext.Request.Headers[ApiTokenHeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(providedToken))
        {
            providedToken = context.HttpContext.Request.Query[ApiTokenQueryName].FirstOrDefault();
        }

        if (string.IsNullOrEmpty(providedToken))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "API token required" });
            return;
        }

        // Use constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(config.ApiToken),
                System.Text.Encoding.UTF8.GetBytes(providedToken)))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid API token" });
            return;
        }

        await Task.CompletedTask;
    }
}
