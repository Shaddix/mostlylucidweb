using System.Security.Claims;
using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.Middleware;

public class ProfileIdentificationMiddleware
{
    private const string SessionCookieName = "sc_sid";
    private const string FingerprintCookieName = "sc_fp";
    private readonly RequestDelegate _next;
    private readonly ILogger<ProfileIdentificationMiddleware> _logger;

    public ProfileIdentificationMiddleware(RequestDelegate next, ILogger<ProfileIdentificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IProfileKeyService profileKeyService)
    {
        if (IsStaticPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var sessionKey = EnsureSessionCookie(context);
        var fingerprintHash = GetFingerprintHash(context);
        var userId = GetUserId(context.User);

        var keyRequest = new ProfileKeyRequest(fingerprintHash, sessionKey, userId);

        try
        {
            var profile = await profileKeyService.AttachOrCreateProfileAsync(keyRequest, context.RequestAborted);

            context.Items["ProfileKey"] = profile.ProfileKey;
            context.Items["ProfileId"] = profile.Id;
            context.Items["SessionKey"] = sessionKey;
            if (!string.IsNullOrEmpty(fingerprintHash))
            {
                context.Items["FingerprintHash"] = fingerprintHash;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile identification failed; continuing without profile context");
        }

        await _next(context);
    }

    private static bool IsStaticPath(PathString path)
    {
        return path.StartsWithSegments("/css")
               || path.StartsWithSegments("/js")
               || path.StartsWithSegments("/img")
               || path.StartsWithSegments("/images")
               || path.StartsWithSegments("/favicon")
               || path.StartsWithSegments("/lib")
               || path.StartsWithSegments("/dist");
    }

    private static string? GetUserId(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? user.Identity?.Name;
    }

    private static string? GetFingerprintHash(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(FingerprintCookieName, out var cookieValue) && !string.IsNullOrWhiteSpace(cookieValue))
        {
            return cookieValue;
        }

        if (context.Request.Headers.TryGetValue("X-Fingerprint-Hash", out var headerValue))
        {
            var header = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
            {
                return header;
            }
        }

        return null;
    }

    private static string EnsureSessionCookie(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(SessionCookieName, out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            // Refresh expiry to keep the sliding window alive
            AppendSessionCookie(context, existing);
            return existing;
        }

        var sessionKey = $"sc-{Guid.NewGuid():N}";
        AppendSessionCookie(context, sessionKey);
        return sessionKey;
    }

    private static void AppendSessionCookie(HttpContext context, string value)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };

        context.Response.Cookies.Append(SessionCookieName, value, options);
    }
}

public static class ProfileIdentificationMiddlewareExtensions
{
    public static IApplicationBuilder UseProfileIdentification(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ProfileIdentificationMiddleware>();
    }
}
