# Mostlylucid.BotDetection

Bot detection middleware for ASP.NET Core applications with behavioral analysis, header inspection, IP-based detection, and optional LLM-based classification.

## Installation

```bash
dotnet add package Mostlylucid.BotDetection
```

## Quick Start

### 1. Configure Services

```csharp
using Mostlylucid.BotDetection;

var builder = WebApplication.CreateBuilder(args);

// Add bot detection services
builder.Services.AddBotDetection(options =>
{
    options.EnableBehavioralAnalysis = true;
    options.EnableHeaderDetection = true;
    options.EnableUserAgentDetection = true;
    options.EnableIpDetection = true;
    options.EnableLlmDetection = false; // Optional: requires Ollama
});

var app = builder.Build();

// Use bot detection middleware
app.UseBotDetection();

app.Run();
```

### 2. Configure Settings (Optional)

Add to your `appsettings.json`:

```json
{
  "BotDetection": {
    "EnableBehavioralAnalysis": true,
    "EnableHeaderDetection": true,
    "EnableUserAgentDetection": true,
    "EnableIpDetection": true,
    "EnableLlmDetection": false,
    "BlockBots": false,
    "CacheExpirationMinutes": 30,
    "MaxRequestsPerMinute": 100
  }
}
```

## Features

### Detection Strategies

1. **User-Agent Detection** - Matches against known bot signatures
2. **Header Detection** - Analyzes HTTP headers for suspicious patterns
3. **IP Detection** - Checks against known bot IP ranges and blocklists
4. **Behavioral Analysis** - Monitors request patterns for bot-like behavior
5. **LLM Detection** (Optional) - Uses Ollama for advanced classification

### Getting Detection Results

```csharp
public class MyController : Controller
{
    private readonly IBotDetectionService _botDetection;

    public MyController(IBotDetectionService botDetection)
    {
        _botDetection = botDetection;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _botDetection.DetectAsync(HttpContext);

        if (result.IsBot)
        {
            // Handle bot traffic
            return StatusCode(403);
        }

        return View();
    }
}
```

### Access Detection Result from HttpContext

```csharp
public class MyMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Detection result is available after middleware runs
        var result = context.Features.Get<BotDetectionResult>();

        if (result?.IsBot == true)
        {
            // Log or handle bot
        }

        await _next(context);
    }
}
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnableBehavioralAnalysis` | bool | true | Enable behavioral analysis |
| `EnableHeaderDetection` | bool | true | Enable header inspection |
| `EnableUserAgentDetection` | bool | true | Enable user-agent matching |
| `EnableIpDetection` | bool | true | Enable IP-based detection |
| `EnableLlmDetection` | bool | false | Enable LLM-based classification |
| `BlockBots` | bool | false | Automatically block detected bots |
| `CacheExpirationMinutes` | int | 30 | Cache duration for detection results |
| `MaxRequestsPerMinute` | int | 100 | Threshold for behavioral analysis |
| `OllamaEndpoint` | string | "http://localhost:11434" | Ollama API endpoint |
| `OllamaModel` | string | "llama3.2" | Ollama model for LLM detection |

## Detection Result

```csharp
public class BotDetectionResult
{
    public bool IsBot { get; set; }
    public double Confidence { get; set; }
    public string? BotType { get; set; }
    public string? DetectedBy { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
```

## Background Services

The package includes a background service that automatically updates bot signatures and blocklists from common sources:

- User-Agent bot signatures
- IP blocklists
- Crawler databases

## License

MIT License - see LICENSE file for details.

## Links

- [GitHub Repository](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.BotDetection)
- [NuGet Package](https://www.nuget.org/packages/Mostlylucid.BotDetection/)
