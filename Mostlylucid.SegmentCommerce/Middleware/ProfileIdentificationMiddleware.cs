using Mostlylucid.SegmentCommerce.Services.Profiles;

namespace Mostlylucid.SegmentCommerce.Middleware;

/// <summary>
/// Middleware that identifies profiles for each request.
/// Uses the ProfileResolver to handle fingerprint/cookie/identity modes.
/// </summary>
public class ProfileIdentificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProfileIdentificationMiddleware> _logger;

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
            // Get or create session profile (links to persistent if identifiable)
            var session = await profileResolver.GetOrCreateSessionAsync(context);
            
            context.Items["SessionId"] = session.Id;
            context.Items["SessionKey"] = session.SessionKey;
            
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

    private static bool IsStaticPath(PathString path)
    {
        return path.StartsWithSegments("/css")
               || path.StartsWithSegments("/js")
               || path.StartsWithSegments("/img")
               || path.StartsWithSegments("/images")
               || path.StartsWithSegments("/favicon")
               || path.StartsWithSegments("/lib")
               || path.StartsWithSegments("/dist")
               || path.StartsWithSegments("/api/fingerprint"); // Don't recurse on fingerprint API
    }
}

public static class ProfileIdentificationMiddlewareExtensions
{
    public static IApplicationBuilder UseProfileIdentification(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ProfileIdentificationMiddleware>();
    }
}
