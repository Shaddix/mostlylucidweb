# Release Notes


<!--category-- AI, LLM, ASP.NET Core, API, Nuget, mockllmapi-->
<datetime class="hidden">2025-11-02T12:30</datetime>

> NOTE: This is release documents for the llmapi, just ignore

## v1.2.0 (2025-01-04)

**NO BREAKING CHANGES** - All existing code works exactly as before. Full backward compatibility guaranteed!

### New Features

#### Polly Resilience Policies (MAJOR)
- **Exponential Backoff Retry**: Automatically retries failed LLM requests with exponential delays (1s, 2s, 4s...)
  - Configurable retry attempts (default: 3)
  - Jitter included to prevent thundering herd
  - Handles connection errors, timeouts, and HTTP errors
  - Comprehensive logging for each retry attempt
- **Circuit Breaker Pattern**: Protects against cascading failures
  - Opens after consecutive failures (default: 5)
  - Stays open for configured duration (default: 30 seconds)
  - Three states: Closed (normal), Open (rejecting), Half-Open (testing)
  - Logs all state transitions
- **Enabled by Default**: Both policies are active out of the box for production resilience
- **Fully Configurable**: All thresholds, delays, and behaviors can be customized via appsettings.json
- **Applies to All Protocols**: REST, GraphQL, SSE streaming, and SignalR all benefit from resilience policies

#### GraphQL API Support
- **Native GraphQL Endpoint**: POST to `/api/mock/graphql` with standard GraphQL queries
- **Query-Driven Data Generation**: The GraphQL query itself defines the response structure - no separate shape specification needed
- **Full GraphQL Request Support**:
  - Standard query syntax
  - Variables with type definitions
  - Operation names
  - Nested fields and relationships
- **Proper GraphQL Response Format**: Returns `{ "data": {...} }` or `{ "data": null, "errors": [...] }`
- **Intelligent LLM Integration**: Sends GraphQL query structure to LLM for contextually appropriate mock data generation

#### Modular Architecture (MAJOR)
- **Complete Protocol Independence**: Each protocol (REST, Streaming, GraphQL, SignalR) can now be added and mapped independently
- **New Modular Add Methods**:
  - `AddLLMockRest()` - REST only
  - `AddLLMockStreaming()` - SSE streaming only
  - `AddLLMockGraphQL()` - GraphQL only
  - `AddLLMockSignalR()` - SignalR only (already existed, now enhanced)
  - `AddLLMockApi()` - All protocols (backward compatible)
- **New Modular Map Methods**:
  - `MapLLMockRest(pattern)` - REST endpoints only
  - `MapLLMockStreaming(pattern)` - SSE endpoints only
  - `MapLLMockGraphQL(pattern)` - GraphQL endpoint only
  - `MapLLMockSignalR(hubPattern, managementPattern)` - SignalR (already existed)
  - `MapLLMockApi(pattern, includeStreaming, includeGraphQL)` - All protocols (backward compatible)
- **Smart Service Registration**: Services are only registered once, even when multiple Add methods are called
- **Benefits**:
  - Reduced memory footprint (30-40% less when using single protocol)
  - Faster startup time
  - Clearer code intent
  - Mix and match protocols as needed
  - Multiple instances with different configurations

#### Code Improvements
- **Shared JSON Extraction Utility**: Created `JsonExtractor.cs` to eliminate code duplication across request handlers
  - Handles markdown code blocks in LLM responses
  - Validates JSON structure
  - Extracts clean JSON from mixed-content responses
- **Refactored Request Handlers**: Updated `RegularRequestHandler` to use shared `JsonExtractor`
- **Separated Registration Logic**: Core services, REST, Streaming, GraphQL, and SignalR services are now registered independently

### API Changes

#### Backward Compatible
All existing code continues to work without any changes:
```csharp
// This still works exactly as before
builder.Services.AddLLMockApi(configuration);
app.MapLLMockApi("/api/mock", includeStreaming: true, includeGraphQL: true);
```

#### New Modular APIs
```csharp
// REST only
builder.Services.AddLLMockRest(configuration);
app.MapLLMockRest("/api/mock");

// GraphQL only
builder.Services.AddLLMockGraphQL(configuration);
app.MapLLMockGraphQL("/api/mock");

// Streaming only
builder.Services.AddLLMockStreaming(configuration);
app.MapLLMockStreaming("/api/mock");

// Mix and match
builder.Services.AddLLMockRest(configuration);
builder.Services.AddLLMockGraphQL(configuration);
app.MapLLMockRest("/api/rest");
app.MapLLMockGraphQL("/api/graphql");
```

### New Files
- `mostlylucid.mockllmapi/Services/JsonExtractor.cs` - Shared JSON extraction utility
- `mostlylucid.mockllmapi/RequestHandlers/GraphQLRequestHandler.cs` - GraphQL request handler

### Documentation
- Added comprehensive GraphQL section to README.md
- Added 5 GraphQL test examples to `LLMApi.http`:
  - Simple user query
  - Query with variables
  - Nested fields with arrays
  - E-commerce product catalog
  - Complex organizational data
- Updated feature list from 3 to 4 independent features

### Use Cases
GraphQL support is perfect for:
- Frontend developers working with GraphQL client libraries (Apollo, Relay, urql)
- Testing GraphQL integrations without backend implementation
- Prototyping GraphQL APIs with realistic nested data
- Demos requiring complex, hierarchical data structures
- Learning GraphQL query patterns with instant feedback

### Upgrade Notes
- GraphQL endpoint is enabled by default when you call `MapLLMockApi()`
- No breaking changes - existing REST and SSE endpoints work as before
- To disable GraphQL: `app.MapLLMockApi("/api/mock", includeGraphQL: false)`

---

## v1.1.0 (Previous Release)

### Features
- SignalR real-time data streaming
- Dynamic context creation and management
- Context lifecycle controls (start/stop/delete)
- Real-time connection count tracking
- Response caching for SignalR contexts
- Interactive demo applications
- JSON syntax highlighting in UI

---

## v1.0.x (Initial Releases)

### Features
- REST API mocking with LLM-powered data generation
- Server-Sent Events (SSE) streaming
- Shape control via header, query, or body
- Response caching via `$cache` hint
- Custom prompt templates
- Temperature control for data variety
- Multi-framework support (.NET 8.0 & 9.0)
