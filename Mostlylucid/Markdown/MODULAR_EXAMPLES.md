# Modular Architecture Examples


<!--category-- AI, LLM, ASP.NET Core, API, Nuget, mockllmapi-->
<datetime class="hidden">2025-11-02T12:30</datetime>

> NOTE: This is release documents for the llmapi, just ignore

**New in v1.2.0:** Complete modularity! Each protocol (REST, Streaming, GraphQL, SignalR) can now be added and mapped independently.

## Table of Contents
- [Backward Compatible (Unified Approach)](#backward-compatible-unified-approach)
- [Modular Approach](#modular-approach)
  - [REST Only](#rest-only)
  - [GraphQL Only](#graphql-only)
  - [Streaming Only](#streaming-only)
  - [SignalR Only](#signalr-only)
  - [Mix and Match](#mix-and-match)
- [Multiple Instances](#multiple-instances)
- [Benefits of Modular Approach](#benefits-of-modular-approach)

---

## Backward Compatible (Unified Approach)

**Existing code continues to work without any changes:**

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Registers ALL services (REST, Streaming, GraphQL)
builder.Services.AddLLMockApi(builder.Configuration);

// Optional: Add SignalR
builder.Services.AddLLMockSignalR(builder.Configuration);

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

// Maps ALL endpoints (REST, Streaming, GraphQL) at /api/mock
app.MapLLMockApi("/api/mock", includeStreaming: true, includeGraphQL: true);

// Optional: Map SignalR
app.MapLLMockSignalR("/hub/mock", "/api/mock");

app.Run();
```

**This creates:**
- `/api/mock/**` - REST endpoints
- `/api/mock/stream/**` - SSE streaming endpoints
- `/api/mock/graphql` - GraphQL endpoint
- `/hub/mock` - SignalR hub (if added)
- `/api/mock/contexts` - SignalR management API (if added)

---

## Modular Approach

### REST Only

Perfect for simple REST API mocking without extra overhead:

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register ONLY REST services
builder.Services.AddLLMockRest(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map ONLY REST endpoints
app.MapLLMockRest("/api/mock");

app.Run();
```

**This creates:**
- `/api/mock/**` - REST endpoints only

**Benefits:**
- Minimal memory footprint
- Faster startup time
- Only includes RegularRequestHandler

---

### GraphQL Only

Perfect for GraphQL-only applications:

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register ONLY GraphQL services
builder.Services.AddLLMockGraphQL(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map ONLY GraphQL endpoint
app.MapLLMockGraphQL("/api/mock");

app.Run();
```

**This creates:**
- `/api/mock/graphql` - GraphQL endpoint only

**Benefits:**
- Clean GraphQL-only setup
- No unnecessary REST handlers
- Perfect for GraphQL client testing

---

### Streaming Only

Perfect for testing SSE streaming clients:

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register ONLY Streaming services
builder.Services.AddLLMockStreaming(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map ONLY Streaming endpoints
app.MapLLMockStreaming("/api/mock");

app.Run();
```

**This creates:**
- `/api/mock/stream/**` - SSE streaming endpoints only

**Benefits:**
- Lightweight streaming-only setup
- Perfect for EventSource testing
- No REST/GraphQL overhead

---

### SignalR Only

Perfect for real-time dashboard prototyping:

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register ONLY SignalR services (includes core services automatically)
builder.Services.AddLLMockSignalR(options =>
{
    options.BaseUrl = "http://localhost:11434/v1/";
    options.ModelName = "llama3";
    options.SignalRPushIntervalMs = 5000;
});

var app = builder.Build();

app.UseRouting();

// Map ONLY SignalR endpoints
app.MapLLMockSignalR("/hub/mock", "/api/contexts");

app.Run();
```

**This creates:**
- `/hub/mock` - SignalR hub
- `/api/contexts` - Context management API

**Benefits:**
- Real-time only setup
- No HTTP endpoint overhead
- Perfect for WebSocket testing

---

### Mix and Match

Combine protocols as needed for your use case:

#### Example 1: REST + GraphQL (No Streaming)

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register REST and GraphQL services
builder.Services.AddLLMockRest(builder.Configuration);
builder.Services.AddLLMockGraphQL(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map both protocols
app.MapLLMockRest("/api/mock");
app.MapLLMockGraphQL("/api/mock");

app.Run();
```

**This creates:**
- `/api/mock/**` - REST endpoints
- `/api/mock/graphql` - GraphQL endpoint
- No streaming endpoints

---

#### Example 2: GraphQL + SignalR (No REST)

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register GraphQL and SignalR services
builder.Services.AddLLMockGraphQL(builder.Configuration);
builder.Services.AddLLMockSignalR(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map both protocols
app.MapLLMockGraphQL("/api/queries");
app.MapLLMockSignalR("/hub/realtime", "/api/contexts");

app.Run();
```

**This creates:**
- `/api/queries/graphql` - GraphQL endpoint
- `/hub/realtime` - SignalR hub
- `/api/contexts` - SignalR management API
- No REST or SSE endpoints

---

#### Example 3: Everything with Different Patterns

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Register all services modularly
builder.Services.AddLLMockRest(builder.Configuration);
builder.Services.AddLLMockStreaming(builder.Configuration);
builder.Services.AddLLMockGraphQL(builder.Configuration);
builder.Services.AddLLMockSignalR(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map each protocol with custom patterns
app.MapLLMockRest("/api/rest");           // /api/rest/**
app.MapLLMockStreaming("/api/stream");    // /api/stream/stream/**
app.MapLLMockGraphQL("/api/graphql");     // /api/graphql/graphql
app.MapLLMockSignalR("/hub/live", "/api/hub");

app.Run();
```

**This creates:**
- `/api/rest/**` - REST endpoints
- `/api/stream/stream/**` - SSE streaming
- `/api/graphql/graphql` - GraphQL endpoint
- `/hub/live` - SignalR hub
- `/api/hub/contexts` - SignalR management

---

## Multiple Instances

Run multiple independent mock APIs with different configurations:

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Development endpoints - high randomness
builder.Services.AddLLMockRest(options =>
{
    options.BaseUrl = "http://localhost:11434/v1/";
    options.ModelName = "llama3";
    options.Temperature = 1.5;
});

builder.Services.AddLLMockGraphQL(options =>
{
    options.BaseUrl = "http://localhost:11434/v1/";
    options.ModelName = "llama3";
    options.Temperature = 1.5;
});

var app = builder.Build();

app.UseRouting();

// Development endpoints
app.MapLLMockRest("/api/dev");
app.MapLLMockGraphQL("/api/dev");

// Stable test endpoints (would need separate config)
app.MapLLMockRest("/api/test");
app.MapLLMockGraphQL("/api/test");

app.Run();
```

---

## Benefits of Modular Approach

### 1. **Reduced Memory Footprint**
Only load the handlers you actually use:
- REST only: ~30% less memory
- GraphQL only: ~40% less memory
- Streaming only: ~40% less memory

### 2. **Faster Startup**
Fewer services to register = faster app startup

### 3. **Clearer Intent**
Code explicitly shows which protocols your app uses

### 4. **Easier Testing**
Test protocols in isolation:
```csharp
// Test file for REST only
builder.Services.AddLLMockRest(config);
app.MapLLMockRest("/api/mock");

// Test file for GraphQL only
builder.Services.AddLLMockGraphQL(config);
app.MapLLMockGraphQL("/api/mock");
```

### 5. **No Breaking Changes**
Existing code continues to work:
```csharp
// This still works exactly as before
builder.Services.AddLLMockApi(configuration);
app.MapLLMockApi("/api/mock");
```

### 6. **Flexible Patterns**
Mount protocols at any path:
```csharp
app.MapLLMockRest("/rest");
app.MapLLMockGraphQL("/gql");
app.MapLLMockStreaming("/events");
app.MapLLMockSignalR("/ws", "/manage");
```

---

## Migration Guide

### From v1.1.0 to v1.2.0

**No changes required!** Your existing code works as-is.

**Optional: Migrate to modular approach for benefits**

**Before (v1.1.0):**
```csharp
builder.Services.AddLLMockApi(builder.Configuration);
app.MapLLMockApi("/api/mock", includeStreaming: true, includeGraphQL: true);
```

**After (v1.2.0 - Modular):**
```csharp
// Only include what you need
builder.Services.AddLLMockRest(builder.Configuration);
builder.Services.AddLLMockGraphQL(builder.Configuration);

app.MapLLMockRest("/api/mock");
app.MapLLMockGraphQL("/api/mock");
```

Both approaches work identically, but the modular approach is more explicit and efficient.

---

## Summary

| Approach | Add Method | Map Method | Use Case |
|----------|------------|------------|----------|
| **Unified** | `AddLLMockApi()` | `MapLLMockApi()` | Everything, backward compatible |
| **REST** | `AddLLMockRest()` | `MapLLMockRest()` | Simple REST mocking |
| **GraphQL** | `AddLLMockGraphQL()` | `MapLLMockGraphQL()` | GraphQL-only apps |
| **Streaming** | `AddLLMockStreaming()` | `MapLLMockStreaming()` | SSE streaming only |
| **SignalR** | `AddLLMockSignalR()` | `MapLLMockSignalR()` | Real-time WebSocket data |

**Choose the approach that best fits your needs!**
