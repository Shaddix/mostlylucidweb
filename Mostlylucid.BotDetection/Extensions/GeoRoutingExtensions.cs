using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.BotDetection.Middleware;
using Mostlylucid.BotDetection.Models;
using Mostlylucid.BotDetection.Services;

namespace Mostlylucid.BotDetection.Extensions;

/// <summary>
/// Extension methods for geo-routing configuration
/// </summary>
public static class GeoRoutingExtensions
{
    /// <summary>
    /// Add geo-routing services
    /// </summary>
    public static IServiceCollection AddGeoRouting(
        this IServiceCollection services,
        Action<GeoRoutingOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<GeoRoutingOptions>(options => { });
        }

        // Register geo location service
        services.AddSingleton<IGeoLocationService, SimpleGeoLocationService>();

        return services;
    }

    /// <summary>
    /// Configure site to only allow specific countries
    /// </summary>
    public static IServiceCollection RestrictSiteToCountries(
        this IServiceCollection services,
        params string[] countryCodes)
    {
        return services.AddGeoRouting(options =>
        {
            options.AllowedCountries = countryCodes;
            options.Enabled = true;
        });
    }

    /// <summary>
    /// Configure site to block specific countries
    /// </summary>
    public static IServiceCollection BlockCountries(
        this IServiceCollection services,
        params string[] countryCodes)
    {
        return services.AddGeoRouting(options =>
        {
            options.BlockedCountries = countryCodes;
            options.Enabled = true;
        });
    }
}

/// <summary>
/// Extension methods for endpoint routing
/// </summary>
public static class EndpointGeoRoutingExtensions
{
    /// <summary>
    /// Require request to be from specific countries
    /// </summary>
    public static RouteHandlerBuilder RequireCountry(
        this RouteHandlerBuilder builder,
        params string[] countryCodes)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var location = httpContext.Items[GeoRoutingMiddleware.GeoLocationKey] as GeoLocation;

            if (location == null)
            {
                // No geo data available, allow through
                return await next(context);
            }

            if (!countryCodes.Contains(location.CountryCode, StringComparer.OrdinalIgnoreCase))
            {
                return Results.StatusCode(451); // Unavailable For Legal Reasons
            }

            return await next(context);
        });
    }

    /// <summary>
    /// Block requests from specific countries
    /// </summary>
    public static RouteHandlerBuilder BlockCountries(
        this RouteHandlerBuilder builder,
        params string[] countryCodes)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var location = httpContext.Items[GeoRoutingMiddleware.GeoLocationKey] as GeoLocation;

            if (location == null)
            {
                return await next(context);
            }

            if (countryCodes.Contains(location.CountryCode, StringComparer.OrdinalIgnoreCase))
            {
                return Results.StatusCode(451);
            }

            return await next(context);
        });
    }

    /// <summary>
    /// Route requests based on country
    /// </summary>
    public static RouteHandlerBuilder RouteByCountry(
        this RouteHandlerBuilder builder,
        Dictionary<string, string> countryRoutes)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var location = httpContext.Items[GeoRoutingMiddleware.GeoLocationKey] as GeoLocation;

            if (location != null && countryRoutes.TryGetValue(location.CountryCode, out var route))
            {
                // Store the target route in context for downstream handling
                httpContext.Items["TargetRoute"] = route;
            }

            return await next(context);
        });
    }

    /// <summary>
    /// Get geo location from context
    /// </summary>
    public static GeoLocation? GetGeoLocation(this HttpContext context)
    {
        return context.Items[GeoRoutingMiddleware.GeoLocationKey] as GeoLocation;
    }

    /// <summary>
    /// Get country code from context
    /// </summary>
    public static string? GetCountryCode(this HttpContext context)
    {
        return context.GetGeoLocation()?.CountryCode;
    }
}
