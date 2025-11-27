#!/usr/bin/env dotnet-script
#r "nuget: Umami.Net, 0.1.0"
#r "nuget: Microsoft.Extensions.DependencyInjection, 9.0.0"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"

using Umami.Net;
using Umami.Net.UmamiData;
using Umami.Net.UmamiData.Models.RequestObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

// Configuration
var websiteId = "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee";
var umamiPath = "https://umami.mostlylucid.net";
var username = "admin";
var password = Environment.GetEnvironmentVariable("UMAMI_PASSWORD") ?? "";

if (string.IsNullOrEmpty(password))
{
    Console.WriteLine("ERROR: Please set UMAMI_PASSWORD environment variable");
    return;
}

// Setup DI
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

services.AddUmamiData(umamiPath, websiteId);

var serviceProvider = services.BuildServiceProvider();
var umamiDataService = serviceProvider.GetRequiredService<UmamiDataService>();
var logger = serviceProvider.GetRequiredService<ILogger<UmamiDataService>>();

Console.WriteLine("=== Umami API Live Testing ===\n");
Console.WriteLine($"Testing against: {umamiPath}");
Console.WriteLine($"Website ID: {websiteId}\n");

// Login
Console.WriteLine(">>> Logging in...");
var loginSuccess = await umamiDataService.LoginAsync(username, password);
if (!loginSuccess)
{
    Console.WriteLine("ERROR: Login failed!");
    return;
}
Console.WriteLine("✓ Login successful\n");

// Test 1: Metrics API - URL metrics
Console.WriteLine("=== Test 1: Metrics API - URL Metrics (24 hours) ===");
var endDate = DateTime.UtcNow;
var startDate = endDate.AddHours(-24);

var metricsRequest = new MetricsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Type = MetricType.url,
    Unit = Unit.day,
    Limit = 10
};

var metricsResult = await umamiDataService.GetMetrics(metricsRequest);
Console.WriteLine($"Status: {metricsResult?.Status}");
if (metricsResult?.Data != null && metricsResult.Data.Length > 0)
{
    Console.WriteLine($"Top {Math.Min(5, metricsResult.Data.Length)} URLs:");
    foreach (var metric in metricsResult.Data.Take(5))
    {
        Console.WriteLine($"  {metric.x}: {metric.y} views");
    }
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

// Test 2: Metrics API - Referrer metrics
Console.WriteLine("=== Test 2: Metrics API - Referrer Metrics ===");
var referrerRequest = new MetricsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Type = MetricType.referrer,
    Unit = Unit.day,
    Limit = 5
};

var referrerResult = await umamiDataService.GetMetrics(referrerRequest);
Console.WriteLine($"Status: {referrerResult?.Status}");
if (referrerResult?.Data != null && referrerResult.Data.Length > 0)
{
    Console.WriteLine($"Top Referrers:");
    foreach (var metric in referrerResult.Data)
    {
        Console.WriteLine($"  {metric.x}: {metric.y} referrals");
    }
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

// Test 3: PageViews API
Console.WriteLine("=== Test 3: PageViews API (7 days) ===");
var pageViewsRequest = new PageViewsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-7),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.day
};

var pageViewsResult = await umamiDataService.GetPageViews(pageViewsRequest);
Console.WriteLine($"Status: {pageViewsResult?.Status}");
if (pageViewsResult?.PageViews != null && pageViewsResult.PageViews.Length > 0)
{
    Console.WriteLine($"Page views data points: {pageViewsResult.PageViews.Length}");
    var totalPageViews = pageViewsResult.PageViews.Sum(pv => pv.y);
    Console.WriteLine($"Total page views: {totalPageViews}");

    Console.WriteLine("Last 3 days:");
    foreach (var pv in pageViewsResult.PageViews.TakeLast(3))
    {
        Console.WriteLine($"  {pv.x}: {pv.y} views");
    }
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

// Test 4: Stats API
Console.WriteLine("=== Test 4: Stats API (24 hours) ===");
var statsRequest = new StatsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate
};

var statsResult = await umamiDataService.GetStats(statsRequest);
Console.WriteLine($"Status: {statsResult?.Status}");
if (statsResult?.Stats != null)
{
    Console.WriteLine($"Page Views: {statsResult.Stats.PageViews?.Value ?? 0}");
    Console.WriteLine($"Visitors: {statsResult.Stats.Visitors?.Value ?? 0}");
    Console.WriteLine($"Visits: {statsResult.Stats.Visits?.Value ?? 0}");
    Console.WriteLine($"Bounce Rate: {statsResult.Stats.BounceRate?.Value ?? 0}");
    Console.WriteLine($"Total Time: {statsResult.Stats.TotalTime?.Value ?? 0}");
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

// Test 5: Events Series API
Console.WriteLine("=== Test 5: Events Series API ===");
var eventsRequest = new EventsSeriesRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Unit = Unit.hour
};

var eventsResult = await umamiDataService.GetEventsSeries(eventsRequest);
Console.WriteLine($"Status: {eventsResult?.Status}");
if (eventsResult?.Data != null && eventsResult.Data.Length > 0)
{
    Console.WriteLine($"Event series data points: {eventsResult.Data.Length}");

    // Show some sample data
    var sampleEvents = eventsResult.Data.Take(5);
    Console.WriteLine("Sample events:");
    foreach (var evt in sampleEvents)
    {
        Console.WriteLine($"  Time: {evt.t}");
        if (evt.y != null && evt.y.Length > 0)
        {
            foreach (var eventData in evt.y)
            {
                Console.WriteLine($"    - Event: {eventData.x}, Count: {eventData.y}");
            }
        }
    }
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

// Test 6: Metrics with URL filter
Console.WriteLine("=== Test 6: Metrics with URL Filter (/blog/) ===");
var filteredMetricsRequest = new MetricsRequest
{
    StartAtDate = startDate,
    EndAtDate = endDate,
    Type = MetricType.url,
    Unit = Unit.day,
    Limit = 10,
    Path = "/blog/"
};

var filteredResult = await umamiDataService.GetMetrics(filteredMetricsRequest);
Console.WriteLine($"Status: {filteredResult?.Status}");
if (filteredResult?.Data != null && filteredResult.Data.Length > 0)
{
    Console.WriteLine($"Filtered blog URLs:");
    foreach (var metric in filteredResult.Data)
    {
        Console.WriteLine($"  {metric.x}: {metric.y} views");
    }
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

// Test 7: Test the PopularPosts scenario
Console.WriteLine("=== Test 7: Popular Posts Scenario (simulating PopularPostsService) ===");
var popularPostsRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddHours(-24),
    EndAtDate = DateTime.UtcNow,
    Type = MetricType.url,
    Unit = Unit.day,
    Limit = 500
};

var popularResult = await umamiDataService.GetMetrics(popularPostsRequest);
Console.WriteLine($"Status: {popularResult?.Status}");
if (popularResult?.Data != null && popularResult.Data.Length > 0)
{
    var blogPosts = popularResult.Data
        .Where(m => m.x.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase))
        .ToList();

    Console.WriteLine($"Total blog post URLs found: {blogPosts.Count}");

    // Aggregate by base slug (removing language extensions)
    var aggregatedPosts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (var post in blogPosts)
    {
        var slug = post.x.Substring(6).Trim('/');
        var baseSlug = slug.Contains('.') ? slug.Substring(0, slug.LastIndexOf('.')) : slug;

        if (aggregatedPosts.ContainsKey(baseSlug))
        {
            aggregatedPosts[baseSlug] += post.y;
        }
        else
        {
            aggregatedPosts[baseSlug] = post.y;
        }
    }

    var topPosts = aggregatedPosts.OrderByDescending(kvp => kvp.Value).Take(5);
    Console.WriteLine("\nTop 5 most popular posts (aggregated):");
    foreach (var post in topPosts)
    {
        Console.WriteLine($"  {post.Key}: {post.Value} views");
    }
}
else
{
    Console.WriteLine("No data returned");
}
Console.WriteLine();

Console.WriteLine("=== All Tests Completed ===");
