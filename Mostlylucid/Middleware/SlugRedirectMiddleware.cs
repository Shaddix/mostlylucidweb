using Mostlylucid.Services.Blog;

namespace Mostlylucid.Middleware;

/// <summary>
/// Middleware to handle automatic redirects for learned slug mappings
/// This runs before the 404 handler and provides 301 Permanent Redirects
/// </summary>
public class SlugRedirectMiddleware(RequestDelegate next, ILogger<SlugRedirectMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ISlugSuggestionService? slugSuggestionService)
    {
        // Only process blog post requests
        if (context.Request.Path.StartsWithSegments("/blog", StringComparison.OrdinalIgnoreCase))
        {
            // Only check if service is available
            if (slugSuggestionService != null)
            {
                try
                {
                    // Extract slug and language from path
                    var pathSegments = context.Request.Path.Value?
                        .TrimStart('/')
                        .Split('/', StringSplitOptions.RemoveEmptyEntries);

                    if (pathSegments != null && pathSegments.Length >= 2 &&
                        pathSegments[0].Equals("blog", StringComparison.OrdinalIgnoreCase))
                    {
                        string slug;
                        var language = "en"; // Default language

                        if (pathSegments.Length == 2)
                        {
                            // /blog/{slug}
                            slug = pathSegments[1];
                        }
                        else if (pathSegments.Length >= 3)
                        {
                            // /blog/{language}/{slug}
                            language = pathSegments[1];
                            slug = pathSegments[2];
                        }
                        else
                        {
                            // Continue to next middleware
                            await next(context);
                            return;
                        }

                        // Check if there's an auto-redirect for this slug
                        var targetSlug = await slugSuggestionService.GetAutoRedirectSlugAsync(
                            slug,
                            language,
                            context.RequestAborted);

                        if (!string.IsNullOrWhiteSpace(targetSlug))
                        {
                            // Build the redirect URL
                            var redirectUrl = language == "en"
                                ? $"/blog/{targetSlug}"
                                : $"/blog/{language}/{targetSlug}";

                            logger.LogInformation(
                                "Auto-redirecting {OriginalPath} to {RedirectUrl}",
                                context.Request.Path, redirectUrl);

                            // 301 Permanent Redirect
                            context.Response.Redirect(redirectUrl, permanent: true);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't break the request
                    logger.LogError(ex, "Error in slug redirect middleware");
                }
            }
        }

        // Continue to next middleware
        await next(context);
    }
}

/// <summary>
/// Extension methods for registering the slug redirect middleware
/// </summary>
public static class SlugRedirectMiddlewareExtensions
{
    public static IApplicationBuilder UseSlugRedirect(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SlugRedirectMiddleware>();
    }
}
