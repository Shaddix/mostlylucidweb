using Umami.Net;
using Umami.Net.UmamiData;
using Umami.Net.Models;
using Umami.Net.UmamiData.Models.RequestObjects;

var builder = WebApplication.CreateBuilder(args);

// Add Umami.Net services for event tracking and analytics data
builder.Services.SetupUmamiClient(builder.Configuration);
builder.Services.SetupUmamiData(builder.Configuration);

var app = builder.Build();

// Main page with demo overview
app.MapGet("/", (HttpContext context) =>
{
    var html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Umami.Net Demo</title>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { 
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                    min-height: 100vh;
                    padding: 40px 20px;
                }
                .container {
                    max-width: 1200px;
                    margin: 0 auto;
                    background: white;
                    border-radius: 12px;
                    box-shadow: 0 20px 60px rgba(0,0,0,0.3);
                    padding: 40px;
                }
                h1 { 
                    color: #667eea; 
                    margin-bottom: 10px;
                    font-size: 2.5rem;
                }
                .subtitle {
                    color: #666;
                    margin-bottom: 30px;
                    font-size: 1.1rem;
                }
                .section {
                    margin-bottom: 40px;
                    padding: 25px;
                    background: #f8f9fa;
                    border-radius: 8px;
                    border-left: 4px solid #667eea;
                }
                .section h2 {
                    color: #333;
                    margin-bottom: 15px;
                    font-size: 1.5rem;
                }
                .endpoint {
                    background: white;
                    padding: 15px;
                    margin: 10px 0;
                    border-radius: 6px;
                    border: 1px solid #e0e0e0;
                    transition: all 0.3s;
                }
                .endpoint:hover {
                    border-color: #667eea;
                    box-shadow: 0 2px 8px rgba(102, 126, 234, 0.2);
                }
                .endpoint a {
                    text-decoration: none;
                    color: #667eea;
                    font-weight: 600;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }
                .endpoint a:hover { color: #764ba2; }
                .description {
                    color: #666;
                    font-size: 0.9rem;
                    margin-top: 8px;
                }
                .badge {
                    background: #667eea;
                    color: white;
                    padding: 4px 12px;
                    border-radius: 12px;
                    font-size: 0.75rem;
                    font-weight: 600;
                }
                .feature-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
                    gap: 20px;
                    margin-top: 20px;
                }
                .feature {
                    background: white;
                    padding: 20px;
                    border-radius: 8px;
                    border: 1px solid #e0e0e0;
                }
                .feature h3 {
                    color: #667eea;
                    margin-bottom: 10px;
                    font-size: 1.1rem;
                }
                .feature p {
                    color: #666;
                    font-size: 0.9rem;
                    line-height: 1.6;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <h1>🎯 Umami.Net Demo</h1>
                <p class="subtitle">A comprehensive demonstration of Umami.Net features - privacy-focused web analytics for .NET</p>

                <div class="section">
                    <h2>📊 Event Tracking Demo</h2>
                    <p style="margin-bottom: 15px;">Test event tracking and see how Umami.Net handles different scenarios.</p>
                    
                    <div class="endpoint">
                        <a href="/track-page-view">
                            Track Page View
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Test basic page view tracking with automatic event queueing.</div>
                    </div>

                    <div class="endpoint">
                        <a href="/track-custom-event">
                            Track Custom Event
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Track a custom event with metadata and event data.</div>
                    </div>

                    <div class="endpoint">
                        <a href="/track-user-action">
                            Track User Action
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Track user interactions like button clicks with context data.</div>
                    </div>
                </div>

                <div class="section">
                    <h2>📈 Analytics Data API Demo</h2>
                    <p style="margin-bottom: 15px;">Retrieve analytics data using the Umami Data API (requires authentication).</p>
                    
                    <div class="endpoint">
                        <a href="/api/stats">
                            Get Statistics
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Get summary statistics for the last 30 days (pageviews, visitors, bounce rate).</div>
                    </div>

                    <div class="endpoint">
                        <a href="/api/active-users">
                            Get Active Users
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Get real-time count of currently active users.</div>
                    </div>

                    <div class="endpoint">
                        <a href="/api/top-pages">
                            Get Top Pages
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Get the top 10 most visited pages in the last 7 days.</div>
                    </div>

                    <div class="endpoint">
                        <a href="/api/metrics/country">
                            Get Country Metrics
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Get visitor distribution by country.</div>
                    </div>

                    <div class="endpoint">
                        <a href="/api/metrics/browser">
                            Get Browser Metrics
                            <span class="badge">GET</span>
                        </a>
                        <div class="description">Get visitor distribution by browser.</div>
                    </div>
                </div>

                <div class="section">
                    <h2>✨ Key Features</h2>
                    <div class="feature-grid">
                        <div class="feature">
                            <h3>🚀 Non-Blocking</h3>
                            <p>Background sender with System.Threading.Channels ensures analytics never block your app.</p>
                        </div>
                        <div class="feature">
                            <h3>🔄 Auto-Retry</h3>
                            <p>Polly retry policies handle transient failures gracefully with exponential backoff.</p>
                        </div>
                        <div class="feature">
                            <h3>🎯 Type-Safe</h3>
                            <p>Strongly-typed request and response models with comprehensive validation.</p>
                        </div>
                        <div class="feature">
                            <h3>🔐 Secure</h3>
                            <p>Automatic JWT token management with refresh on expiration.</p>
                        </div>
                        <div class="feature">
                            <h3>📊 Comprehensive</h3>
                            <p>Full API coverage for events, stats, metrics, page views, and active users.</p>
                        </div>
                        <div class="feature">
                            <h3>🔍 Version Auto-Detection</h3>
                            <p>Automatically detects and adapts to Umami v1, v2, or v3 APIs.</p>
                        </div>
                    </div>
                </div>

                <div class="section">
                    <h2>📚 Documentation</h2>
                    <p style="margin-bottom: 15px;">Learn more about Umami.Net:</p>
                    <ul style="list-style: none; padding-left: 0;">
                        <li style="margin: 8px 0;">
                            <a href="https://github.com/scottgal/mostlylucidweb/tree/main/Umami.Net" style="color: #667eea; text-decoration: none;">
                                📖 Full README and Documentation
                            </a>
                        </li>
                        <li style="margin: 8px 0;">
                            <a href="https://www.nuget.org/packages/Umami.Net/" style="color: #667eea; text-decoration: none;">
                                📦 NuGet Package
                            </a>
                        </li>
                        <li style="margin: 8px 0;">
                            <a href="https://umami.is" style="color: #667eea; text-decoration: none;">
                                🌐 Umami Analytics
                            </a>
                        </li>
                    </ul>
                </div>
            </div>
        </body>
        </html>
        """;
    
    return Results.Content(html, "text/html");
});

// Event tracking endpoints
app.MapGet("/track-page-view", async (UmamiBackgroundSender umami, HttpContext context) =>
{
    await umami.TrackPageView(context.Request.Path, "Demo Page View");
    
    return Results.Content("""
        <!DOCTYPE html>
        <html>
        <head>
            <title>Page View Tracked</title>
            <style>
                body { 
                    font-family: sans-serif; 
                    max-width: 800px; 
                    margin: 100px auto; 
                    padding: 40px;
                    background: #f5f5f5;
                }
                .success {
                    background: #d4edda;
                    border: 1px solid #c3e6cb;
                    border-radius: 8px;
                    padding: 30px;
                    color: #155724;
                }
                h1 { margin-top: 0; }
                pre {
                    background: white;
                    padding: 15px;
                    border-radius: 4px;
                    overflow-x: auto;
                    border: 1px solid #c3e6cb;
                }
                a { color: #155724; margin-right: 20px; }
            </style>
        </head>
        <body>
            <div class="success">
                <h1>✅ Page View Tracked!</h1>
                <p>Successfully tracked a page view event.</p>
                <pre><code>await umami.TrackPageView("/track-page-view", "Demo Page View");</code></pre>
                <p style="margin-top: 20px;">
                    <a href="/">← Back to Demo Home</a>
                    <a href="/api/stats">View Statistics →</a>
                </p>
            </div>
        </body>
        </html>
        """, "text/html");
});

app.MapGet("/track-custom-event", async (UmamiBackgroundSender umami) =>
{
    await umami.Track("demo-custom-event", eventData: new UmamiEventData
    {
        { "action", "button-click" },
        { "category", "demo" },
        { "value", "test-123" }
    });
    
    return Results.Content("""
        <!DOCTYPE html>
        <html>
        <head>
            <title>Custom Event Tracked</title>
            <style>
                body { 
                    font-family: sans-serif; 
                    max-width: 800px; 
                    margin: 100px auto; 
                    padding: 40px;
                    background: #f5f5f5;
                }
                .success {
                    background: #d4edda;
                    border: 1px solid #c3e6cb;
                    border-radius: 8px;
                    padding: 30px;
                    color: #155724;
                }
                h1 { margin-top: 0; }
                pre {
                    background: white;
                    padding: 15px;
                    border-radius: 4px;
                    overflow-x: auto;
                    border: 1px solid #c3e6cb;
                }
                a { color: #155724; margin-right: 20px; }
            </style>
        </head>
        <body>
            <div class="success">
                <h1>✅ Custom Event Tracked!</h1>
                <p>Successfully tracked a custom event with metadata.</p>
                <pre><code>await umami.Track("demo-custom-event", eventData: new UmamiEventData
                {
                    { "action", "button-click" },
                    { "category", "demo" },
                    { "value", "test-123" }
                });</code></pre>
                <p style="margin-top: 20px;">
                    <a href="/">← Back to Demo Home</a>
                </p>
            </div>
        </body>
        </html>
        """, "text/html");
});

app.MapGet("/track-user-action", async (UmamiBackgroundSender umami) =>
{
    await umami.Track("user-interaction", eventData: new UmamiEventData
    {
        { "element", "demo-button" },
        { "timestamp", DateTime.UtcNow.ToString("O") },
        { "session", Guid.NewGuid().ToString() }
    });
    
    return Results.Content("""
        <!DOCTYPE html>
        <html>
        <head>
            <title>User Action Tracked</title>
            <style>
                body { 
                    font-family: sans-serif; 
                    max-width: 800px; 
                    margin: 100px auto; 
                    padding: 40px;
                    background: #f5f5f5;
                }
                .success {
                    background: #d4edda;
                    border: 1px solid #c3e6cb;
                    border-radius: 8px;
                    padding: 30px;
                    color: #155724;
                }
                h1 { margin-top: 0; }
                pre {
                    background: white;
                    padding: 15px;
                    border-radius: 4px;
                    overflow-x: auto;
                    border: 1px solid #c3e6cb;
                }
                a { color: #155724; margin-right: 20px; }
            </style>
        </head>
        <body>
            <div class="success">
                <h1>✅ User Action Tracked!</h1>
                <p>Successfully tracked a user interaction event with session data.</p>
                <pre><code>await umami.Track("user-interaction", eventData: new UmamiEventData
                {
                    { "element", "demo-button" },
                    { "timestamp", DateTime.UtcNow.ToString("O") },
                    { "session", Guid.NewGuid().ToString() }
                });</code></pre>
                <p style="margin-top: 20px;">
                    <a href="/">← Back to Demo Home</a>
                </p>
            </div>
        </body>
        </html>
        """, "text/html");
});

// Analytics Data API endpoints
app.MapGet("/api/stats", async (UmamiDataService umamiData) =>
{
    var request = new StatsRequest
    {
        StartAtDate = DateTime.UtcNow.AddDays(-30),
        EndAtDate = DateTime.UtcNow
    };
    
    var result = await umamiData.GetStats(request);
    
    if (result.Status == System.Net.HttpStatusCode.OK && result.Data != null)
    {
        return Results.Ok(new
        {
            success = true,
            data = result.Data,
            message = "Statistics retrieved successfully for the last 30 days"
        });
    }
    
    return Results.Json(new
    {
        success = false,
        status = result.Status.ToString(),
        message = result.Message ?? "Failed to retrieve statistics"
    }, statusCode: (int)result.Status);
});

app.MapGet("/api/active-users", async (UmamiDataService umamiData) =>
{
    var result = await umamiData.GetActiveUsers();
    
    if (result.Status == System.Net.HttpStatusCode.OK && result.Data != null)
    {
        return Results.Ok(new
        {
            success = true,
            activeUsers = result.Data.ActiveUsers,
            message = $"Currently {result.Data.ActiveUsers} active user(s)"
        });
    }
    
    return Results.Json(new
    {
        success = false,
        status = result.Status.ToString(),
        message = result.Message ?? "Failed to retrieve active users"
    }, statusCode: (int)result.Status);
});

app.MapGet("/api/top-pages", async (UmamiDataService umamiData) =>
{
    var request = new MetricsRequest
    {
        StartAtDate = DateTime.UtcNow.AddDays(-7),
        EndAtDate = DateTime.UtcNow,
        Type = MetricType.path,
        Unit = Unit.day,
        Limit = 10
    };
    
    var result = await umamiData.GetMetrics(request);
    
    if (result.Status == System.Net.HttpStatusCode.OK && result.Data != null)
    {
        return Results.Ok(new
        {
            success = true,
            data = result.Data.Select(m => new { path = m.x, views = m.y }),
            message = "Top 10 pages for the last 7 days"
        });
    }
    
    return Results.Json(new
    {
        success = false,
        status = result.Status.ToString(),
        message = result.Message ?? "Failed to retrieve top pages"
    }, statusCode: (int)result.Status);
});

app.MapGet("/api/metrics/country", async (UmamiDataService umamiData) =>
{
    var request = new MetricsRequest
    {
        StartAtDate = DateTime.UtcNow.AddDays(-30),
        EndAtDate = DateTime.UtcNow,
        Type = MetricType.country,
        Unit = Unit.day,
        Limit = 20
    };
    
    var result = await umamiData.GetMetrics(request);
    
    if (result.Status == System.Net.HttpStatusCode.OK && result.Data != null)
    {
        return Results.Ok(new
        {
            success = true,
            data = result.Data.Select(m => new { country = m.x, visitors = m.y }),
            message = "Visitor distribution by country for the last 30 days"
        });
    }
    
    return Results.Json(new
    {
        success = false,
        status = result.Status.ToString(),
        message = result.Message ?? "Failed to retrieve country metrics"
    }, statusCode: (int)result.Status);
});

app.MapGet("/api/metrics/browser", async (UmamiDataService umamiData) =>
{
    var request = new MetricsRequest
    {
        StartAtDate = DateTime.UtcNow.AddDays(-30),
        EndAtDate = DateTime.UtcNow,
        Type = MetricType.browser,
        Unit = Unit.day,
        Limit = 10
    };
    
    var result = await umamiData.GetMetrics(request);
    
    if (result.Status == System.Net.HttpStatusCode.OK && result.Data != null)
    {
        return Results.Ok(new
        {
            success = true,
            data = result.Data.Select(m => new { browser = m.x, visitors = m.y }),
            message = "Visitor distribution by browser for the last 30 days"
        });
    }
    
    return Results.Json(new
    {
        success = false,
        status = result.Status.ToString(),
        message = result.Message ?? "Failed to retrieve browser metrics"
    }, statusCode: (int)result.Status);
});

app.Run();
