# Towards 1.0: Making Umami.NET Production Ready

<!-- category -- C#, Umami, Analytics, Open Source -->
<datetime class="hidden">2025-01-20T14:30</datetime>

## Introduction

When I first integrated [Umami analytics](https://umami.is/) into my blog platform, I quickly ran into a frustrating reality: Umami's API documentation is... let's be charitable and call it "minimal." Error messages are cryptic at best, non-existent at worst. Parameters change between versions without warning. And don't even get me started on the "beep boop" bot detection response.

So I built [Umami.NET](https://github.com/scottgal/mostlylucidweb/tree/main/Umami.Net) - not just as a simple HTTP wrapper, but as a production-ready client library that compensates for all of Umami's quirks. As I've been working towards a 1.0 release, I've focused on three critical areas:

1. **Comprehensive validation with helpful error messages**
2. **Robust testing infrastructure**
3. **Graceful error handling for real-world scenarios**

Let me walk you through what makes this library production-ready.

## The Problem: Umami's Documentation Gap

Here's what you're up against when working with Umami's API directly:

- **No input validation** - Send a malformed GUID? Silent failure.
- **Cryptic responses** - Bot detected? You get `"beep boop"`. That's it.
- **Breaking changes** - Parameters renamed between versions (`path` vs `url`, `hostname` vs `host`)
- **Timestamp confusion** - Unix milliseconds? Seconds? Good luck figuring it out.
- **JWT responses** - Sometimes full payloads, sometimes just a visitor ID. No documentation explaining when or why.

This is fine for a quick prototype, but for production? You need something better.

## Guard Clauses: Fail Fast with Context

The worst bugs are the ones that fail silently. Umami.NET catches configuration errors at startup before they can cause problems in production.

### Configuration Validation

```csharp
public static void ValidateSettings(UmamiClientSettings settings)
{
    // Guard: UmamiPath is required
    if (string.IsNullOrEmpty(settings.UmamiPath))
        throw new ArgumentNullException(settings.UmamiPath,
            "UmamiUrl is required");

    // Guard: UmamiPath must be valid URI
    if (!Uri.TryCreate(settings.UmamiPath, UriKind.Absolute, out _))
        throw new FormatException(
            "UmamiUrl must be a valid Uri");

    // Guard: WebsiteId is required
    if (string.IsNullOrEmpty(settings.WebsiteId))
        throw new ArgumentNullException(settings.WebsiteId,
            "WebsiteId is required");

    // Guard: WebsiteId must be valid GUID
    if (!Guid.TryParseExact(settings.WebsiteId, "D", out _))
        throw new FormatException(
            "WebSiteId must be a valid Guid");
}
```

This runs at startup in your `Program.cs`. If your configuration is wrong, you know immediately - not when the first analytics event tries to send.

### Request Validation with Helpful Suggestions

But the real magic is in the query string helper. Check out these error messages:

```csharp
public static string ToQueryString(this object obj)
{
    if (obj == null)
    {
        throw new ArgumentNullException(nameof(obj),
            "Cannot convert null object to query string. " +
            "Suggestion: Ensure you create and populate a request object " +
            "before calling ToQueryString().");
    }

    foreach (var property in objectType.GetProperties())
    {
        if (attribute.IsRequired)
        {
            if (propertyValue == null)
            {
                throw new ArgumentException(
                    $"Required parameter '{propertyName}' " +
                    $"(property '{property.Name}') cannot be null. " +
                    $"Suggestion: Set the {property.Name} property " +
                    $"on your {objectType.Name} object...",
                    property.Name);
            }

            // For strings, check for empty/whitespace
            if (propertyValue is string strValue &&
                string.IsNullOrWhiteSpace(strValue))
            {
                throw new ArgumentException(
                    $"Required parameter '{propertyName}' " +
                    $"cannot be empty or whitespace. " +
                    $"Suggestion: Set {property.Name} to a valid non-empty value.",
                    property.Name);
            }
        }
    }
}
```

Notice the `Suggestion:` prefix? Every error message tells you **what went wrong** and **how to fix it**. This is the documentation Umami should have provided.

### Date Range Validation

When building analytics queries, date ranges can be tricky. The library catches these mistakes for you:

```csharp
public DateTime StartAtDate
{
    get => _startAtDate;
    set
    {
        if (_endAtDate != default && value > _endAtDate)
        {
            throw new ArgumentException(
                $"StartAtDate ({value:O}) must be before EndAtDate ({_endAtDate:O}). " +
                "Suggestion: Set StartAtDate to an earlier date or adjust EndAtDate.",
                nameof(StartAtDate));
        }
        _startAtDate = value;
    }
}

public virtual void Validate()
{
    if (StartAtDate == default)
    {
        throw new InvalidOperationException(
            "StartAtDate is required. " +
            "Suggestion: Set StartAtDate to a valid date " +
            "(e.g., DateTime.UtcNow.AddDays(-7) for last 7 days).");
    }
}
```

## Handling Umami's Quirks

### The "Beep Boop" Problem

Umami's bot detection returns a plain text response: `"beep boop"`. Not JSON. Not a proper status code. Just... beep boop.

Here's how Umami.NET handles it:

```csharp
public async Task<UmamiDataResponse> DecodeResponse(HttpResponseMessage response)
{
    var responseString = await response.Content.ReadAsStringAsync();

    // Handle bot detection
    if (responseString.Contains("beep") && responseString.Contains("boop"))
    {
        logger.LogWarning("Bot detected - data not stored in Umami");
        return new UmamiDataResponse(ResponseStatus.BotDetected);
    }

    // Handle JWT response
    try
    {
        var jwtPayload = DecodeJwt(responseString);
        return new UmamiDataResponse(ResponseStatus.Success, jwtPayload);
    }
    catch (Exception e)
    {
        logger.LogError(e, "Failed to decode response");
        return new UmamiDataResponse(ResponseStatus.Failed);
    }
}
```

Your code gets a clean enum:

```csharp
public enum ResponseStatus
{
    Failed,
    BotDetected,
    Success
}
```

No more parsing weird responses - just check the status.

### Parameter Name Changes

Umami renamed parameters between API versions. Did they document this? Of course not. The library handles both:

```csharp
// Support both old and new parameter names
request.Path = queryParams["path"] ?? queryParams["url"];
request.Hostname = queryParams["hostname"] ?? queryParams["host"];
```

### Timestamp Conversions

Umami uses Unix milliseconds for timestamps. Here's a helper that makes it painless:

```csharp
public static long ToMilliseconds(this DateTime dateTime)
{
    var dateTimeOffset = new DateTimeOffset(dateTime.ToUniversalTime());
    return dateTimeOffset.ToUnixTimeMilliseconds();
}
```

Now you can work with normal `DateTime` objects and let the library handle the conversion.

## Background Processing with Channels

Analytics should never block your application. Umami.NET includes a background sender using `System.Threading.Channels`:

```csharp
public class UmamiBackgroundSender : IHostedService
{
    private readonly Channel<UmamiPayload> _channel;
    private readonly UmamiClient _client;

    public async Task Track(string eventName,
        string? url = null,
        UmamiEventData? data = null)
    {
        var payload = new UmamiPayload
        {
            Website = _settings.WebsiteId,
            Name = eventName,
            Url = url ?? string.Empty,
            Data = data
        };

        // Non-blocking write to channel
        await _channel.Writer.WriteAsync(payload);
    }

    private async Task ProcessQueue(CancellationToken stoppingToken)
    {
        await foreach (var payload in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _client.Send(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send event to Umami");
            }
        }
    }
}
```

Events are queued in memory and processed asynchronously. Your web requests return instantly, analytics happen in the background.

## Retry Policies with Polly

Network failures happen. The library uses Polly for resilient HTTP calls:

```csharp
public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    var delay = Backoff.DecorrelatedJitterBackoffV2(
        TimeSpan.FromSeconds(1),
        retryCount: 3);

    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == HttpStatusCode.ServiceUnavailable)
        .WaitAndRetryAsync(delay);
}
```

Transient failures and 503 errors trigger automatic retries with exponential backoff. Your analytics are resilient to temporary network issues.

## Authentication with Auto-Retry

When fetching analytics data (not just sending events), you need authentication. The library handles token expiration automatically:

```csharp
public async Task<UmamiResult<StatsResponseModel>> GetStats(StatsRequest statsRequest)
{
    var response = await _httpClient.GetAsync(url);

    // Token expired? Re-authenticate and retry
    if (response.StatusCode == HttpStatusCode.Unauthorized)
    {
        await _authService.Login();
        return await GetStats(statsRequest); // Recursive retry
    }

    // Parse and return
    var content = await response.Content.ReadFromJsonAsync<StatsResponseModel>();
    return new UmamiResult<StatsResponseModel>(
        response.StatusCode,
        response.ReasonPhrase ?? string.Empty,
        content);
}
```

You never have to think about token management - it just works.

## Testing Infrastructure

Production-ready code needs comprehensive tests. Here's what I built:

### FakeLogger for Log Verification

Using Microsoft's `FakeLogger` package, tests can verify logging behavior:

```csharp
[Fact]
public async Task Login_Success_LogsMessage()
{
    // Arrange
    var fakeLogger = new FakeLogger<AuthService>();
    var authService = new AuthService(httpClient, settings, fakeLogger);

    // Act
    await authService.Login();

    // Assert
    var logs = fakeLogger.Collector.GetSnapshot();
    Assert.Contains("Login successful", logs.Select(x => x.Message));
}
```

### Custom Mock HTTP Handlers

Testing async operations is tricky. Here's a pattern using `TaskCompletionSource`:

```csharp
[Fact]
public async Task BackgroundSender_ProcessesEventAsynchronously()
{
    var tcs = new TaskCompletionSource<bool>();

    var handler = EchoMockHandler.Create(async (message, token) =>
    {
        try
        {
            // Assert the request was sent correctly
            var payload = await message.Content.ReadFromJsonAsync<UmamiPayload>();
            Assert.Equal("test-event", payload.Name);

            tcs.SetResult(true); // Signal test completion
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
        catch (Exception e)
        {
            tcs.SetException(e);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    });

    // Track event
    await backgroundSender.Track("test-event");

    // Wait for background processing with timeout
    var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
    if (completedTask != tcs.Task)
        throw new TimeoutException("Event was not processed within timeout");

    await tcs.Task; // Throw if assertions failed
}
```

This pattern ensures:
- Background events are actually processed
- Processing completes within reasonable time
- Assertions in the mock handler are properly reported

### Comprehensive Test Coverage

The test suite covers:

- ✅ Configuration validation (invalid GUIDs, missing URLs)
- ✅ Event tracking with and without data
- ✅ Page view tracking
- ✅ User identification
- ✅ Bot detection handling
- ✅ JWT response decoding
- ✅ Background processing with timeouts
- ✅ Date range validation
- ✅ Query string generation
- ✅ Authentication and token refresh
- ✅ Metrics and pageviews data retrieval

## Real-World Usage

Here's how simple it is to use in an ASP.NET Core application:

### Setup in Program.cs

```csharp
builder.Services.SetupUmamiClient(builder.Configuration);
```

That's it. The library reads your `appsettings.json`:

```json
{
  "Analytics": {
    "UmamiPath": "https://analytics.yoursite.com",
    "WebsiteId": "your-website-guid"
  }
}
```

### Track Events

```csharp
public class HomeController : Controller
{
    private readonly UmamiBackgroundSender _umami;

    public HomeController(UmamiBackgroundSender umami)
    {
        _umami = umami;
    }

    public IActionResult Index()
    {
        // Non-blocking event tracking
        await _umami.TrackPageView("/", "Home Page");

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(string email)
    {
        // Track with custom data
        await _umami.Track("newsletter-signup",
            data: new UmamiEventData
            {
                { "source", "homepage" },
                { "email_domain", email.Split('@')[1] }
            });

        return RedirectToAction("ThankYou");
    }
}
```

### Fetch Analytics Data

```csharp
public class AnalyticsDashboardController : Controller
{
    private readonly IUmamiDataService _umamiData;

    public async Task<IActionResult> Stats()
    {
        var request = new StatsRequest
        {
            StartAtDate = DateTime.UtcNow.AddDays(-30),
            EndAtDate = DateTime.UtcNow
        };

        var result = await _umamiData.GetStats(request);

        if (result.Status == HttpStatusCode.OK)
        {
            var stats = result.Data;
            // stats.Visitors, stats.PageViews, stats.BounceRate, etc.
            return View(stats);
        }

        return View("Error");
    }
}
```

## What's Next for 1.0?

The library is feature-complete and battle-tested in production on this very blog. Before the 1.0 release, I'm focusing on:

- 📝 Comprehensive API documentation
- 🎯 NuGet package publishing
- 📊 Performance benchmarks
- 🔧 Additional convenience methods for common analytics queries

## Conclusion

Building a production-ready library isn't just about wrapping an API - it's about creating an experience that's **better** than using the API directly. Umami.NET compensates for Umami's documentation gaps with:

- **Validation that explains what went wrong and how to fix it**
- **Graceful handling of quirky API behaviors**
- **Comprehensive testing that proves it works**
- **Background processing that doesn't block your app**
- **Resilient error handling with automatic retries**

If you're using Umami analytics in a .NET application, I'd love for you to try [Umami.NET](https://github.com/scottgal/mostlylucidweb/tree/main/Umami.Net). It's open source, heavily tested, and designed to make your life easier.

Got questions or suggestions? Open an issue on GitHub or reach out in the comments below!
