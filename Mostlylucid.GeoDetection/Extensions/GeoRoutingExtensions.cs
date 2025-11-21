using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.GeoDetection.Middleware;
using Mostlylucid.GeoDetection.Models;
using Mostlylucid.GeoDetection.Services;

namespace Mostlylucid.GeoDetection.Extensions;

/// <summary>
/// Extension methods for geo-routing middleware configuration
/// </summary>
public static class GeoRoutingExtensions
{
    /// <summary>
    /// Use geo-routing middleware
    /// </summary>
    public static IApplicationBuilder UseGeoRouting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GeoRoutingMiddleware>();
    }
}
