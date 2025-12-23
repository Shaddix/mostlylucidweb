using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.Middleware;

/// <summary>
/// Middleware that identifies profiles for each request.
/// Supports cookieless session tracking via X-Session-ID header or _sid query parameter.
/// Uses the ProfileResolver to handle fingerprint/cookie/identity modes.
/// </summary>
public class ProfileIdentificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProfileIdentificationMiddleware> _logger;
    
    // Header and query param names for cookieless session tracking
    public const string SessionIdHeader = "X-Session-ID";
    public const string SessionIdQueryParam = "_sid";

    public ProfileIdentificationMiddleware(RequestDelegate next, ILogger<ProfileIdentificationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IProfileResolver profileResolver)
    {
        if (IsStaticPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            // Extract session key from header, query, or let resolver create one
            var sessionKey = ExtractSessionKey(context);
            if (!string.IsNullOrEmpty(sessionKey))
            {
                context.Items["ProvidedSessionKey"] = sessionKey;
            }
            
            // Get or create session profile (links to persistent if identifiable)
            var session = await profileResolver.GetOrCreateSessionAsync(context);
            
            context.Items["SessionId"] = session.Id;
            context.Items["SessionKey"] = session.SessionKey;
            
            // Add session key to response header for HTMX to pick up
            if (!context.Response.HasStarted)
            {
                context.Response.Headers["X-Session-ID"] = session.SessionKey;
            }
            
            if (session.PersistentProfileId.HasValue)
            {
                context.Items["ProfileId"] = session.PersistentProfileId.Value;
                context.Items["IdentificationMode"] = session.IdentificationMode.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Profile identification failed; continuing without profile context");
        }

        await _next(context);
    }

    /// <summary>
    /// Extracts session key from header or query parameter (cookieless mode).
    /// Priority: Header > Query > Cookie
    /// </summary>
    private static string? ExtractSessionKey(HttpContext context)
    {
        // 1. Check header (HTMX requests)
        if (context.Request.Headers.TryGetValue(SessionIdHeader, out var headerValue) && 
            !string.IsNullOrEmpty(headerValue.ToString()))
        {
            return headerValue.ToString();
        }
        
        // 2. Check query parameter (initial navigation, links)
        if (context.Request.Query.TryGetValue(SessionIdQueryParam, out var queryValue) && 
            !string.IsNullOrEmpty(queryValue.ToString()))
        {
            return queryValue.ToString();
        }
        
        return null;
    }

    private static bool IsStaticPath(PathString path)
    {
        return path.StartsWithSegments("/css")
               || path.StartsWithSegments("/js")
               || path.StartsWithSegments("/img")
               || path.StartsWithSegments("/images")
               || path.StartsWithSegments("/favicon")
               || path.StartsWithSegments("/lib")
               || path.StartsWithSegments("/dist")
               || path.StartsWithSegments("/api/fingerprint")
               || path.StartsWithSegments("/api/placeholder"); // Don't track placeholder images
    }
}

public static class ProfileIdentificationMiddlewareExtensions
{
    public static IApplicationBuilder UseProfileIdentification(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ProfileIdentificationMiddleware>();
    }
}
