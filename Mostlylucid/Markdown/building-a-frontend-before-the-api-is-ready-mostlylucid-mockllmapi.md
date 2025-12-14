# Building a Frontend Before the API is Ready: Without Brittle Fixtures

<datetime class="hidden">2025-12-13T14:30</datetime>
<!--category-- ASP.NET Core, LLM, API Development, Testing, Mock APIs -->

## Introduction

How many times have you been blocked waiting for backend APIs to be ready? Or spent hours maintaining brittle mock data that becomes stale the moment requirements change?

Enter `mostlylucid.mockllmapi` - a production-ready ASP.NET Core mocking platform that uses Large Language Models to generate realistic, contextually aware API responses on the fly. Instead of maintaining JSON fixtures, you get intelligent mocks that adapt to your requests and remember state across calls.

**What it supports:** Every protocol you need - REST, GraphQL, gRPC, SignalR, Server-Sent Events, and OpenAPI. Unlike static fixtures, responses are generated dynamically based on your request context, making multi-step workflows and complex testing scenarios trivial.

### Project Links

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![GitHub Release](https://img.shields.io/github/v/release/scottgal/LLMApi)](https://github.com/scottgal/LLMApi/releases)
[![License: Unlicense](https://img.shields.io/badge/license-Unlicense-blue.svg)](http://unlicense.org/)

- **Repository:** [https://github.com/scottgal/LLMApi](https://github.com/scottgal/LLMApi)
- **Releases:** https://github.com/scottgal/LLMApi/releases
- **Companion Package:** [mostlylucid.mockllmapi.Testing](https://github.com/scottgal/LLMApi/blob/master/mostlylucid.mockllmapi.Testing/README.md) - Testing utilities with fluent HttpClient integration

### Three Ways to Use It

You can use mostlylucid.mockllmapi in three ways, depending on how isolated you want your dev environment to be:

1. **ASP.NET Core NuGet package** - Add to your existing projects
2. **Standalone CLI tool** - Cross-platform executable (download from [releases](https://github.com/scottgal/LLMApi/releases))
3. **Docker container** - Zero installation required

[TOC]

## The Killer Feature: Context Memory

> **📖 Full Guide:** [API Contexts Documentation](https://github.com/scottgal/LLMApi/blob/master/docs/API-CONTEXTS.md)

Traditional mock APIs have a fatal flaw: each request is independent. Get a user with ID 42, then fetch their orders, and you'll get orders for user ID 99. No consistency.

**API Contexts** solve this with shared memory across related requests:

```javascript
// Request 1: Get a user
// Note: 'context' is a simple query parameter - no cookies or sessions needed
fetch('/api/users/123?context=checkout-session')
// Response: { id: 42, name: "Alice Smith", email: "alice@example.com" }

// Request 2: Get orders (same context parameter)
fetch('/api/orders?userId=42&context=checkout-session')
// Response: { userId: 42, customerName: "Alice Smith", items: [...] }
// Perfect! Same user, consistent data
```

The LLM sees previous requests in the same context and generates consistent data. **This is the game-changer for multi-step workflows.**

**Features:**

**Lifecycle:**
- Automatic expiration after 15 minutes of inactivity (configurable)
- Each request refreshes the timer

**Behaviour:**
- Intelligent extraction of ALL fields from responses

**Safety:**
- Zero memory leaks - contexts clean themselves up

**Use Cases:**
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

> **📖 Complete Guide:** [Docker Deployment Guide](https://github.com/scottgal/LLMApi/blob/master/docs/DOCKER_GUIDE.md)

```bash
git clone https://github.com/scottgal/LLMApi.git
cd LLMApi
docker compose up -d
```

### Prerequisites: LLM Backend

You need **one of**: Ollama, OpenAI, or LM Studio:

```bash
# Recommended: Ollama with ministral-3:3b (ultra-fast, accurate JSON generation)
ollama pull ministral-3:3b
```

See [Ollama Models Guide](https://github.com/scottgal/LLMApi/blob/master/docs/OLLAMA_MODELS.md) for all model recommendations and comparisons.

### Try It Immediately

Once running, make your first request:

```bash
curl http://localhost:5000/api/mock/users
# Response: [{"id": 1, "name": "Alice Johnson", "email": "alice@example.com"}, ...]
```

That's it! You now have a working mock API that generates realistic data on demand.

## Real Example: Search from mostlylucid.net

Here's the actual search code from this blog - **this is unchanged production frontend code**, no adaptations needed for the mock:

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

Beyond just generating random data, you often need precise control over the JSON structure. Shape control lets you tell the LLM exactly what structure to generate - the most powerful feature for frontend development.

### Basic Shape

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

### Nested Shape
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

### TypeScript Alignment
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

Now let's combine shape control with API contexts to handle complex, multi-step workflows. Remember the context memory feature from earlier? Here's how it shines in real-world asynchronous operations.

This example from mostlylucid.net's translation service shows how the LLM maintains state across a complete async workflow:

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

So far we've focused on REST, but modern applications need more. Whether you're building with GraphQL, implementing real-time features with SignalR, or working with gRPC services, mostlylucid.mockllmapi has you covered.

**Supported protocols:**

- ✓ REST
- ✓ GraphQL
- ✓ gRPC
- ✓ SignalR
- ✓ Server-Sent Events (SSE)
- ✓ OpenAPI / Swagger

### GraphQL

> **📖 Guide:** [GraphQL Section](https://github.com/scottgal/LLMApi#graphql-api-mocking)

```bash
curl -X POST http://localhost:5000/api/mock/graphql \
  -d '{"query": "{ users { id name email } }"}'
```

The query IS the shape - no separate schema needed.

### gRPC

> **📖 Complete Guide:** [gRPC Support](https://github.com/scottgal/LLMApi/blob/master/docs/GRPC_SUPPORT.md)

```bash
# Upload .proto file
curl -X POST http://localhost:5116/api/grpc-protos \
  --data-binary "@user_service.proto"

# Call via JSON or binary Protobuf
curl -X POST http://localhost:5116/api/grpc/userservice/UserService/GetUser \
  -d '{"user_id": 123}'
```

### SignalR Real-Time

> **📖 Guide:** [SignalR Demo Guide](https://github.com/scottgal/LLMApi/blob/master/SIGNALR_DEMO_GUIDE.md)

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

> **📖 Guide:** [SSE Streaming Modes](https://github.com/scottgal/LLMApi/blob/master/docs/SSE_STREAMING_MODES.md)

```javascript
const eventSource = new EventSource('/api/mock/stream/users');
eventSource.onmessage = (event) => {
    const data = JSON.parse(event.data);
    console.log('Token:', data.chunk); // Progressive generation
};
```

### OpenAPI / Swagger

> **📖 Complete Guide:** [OpenAPI Features](https://github.com/scottgal/LLMApi/blob/master/docs/OPENAPI-FEATURES.md)

```bash
# CLI: Load any OpenAPI spec
llmock serve --spec https://petstore3.swagger.io/api/v3/openapi.json

# All endpoints become live mocks automatically
curl http://localhost:5000/petstore/pet/123
```

## Pluggable Tools: Mix Real & Mock Data

> **📖 Complete Guide:** [Tools & Actions](https://github.com/scottgal/LLMApi/blob/master/docs/TOOLS_ACTIONS.md)

Sometimes you need a hybrid approach - real data from production combined with generated mock data. The pluggable tools system lets you call actual APIs during mock generation, creating incredibly realistic test scenarios.

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

The mock fetches REAL user data, then the LLM generates orders using it. **Extremely useful for realistic testing with hybrid mock/real workflows.**

## ASP.NET Core Integration

If you're building with ASP.NET Core, integration is seamless. The beauty of this approach is **zero code changes** to your services - you simply configure `HttpClient` to point at the mock during development and at the real API in production.

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

This pattern works for any `HttpClient` in your application - translation services, payment gateways, external APIs, you name it.

## When to Use This

Before we dive into advanced features, let's be clear about when this tool makes sense for your workflow.

**Perfect for:**
- **Frontend development before backend exists** - Stop blocking on backend teams
- **Multi-step workflow testing** - Context memory handles complex scenarios
- **API prototyping** - Experiment with response shapes before committing
- **Offline development** - Work without network dependencies
- **Error scenario testing** - Simulate failures without breaking production
- **CI/CD pipelines** - No external dependencies means faster, more reliable builds

**Not ideal for:**
- **Production environments** - This is a development and testing tool
- **Deterministic test data** - Use fixtures when you need exact reproducibility
- **Contract testing** - Always validate against real APIs for production contracts

Now that you know where it fits, let's explore the advanced capabilities.

## Advanced Features

> These features are optional - you can get tremendous value from the basics alone. But when you need production-grade realism at scale, these tools are here.

### Multiple LLM Backends

> **📖 Guide:** [Multiple LLM Backends](https://github.com/scottgal/LLMApi/blob/master/docs/MULTIPLE_LLM_BACKENDS.md)

```bash
# Fast for dev
curl http://localhost:5000/api/mock/users

# High quality for demos
curl "http://localhost:5000/api/mock/users?backend=quality"

# Cloud AI for production-like
curl "http://localhost:5000/api/mock/users?backend=openai"
```

### Rate Limiting Simulation

> **📖 Guide:** [Rate Limiting & Batching](https://github.com/scottgal/LLMApi/blob/master/docs/RATE_LIMITING_BATCHING.md)

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

## Testing Utilities: mostlylucid.mockllmapi.Testing

> **📖 Package:** [mostlylucid.mockllmapi.Testing](https://github.com/scottgal/LLMApi/blob/master/mostlylucid.mockllmapi.Testing/README.md)

All the features above are great for development, but what about automated testing? The companion testing package provides a fluent API that makes integration tests a breeze - configure mock behavior declaratively and let `HttpClient` do the rest.

### Installation

```bash
dotnet add package mostlylucid.mockllmapi.Testing
```

### Basic Usage

```csharp
using mostlylucid.mockllmapi.Testing;

// Create a client with a single endpoint configuration
var client = HttpClientExtensions.CreateMockLlmClient(
    baseAddress: "http://localhost:5116",
    pathPattern: "/users",
    configure: endpoint => endpoint
        .WithShape(new { id = 0, name = "", email = "" })
        .WithCache(5)
);

// Make requests - configuration is automatically applied
var response = await client.GetAsync("/users");
var users = await response.Content.ReadFromJsonAsync<User[]>();
```

### Multiple Endpoints

```csharp
var client = HttpClientExtensions.CreateMockLlmClient(
    "http://localhost:5116",
    configure: handler => handler
        .ForEndpoint("/users", config => config
            .WithShape(new { id = 0, name = "", email = "" })
            .WithCache(10))
        .ForEndpoint("/posts", config => config
            .WithShape(new { id = 0, title = "", content = "", authorId = 0 })
            .WithCache(20))
        .ForEndpoint("/error", config => config
            .WithError(404, "Resource not found"))
);

// Each endpoint automatically uses its configuration
var usersResponse = await client.GetAsync("/users");
var postsResponse = await client.GetAsync("/posts");
var errorResponse = await client.GetAsync("/error"); // Returns 404
```

### Configuration Options

**Shape Configuration:**
```csharp
// Using anonymous objects
.WithShape(new { id = 0, name = "", active = true })

// Using JSON strings
.WithShape("{ \"id\": 0, \"name\": \"\", \"tags\": [] }")

// Complex nested structures
.WithShape(new
{
    user = new { id = 0, name = "" },
    posts = new[] { new { id = 0, title = "" } }
})
```

**Error Simulation:**
```csharp
// Simple error
.WithError(404)

// With custom message
.WithError(404, "User not found")

// With details
.WithError(422, "Validation failed", "Email address is invalid")
```

**Streaming:**
```csharp
// Enable streaming with token-by-token output
.WithStreaming()
.WithSseMode("LlmTokens")

// Stream complete objects
.WithStreaming()
.WithSseMode("CompleteObjects")

// Stream array items individually
.WithStreaming()
.WithSseMode("ArrayItems")
```

### Dependency Injection

**Typed Client:**
```csharp
services.AddMockLlmHttpClient<IUserApiClient>(
    baseApiPath: "/api/mock",
    configure: handler => handler
        .ForEndpoint("/users", config => config
            .WithShape(new { id = 0, name = "", email = "" }))
);
```

**Named Client:**
```csharp
services.AddMockLlmHttpClient(
    name: "MockApi",
    baseApiPath: "/api/mock",
    configure: handler => handler
        .ForEndpoint("/data", config => config
            .WithShape(new { value = 0 }))
);

// Usage
var client = httpClientFactory.CreateClient("MockApi");
```

### Integration Testing Example

```csharp
[Fact]
public async Task Should_Handle_User_Creation()
{
    // Arrange
    var client = HttpClientExtensions.CreateMockLlmClient(
        "http://localhost:5116",
        "/users",
        config => config
            .WithMethod("POST")
            .WithShape(new { id = 0, name = "", email = "", createdAt = "" })
    );

    // Act
    var newUser = new { name = "John Doe", email = "john@example.com" };
    var response = await client.PostAsJsonAsync("/users", newUser);

    // Assert
    response.EnsureSuccessStatusCode();
    var created = await response.Content.ReadFromJsonAsync<User>();
    Assert.NotNull(created);
    Assert.NotEqual(0, created.Id);
}

[Fact]
public async Task Should_Handle_Not_Found_Error()
{
    // Arrange
    var client = HttpClientExtensions.CreateMockLlmClient(
        "http://localhost:5116",
        "/users/999",
        config => config.WithError(404, "User not found")
    );

    // Act
    var response = await client.GetAsync("/users/999");

    // Assert
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

### How It Works

The `MockLlmHttpHandler` is a `DelegatingHandler` that:

1. Intercepts outgoing HTTP requests
2. Matches requests against configured endpoint patterns
3. Injects mock configuration via query parameters and HTTP headers
4. Forwards the modified request to the actual mock LLM API

This allows you to use a real `HttpClient` in your tests while easily controlling mock API behavior without modifying your application code.

## Best Practices & Tips

After working with this tool across multiple projects, here are the patterns that work best:

1. **Always use contexts for workflows** - Ensures consistent IDs and data across multi-step operations
2. **Use shape for type safety** - Make it match your TypeScript interfaces
3. **Mix real and mock data with tools** - Best of both worlds
4. **Choose the right model** (see [Ollama Models Guide](https://github.com/scottgal/LLMApi/blob/master/docs/OLLAMA_MODELS.md) for complete details):
   - **RECOMMENDED for dev**: `ministral-3:3b` (3B params, 32K context) - **KILLER for JSON!** Ultra-fast, highly accurate, minimal RAM
   - **Production-like**: `llama3` (8B params, 8K context) - Best balance of quality and performance
   - **High quality**: `mistral-nemo` (12B params, 128K context) - Complex schemas and massive datasets
   - **Resource-constrained**: `gemma3:4b` or `phi3` - Lighter alternatives

## Complete Documentation

- **[Main Repository](https://github.com/scottgal/LLMApi)** - Source and overview
- **[Configuration Reference](https://github.com/scottgal/LLMApi/blob/master/docs/CONFIGURATION_REFERENCE.md)** - All settings
- **[API Contexts Guide](https://github.com/scottgal/LLMApi/blob/master/docs/API-CONTEXTS.md)** - Context memory deep dive
- **[Tools & Actions](https://github.com/scottgal/LLMApi/blob/master/docs/TOOLS_ACTIONS.md)** - External API integration
- **[OpenAPI Features](https://github.com/scottgal/LLMApi/blob/master/docs/OPENAPI-FEATURES.md)** - Spec-based mocking
- **[gRPC Support](https://github.com/scottgal/LLMApi/blob/master/docs/GRPC_SUPPORT.md)** - Protocol buffers
- **[Docker Guide](https://github.com/scottgal/LLMApi/blob/master/docs/DOCKER_GUIDE.md)** - Container deployment
- **[Multiple Backends](https://github.com/scottgal/LLMApi/blob/master/docs/MULTIPLE_LLM_BACKENDS.md)** - Multi-provider setup
- **[Rate Limiting](https://github.com/scottgal/LLMApi/blob/master/docs/RATE_LIMITING_BATCHING.md)** - Simulate limits
- **[Testing Package](https://github.com/scottgal/LLMApi/blob/master/mostlylucid.mockllmapi.Testing/README.md)** - HttpClient utilities

## Conclusion

Frontend development doesn't have to wait for backend APIs. `mostlylucid.mockllmapi` gives you:

- **Context memory** - Consistent, stateful data across multi-step workflows
- **Shape control** - Precise schema definitions that match your types
- **Universal protocol support** - REST, GraphQL, gRPC, SignalR, SSE, OpenAPI
- **Hybrid testing** - Mix real production data with generated mocks
- **Zero maintenance** - No JSON fixtures to update when requirements change
- **Testing utilities** - Fluent API for integration tests

The difference between this and traditional mocking? Your frontend works against realistic, contextually aware data from day one. No more "it worked with mock data but failed with real data" surprises.

Whether you're building a simple blog or a complex enterprise application, you'll iterate faster, test more thoroughly, and ship with confidence.

**Ready to get started?**

```bash
docker compose up -d
```

That's it. No backend required.
