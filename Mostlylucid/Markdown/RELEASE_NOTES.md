# Release Notes


<!--category-- AI, LLM, ASP.NET Core, API, Nuget, mockllmapi-->
<datetime class="hidden">2025-11-02T12:30</datetime>

> NOTE: This is release documents for the llmapi, just ignore

## v1.5.0 (2025-01-05)

**NO BREAKING CHANGES** - All existing code continues to work!

### New Features

#### API Contexts - Shared Memory Across Requests (MAJOR)

**Cross-Endpoint Context Support**: All endpoint types (REST, Streaming, GraphQL, SignalR) now support shared context for maintaining consistency across related requests!

- **Pass Context Name via**: Query param (`?context=name`), HTTP header (`X-Api-Context: name`), or request body (`{"context": "name"}`)
- **Alternative Parameter**: Use `api-context` instead of `context` for clarity
- **Automatic Context Tracking**: System remembers recent calls (method, path, request, response) in each context
- **Smart Data Extraction**: Automatically extracts and tracks IDs, names, emails from responses
- **Consistency Across Calls**: LLM generates new responses that maintain consistency with previous context history

**Use Cases:**
- **E-commerce Flows**: Create user ? add items to cart ? checkout - cart remembers user ID from first call
- **Stock Tickers**: Generate realistic price variance based on previous prices (no wild jumps)
- **Game State**: Track player stats, inventory, scores consistently across multiple API calls
- **User Journeys**: Multi-step workflows where later steps reference earlier data

**Example:**
```bash
# First call - create user
curl "http://localhost:5000/api/mock/users?context=session1" \
  -H "X-Response-Shape: {\"id\":0,\"name\":\"string\",\"email\":\"string\"}"
# Response: {"id": 12345, "name": "Alice", "email": "alice@example.com"}

# Second call - create order (LLM uses user data from context)
curl "http://localhost:5000/api/mock/orders?context=session1" \
  -H "X-Response-Shape: {\"orderId\":0,\"userId\":0,\"total\":0.0}"
# Response: {"orderId": 67890, "userId": 12345, "total": 49.99}
# Notice userId matches! LLM used context history
```

**SignalR Context Support**: SignalR hubs can now reference API contexts for realistic data variance:
```json
{
  "HubContexts": [
    {
      "Name": "stock-prices",
      "Description": "Stock ticker with realistic price changes",
      "ApiContextName": "stocks"  // NEW: References shared context
    }
  ]
}
```

#### Token Budget Management & Intelligent Truncation

- **MaxInputTokens Configuration**: Control token budget per model (default: 2048)
- **80/20 Priority Allocation**: Base prompt gets 80% of tokens, context history gets 20%
- **Automatic Truncation**: When context history exceeds budget, older calls are dropped
- **Detailed Logging**: See exactly which calls were dropped and why:
  ```
  Context 'session1': Dropped 3/8 calls to fit 2048 token limit (20% of max).
  Dropped: [GET /users/1, GET /users/2, POST /orders]
  ```
- **Token Estimation**: Approximates 1 token � 4 characters for quick budget calculations

#### Model-Specific Configuration Examples

Added comprehensive configuration examples for 6 popular Ollama models in appsettings.json:
- **Llama 3 / 3.1** (4K context) - Recommended
- **TinyLlama** (2K context) - Fast but limited
- **Qwen 3 14B** (4K context) - Good quality
- **Mistral/Mixtral** (8K context) - High quality
- **Llama 2** (4K context) - Older
- **Phi-3** (4K context) - Microsoft

Each includes recommended `MaxInputTokens` values for optimal performance.

### New Files
- `mostlylucid.mockllmapi/Services/ContextExtractor.cs` - Extracts context names from requests
- `docs/API-CONTEXTS` - Complete guide to context features with Mermaid diagrams
- `docs/OPENAPI-FEATURES.md` - Complete guide to OpenAPI dynamic mocking features

### Documentation Improvements
- Added context usage examples to `management.http` (~220 lines of examples)
- Created comprehensive blog-format documentation with architecture diagrams
- Updated README with navigation to new feature docs
- Added troubleshooting guide for token limits

### Technical Details

**Context Extraction Precedence**: Query param > HTTP header > Request body

**Context Storage**: ConcurrentDictionary with case-insensitive keys for thread safety

**Context Lifecycle**:
1. Extract context name from request
2. Retrieve context history (recent calls, shared data, summary)
3. Build prompt with context history (20% of token budget)
4. Generate response using LLM
5. Store response in context for future calls

**Automatic Summarization**: When context exceeds 15 recent calls, older calls are summarized to save memory

**Shared Data Tracking**: Automatically extracts common patterns (IDs, names, emails) for consistency

### Configuration

```json
{
  "MockLlmApi": {
    "MaxInputTokens": 4096,  // NEW: Token budget per model

    // Model examples with recommended token limits
    "ModelName": "llama3",
    "Temperature": 1.2
  }
}
```

### Upgrade Notes
- Context support is opt-in - pass context parameter to enable
- MaxInputTokens defaults to 2048, adjust based on your model
- No breaking changes to existing functionality
- All 147 tests passing

---

## v1.2.1 (2025-01-04)

**Critical Bug Fix**

### Fixed
- **Retry Policy HTTP Request Reuse Issue**: Fixed a critical bug where `HttpRequestMessage` objects were being reused across retry attempts, causing "request has already been sent" errors
  - **Root Cause**: In .NET, `HttpRequestMessage` can only be sent once. When Polly's retry policy attempted to retry a failed request, it was trying to reuse the same request object
  - **Solution**: Now creates a fresh `HttpRequestMessage` inside the retry lambda for each attempt, ensuring clean retries
  - **Impact**: All three LLM client methods are now properly retry-safe:
    - `GetCompletionAsync()` - Regular completions
    - `GetStreamingCompletionAsync()` - Streaming responses
    - `GetNCompletionsAsync()` - Multiple completions
  - **Test Results**: All 92 unit tests now pass (previously had intermittent retry failures)

### Technical Details
Changed from this (broken):
```csharp
// ? BAD: Request created once, reused for retries
var httpReq = new HttpRequestMessage(...);
await _resiliencePipeline.ExecuteAsync(async ct =>
    await client.SendAsync(httpReq, ct), cancellationToken);
```

To this (fixed):
```csharp
// ? GOOD: Fresh request for each retry attempt
await _resiliencePipeline.ExecuteAsync(async ct => {
    using var httpReq = new HttpRequestMessage(...);
    return await client.SendAsync(httpReq, ct);
}, cancellationToken);
```

This ensures that retries work correctly when requests fail or timeout, making the resilience policies fully functional as intended.

---

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
