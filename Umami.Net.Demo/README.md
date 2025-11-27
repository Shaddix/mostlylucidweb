# Umami.Net Demo

A comprehensive demonstration of the [Umami.Net](https://www.nuget.org/packages/Umami.Net/) library features - a production-ready .NET client for [Umami Analytics](https://umami.is).

## Features Demonstrated

### 📊 Event Tracking
- **Page View Tracking**: Track page views with automatic event queueing
- **Custom Events**: Track custom events with metadata and event data
- **User Actions**: Track user interactions with contextual information
- **Background Processing**: Non-blocking event processing using `System.Threading.Channels`

### 📈 Analytics Data API
- **Statistics**: Get summary statistics (pageviews, visitors, bounce rate)
- **Active Users**: Real-time count of active users
- **Top Pages**: Most visited pages with view counts
- **Metrics by Dimension**: Country and browser distribution
- **Automatic Authentication**: JWT token management with auto-refresh

### 🎯 Key Features Showcased
- ✅ Non-blocking background sender
- ✅ Polly retry policies for resilience
- ✅ Type-safe request/response models
- ✅ Automatic API version detection (v1/v2/v3)
- ✅ Comprehensive error handling
- ✅ Structured logging

## Prerequisites

- .NET 9.0 SDK
- **Umami Analytics Instance** (required)
  - Either self-hosted or cloud-hosted Umami
  - Admin credentials for Data API access

## Configuration

Before running the demo, update `appsettings.json` or use environment variables:

### Option 1: appsettings.json

```json
{
  "Analytics": {
    "UmamiPath": "https://analytics.yoursite.com",
    "WebsiteId": "12345678-1234-1234-1234-123456789abc",
    "UserName": "admin",
    "Password": "your-secure-password"
  }
}
```

### Option 2: Environment Variables

```bash
export Analytics__UmamiPath="https://analytics.yoursite.com"
export Analytics__WebsiteId="12345678-1234-1234-1234-123456789abc"
export Analytics__UserName="admin"
export Analytics__Password="your-secure-password"
```

### Option 3: User Secrets (Development)

```bash
cd Umami.Net.Demo
dotnet user-secrets set "Analytics:UmamiPath" "https://analytics.yoursite.com"
dotnet user-secrets set "Analytics:WebsiteId" "12345678-1234-1234-1234-123456789abc"
dotnet user-secrets set "Analytics:UserName" "admin"
dotnet user-secrets set "Analytics:Password" "your-password"
```

## Running the Demo

### Quick Start

```bash
cd Umami.Net.Demo
dotnet restore
dotnet run
```

Then open your browser to `http://localhost:5000`

### Building and Running

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the demo
dotnet run
```

The application will start on `http://localhost:5000` (or `https://localhost:5001`)

## Demo Endpoints

### Web UI Endpoints

Visit these URLs in your browser:

| Endpoint | Description |
|----------|-------------|
| `/` | Main demo homepage with links to all features |
| `/track-page-view` | Demonstrates page view tracking |
| `/track-custom-event` | Shows custom event tracking with metadata |
| `/track-user-action` | Example of tracking user interactions |

### API Endpoints

These return JSON responses:

| Endpoint | Description | Authentication Required |
|----------|-------------|------------------------|
| `/api/stats` | Get 30-day statistics | Yes |
| `/api/active-users` | Get real-time active user count | Yes |
| `/api/top-pages` | Get top 10 pages (last 7 days) | Yes |
| `/api/metrics/country` | Get visitor distribution by country | Yes |
| `/api/metrics/browser` | Get visitor distribution by browser | Yes |

## Testing the Demo

### 1. Test Event Tracking (No Auth Required)

```bash
# Track a page view
curl http://localhost:5000/track-page-view

# Track a custom event
curl http://localhost:5000/track-custom-event

# Track a user action
curl http://localhost:5000/track-user-action
```

### 2. Test Analytics Data API (Requires Auth)

```bash
# Get statistics
curl http://localhost:5000/api/stats | jq

# Get active users
curl http://localhost:5000/api/active-users | jq

# Get top pages
curl http://localhost:5000/api/top-pages | jq

# Get country metrics
curl http://localhost:5000/api/metrics/country | jq

# Get browser metrics
curl http://localhost:5000/api/metrics/browser | jq
```

## Understanding the Code

### Event Tracking

The demo uses `UmamiBackgroundSender` for non-blocking event tracking:

```csharp
app.MapGet("/track-page-view", async (UmamiBackgroundSender umami, HttpContext context) =>
{
    // Non-blocking - returns immediately, event processed in background
    await umami.TrackPageView(context.Request.Path, "Demo Page View");
    
    return Results.Ok("Page view tracked!");
});
```

### Analytics Data Retrieval

The demo uses `IUmamiDataService` for retrieving analytics:

```csharp
app.MapGet("/api/stats", async (IUmamiDataService umamiData) =>
{
    var request = new StatsRequest
    {
        StartAtDate = DateTime.UtcNow.AddDays(-30),
        EndAtDate = DateTime.UtcNow
    };
    
    var result = await umamiData.GetStats(request);
    
    if (result.Status == HttpStatusCode.OK)
    {
        return Results.Ok(result.Data);
    }
    
    return Results.Problem(result.Message);
});
```

## Expected Behavior

### Event Tracking
- Events are queued immediately and processed in the background
- No blocking of the HTTP request
- Automatic retry on failures
- Bot detection handled gracefully

### Analytics Data
- Returns JSON with structured data
- Handles authentication automatically
- Auto-refreshes JWT tokens on expiration
- Graceful error handling with helpful messages

## Troubleshooting

### "Analytics configuration not found"

**Solution**: Check your `appsettings.json` or environment variables are correctly set.

### 401 Unauthorized on Data API calls

**Causes**:
- Wrong username/password
- User doesn't have access to the website ID

**Solution**: Verify your Umami credentials and ensure the user has access to the specified website.

### "beep boop" Response (Bot Detection)

**Cause**: Umami detected the request as a bot (expected for server-side tracking).

**Solution**: This is normal for server-to-server tracking. The library handles this gracefully and logs it appropriately.

### No Data in Analytics

**Causes**:
- Events are queued but not yet sent
- Umami server is unreachable
- Website ID is incorrect

**Solution**: 
1. Check logs for error messages
2. Verify Umami server is accessible
3. Confirm Website ID matches your Umami instance

## Architecture

```
┌─────────────────┐
│   Browser/      │
│   HTTP Client   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐      ┌──────────────────┐
│   Minimal API   │─────▶│ UmamiBackground  │
│   Endpoints     │      │    Sender        │
└────────┬────────┘      └────────┬─────────┘
         │                        │
         │                        ▼
         │               ┌────────────────┐
         │               │    Channel     │
         │               │     Queue      │
         │               └────────┬───────┘
         │                        │
         ▼                        ▼
┌─────────────────┐      ┌────────────────┐
│  UmamiData      │      │  UmamiClient   │
│  Service        │      │                │
└────────┬────────┘      └────────┬───────┘
         │                        │
         └────────┬───────────────┘
                  │
                  ▼
         ┌────────────────┐
         │  Umami Server  │
         │  (Analytics)   │
         └────────────────┘
```

## Performance

- **Event Tracking**: <1ms overhead (non-blocking)
- **Data API Calls**: 50-200ms (depends on Umami server)
- **Background Queue**: Processes events asynchronously
- **Memory Usage**: Minimal (~10MB for queue)

## Production Considerations

This demo shows best practices for production use:

1. **Configuration Management**: Uses ASP.NET Core configuration system
2. **Dependency Injection**: Proper service registration
3. **Error Handling**: Graceful degradation on failures
4. **Logging**: Structured logging with configurable levels
5. **Async/Await**: Non-blocking operations throughout
6. **Type Safety**: Strongly-typed models and requests

## Next Steps

After exploring the demo:

1. **Integrate into your app**: Copy the configuration and service registration
2. **Customize events**: Track your specific user interactions
3. **Build dashboards**: Use the Data API to create custom analytics views
4. **Monitor performance**: Use logging to ensure analytics doesn't impact users
5. **Read the docs**: Check the [full Umami.Net README](https://github.com/scottgal/mostlylucidweb/tree/main/Umami.Net)

## Links

- **NuGet Package**: https://www.nuget.org/packages/Umami.Net/
- **Source Code**: https://github.com/scottgal/mostlylucidweb/tree/main/Umami.Net
- **Umami Analytics**: https://umami.is/
- **Blog Post**: https://www.mostlylucid.net/blog/umamiproductionready

## License

MIT License - Free to use, modify, and distribute.
