# Building a Frontend Before the API is Ready: mostlylucid.mockllmapi

<datetime class="hidden">2025-12-13T14:30</datetime>
<!--category-- ASP.NET Core, LLM, API Development, Testing, Mock APIs -->

## Introduction

How many times have you been blocked waiting for backend APIs to be ready? Or spent hours maintaining brittle mock data that becomes stale the moment requirements change?

Enter `mostlylucid.mockllmapi` - a production-ready ASP.NET Core mocking platform that uses Large Language Models to generate realistic, contextually aware API responses on the fly. Instead of maintaining JSON fixtures, you get intelligent mocks that adapt to your requests, remember state across calls, and support every protocol you need: REST, GraphQL, gRPC, SignalR, and OpenAPI.

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![GitHub Release](https://img.shields.io/github/v/release/scottgal/LLMApi)](https://github.com/scottgal/LLMApi/releases)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](http://unlicense.org/)

**Companion Package:** [mostlylucid.mockllmapi.Testing](./mostlylucid.mockllmapi.Testing/README.md) - Testing utilities with fluent HttpClient integration

**Repository:** [https://github.com/scottgal/LLMApi](https://github.com/scottgal/LLMApi)

**Releases:** https://github.com/scottgal/LLMApi/releases 

**Three ways to use it:**
1. **ASP.NET Core NuGet package** - Add to your existing projects
2. **Standalone CLI tool** - Cross-platform executable (download from [releases](https://github.com/scottgal/LLMApi/releases))
3. **Docker container** - Zero installation required

[TOC]

## The Killer Feature: Context Memory

> **📖 Full Guide:** [API Contexts Documentation](https://github.com/scottgal/LLMApi/blob/main/docs/API-CONTEXTS.md)

Traditional mock APIs have a fatal flaw: each request is independent. Get a user with ID 42, then fetch their orders, and you'll get orders for user ID 99. No consistency.

**API Contexts** solve this with shared memory across related requests:

```javascript
// Request 1: Get a user
fetch('/api/users/123?context=checkout-session')
// Response: { id: 42, name: "Alice Smith", email: "alice@example.com" }

// Request 2: Get orders (same context)
fetch('/api/orders?userId=42&context=checkout-session')
// Response: { userId: 42, customerName: "Alice Smith", items: [...] }
// Perfect! Same user, consistent data
```

The LLM sees previous requests and generates consistent data. **This is the game-changer for multi-step workflows.**

**Features:**
- Automatic expiration after 15 minutes of inactivity (configurable)
- Each request refreshes the timer
- Intelligent extraction of ALL fields from responses
- Zero memory leaks - contexts clean themselves up
- Perfect for CI/CD - no state between runs

## Quick Start

### Option 1: NuGet Package

```bash
dotnet add package mostlylucid.mockllmapi
```

```csharp
// Program.cs
builder.Services.AddLLMockApi(builder.Configuration);
app.MapLLMockApi("/api/mock");
```

### Option 2: CLI Tool

```bash
# Download from https://github.com/scottgal/LLMApi/releases
llmock serve --port 5000
```

### Option 3: Docker

> **📖 Complete Guide:** [Docker Deployment Guide](https://github.com/scottgal/LLMApi/blob/main/docs/DOCKER_GUIDE.md)

```bash
git clone https://github.com/scottgal/LLMApi.git
cd LLMApi
docker compose up -d
```

### Prerequisites: LLM Backend

You need Ollama, OpenAI, or LM Studio:

```bash
# Recommended: Ollama with llama3
ollama pull llama3
```

See [Ollama Models Guide](https://github.com/scottgal/LLMApi/blob/main/docs/OLLAMA_MODELS.md) for recommendations.

## Real Example: Search from mostlylucid.net

Here's the actual search code from this blog:

```javascript
// typeahead.js from mostlylucid.net
export function typeahead() {
    return {
        query: '',
        results: [],
        search() {
            fetch(`/api/search/${encodeURIComponent(this.query)}`)
                .then(response => response.json())
                .then(data => { this.results = data; });
        }
    }
}
```

**Mock it:**

```bash
# Using CLI
llmock serve --port 5000

# Query returns contextual results
curl http://localhost:5000/api/search/markdown
# LLM generates blog posts about Markdown

curl http://localhost:5000/api/search/docker
# LLM generates blog posts about Docker
```

Each response is unique and realistic, adapting to the query.

## Shape Control: Define Your Schema

> **📖 See the article section below for full details**

The most powerful feature for frontend dev - tell the LLM exactly what JSON structure to generate:

```bash
# Without shape - random structure
curl http://localhost:5000/api/mock/users
# Response: { "userId": 1, "fullName": "Alice" }

# With shape - you control it
curl "http://localhost:5000/api/mock/users" \
  -H 'X-Response-Shape: {"id":0,"name":"string","email":"string"}'
# Response: { "id": 1, "name": "Alice", "email": "alice@example.com" }
```

**Three ways to specify shape:**
1. Query parameter - `?shape={...}`
2. HTTP header - `X-Response-Shape: {...}` (recommended)
3. Request body - `{"shape": {...}}`

**Complex nesting:**
```javascript
const shape = {
  company: {
    id: 0,
    name: "string",
    employees: [{
      id: 0,
      firstName: "string",
      department: { id: 0, name: "string" },
      projects: [{ id: 0, title: "string" }]
    }]
  }
};

fetch('/api/mock/company', {
  headers: { 'X-Response-Shape': JSON.stringify(shape) }
});
```

**TypeScript integration:**
```typescript
interface User {
  id: number;
  name: string;
  email: string;
}

const USER_SHAPE: Partial<User> = { id: 0, name: "", email: "" };

// Shape becomes your type definition AND mock schema!
```

## Multi-Step Workflows with Context

Real example from mostlylucid.net's translation service:

```bash
# Step 1: Start translation
curl -X POST http://localhost:5000/api/translate/start?context=translate-session \
  -d '{"language": "es", "markdown": "# Hello World"}'
# Response: { "taskId": "abc-123", "status": "processing" }

# Step 2: Check status (LLM remembers the task)
curl http://localhost:5000/api/translate/status/abc-123?context=translate-session
# Response: { "taskId": "abc-123", "status": "complete" }

# Step 3: Get result (same taskId!)
curl http://localhost:5000/api/translate/result/abc-123?context=translate-session
# Response: { "taskId": "abc-123", "translatedText": "# Hola Mundo" }
```

Notice how `taskId` is consistent across all requests. **Context makes this possible.**

## Beyond REST: All the Protocols

### GraphQL

> **📖 Guide:** [GraphQL Section](https://github.com/scottgal/LLMApi#graphql-api-mocking)

```bash
curl -X POST http://localhost:5000/api/mock/graphql \
  -d '{"query": "{ users { id name email } }"}'
```

The query IS the shape - no separate schema needed.

### gRPC

> **📖 Complete Guide:** [gRPC Support](https://github.com/scottgal/LLMApi/blob/main/docs/GRPC_SUPPORT.md)

```bash
# Upload .proto file
curl -X POST http://localhost:5116/api/grpc-protos \
  --data-binary "@user_service.proto"

# Call via JSON or binary Protobuf
curl -X POST http://localhost:5116/api/grpc/userservice/UserService/GetUser \
  -d '{"user_id": 123}'
```

### SignalR Real-Time

> **📖 Guide:** [SignalR Demo Guide](https://github.com/scottgal/LLMApi/blob/main/SIGNALR_DEMO_GUIDE.md)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub/mock")
    .build();

connection.on("DataUpdate", (message) => {
    console.log(message.data); // Live generated data
});

await connection.start();
await connection.invoke("SubscribeToContext", "stock-prices");
```

Perfect for dashboard prototyping.

### Server-Sent Events (SSE)

> **📖 Guide:** [SSE Streaming Modes](https://github.com/scottgal/LLMApi/blob/main/docs/SSE_STREAMING_MODES.md)

```javascript
const eventSource = new EventSource('/api/mock/stream/users');
eventSource.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log('Token:', data.chunk); // Progressive generation
};
```

### OpenAPI / Swagger

> **📖 Complete Guide:** [OpenAPI Features](https://github.com/scottgal/LLMApi/blob/main/docs/OPENAPI-FEATURES.md)

```bash
# CLI: Load any OpenAPI spec
llmock serve --spec https://petstore3.swagger.io/api/v3/openapi.json

# All endpoints become live mocks automatically
curl http://localhost:5000/petstore/pet/123
```

## Pluggable Tools: Mix Real & Mock Data

> **📖 Complete Guide:** [Tools & Actions](https://github.com/scottgal/LLMApi/blob/main/docs/TOOLS_ACTIONS.md)

Call real APIs during mocking:

```json
{
  "Tools": [{
    "Name": "getUserData",
    "Type": "http",
    "HttpConfig": {
      "Endpoint": "https://api.production.com/users/{userId}",
      "Headers": { "Authorization": "Bearer ${PROD_API_KEY}" }
    }
  }]
}
```

```bash
curl "http://localhost:5000/api/mock/orders?useTool=getUserData&userId=123"
```

The mock fetches REAL user data, then LLM generates orders using it. **Insanely powerful for realistic testing.**

## ASP.NET Core Integration

Point `HttpClient` to the mock during dev:

```csharp
// Real code from mostlylucid.net
builder.Services.AddHttpClient<IMarkdownTranslatorService, MarkdownTranslatorService>(
    client => {
        var baseUrl = builder.Configuration["TranslationService:BaseUrl"]
            ?? "http://localhost:5000";  // Mock during dev
        client.BaseAddress = new Uri(baseUrl);
    }
);
```

**appsettings.Development.json:**
```json
{
  "TranslationService": {
    "BaseUrl": "http://localhost:5000"  // Mock
  }
}
```

**appsettings.Production.json:**
```json
{
  "TranslationService": {
    "BaseUrl": "https://api.production.com"  // Real
  }
}
```

## Advanced Features

### Multiple LLM Backends

> **📖 Guide:** [Multiple LLM Backends](https://github.com/scottgal/LLMApi/blob/main/docs/MULTIPLE_LLM_BACKENDS.md)

```bash
# Fast for dev
curl http://localhost:5000/api/mock/users

# High quality for demos
curl "http://localhost:5000/api/mock/users?backend=quality"

# Cloud AI for production-like
curl "http://localhost:5000/api/mock/users?backend=openai"
```

### Rate Limiting Simulation

> **📖 Guide:** [Rate Limiting & Batching](https://github.com/scottgal/LLMApi/blob/main/docs/RATE_LIMITING_BATCHING.md)

Test how your app handles rate limits:

```json
{
  "EnableRateLimiting": true,
  "RateLimitDelayRange": "500-2000"
}
```

### Error Simulation

```bash
# Test 429 rate limiting
curl "http://localhost:5000/api/mock/users?error=429&errorMessage=Rate%20limit%20exceeded"

# Test 503 unavailable
curl "http://localhost:5000/api/mock/users?error=503"
```

Supports all 4xx and 5xx codes.

### Response Caching

```bash
# Generate and cache 10 variants
curl "http://localhost:5000/api/mock/users?shape={\"$cache\":10,\"id\":0,\"name\":\"string\"}"
```

Subsequent requests get instant cached responses.

## Testing Utilities

> **📖 Package:** [mostlylucid.mockllmapi.Testing](https://github.com/scottgal/LLMApi/blob/main/mostlylucid.mockllmapi.Testing/README.md)

```bash
dotnet add package mostlylucid.mockllmapi.Testing
```

Fluent API for tests:

```csharp
var client = HttpClientExtensions.CreateMockLlmClient(
    baseAddress: "http://localhost:5116",
    configure: handler => handler
        .ForEndpoint("/users", config => config
            .WithShape(new { id = 0, name = "", email = "" })
            .WithCache(5))
        .ForEndpoint("/error", config => config
            .WithError(500))
);

var users = await client.GetFromJsonAsync<User[]>("/users");
```

## When to Use This

**Perfect for:**
- Frontend dev before backend exists
- Multi-step workflow testing (with context memory!)
- Prototyping APIs before committing to schemas
- Offline development
- Testing error scenarios
- CI/CD without external dependencies

**Not ideal for:**
- Production (it's a dev tool)
- Deterministic test data (use fixtures for that)
- Contract testing (validate real APIs separately)

## Tips

1. **Always use contexts for workflows** - Ensures consistent IDs and data
2. **Use shape for type safety** - Make it match your TypeScript interfaces
3. **Mix real and mock data with tools** - Best of both worlds
4. **Choose the right model:**
   - Dev: `gemma3:4b` (fast, lightweight)
   - Production-like: `llama3` (best balance)
   - Complex: `mistral-nemo` (128K context)

## Complete Documentation

- **[Main Repository](https://github.com/scottgal/LLMApi)** - Source and overview
- **[Configuration Reference](https://github.com/scottgal/LLMApi/blob/main/docs/CONFIGURATION_REFERENCE.md)** - All settings
- **[API Contexts Guide](https://github.com/scottgal/LLMApi/blob/main/docs/API-CONTEXTS.md)** - Context memory deep dive
- **[Tools & Actions](https://github.com/scottgal/LLMApi/blob/main/docs/TOOLS_ACTIONS.md)** - External API integration
- **[OpenAPI Features](https://github.com/scottgal/LLMApi/blob/main/docs/OPENAPI-FEATURES.md)** - Spec-based mocking
- **[gRPC Support](https://github.com/scottgal/LLMApi/blob/main/docs/GRPC_SUPPORT.md)** - Protocol buffers
- **[Docker Guide](https://github.com/scottgal/LLMApi/blob/main/docs/DOCKER_GUIDE.md)** - Container deployment
- **[Multiple Backends](https://github.com/scottgal/LLMApi/blob/main/docs/MULTIPLE_LLM_BACKENDS.md)** - Multi-provider setup
- **[Rate Limiting](https://github.com/scottgal/LLMApi/blob/main/docs/RATE_LIMITING_BATCHING.md)** - Simulate limits
- **[Testing Package](https://github.com/scottgal/LLMApi/blob/main/mostlylucid.mockllmapi.Testing/README.md)** - HttpClient utilities

## Conclusion

`mostlylucid.mockllmapi` eliminates the wait for backend APIs. The combination of:

- **Context memory** - Consistent data across workflows
- **Shape control** - Precise schema definitions
- **All protocols** - REST, GraphQL, gRPC, SignalR, SSE, OpenAPI
- **External tools** - Mix real and mock data
- **Zero maintenance** - No fixtures to update

...makes it a complete solution for modern API development.

Whether building a blog like this or a complex enterprise app, you'll iterate faster and ship with confidence.

**Get started:** `docker compose up -d` and start building!
