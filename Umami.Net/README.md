# Umami.Net

## UmamiClient

This is a .NET Core client for the Umami tracking API.
It's based on the Umami Node client, which can be found [here](https://github.com/umami-software/node).

You can see how to set up Umami as a docker
container [here](https://www.mostlylucid.net/blog/usingumamiforlocalanalytics).
You can read more detail about it's creation on my
blog [here](https://www.mostlylucid.net/blog/addingumamitrackingclientfollowup).

To use this client you need the following appsettings.json configuration:

```json
{
  "Analytics":{
    "UmamiPath" : "https://umamilocal.mostlylucid.net",
    "WebsiteId" : "32c2aa31-b1ac-44c0-b8f3-ff1f50403bee"
  },
}
```

Where `UmamiPath` is the path to your Umami instance and `WebsiteId` is the id of the website you want to track.

To use the client you need to add the following to your `Program.cs`:

```csharp
using Umami.Net;

services.SetupUmamiClient(builder.Configuration);
```

This will add the Umami client to the services collection.

You can then use the client in two ways:

## Track

1. Inject the `UmamiClient` into your class and call the `Track` method:

```csharp    
 // Inject UmamiClient umamiClient
 await umamiClient.Track("Search", new UmamiEventData(){{"query", encodedQuery}});
```

2. Use the `UmamiBackgroundSender` to track events in the background (this uses an `IHostedService` to send events in
   the background):

```csharp
 // Inject UmamiBackgroundSender umamiBackgroundSender
await umamiBackgroundSender.Track("Search", new UmamiEventData(){{"query", encodedQuery}});
```

The client will send the event to the Umami API and it will be stored.

The `UmamiEventData` is a dictionary of key value pairs that will be sent to the Umami API as the event data.

There are additionally more low level methods that can be used to send events to the Umami API.

## Track PageView

There's also a convenience method to track a page view. This will send an event to the Umami API with the url set (which
counts as a pageview).

```csharp
  await  umamiBackgroundSender.TrackPageView("api/search/" + encodedQuery, "searchEvent", eventData: new UmamiEventData(){{"query", encodedQuery}});
  
   await umamiClient.TrackPageView("api/search/" + encodedQuery, "searchEvent", eventData: new UmamiEventData(){{"query", encodedQuery}});
```

Here we're setting the url to "api/search/" + encodedQuery and the event type to "searchEvent". We're also passing in a
dictionary of key value pairs as the event data.

## Raw 'Send' method

On both the `UmamiClient` and `UmamiBackgroundSender` you can call the following method.

```csharp


 Send(UmamiPayload? payload = null, UmamiEventData? eventData = null,
        string eventType = "event")
```

If you don't pass in a `UmamiPayload` object, the client will create one for you using the `WebsiteId` from the
appsettings.json.

```csharp
    public  UmamiPayload GetPayload(string? url = null, UmamiEventData? data = null)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var request = httpContext?.Request;

        var payload = new UmamiPayload
        {
            Website = settings.WebsiteId,
            Data = data,
            Url = url ?? httpContext?.Request?.Path.Value,
            IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = request?.Headers["User-Agent"].FirstOrDefault(),
            Referrer = request?.Headers["Referer"].FirstOrDefault(),
           Hostname = request?.Host.Host,
        };
        
        return payload;
    }

```

You can see that this populates the `UmamiPayload` object with the `WebsiteId` from the appsettings.json, the `Url`,
`IpAddress`, `UserAgent`, `Referrer` and `Hostname` from the `HttpContext`.

NOTE: eventType can only be "event" or "identify" as per the Umami API.

## UmamiData

There's also a service that can be used to pull data from the Umami API. This is a service that allows me to pull data
from my Umami instance to use in stuff like sorting posts by popularity etc...

To set it up you need to add a username and password for your umami instance to the Analytics element in your settings
file:

```json
    "Analytics":{
        "UmamiPath" : "https://umami.mostlylucid.net",
        "WebsiteId" : "1e3b7657-9487-4857-a9e9-4e1920aa8c42",
        "UserName": "admin",
        "Password": ""
     
    }
```

Then in your `Program.cs` you set up the `UmamiDataService` as follows:

```csharp
    services.SetupUmamiData(config);
```

You can then inject the `UmamiDataService` into your class and use it to pull data from the Umami API.

# Usage

Now you have the `UmamiDataService` in your service collection you can start using it!

## Methods

The methods are all from the Umami API definition you can read about them here:
https://umami.is/docs/api/website-stats

All returns are wrapped in an `UmamiResults<T>` object which has a `Success` property and a `Result` property. The
`Result` property is the object returned from the Umami API.

```csharp
public record UmamiResult<T>(HttpStatusCode Status, string Message, T? Data);
```

All requests apart from `ActiveUsers` have a base request object with two compulsory properties. I added convenience
DateTimes to the base request object to make it easier to set the start and end dates.

```csharp
public class BaseRequest
{
    [QueryStringParameter("startAt", isRequired: true)]
    public long StartAt => StartAtDate.ToMilliseconds(); // Timestamp (in ms) of starting date
    [QueryStringParameter("endAt", isRequired: true)]
    public long EndAt => EndAtDate.ToMilliseconds(); // Timestamp (in ms) of end date
    public DateTime StartAtDate { get; set; }
    public DateTime EndAtDate { get; set; }
}
```

The service has the following methods:

### Active Users

This just gets the total number of CURRENT active users on the site

```csharp
public async Task<UmamiResult<ActiveUsersResponse>> GetActiveUsers()
```

### Stats

This returns a bunch of statistics about the site, including the number of users, page views, etc.

```csharp
public async Task<UmamiResult<StatsResponseModels>> GetStats(StatsRequest statsRequest)    
```

You may set a number of parameters to filter the data returned from the API. For instance using `url` will return the
stats for a specific URL.

<details>
<summary>StatsRequest object</summary>

```csharp
public class StatsRequest : BaseRequest
{
    [QueryStringParameter("url")]
    public string? Url { get; set; } // Name of URL
    
    [QueryStringParameter("referrer")]
    public string? Referrer { get; set; } // Name of referrer
    
    [QueryStringParameter("title")]
    public string? Title { get; set; } // Name of page title
    
    [QueryStringParameter("query")]
    public string? Query { get; set; } // Name of query
    
    [QueryStringParameter("event")]
    public string? Event { get; set; } // Name of event
    
    [QueryStringParameter("host")]
    public string? Host { get; set; } // Name of hostname
    
    [QueryStringParameter("os")]
    public string? Os { get; set; } // Name of operating system
    
    [QueryStringParameter("browser")]
    public string? Browser { get; set; } // Name of browser
    
    [QueryStringParameter("device")]
    public string? Device { get; set; } // Name of device (e.g., Mobile)
    
    [QueryStringParameter("country")]
    public string? Country { get; set; } // Name of country
    
    [QueryStringParameter("region")]
    public string? Region { get; set; } // Name of region/state/province
    
    [QueryStringParameter("city")]
    public string? City { get; set; } // Name of city
}
```

</details>

The JSON object Umami returns is as follows.

```json
{
  "pageviews": { "value": 5, "change": 5 },
  "visitors": { "value": 1, "change": 1 },
  "visits": { "value": 3, "change": 2 },
  "bounces": { "value": 0, "change": 0 },
  "totaltime": { "value": 4, "change": 4 }
}
```

This is wrapped inside my `StatsResponseModel` object.

```csharp
namespace Umami.Net.UmamiData.Models.ResponseObjects;

public class StatsResponseModels
{
    public Pageviews pageviews { get; set; }
    public Visitors visitors { get; set; }
    public Visits visits { get; set; }
    public Bounces bounces { get; set; }
    public Totaltime totaltime { get; set; }


    public class Pageviews
    {
        public int value { get; set; }
        public int prev { get; set; }
    }

    public class Visitors
    {
        public int value { get; set; }
        public int prev { get; set; }
    }

    public class Visits
    {
        public int value { get; set; }
        public int prev { get; set; }
    }

    public class Bounces
    {
        public int value { get; set; }
        public int prev { get; set; }
    }

    public class Totaltime
    {
        public int value { get; set; }
        public int prev { get; set; }
    }
}
```

### Metrics

Metrics in Umami provide you the number of views for specific types of properties.

#### Events

One example of these is Events`:

'Events' in Umami are specific items you can track on a site. When tracking events using Umami.Net you can set a number
of properties which are tracked with the event name. For instance here I track `Search` requests with the URL and the
search term.

```csharp
       await  umamiBackgroundSender.Track( "searchEvent", eventData: new UmamiEventData(){{"query", encodedQuery}});
```

To fetch data about this event you would use the `Metrics` method:

```csharp
public async Task<UmamiResult<MetricsResponseModels[]>> GetMetrics(MetricsRequest metricsRequest)
```

As with the other methods this accepts the `MetricsRequest` object (with the compulsory `BaseRequest` properties) and a
number of optional properties to filter the data.

<details>
<summary>MetricsRequest object</summary>

```csharp
public class MetricsRequest : BaseRequest
{
    [QueryStringParameter("type", isRequired: true)]
    public MetricType Type { get; set; } // Metrics type

    [QueryStringParameter("url")]
    public string? Url { get; set; } // Name of URL
    
    [QueryStringParameter("referrer")]
    public string? Referrer { get; set; } // Name of referrer
    
    [QueryStringParameter("title")]
    public string? Title { get; set; } // Name of page title
    
    [QueryStringParameter("query")]
    public string? Query { get; set; } // Name of query
    
    [QueryStringParameter("host")]
    public string? Host { get; set; } // Name of hostname
    
    [QueryStringParameter("os")]
    public string? Os { get; set; } // Name of operating system
    
    [QueryStringParameter("browser")]
    public string? Browser { get; set; } // Name of browser
    
    [QueryStringParameter("device")]
    public string? Device { get; set; } // Name of device (e.g., Mobile)
    
    [QueryStringParameter("country")]
    public string? Country { get; set; } // Name of country
    
    [QueryStringParameter("region")]
    public string? Region { get; set; } // Name of region/state/province
    
    [QueryStringParameter("city")]
    public string? City { get; set; } // Name of city
    
    [QueryStringParameter("language")]
    public string? Language { get; set; } // Name of language
    
    [QueryStringParameter("event")]
    public string? Event { get; set; } // Name of event
    
    [QueryStringParameter("limit")]
    public int? Limit { get; set; } = 500; // Number of events returned (default: 500)
}
```

</details>

Here you can see that you can specify a number of properties in the request element to specify what metrics you want to
return.

You can also set a `Limit` property to limit the number of results returned.

For instance to get the event over the past day I mentioned above you would use the following request:

```csharp
var metricsRequest = new MetricsRequest
{
    StartAtDate = DateTime.Now.AddDays(-1),
    EndAtDate = DateTime.Now,
    Type = MetricType.@event,
    Event = "searchEvent"
};
```

The JSON object returned from the API is as follows:

```json
[
  { "x": "searchEvent", "y": 46 }
]
```

And again I wrap this in my `MetricsResponseModels` object.

```csharp
public class MetricsResponseModels
{
    public string x { get; set; }
    public int y { get; set; }
}
```

Where x is the event name and y is the number of times it has been triggered.

#### Page Views

One of the most useful metrics is the number of page views. This is the number of times a page has been viewed on the
site. Below is the test I use to get the number of page views over the past 30 days. You'll note the `Type` parameter is
set as `MetricType.url` however this is also the default value so you don't need to set it.

```csharp
  [Fact]
    public async Task Metrics_StartEnd()
    {
        var setup = new SetupUmamiData();
        var serviceProvider = setup.Setup();
        var websiteDataService = serviceProvider.GetRequiredService<UmamiDataService>();
        
        var metrics = await websiteDataService.GetMetrics(new MetricsRequest()
        {
            StartAtDate = DateTime.Now.AddDays(-30),
            EndAtDate = DateTime.Now,
            Type = MetricType.url,
            Limit = 500
        });
        Assert.NotNull(metrics);
        Assert.Equal( HttpStatusCode.OK, metrics.Status);

    }
```

This returns a `MetricsResponse` object which has the following JSON structure:

```json
[
  {
    "x": "/",
    "y": 1
  },
  {
    "x": "/blog",
    "y": 1
  },
  {
    "x": "/blog/usingumamidataforwebsitestats",
    "y": 1
  }
]
```

Where `x` is the URL and `y` is the number of times it has been viewed.

### PageViews

This returns the number of page views for a specific URL.

Again here is a test I use for this method:

```csharp
    [Fact]
    public async Task PageViews_StartEnd_Day_Url()
    {
        var setup = new SetupUmamiData();
        var serviceProvider = setup.Setup();
        var websiteDataService = serviceProvider.GetRequiredService<UmamiDataService>();
    
        var pageViews = await websiteDataService.GetPageViews(new PageViewsRequest()
        {
            StartAtDate = DateTime.Now.AddDays(-7),
            EndAtDate = DateTime.Now,
            Unit = Unit.day,
            Url = "/blog"
        });
        Assert.NotNull(pageViews);
        Assert.Equal( HttpStatusCode.OK, pageViews.Status);

    }
```

This returns a `PageViewsResponse` object which has the following JSON structure:

```json
[
  {
    "date": "2024-09-06 00:00",
    "value": 1
  }
]
```

Where `date` is the date and `value` is the number of page views, this is repeated for each day in the range specified (
or hour, month, etc. depending on the `Unit` property).

As with the other methods this accepts the `PageViewsRequest` object (with the compulsory `BaseRequest` properties) and
a number of optional properties to filter the data.

<details>
<summary>PageViewsRequest object</summary>

```csharp
public class PageViewsRequest : BaseRequest
{
    // Required properties

    [QueryStringParameter("unit", isRequired: true)]
    public Unit Unit { get; set; } = Unit.day; // Time unit (year | month | hour | day)
    
    [QueryStringParameter("timezone")]
    [TimeZoneValidator]
    public string Timezone { get; set; }

    // Optional properties
    [QueryStringParameter("url")]
    public string? Url { get; set; } // Name of URL
    [QueryStringParameter("referrer")]
    public string? Referrer { get; set; } // Name of referrer
    [QueryStringParameter("title")]
    public string? Title { get; set; } // Name of page title
    [QueryStringParameter("host")]
    public string? Host { get; set; } // Name of hostname
    [QueryStringParameter("os")]
    public string? Os { get; set; } // Name of operating system
    [QueryStringParameter("browser")]
    public string? Browser { get; set; } // Name of browser
    [QueryStringParameter("device")]
    public string? Device { get; set; } // Name of device (e.g., Mobile)
    [QueryStringParameter("country")]
    public string? Country { get; set; } // Name of country
    [QueryStringParameter("region")]
    public string? Region { get; set; } // Name of region/state/province
    [QueryStringParameter("city")]
    public string? City { get; set; } // Name of city
}
```

</details>

As with the other methods you can set a number of properties to filter the data returned from the API, for instance you
could set the
`Country` property to get the number of page views from a specific country.

# Using the Service

In this site I have some code which lets me use this service to get the number of views each blog page has. In the code
below I take a start and end date and a prefix (which is `/blog` in my case) and get the number of views for each page
in the blog.

I then cache this data for an hour so I don't have to keep hitting the Umami API.

```csharp
public class UmamiDataSortService(
    UmamiDataService dataService,
    IMemoryCache cache)
{
    public async Task<List<MetricsResponseModels>?> GetMetrics(DateTime startAt, DateTime endAt, string prefix="" )
    {
        using var activity = Log.Logger.StartActivity("GetMetricsWithPrefix");
        try
        {
            var cacheKey = $"Metrics_{startAt}_{endAt}_{prefix}";
            if (cache.TryGetValue(cacheKey, out List<MetricsResponseModels>? metrics))
            {
                activity?.AddProperty("CacheHit", true);
                return metrics;
            }
            activity?.AddProperty("CacheHit", false);
            var metricsRequest = new MetricsRequest()
            {
                StartAtDate = startAt,
                EndAtDate = endAt,
                Type = MetricType.url,
                Limit = 500
            };
            var metricRequest = await dataService.GetMetrics(metricsRequest);

            if(metricRequest.Status != HttpStatusCode.OK)
            {
                return null;
            }
            var filteredMetrics = metricRequest.Data.Where(x => x.x.StartsWith(prefix)).ToList();
            cache.Set(cacheKey, filteredMetrics, TimeSpan.FromHours(1));
            activity?.AddProperty("MetricsCount", filteredMetrics?.Count()?? 0);
            activity?.Complete();
            return filteredMetrics;
        }
        catch (Exception e)
        {
            activity?.Complete(LogEventLevel.Error, e);
         
            return null;
        }
    }

```

---

# Complete API Reference

## New Endpoints (v2.0+)

### Events Series

Get event occurrences over time with timestamps.

```csharp
// Get all events over the past week
var request = new EventsSeriesRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-7),
    EndAtDate = DateTime.UtcNow,
    Unit = Unit.day,
    EventName = "button-click" // Optional: filter by event name
};

var result = await dataService.GetEventsSeries(request);

// Or use convenience method
var result = await dataService.GetEventsSeries(
    startDate: DateTime.UtcNow.AddDays(-7),
    endDate: DateTime.UtcNow,
    unit: Unit.day,
    timezone: "America/Los_Angeles",
    eventName: "purchase"
);

// Response format:
// [
//   { "x": "signup", "t": "2024-01-15T10:00:00Z", "y": 42 },
//   { "x": "signup", "t": "2024-01-16T10:00:00Z", "y": 38 }
// ]
```

### Expanded Metrics

Get detailed engagement metrics including bounces and total time.

```csharp
var request = new MetricsRequest
{
    StartAtDate = DateTime.UtcNow.AddDays(-30),
    EndAtDate = DateTime.UtcNow,
    Type = MetricType.url,
    Limit = 50
};

var result = await dataService.GetExpandedMetrics(request);

// Response includes:
// - name: The dimension value (e.g., URL, OS name)
// - pageviews: Total page hits
// - visitors: Unique visitor count
// - visits: Unique visit count
// - bounces: Single-page visits
// - totaltime: Total time on site in milliseconds

foreach (var metric in result.Data)
{
    Console.WriteLine($"{metric.name}: {metric.visitors} visitors, " +
                     $"{metric.pageviews} views, " +
                     $"avg time: {metric.totaltime / metric.visits}ms");
}
```

## All Supported Metric Types

```csharp
public enum MetricType
{
    url,        // Page URLs - most common for page views
    path,       // URL paths only
    entry,      // Entry pages
    exit,       // Exit pages
    title,      // Page titles
    query,      // Query string parameters
    referrer,   // Traffic sources/referrers
    browser,    // Browser names (Chrome, Firefox, etc.)
    os,         // Operating systems (Windows, macOS, etc.)
    device,     // Device types (Mobile, Desktop, Tablet)
    country,    // Country codes (US, UK, etc.)
    region,     // States/provinces
    city,       // City names
    language,   // Language codes (en, es, fr, etc.)
    screen,     // Screen resolutions
    event,      // Custom event names
    hostname,   // Domain names
    tag,        // Content tags
    channel,    // Traffic channels (organic, social, etc.)
    domain      // Full domains
}
```

## Complete Example: Analytics Dashboard

Here's a complete example showing how to build an analytics dashboard:

```csharp
public class AnalyticsDashboardService
{
    private readonly UmamiDataService _dataService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AnalyticsDashboardService> _logger;

    public AnalyticsDashboardService(
        UmamiDataService dataService,
        IMemoryCache cache,
        ILogger<AnalyticsDashboardService> logger)
    {
        _dataService = dataService;
        _cache = cache;
        _logger = logger;
    }

    // Get overview stats for the past 30 days
    public async Task<DashboardStats> GetDashboardStats()
    {
        var cacheKey = "dashboard_stats_30d";

        if (_cache.TryGetValue(cacheKey, out DashboardStats? cached))
            return cached!;

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-30);

        // Get summary statistics
        var statsRequest = new StatsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate
        };

        var statsResult = await _dataService.GetStats(statsRequest);

        // Get top pages with detailed metrics
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate,
            Type = MetricType.url,
            Limit = 10
        };

        var expandedMetrics = await _dataService.GetExpandedMetrics(metricsRequest);

        // Get traffic sources
        var referrerRequest = new MetricsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate,
            Type = MetricType.referrer,
            Limit = 10
        };

        var referrers = await _dataService.GetMetrics(referrerRequest);

        // Get geographic distribution
        var countryRequest = new MetricsRequest
        {
            StartAtDate = startDate,
            EndAtDate = endDate,
            Type = MetricType.country,
            Limit = 20
        };

        var countries = await _dataService.GetMetrics(countryRequest);

        var dashboard = new DashboardStats
        {
            TotalPageViews = statsResult.Data?.pageviews.value ?? 0,
            UniqueVisitors = statsResult.Data?.visitors.value ?? 0,
            BounceRate = CalculateBounceRate(statsResult.Data),
            TopPages = expandedMetrics.Data?.ToList() ?? new(),
            TopReferrers = referrers.Data?.ToList() ?? new(),
            CountryDistribution = countries.Data?.ToList() ?? new()
        };

        _cache.Set(cacheKey, dashboard, TimeSpan.FromMinutes(15));
        return dashboard;
    }

    // Get real-time active users
    public async Task<int> GetActiveUsers()
    {
        var result = await _dataService.GetActiveUsers();
        return result.Data?.visitors ?? 0;
    }

    // Get page view trends
    public async Task<List<TimeSeriesDataPoint>> GetPageViewTrends(int days = 30)
    {
        var request = new PageViewsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-days),
            EndAtDate = DateTime.UtcNow,
            Unit = days <= 2 ? Unit.hour : Unit.day,
            Timezone = "UTC"
        };

        var result = await _dataService.GetPageViews(request);

        return result.Data?.pageviews
            .Select(pv => new TimeSeriesDataPoint
            {
                Timestamp = DateTime.Parse(pv.x),
                Value = pv.y
            })
            .ToList() ?? new();
    }

    // Get most popular blog posts
    public async Task<List<BlogPostStats>> GetPopularBlogPosts(int limit = 10)
    {
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-30),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.url,
            Limit = 500
        };

        var result = await _dataService.GetExpandedMetrics(request);

        if (result.Data == null)
            return new();

        // Filter for blog posts and aggregate language variants
        var blogPosts = new Dictionary<string, BlogPostStats>();

        foreach (var metric in result.Data.Where(m => m.name.StartsWith("/blog/")))
        {
            var slug = metric.name.Substring(6);
            var baseSlug = slug.Contains('.')
                ? slug.Substring(0, slug.LastIndexOf('.'))
                : slug;

            if (blogPosts.TryGetValue(baseSlug, out var existing))
            {
                existing.PageViews += metric.pageviews;
                existing.UniqueVisitors += metric.visitors;
                existing.TotalTime += metric.totaltime;
            }
            else
            {
                blogPosts[baseSlug] = new BlogPostStats
                {
                    Slug = baseSlug,
                    Url = $"/blog/{baseSlug}",
                    PageViews = metric.pageviews,
                    UniqueVisitors = metric.visitors,
                    TotalTime = metric.totaltime,
                    AverageTime = metric.visits > 0
                        ? metric.totaltime / metric.visits
                        : 0
                };
            }
        }

        return blogPosts.Values
            .OrderByDescending(p => p.PageViews)
            .Take(limit)
            .ToList();
    }

    private double CalculateBounceRate(StatsResponseModels? stats)
    {
        if (stats == null || stats.visits.value == 0)
            return 0;

        return (double)stats.bounces.value / stats.visits.value * 100;
    }
}

public class DashboardStats
{
    public int TotalPageViews { get; set; }
    public int UniqueVisitors { get; set; }
    public double BounceRate { get; set; }
    public List<ExpandedMetricsResponseModel> TopPages { get; set; } = new();
    public List<MetricsResponseModels> TopReferrers { get; set; } = new();
    public List<MetricsResponseModels> CountryDistribution { get; set; } = new();
}

public class TimeSeriesDataPoint
{
    public DateTime Timestamp { get; set; }
    public int Value { get; set; }
}

public class BlogPostStats
{
    public string Slug { get; set; } = "";
    public string Url { get; set; } = "";
    public int PageViews { get; set; }
    public int UniqueVisitors { get; set; }
    public long TotalTime { get; set; }
    public long AverageTime { get; set; }
}
```

## Error Handling Best Practices

Always check the HTTP status code and handle errors appropriately:

```csharp
public async Task<List<MetricsResponseModels>> GetTopPagesWithRetry()
{
    var maxRetries = 3;
    var retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
        {
            var request = new MetricsRequest
            {
                StartAtDate = DateTime.UtcNow.AddDays(-7),
                EndAtDate = DateTime.UtcNow,
                Type = MetricType.url,
                Limit = 10
            };

            var result = await _dataService.GetMetrics(request);

            if (result.Status == HttpStatusCode.OK && result.Data != null)
            {
                return result.Data.ToList();
            }

            if (result.Status == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Authentication failed, will retry");
                retryCount++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                continue;
            }

            _logger.LogError("Failed to get metrics: {Status} - {Message}",
                result.Status, result.Message);
            return new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting metrics");
            retryCount++;

            if (retryCount >= maxRetries)
                throw;

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
        }
    }

    return new();
}
```

## Caching Strategy

Implement smart caching to reduce API calls:

```csharp
public class CachedUmamiService
{
    private readonly UmamiDataService _dataService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedUmamiService> _logger;

    // Cache durations based on data volatility
    private static readonly Dictionary<string, TimeSpan> CacheDurations = new()
    {
        ["active-users"] = TimeSpan.FromSeconds(30),      // Real-time
        ["hourly-stats"] = TimeSpan.FromMinutes(5),       // Recent
        ["daily-stats"] = TimeSpan.FromMinutes(15),       // Today
        ["monthly-stats"] = TimeSpan.FromHours(1),        // Historical
        ["popular-posts"] = TimeSpan.FromMinutes(30)      // Trending
    };

    public async Task<T?> GetCachedData<T>(
        string cacheKey,
        string cacheCategory,
        Func<Task<UmamiResult<T>>> fetchFunc)
    {
        var fullKey = $"umami:{cacheCategory}:{cacheKey}";

        // Try cache first
        var cachedData = await _cache.GetStringAsync(fullKey);
        if (cachedData != null)
        {
            return JsonSerializer.Deserialize<T>(cachedData);
        }

        // Fetch from API
        var result = await fetchFunc();

        if (result.Status == HttpStatusCode.OK && result.Data != null)
        {
            var serialized = JsonSerializer.Serialize(result.Data);
            var duration = CacheDurations.GetValueOrDefault(
                cacheCategory,
                TimeSpan.FromMinutes(10)
            );

            await _cache.SetStringAsync(
                fullKey,
                serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = duration
                }
            );

            return result.Data;
        }

        _logger.LogWarning("Failed to fetch data: {Status}", result.Status);
        return default;
    }
}
```

## Background Polling Service

Implement a background service to keep popular content cached:

```csharp
public class UmamiPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UmamiPollingService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(15);

    public UmamiPollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<UmamiPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Umami Polling Service started");

        // Wait 2 minutes before first poll
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollUmamiData(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Umami data");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("Umami Polling Service stopped");
    }

    private async Task PollUmamiData(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<UmamiDataService>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        // Update popular posts
        var metricsRequest = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.url,
            Limit = 50
        };

        var result = await dataService.GetExpandedMetrics(metricsRequest);

        if (result.Status == HttpStatusCode.OK && result.Data != null)
        {
            cache.Set("popular_content", result.Data, TimeSpan.FromMinutes(20));
            _logger.LogInformation("Updated popular content cache with {Count} items",
                result.Data.Length);
        }

        // Update active users count
        var activeUsers = await dataService.GetActiveUsers();
        if (activeUsers.Status == HttpStatusCode.OK)
        {
            cache.Set("active_users", activeUsers.Data?.visitors ?? 0,
                TimeSpan.FromSeconds(30));
        }
    }
}

// Register in Program.cs
services.AddHostedService<UmamiPollingService>();
```

## Testing

Example integration tests:

```csharp
public class UmamiDataServiceTests : IClassFixture<UmamiTestFixture>
{
    private readonly UmamiDataService _dataService;

    public UmamiDataServiceTests(UmamiTestFixture fixture)
    {
        _dataService = fixture.DataService;
    }

    [Fact]
    public async Task GetStats_ReturnsValidData()
    {
        // Arrange
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow
        };

        // Act
        var result = await _dataService.GetStats(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.pageviews.value >= 0);
        Assert.True(result.Data.visitors.value >= 0);
    }

    [Fact]
    public async Task GetMetrics_WithUrlType_ReturnsPageViews()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-30),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.url,
            Limit = 10
        };

        // Act
        var result = await _dataService.GetMetrics(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Length <= 10);
        Assert.All(result.Data, metric =>
        {
            Assert.NotEmpty(metric.x);
            Assert.True(metric.y >= 0);
        });
    }

    [Fact]
    public async Task GetExpandedMetrics_ReturnsDetailedData()
    {
        // Arrange
        var request = new MetricsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Type = MetricType.url,
            Limit = 5
        };

        // Act
        var result = await _dataService.GetExpandedMetrics(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.NotNull(result.Data);
        Assert.All(result.Data, metric =>
        {
            Assert.NotEmpty(metric.name);
            Assert.True(metric.pageviews >= 0);
            Assert.True(metric.visitors >= 0);
            Assert.True(metric.visits >= 0);
            Assert.True(metric.bounces >= 0);
            Assert.True(metric.totaltime >= 0);
        });
    }

    [Fact]
    public async Task GetActiveUsers_ReturnsNonNegativeCount()
    {
        // Act
        var result = await _dataService.GetActiveUsers();

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.visitors >= 0);
    }

    [Fact]
    public async Task GetEventsSeries_ReturnsTimeSeriesData()
    {
        // Arrange
        var request = new EventsSeriesRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-7),
            EndAtDate = DateTime.UtcNow,
            Unit = Unit.day
        };

        // Act
        var result = await _dataService.GetEventsSeries(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.NotNull(result.Data);
        Assert.All(result.Data, item =>
        {
            Assert.NotEmpty(item.x);
            Assert.NotEmpty(item.t);
            Assert.True(item.y >= 0);
        });
    }
}

public class UmamiTestFixture : IDisposable
{
    public UmamiDataService DataService { get; }

    public UmamiTestFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json")
            .Build();

        var services = new ServiceCollection();
        services.SetupUmamiData(configuration);
        var provider = services.BuildServiceProvider();

        DataService = provider.GetRequiredService<UmamiDataService>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

## Performance Tips

1. **Batch Requests**: Combine multiple metric types in a single time range
2. **Use Appropriate Units**: `Unit.hour` for recent data, `Unit.day` for historical
3. **Limit Results**: Set reasonable `Limit` values (default: 500)
4. **Cache Aggressively**: Historical data doesn't change
5. **Background Processing**: Use hosted services for expensive queries
6. **Connection Pooling**: Reuse HttpClient (already configured)

## Troubleshooting

### Authentication Failures
- Verify username/password in configuration
- Check Umami instance is accessible
- Ensure user has admin role

### No Data Returned
- Verify website ID is correct
- Check date ranges (not too far in the past)
- Ensure data exists for the time period

### Slow Responses
- Reduce `Limit` value
- Use longer time `Unit` (day vs hour)
- Implement caching
- Check network latency to Umami instance

## Version History

- **2.0.0** - Added Events Series, Expanded Metrics, improved error handling
- **1.5.0** - Added background sender, retry logic
- **1.0.0** - Initial release with basic tracking and stats

## License

MIT License

## Support

- GitHub Issues: https://github.com/scottgal/mostlylucid.dse
- Umami Docs: https://umami.is/docs
- Blog Posts: https://www.mostlylucid.net/blog

```