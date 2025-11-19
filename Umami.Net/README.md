# Umami.Net

A comprehensive .NET client library for [Umami Analytics](https://umami.is) - the privacy-focused, open-source web analytics platform.

[![NuGet](https://img.shields.io/nuget/v/Umami.Net.svg)](https://www.nuget.org/packages/Umami.Net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- 📊 **Event Tracking** - Track custom events and page views
- 📈 **Analytics Data API** - Retrieve statistics, metrics, and insights
- 🔄 **Background Processing** - Non-blocking event sending
- 🔐 **Built-in Authentication** - Automatic JWT token management
- ⚡ **Resilient** - Retry logic with Polly integration
- 🛡️ **Defensive** - Comprehensive validation and error handling
- 📝 **Well-Documented** - Extensive XML documentation and examples
- 🎯 **Type-Safe** - Strongly-typed request/response models
- 🔧 **Auto-Detection** - Automatically detects Umami v1, v2, or v3 API - **It Just Works™**
- 🆕 **Umami v3 Support** - Full support for the latest Umami v3 API features

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Event Tracking](#event-tracking)
  - [Basic Setup](#basic-setup)
  - [Track Events](#track-events)
  - [Track Page Views](#track-page-views)
  - [Background Sending](#background-sending)
- [Analytics Data API](#analytics-data-api)
  - [Configuration](#configuration)
  - [Getting Stats](#getting-stats)
  - [Getting Metrics](#getting-metrics)
  - [Expanded Metrics](#expanded-metrics)
  - [Page Views Over Time](#page-views-over-time)
  - [Events Series](#events-series)
  - [Active Users](#active-users)
- [Common Use Cases](#common-use-cases)
- [Error Handling](#error-handling)
- [Performance Tips](#performance-tips)
- [API Reference](#api-reference)
- [Contributing](#contributing)

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Umami.Net
```

Or via Package Manager Console:

```powershell
Install-Package Umami.Net
```

## Quick Start

### 1. Add Configuration

Add to your `appsettings.json`:

```json
{
  "Analytics": {
    "UmamiPath": "https://analytics.yoursite.com",
    "WebsiteId": "your-website-id-here"
  }
}
```

For data API access, also add authentication:

```json
{
  "Analytics": {
    "UmamiPath": "https://analytics.yoursite.com",
    "WebsiteId": "your-website-id-here",
    "UserName": "admin",
    "Password": "your-password"
  }
}
```

### 2. Register Services

In your `Program.cs`:

```csharp
using Umami.Net;

// For event tracking only
builder.Services.SetupUmamiClient(builder.Configuration);

// For data API access
builder.Services.SetupUmamiData(builder.Configuration);
```

### 3. Start Tracking

```csharp
public class MyController : ControllerBase
{
    private readonly UmamiClient _client;

    public MyController(UmamiClient client)
    {
        _client = client;
    }

    public async Task<IActionResult> MyAction()
    {
        // Track an event
        await _client.Track("button-click", new UmamiEventData
        {
            { "button", "subscribe" },
            { "location", "header" }
        });

        return Ok();
    }
}
```

## API Version Compatibility

**Umami.Net automatically detects and supports Umami v1, v2, and v3 APIs** - no configuration needed!

### How It Works

The library intelligently detects which API version your Umami instance uses:

- **Tries v3/v2 first** (modern API with `path`/`hostname` parameters)
- **Auto-fallback to v1** if it receives a 400 Bad Request
- **Seamless retry** with converted parameter names
- **Zero configuration** required from you

### What This Means For You

✅ **Just works** with any Umami instance
✅ **No version detection** code needed
✅ **Backwards compatible** with older Umami servers
✅ **Forward compatible** with future updates
✅ **Automatic adaptation** on first request

You don't need to know or care which Umami version you're running - the library handles it automatically!

### Supported Versions

| Umami Version | API Version | Support Status | Key Features |
|--------------|-------------|----------------|--------------|
| v1.x | v1 API | ✅ Fully Supported (auto-detected) | Basic analytics |
| v2.x | v2 API | ✅ Fully Supported | Modern API, `path` type |
| v3.x | v3 API | ✅ Fully Supported (latest) | Enhanced metrics, optional `unit` |

### Umami v3 Enhancements

Umami v3 includes several improvements that this library fully supports:

- **Optional Unit Parameter** - `unit` is now optional in metrics requests
- **Enhanced Metric Types** - All v2 metric types plus additional granularity
- **Improved Timezone Support** - Better handling of timezone-aware queries
- **Backwards Compatible** - v3 maintains compatibility with v2 API structure

## Event Tracking

### Basic Setup

The library provides two ways to send events:

1. **UmamiClient** - Synchronous, immediate sending
2. **UmamiBackgroundSender** - Asynchronous, non-blocking (recommended for high-traffic sites)

### Track Events

Track custom events with optional data:

```csharp
// Inject the client
private readonly UmamiClient _client;

// Track a simple event
await _client.Track("video-play");

// Track with event data
await _client.Track("search", new UmamiEventData
{
    { "query", "umami analytics" },
    { "results", "42" },
    { "duration_ms", "156" }
});

// Track with custom URL (overrides auto-detected URL)
await _client.Track("form-submit",
    eventData: new UmamiEventData { { "form", "newsletter" } },
    url: "/custom/path");
```

###  Track Page Views

Track page views explicitly:

```csharp
// Basic page view
await _client.TrackPageView("/blog/my-post");

// Page view with event name and data
await _client.TrackPageView(
    url: "/products/item-123",
    eventType: "product-view",
    eventData: new UmamiEventData
    {
        { "category", "electronics" },
        { "price", "299.99" }
    }
);
```

### Background Sending

For better performance, use the background sender:

```csharp
private readonly UmamiBackgroundSender _backgroundSender;

// Events are queued and sent in the background
await _backgroundSender.Track("user-signup", new UmamiEventData
{
    { "plan", "premium" },
    { "referral", "google" }
});

// Non-blocking page view tracking
await _backgroundSender.TrackPageView("/checkout/complete", "purchase");
```

### Low-Level Send Method

For advanced scenarios, use the raw `Send` method:

```csharp
var payload = new UmamiPayload
{
    Website = "your-website-id",
    Url = "/custom/page",
    Title = "Custom Page Title",
    Referrer = "https://google.com",
    Data = new UmamiEventData { { "custom_field", "value" } }
};

await _client.Send(payload, eventType: "event");
```

> **Note:** Event type can only be "event" or "identify" as per the Umami API specification.

## Analytics Data API

### Configuration

Ensure your configuration includes username and password for API access:

```json
{
  "Analytics": {
    "UmamiPath": "https://analytics.yoursite.com",
    "WebsiteId": "your-website-id",
    "UserName": "admin",
    "Password": "your-secure-password"
  }
}
```

Register the data service:

```csharp
builder.Services.SetupUmamiData(builder.Configuration);
```

### Getting Stats

Get summary statistics for a date range:

```csharp
private readonly UmamiDataService _dataService;

var statsRequest = new StatsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-30),
    EndAtDate = DateTime.UtcNow
};

var result = await _dataService.GetStats(statsRequest);

if (result.Status == HttpStatusCode.OK && result.Data != null)
{
    Console.WriteLine($"Page Views: {result.Data.pageviews.value}");
    Console.WriteLine($"Visitors: {result.Data.visitors.value}");
    Console.WriteLine($"Visits: {result.Data.visits.value}");
    Console.WriteLine($"Bounce Rate: {(double)result.Data.bounces.value / result.Data.visits.value:P}");
}
```

Filter stats by specific criteria:

```csharp
var statsRequest = new StatsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-7),
    EndAtDate = DateTime.UtcNow,
    Url = "/blog/my-post",  // Stats for specific URL
    Country = "US",          // US visitors only
    Device = "Mobile"        // Mobile devices only
};

var result = await _dataService.GetStats(statsRequest);
```

### Getting Metrics

Get aggregated counts for different dimensions:

```csharp
// Top 10 most viewed pages (v2/v3 - use 'path' instead of 'url')
var metricsRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-7),
    EndAtDate = DateTime.UtcNow,
    Type = MetricType.path,  // v2/v3 use 'path'
    Unit = Unit.day,         // Optional in v3, but recommended
    Limit = 10,
    Timezone = "UTC"         // Optional: specify timezone
};

var result = await _dataService.GetMetrics(metricsRequest);

if (result.Status == HttpStatusCode.OK)
{
    foreach (var metric in result.Data)
    {
        Console.WriteLine($"{metric.x}: {metric.y} views");
    }
}
```

#### Umami v3: Simplified Metrics Request

In Umami v3, you can omit the `unit` parameter for simpler queries:

```csharp
// Minimal v3 metrics request
var metricsRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-24Hours),
    EndAtDate = DateTime.UtcNow,
    Type = MetricType.path,
    Limit = 100
    // Unit is optional in v3!
};

var result = await _dataService.GetMetrics(metricsRequest);
```

#### Available Metric Types

```csharp
public enum MetricType
{
    url,        // Page URLs (v1 legacy - use 'path' for v2/v3)
    path,       // URL paths (v2/v3 - recommended)
    referrer,   // Traffic sources
    browser,    // Browser analytics
    os,         // Operating systems
    device,     // Device types
    country,    // Geographic data
    region,     // States/provinces
    city,       // City-level data
    language,   // Language codes
    screen,     // Screen resolutions
    @event,     // Custom events
    query,      // Query parameters
    title,      // Page titles
    host,       // Hostnames
    entry,      // Entry pages
    exit,       // Exit pages
    hostname,   // Domain names
    tag,        // Content tags
    channel,    // Traffic channels
    domain      // Full domains
}
```

### Expanded Metrics

Get detailed engagement data including bounces and time:

```csharp
var metricsRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-30),
    EndAtDate = DateTime.UtcNow,
    Type = MetricType.url,
    Unit = Unit.day,
    Limit = 20
};

var result = await _dataService.GetExpandedMetrics(metricsRequest);

if (result.Status == HttpStatusCode.OK)
{
    foreach (var metric in result.Data)
    {
        var bounceRate = metric.visits > 0
            ? (double)metric.bounces / metric.visits * 100
            : 0;

        var avgTime = metric.visits > 0
            ? metric.totaltime / metric.visits
            : 0;

        Console.WriteLine($"\n{metric.name}");
        Console.WriteLine($"  Page Views: {metric.pageviews}");
        Console.WriteLine($"  Unique Visitors: {metric.visitors}");
        Console.WriteLine($"  Visits: {metric.visits}");
        Console.WriteLine($"  Bounce Rate: {bounceRate:F1}%");
        Console.WriteLine($"  Avg Time: {avgTime}ms");
    }
}
```

### Page Views Over Time

Get time-series data for page views:

```csharp
var request = new PageViewsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-30),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.day,
    Timezone = "America/Los_Angeles",
    Url = "/blog"  // Optional: filter by URL
};

var result = await _dataService.GetPageViews(request);

if (result.Status == HttpStatusCode.OK)
{
    foreach (var dataPoint in result.Data.pageviews)
    {
        Console.WriteLine($"{dataPoint.x}: {dataPoint.y} views");
    }
}
```

### Events Series

Get event occurrences over time:

```csharp
var request = new EventsSeriesRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-7),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.hour,
    EventName = "button-click"  // Optional: filter by event name
};

var result = await _dataService.GetEventsSeries(request);

if (result.Status == HttpStatusCode.OK)
{
    foreach (var item in result.Data)
    {
        Console.WriteLine($"Event: {item.x}, Time: {item.t}, Count: {item.y}");
    }
}
```

### Active Users

Get current active users on your site:

```csharp
var result = await _dataService.GetActiveUsers();

if (result.Status == HttpStatusCode.OK)
{
    Console.WriteLine($"Active Users: {result.Data.visitors}");
}
```

## Common Use Cases

### Popular Posts Widget (Umami v3)

This example shows a production-ready implementation for displaying popular blog posts:

```csharp
public class PopularPostsService
{
    private readonly UmamiDataService _dataService;
    private readonly IBlogService _blogService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PopularPostsService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<PopularPost?> GetPopularPost()
    {
        await _semaphore.WaitAsync();
        try
        {
            const string cacheKey = "trending_post_24h";

            if (_cache.TryGetValue(cacheKey, out PopularPost cached))
                return cached;

            // Get path metrics from last 24 hours (v3 API)
            var metricsRequest = new MetricsRequest
            {
                StartAtDate = DateTime.UtcNow.AddHours(-24),
                EndAtDate = DateTime.UtcNow,
                Type = MetricType.path,  // v3 uses 'path'
                Unit = Unit.hour,        // Optional in v3
                Timezone = "UTC",
                Limit = 100
            };

            var result = await _dataService.GetMetrics(metricsRequest);

            if (result?.Status != HttpStatusCode.OK || result.Data == null)
            {
                _logger.LogWarning("Failed to get metrics from Umami");
                return null;
            }

            _logger.LogInformation("Received {Count} paths from Umami", result.Data.Length);

            // Filter for blog posts
            var blogPosts = result.Data
                .Where(m => m.x.StartsWith("/blog/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!blogPosts.Any())
                return null;

            // Aggregate by slug (removing language extensions)
            var aggregated = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var post in blogPosts)
            {
                var slug = post.x.Substring(6).Trim('/');
                var baseSlug = slug.Contains('.')
                    ? slug.Substring(0, slug.LastIndexOf('.'))
                    : slug;

                aggregated[baseSlug] = aggregated.GetValueOrDefault(baseSlug) + post.y;
            }

            var topPost = aggregated.OrderByDescending(kvp => kvp.Value).First();

            // Get full blog post details
            var blogPost = await _blogService.GetPost(
                new BlogPostQueryModel(topPost.Key, "en"));

            var popularPost = new PopularPost
            {
                Url = $"/blog/{topPost.Key}",
                Title = blogPost?.Title ?? topPost.Key,
                Views = topPost.Value,
                PublishedDate = blogPost?.PublishedDate ?? DateTime.UtcNow,
                Categories = blogPost?.Categories ?? Array.Empty<string>()
            };

            _cache.Set(cacheKey, popularPost, TimeSpan.FromMinutes(10));
            return popularPost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting popular post");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

public class PopularPost
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public int Views { get; set; }
    public DateTime PublishedDate { get; set; }
    public string[] Categories { get; set; } = Array.Empty<string>();
}
```

### Traffic Analytics Dashboard

```csharp
public class AnalyticsDashboard
{
    private readonly UmamiDataService _dataService;

    public async Task<DashboardData> GetDashboard()
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-30);

        // Get summary stats
        var statsTask = _dataService.GetStats(new StatsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate
        });

        // Get top countries
        var countriesTask = _dataService.GetMetrics(new MetricsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate,
            Type = MetricType.country,
            Unit = Unit.day,
            Limit = 10
        });

        // Get top referrers
        var referrersTask = _dataService.GetMetrics(new MetricsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate,
            Type = MetricType.referrer,
            Unit = Unit.day,
            Limit = 10
        });

        await Task.WhenAll(statsTask, countriesTask, referrersTask);

        return new DashboardData
        {
            Stats = statsTask.Result.Data,
            TopCountries = countriesTask.Result.Data?.ToList() ?? new(),
            TopReferrers = referrersTask.Result.Data?.ToList() ?? new()
        };
    }
}
```

### Real-Time Activity Monitor

```csharp
public class ActivityMonitor : BackgroundService
{
    private readonly UmamiDataService _dataService;
    private readonly IHubContext<AnalyticsHub> _hubContext;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var activeUsers = await _dataService.GetActiveUsers();

            if (activeUsers.Status == HttpStatusCode.OK)
            {
                await _hubContext.Clients.All.SendAsync(
                    "ActiveUsersUpdate",
                    activeUsers.Data.visitors,
                    stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

## Error Handling

All API methods return `UmamiResult<T>` with comprehensive error information:

```csharp
var result = await _dataService.GetMetrics(request);

if (result.Status == HttpStatusCode.OK)
{
    // Success - use result.Data
    ProcessData(result.Data);
}
else
{
    // Error - result.Message contains helpful information
    _logger.LogWarning("Metrics failed: {Message}", result.Message);

    // Common error codes and what they mean:
    switch (result.Status)
    {
        case HttpStatusCode.BadRequest:
            // Missing or invalid parameters
            // The error message will tell you what to fix
            break;

        case HttpStatusCode.Unauthorized:
            // Authentication failed - check credentials
            break;

        case HttpStatusCode.NotFound:
            // Website ID not found - verify configuration
            break;

        case HttpStatusCode.TooManyRequests:
            // Rate limited - implement caching or backoff
            break;
    }
}
```

### Validation Errors

The library validates requests before sending:

```csharp
try
{
    var request = new MetricsRequest
    {
        Type = MetricType.url,
        // Missing required: StartAtDate, EndAtDate, Unit
    };

    var result = await _dataService.GetMetrics(request);
}
catch (ArgumentException ex)
{
    // ex.Message will contain helpful suggestions:
    // "StartAtDate is required. Suggestion: Set StartAtDate to a valid date
    // (e.g., DateTime.UtcNow.AddDays(-7) for last 7 days)."
    _logger.LogError(ex.Message);
}
```

## Performance Tips

### 1. Use Background Sender for High Traffic

```csharp
// ❌ Don't do this in high-traffic endpoints
await _client.Track("page-view");  // Blocks the request

// ✅ Do this instead
await _backgroundSender.Track("page-view");  // Non-blocking
```

### 2. Implement Caching

```csharp
public class CachedUmamiService
{
    private readonly UmamiDataService _dataService;
    private readonly IDistributedCache _cache;

    public async Task<MetricsResponseModels[]> GetTopPages()
    {
        const string cacheKey = "top_pages";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached != null)
            return JsonSerializer.Deserialize<MetricsResponseModels[]>(cached);

        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.url,
            Unit = Unit.day,
            Limit = 10
        };

        var result = await _dataService.GetMetrics(request);

        if (result.Status == HttpStatusCode.OK)
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result.Data),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                });

            return result.Data;
        }

        return Array.Empty<MetricsResponseModels>();
    }
}
```

### 3. Use Appropriate Time Units

```csharp
// ✅ Good - hourly granularity for recent data
var recentRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddHours(-24),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.hour  // Detailed view
};

// ✅ Good - daily granularity for historical data
var historicalRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-90),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.day  // Aggregated view
};

// ❌ Avoid - too granular for long periods
var inefficientRequest = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-90),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.hour  // Way too much data!
};
```

### 4. Set Reasonable Limits

```csharp
// ✅ Good - limited results for display
var request = new MetricsRequest
{
    // ...
    Limit = 10  // Top 10 items
};

// ❌ Avoid - requesting everything when you don't need it
var request = new MetricsRequest
{
    // ...
    Limit = 500  // Default, but maybe you only need 10?
};
```

## API Reference

### Event Tracking

| Method | Description |
|--------|-------------|
| `Track(eventName, eventData, url)` | Track a custom event |
| `TrackPageView(url, eventType, eventData)` | Track a page view |
| `Send(payload, eventData, eventType)` | Low-level send method |

### Analytics Data

| Method | Description |
|--------|-------------|
| `GetActiveUsers()` | Current active users |
| `GetStats(request)` | Summary statistics |
| `GetMetrics(request)` | Aggregated metrics |
| `GetExpandedMetrics(request)` | Metrics with engagement data |
| `GetPageViews(request)` | Page views over time |
| `GetEventsSeries(request)` | Events over time |

### Request Models

- `StatsRequest` - Summary statistics
- `MetricsRequest` - Metrics and expanded metrics
- `PageViewsRequest` - Page views over time
- `EventsSeriesRequest` - Events series data

All requests inherit from `BaseRequest` which requires:
- `StartAtDate` - Start of date range
- `EndAtDate` - End of date range

## Troubleshooting

### Common Issues

**Issue: 400 Bad Request**
```
Suggestion: Check that all required parameters are set correctly.
Ensure Type and Unit are specified, and dates are in the correct range.
```
**Fix:** Make sure you set `Type` and `Unit` on your `MetricsRequest`.

**Issue: 401 Unauthorized**
```
Suggestion: Verify your Umami username and password in the Analytics configuration.
```
**Fix:** Check your `appsettings.json` has correct `UserName` and `Password`.

**Issue: No data returned**
```
Suggestion: Ensure data exists for the time period.
```
**Fix:** Verify your date range contains data and Website ID is correct.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Links

- **NuGet Package**: https://www.nuget.org/packages/Umami.Net/
- **GitHub Repository**: https://github.com/scottgal/mostlylucidweb/tree/main/Umami.Net
- **Umami Documentation**: https://umami.is/docs
- **Blog Post**: https://www.mostlylucid.net/blog/addingumamitrackingclientfollowup

## Support

- GitHub Issues: [Report an Issue](https://github.com/scottgal/mostlylucidweb/issues)
- Documentation: [Umami API Docs](https://umami.is/docs/api)

---

**Made with ❤️ for the Umami community**
