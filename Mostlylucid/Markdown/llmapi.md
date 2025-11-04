# Using Local LLMs to Automatically Simulate APIs in ASP.NET Core MimimalAPI


<!--category-- AI, LLM, ASP.NET Core, API, Nuget, mockllmapi-->
<datetime class="hidden">2025-11-02T12:30</datetime>

# Introduction 
One of my constant obsessions is sample data. It's an often annoying aspect of developing systems that you have the catch 22 of needing data to appropritately test fucntionality while developing the system which would let you create said data.
Combined with my current love of 'AI assisted' coding random ideas it's led to the ceation of a little LLM enabled test data generator along with a nuget package with a middleware that can generate simulated API responses using an API:


[![NuGet](https://img.shields.io/nuget/v/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
 
You can find the[ GitHub here ](https://github.com/scottgal/LLMApi)for the project, all public domain etc...

[TOC]  

# Concept
As with a lot of "AI Assisted" coding ideas it started with an idea about simulating output using LLMs. I was working on another project ([LucidForums](https://github.com/scottgal/lucidforums), a hilariously dysfunctional self populating LLM based forum experiment) and LLMs are really good (if a little slow) at generating sample data so *what if* I could use them to simulate any API. 

# Requirements
1. It shoudl be simple to implement in an ASP.NET Core App.   
2. The data should be fairly realistic
3. The data should be random
4. It can accept JSON 'shapes' which tell the LLM what the response should be like
5. It can use local Open Source LLMs 
6. It should be a free to use Nuget control

This is what I came up with. I'll add more detail on the thinking as I add more functionality. It's really neat, really works and is faster than I'd feared. However; I have an A4000 16Gb, you could select smaller edge models but the quality would likey vary massively.

Future additions will likely include caching in case you want faster perf.

Here's the readme for the package.

# mostlylucid.mockllmapi

A lightweight ASP.NET Core middleware for generating realistic mock API responses using local LLMs (via Ollama). Add intelligent mock endpoints to any project with just 2 lines of code!

[![NuGet](https://img.shields.io/nuget/v/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)
[![NuGet](https://img.shields.io/nuget/dt/mostlylucid.mockllmapi.svg)](https://www.nuget.org/packages/mostlylucid.mockllmapi)

## Features

This package provides **three independent features** - use any combination you need:

### 1. REST API Mocking
- **Super Simple**: `AddLLMockApi()` + `MapLLMockApi("/api/mock")` = instant mock API
- **Shape Control**: Specify exact JSON structure via header, query param, or request body
- **All HTTP Methods**: Supports GET, POST, PUT, DELETE, PATCH
- **Wildcard Routing**: Any path under your chosen endpoint works

### 2. Server-Sent Events (SSE) Streaming
- **Progressive Streaming**: SSE support with progressive JSON generation
- **Real-time Updates**: Stream data token-by-token to clients
- **Works standalone**: No REST API setup required

### 3. SignalR Real-Time Streaming
- **WebSocket Streaming**: Continuous real-time mock data via SignalR
- **Multiple Contexts**: Run multiple independent data streams simultaneously
- **Lifecycle Management**: Start/stop contexts dynamically with management API
- **Works standalone**: No REST API setup required

### Common Features
- **Configurable**: appsettings.json or inline configuration
- **Highly Variable Data**: Each request/update generates completely different realistic data
- **NuGet Package**: Easy to add to existing projects

## Quick Start

### Installation

```bash
dotnet add package mostlylucid.mockllmapi
```

### Prerequisites

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
2. Install [Ollama](https://ollama.ai/) and pull a model:
   ```bash
   ollama pull llama3
   ```

### Basic Usage

**Program.cs:**
```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Add LLMock API services
builder.Services.Addmostlylucid.mockllmapi(builder.Configuration);

var app = builder.Build();

// Map mock endpoints at /api/mock
app.Mapmostlylucid.mockllmapi("/api/mock");

app.Run();
```

**appsettings.json:**
```json
{
  "mostlylucid.mockllmapi": {
    "BaseUrl": "http://localhost:11434/v1/",
    "ModelName": "llama3",
    "Temperature": 1.2
  }
}
```

That's it! Now all requests to `/api/mock/**` return intelligent mock data.

## Configuration Options

### Via appsettings.json (Recommended)

```json
{
  "mostlylucid.mockllmapi": {
    "BaseUrl": "http://localhost:11434/v1/",
    "ModelName": "llama3",
    "Temperature": 1.2,
    "TimeoutSeconds": 30,
    "EnableVerboseLogging": false,
    "CustomPromptTemplate": null
  }
}
```

### Via Code

```csharp
builder.Services.Addmostlylucid.mockllmapi(options =>
{
    options.BaseUrl = "http://localhost:11434/v1/";
    options.ModelName = "mixtral";
    options.Temperature = 1.5;
    options.TimeoutSeconds = 60;
});
```

### Custom Endpoint Patterns

```csharp
// Default: /api/mock/** and /api/mock/stream/**
app.Mapmostlylucid.mockllmapi("/api/mock");

// Custom pattern
app.Mapmostlylucid.mockllmapi("/demo");
// Creates: /demo/** and /demo/stream/**

// Without streaming
app.Mapmostlylucid.mockllmapi("/api/mock", includeStreaming: false);
```

## Usage Examples

### Basic Request

```bash
curl http://localhost:5000/api/mock/users?limit=5
```

Returns realistic user data generated by the LLM.

### With Shape Control

```bash
curl -X POST http://localhost:5000/api/mock/orders \
  -H "X-Response-Shape: {\"orderId\":\"string\",\"total\":0.0,\"items\":[{\"sku\":\"string\",\"qty\":0}]}" \
  -H "Content-Type: application/json" \
  -d '{"customerId":"cus_123"}'
```

LLM generates data matching your exact shape specification.

### Streaming (SSE - Server-Sent Events)

**SSE streaming is part of the REST API - just enable it when mapping endpoints:**

```csharp
// SSE streaming is automatically available at /api/mock/stream/**
app.MapLLMockApi("/api/mock", includeStreaming: true);
```

**Usage:**

```bash
curl -N http://localhost:5000/api/mock/stream/products?category=electronics \
  -H "Accept: text/event-stream"
```

Returns Server-Sent Events as JSON is generated token-by-token:
```
data: {"chunk":"{","done":false}
data: {"chunk":"\"id\"","done":false}
data: {"chunk":":","done":false}
data: {"chunk":"123","done":false}
...
data: {"content":"{\"id\":123,\"name\":\"Product\"}","done":true,"schema":"{...}"}
```

**JavaScript Example:**
```javascript
const eventSource = new EventSource('/api/mock/stream/users?limit=5');

eventSource.onmessage = (event) => {
    const data = JSON.parse(event.data);

    if (data.done) {
        console.log('Complete:', data.content);
        eventSource.close();
    } else {
        console.log('Chunk:', data.chunk);
    }
};
```

**With Shape Control:**
```bash
curl -N "http://localhost:5000/api/mock/stream/orders?shape=%7B%22id%22%3A0%2C%22items%22%3A%5B%5D%7D"
```

The streaming endpoint supports all the same features as regular endpoints:
- Shape control (query param, header, or body)
- JSON Schema support
- Custom prompts
- All HTTP methods (GET, POST, PUT, DELETE, PATCH)

## SignalR Real-Time Data Streaming

LLMock API includes optional SignalR support for continuous, real-time mock data generation. This is perfect for:
- Dashboard prototypes requiring live updates
- Testing real-time UI components
- Demos with constantly changing data
- WebSocket/SignalR integration testing

### Quick Start with SignalR

**SignalR works independently - you don't need the REST API endpoints to use SignalR streaming.**

**1. Minimal SignalR-only setup:**

```csharp
using mostlylucid.mockllmapi;

var builder = WebApplication.CreateBuilder(args);

// Add SignalR services (no REST API needed!)
builder.Services.AddLLMockSignalR(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Map SignalR hub and management endpoints
app.MapLLMockSignalR("/hub/mock", "/api/mock");

app.Run();
```

**Optional: Add REST API too**

If you also want the REST API endpoints, add these lines:

```csharp
// Add core LLMock API services (optional)
builder.Services.AddLLMockApi(builder.Configuration);

// Map REST API endpoints (optional)
app.MapLLMockApi("/api/mock", includeStreaming: true);
```

**2. Configure in appsettings.json:**

```json
{
  "MockLlmApi": {
    "BaseUrl": "http://localhost:11434/v1/",
    "ModelName": "llama3",
    "Temperature": 1.2,

    "SignalRPushIntervalMs": 5000,
    "HubContexts": [
      {
        "Name": "weather",
        "Description": "Weather data with temperature, condition, humidity, and wind speed"
      },
      {
        "Name": "stocks",
        "Description": "Stock market data with symbol, current price, change percentage, and trading volume"
      }
    ]
  }
}
```

**3. Connect from client:**

```javascript
// Using @microsoft/signalr
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub/mock")
    .withAutomaticReconnect()
    .build();

// Subscribe to a context
connection.on("DataUpdate", (message) => {
    console.log(`${message.context}:`, message.data);
    // message.data contains generated JSON matching the shape
    // message.timestamp is unix timestamp in ms
});

await connection.start();
await connection.invoke("SubscribeToContext", "weather");
```

### Hub Context Configuration

Each hub context simulates a complete API request and generates data continuously:

```json
{
  "Name": "orders",                 // Context name (SignalR group identifier)
  "Description": "Order data..."    // Plain English description (LLM generates JSON from this)
  // Optional:
  // "IsActive": true,              // Start in active/stopped state (default: true)
  // "Shape": "{...}",              // Explicit JSON shape or JSON Schema
  // "IsJsonSchema": false           // Auto-detected if not specified
}
```

**Recommended: Use Plain English Descriptions**

Let the LLM automatically generate appropriate JSON structures:

```json
{
  "Name": "sensors",
  "Description": "IoT sensor data with device ID, temperature, humidity, battery level, and last reading timestamp"
}
```

The LLM automatically generates an appropriate JSON schema from your description - no manual Shape required!

### Dynamic Context Creation API

Create and manage SignalR contexts at runtime using the management API:

#### Create Context

```bash
POST /api/mock/contexts
Content-Type: application/json

{
  "name": "crypto",
  "description": "Cryptocurrency prices with symbol, USD price, 24h change percentage, and market cap"
}
```

Response:
```json
{
  "message": "Context 'crypto' registered successfully",
  "context": {
    "name": "crypto",
    "description": "Cryptocurrency prices...",
    "method": "GET",
    "path": "/crypto",
    "shape": "{...generated JSON schema...}",
    "isJsonSchema": true
  }
}
```

#### List All Contexts

```bash
GET /api/mock/contexts
```

Response:
```json
{
  "contexts": [
    {
      "name": "weather",
      "description": "Realistic weather data with temperature, conditions, humidity, and wind speed for a single location",
      "method": "GET",
      "path": "/weather/current",
      "shape": "{...}"
    },
    {
      "name": "crypto",
      "description": "Cryptocurrency prices...",
      "shape": "{...}"
    }
  ],
  "count": 2
}
```

Note: The list endpoint merges contexts configured in appsettings.json with any dynamically created contexts at runtime. Descriptions from appsettings are included even if those contexts have not yet been dynamically registered.

#### Get Specific Context

```bash
GET /api/mock/contexts/weather
```

Response:
```json
{
  "name": "weather",
  "method": "GET",
  "path": "/weather/current",
  "shape": "{\"temperature\":0,\"condition\":\"string\"}",
  "isJsonSchema": false
}
```

#### Delete Context

```bash
DELETE /api/mock/contexts/crypto
```

Response:
```json
{
  "message": "Context 'crypto' deleted successfully"
}
```

#### Start Context (Resume Data Generation)

```bash
POST /api/mock/contexts/crypto/start
```

Response:
```json
{
  "message": "Context 'crypto' started successfully"
}
```

Starts generating data for a stopped context without affecting connected clients.

#### Stop Context (Pause Data Generation)

```bash
POST /api/mock/contexts/crypto/stop
```

Response:
```json
{
  "message": "Context 'crypto' stopped successfully"
}
```

Stops generating new data but keeps the context registered. Clients remain connected but receive no updates until started again.

### Complete Client Example

```html
<!DOCTYPE html>
<html>
<head>
    <script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.0/dist/browser/signalr.min.js"></script>
</head>
<body>
    <h1>Live Weather Data</h1>
    <div id="weather-data"></div>

    <script>
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hub/mock")
            .withAutomaticReconnect()
            .build();

        connection.on("DataUpdate", (message) => {
            if (message.context === "weather") {
                const weatherDiv = document.getElementById("weather-data");
                weatherDiv.innerHTML = `
                    <h2>Current Weather</h2>
                    <p>Temperature: ${message.data.temperature}°F</p>
                    <p>Condition: ${message.data.condition}</p>
                    <p>Humidity: ${message.data.humidity}%</p>
                    <p>Updated: ${new Date(message.timestamp).toLocaleTimeString()}</p>
                `;
            }
        });

        connection.start()
            .then(() => {
                console.log("Connected to SignalR hub");
                return connection.invoke("SubscribeToContext", "weather");
            })
            .then(() => {
                console.log("Subscribed to weather context");
            })
            .catch(err => console.error(err));
    </script>
</body>
</html>
```

### Dynamic Context Creation from UI

```javascript
async function createDynamicContext() {
    // Create the context
    const response = await fetch("/api/mock/contexts", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            name: "stocks",
            description: "Stock market data with ticker symbol, current price, daily change percentage, and trading volume"
        })
    });

    const result = await response.json();
    console.log("Context created:", result.context);

    // Subscribe to receive data
    await connection.invoke("SubscribeToContext", "stocks");
    console.log("Now receiving live stock data!");
}
```

### SignalR Hub Methods

The `MockLlmHub` supports the following methods:

**SubscribeToContext(string context)**
- Subscribes the client to receive data updates for a specific context
- Client will receive `DataUpdate` events with generated data
- **New in v1.1.0:** Automatically increments the context's connection count

**UnsubscribeFromContext(string context)**
- Unsubscribes the client from a context
- Client will no longer receive updates for that context
- **New in v1.1.0:** Automatically decrements the context's connection count

**Events received by client:**

**DataUpdate** - Contains generated mock data
```javascript
{
    context: "weather",       // Context name
    method: "GET",            // Simulated HTTP method
    path: "/weather/current", // Simulated path
    timestamp: 1699564820000, // Unix timestamp (ms)
    data: {                   // Generated JSON matching the shape
        temperature: 72,
        condition: "Sunny",
        humidity: 45,
        windSpeed: 8
    }
}
```

**Subscribed** - Confirmation of subscription
```javascript
{
    context: "weather",
    message: "Subscribed to weather"
}
```

**Unsubscribed** - Confirmation of unsubscription
```javascript
{
    context: "weather",
    message: "Unsubscribed from weather"
}
```

### Configuration Options

```json
{
  "MockLlmApi": {
    "SignalRPushIntervalMs": 5000,  // Interval between data pushes (ms)
    "HubContexts": [...]             // Array of pre-configured contexts
  }
}
```

### JSON Schema Support

Hub contexts support both simple JSON shapes and full JSON Schema:

**Simple Shape:**
```json
{
  "Name": "users",
  "Shape": "{\"id\":0,\"name\":\"string\",\"email\":\"string\"}"
}
```

**JSON Schema:**
```json
{
  "Name": "products",
  "Shape": "{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"number\"},\"name\":{\"type\":\"string\"},\"price\":{\"type\":\"number\"}},\"required\":[\"id\",\"name\",\"price\"]}",
  "IsJsonSchema": true
}
```

The system auto-detects JSON Schema by looking for `$schema`, `type`, or `properties` fields.

### Architecture

```mermaid
graph TD
    Client[SignalR Client] -->|Subscribe| Hub[MockLlmHub]
    Hub -->|Join Group| Group[SignalR Group]
    BG[Background Service] -->|Generate Data| LLM[Ollama LLM]
    LLM -->|JSON Response| BG
    BG -->|Push Data| Group
    Group -->|DataUpdate Event| Client
    API[Management API] -->|CRUD| Manager[DynamicHubContextManager]
    Manager -->|Register/Unregister| BG
```

**Components:**
- **MockLlmHub**: SignalR hub handling client connections and subscriptions
- **MockDataBackgroundService**: Hosted service continuously generating data
- **DynamicHubContextManager**: Thread-safe manager for runtime context registration
- **HubContextConfig**: Configuration model for each data context

### Use Cases

**1. Dashboard Prototyping**
```javascript
// Subscribe to multiple data sources
await connection.invoke("SubscribeToContext", "sales");
await connection.invoke("SubscribeToContext", "traffic");
await connection.invoke("SubscribeToContext", "alerts");
// Now receiving live updates for all three!
```

**2. IoT Simulation**
```json
{
  "Name": "sensors",
  "Description": "IoT temperature sensors with device ID, current temperature, battery percentage, and signal strength",
  "Path": "/iot/sensors"
}
```

**3. Financial Data**
```json
{
  "Name": "trading",
  "Description": "Real-time stock trades with timestamp, symbol, price, volume, and buyer/seller IDs",
  "Path": "/trading/live"
}
```

**4. Gaming Leaderboard**
```json
{
  "Name": "leaderboard",
  "Description": "Gaming leaderboard with player name, score, rank, level, and country",
  "Path": "/game/leaderboard"
}
```

### Context Lifecycle Management

**New in v1.1.0:** Full lifecycle control over SignalR contexts with real-time status tracking!

Each context has the following properties:
- **IsActive**: Whether the context is generating data (default: true)
- **ConnectionCount**: Number of currently connected clients (auto-tracked)

Contexts can be in two states:
- **Active**: Background service generates new data every `SignalRPushIntervalMs` (default: 5 seconds)
- **Stopped**: Context remains registered, clients stay connected, but no new data is generated

**Key Features:**
- Start/stop contexts without disconnecting clients
- Real-time connection count tracking
- Status badges showing active/stopped state
- Duplicate context prevention
- Contexts from appsettings.json automatically appear in the UI

**Response Caching for SignalR:**
**New in v1.1.0:** Intelligent caching reduces LLM load and improves consistency!

- **Batch Prefilling**: Generates 5-10 responses at once (configurable via `MaxCachePerKey`)
- **Per-Context Queues**: Each context maintains its own cache of pre-generated responses
- **Background Refilling**: Automatically refills cache when it drops below 50% capacity
- **Smart Scheduling**: Minimizes LLM calls while maintaining data freshness

This significantly reduces LLM load for high-frequency contexts, especially with multiple clients.

**Example Workflow:**
```bash
# Create a context
POST /api/mock/contexts
{ "name": "metrics", "description": "Server metrics" }

# Stop data generation (clients remain connected)
POST /api/mock/contexts/metrics/stop

# Resume data generation
POST /api/mock/contexts/metrics/start

# Remove context entirely
DELETE /api/mock/contexts/metrics
```

### Demo Applications

The package includes two complete demo applications with interactive web interfaces featuring full context management:

#### SignalR Demo (`/`) — Real-Time Data Streaming with Management UI

**New in v1.1.0:** Enhanced 3-column layout with full context lifecycle management!

**Features:**
- **Create Context Panel**: Enter plain English descriptions, system generates appropriate JSON schema
- **Active Contexts Panel**:
    - Live list of all contexts with status badges (Active/Stopped)
    - Automatically displays contexts from appsettings.json on startup
    - Real-time connection count for each context
    - Per-context controls: Connect, Disconnect, Start, Stop, Delete
    - Auto-refresh on changes
- **Live Data Panel**: Real-time updates from subscribed contexts (every 5 seconds)
    - **JSON Syntax Highlighting**: Beautiful dark-themed display with color-coded elements
    - Keys (red), strings (green), numbers (orange), booleans (cyan), null (purple)
    - Clean, readable format with proper timestamp display
    - No external dependencies - lightweight built-in highlighter
 
**Quick-Start Examples:** One-click buttons for 5 pre-configured scenarios:
- **IoT Sensors**: Temperature sensors with device ID, readings, battery percentage
- **Stock Market**: Real-time prices with ticker, price, change percentage, volume
- **E-commerce Orders**: Orders with ID, customer, items array, status, total
- **Server Metrics**: Monitoring data with hostname, CPU, memory, disk, connections
- **Gaming Leaderboard**: Player stats with name, score, rank, level, country

**Perfect for:** Dashboards, live monitoring, IoT simulations, real-time feeds, prototyping

#### SSE Streaming Demo (`/Streaming`) — Progressive JSON Generation

**New in v1.1.0:** Quick-start example buttons for instant streaming!

**Features:**
- Configure HTTP method, path, and optional JSON shape
- Watch JSON being generated token-by-token in real-time
- Live statistics: chunk count, duration, data size
- Connection status indicators

**Quick-Start Examples:** One-click buttons for 4 streaming scenarios:
- **User List**: Array of user objects with ID, name, email
- **Product Catalog**: Product inventory with SKU, name, price, stock status
- **Order Details**: Nested orders with customer info and items array
- **Weather Data**: Current conditions with temperature, humidity, wind speed

**Perfect for:** Observing LLM generation, debugging shapes, understanding streaming behavior, testing SSE

**Run the demos:**
```bash
cd LLMApi
dotnet run
```

Navigate to:
- `http://localhost:5116` - SignalR real-time data streaming with management UI
- `http://localhost:5116/Streaming` - SSE progressive generation

Both demos include:
- Comprehensive inline documentation
- Interactive quick-start examples
- Code snippets for integration
- Real-time status indicators
- Full context lifecycle controls

## Advanced Features

### Response Schema Echo (Shape) Export

You can optionally have the middleware echo back the JSON shape/schema that was used to generate the mock response.

Configuration:
- Option: IncludeShapeInResponse (bool, default false)
- Per-request override: add query parameter includeSchema=true (or 1)
- Header emitted: X-Response-Schema (only when a shape was provided and size ≤ 4000 chars)
- Streaming: the final SSE event includes a schema field when enabled

Examples:

- Enable globally (appsettings.json):
```json
{
  "mostlylucid.mockllmapi": {
    "IncludeShapeInResponse": true
  }
}
```

- Enable per request (overrides config):
```bash
curl "http://localhost:5000/api/mock/users?shape=%7B%22id%22%3A0%2C%22name%22%3A%22string%22%7D&includeSchema=true"
```
Response includes header:
```
X-Response-Schema: {"id":0,"name":"string"}
```

- Streaming: final event contains schema field when enabled
```
...
data: {"content":"{full json}","done":true,"schema":{"id":0,"name":"string"}}
```

Notes:
- If no shape was provided (via query/header/body), the header is not added
- Very large shapes (> 4000 chars) are not added to the header to avoid transport issues, but normal response continues

Use cases:
- Client-side TypeScript type generation
- API documentation and schema validation
- Debugging shape parsing
- Runtime validation

### Custom Prompt Templates

Override the default prompts with your own:

```json
{
  "mostlylucid.mockllmapi": {
    "CustomPromptTemplate": "Generate mock data for {method} {path}. Body: {body}. Use seed: {randomSeed}"
  }
}
```

Available placeholders:
- `{method}` - HTTP method (GET, POST, etc.)
- `{path}` - Full request path with query string
- `{body}` - Request body
- `{randomSeed}` - Generated random seed (GUID)
- `{timestamp}` - Unix timestamp
- `{shape}` - Shape specification (if provided)

### Multiple Instances

Mount multiple mock APIs with different configurations:

```csharp
// Development data with high randomness
builder.Services.Addmostlylucid.mockllmapi("Dev", options =>
{
    options.Temperature = 1.5;
    options.ModelName = "llama3";
});

// Stable test data
builder.Services.Addmostlylucid.mockllmapi("Test", options =>
{
    options.Temperature = 0.3;
    options.ModelName = "llama3";
});

app.Mapmostlylucid.mockllmapi("/api/dev");
app.Mapmostlylucid.mockllmapi("/api/test");
```

## Shape Specification

Three ways to control response structure:

1. **Header** (recommended): `X-Response-Shape: {"field":"type"}`
2. **Query param**: `?shape=%7B%22field%22%3A%22type%22%7D` (URL-encoded JSON)
3. **Body field**: `{"shape": {...}, "actualData": ...}`

### Caching Multiple Responses via Shape

You can instruct the middleware to pre-generate and cache multiple response variants for a specific request/shape by adding a special field inside the shape object: "$cache": N.

- The cache key is derived from HTTP method + path (including query) + the sanitized shape (with $cache removed) using System.IO.Hashing XXHash64.
- Up to N responses are prefetched from the LLM and stored in-memory (capped by MaxCachePerKey in options; default 5).
- Subsequent non-streaming requests for the same key are served from a depleting queue; when exhausted, a fresh batch of N is prefetched automatically.
- Streaming endpoints are not cached.

Examples

- Header shape:
  X-Response-Shape: {"$cache":3,"orderId":"string","status":"string","items":[{"sku":"string","qty":0}]}

- Body shape:
  {
  "shape": {
  "$cache": 5,
  "invoiceId": "string",
  "customer": { "id": "string", "name": "string" },
  "items": [ { "sku": "string", "qty": 0, "price": 0.0 } ],
  "total": 0.0
  }
  }

- Query param (URL-encoded):
  ?shape=%7B%22%24cache%22%3A2%2C%22users%22%3A%5B%7B%22id%22%3A0%2C%22name%22%3A%22string%22%7D%5D%7D

Configuration

- MaxCachePerKey (int, default 5): caps the number requested by "$cache" per key.

Notes

- The "$cache" hint is removed from the shape before it is sent to the LLM.
- If "$cache" is omitted or 0, the request behaves as before (no caching/warmup).
- Cached variants are kept in-memory for the app lifetime; restart clears the cache.


## Testing

Use the included `LLMApi.http` file with:
- Visual Studio / Rider HTTP client
- VS Code REST Client extension
- Any HTTP client

## Testing

The project includes comprehensive unit tests:

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed
```

**Test Coverage:**
- ✅ Body reading (empty, JSON content)
- ✅ Shape extraction (query param, header, body field, precedence)
- ✅ Prompt generation (randomness, shape inclusion, streaming modes)
- ✅ Request building (temperature, model, messages)
- ✅ Edge cases (invalid JSON, missing data)

## Architecture

```mermaid
graph LR
    Client[Client] -->|HTTP Request| API[LLMApi<br/>Minimal API]
    API -->|Chat Completion| Ollama[Ollama API<br/>localhost:11434]
    Ollama -->|Inference| Model[llama3 Model]
    Model -->|Response| Ollama
    Ollama -->|JSON/Stream| API
    API -->|JSON/SSE| Client

    API -.->|uses| Helper[AutoApiHelper]

    style API fill:#4CAF50
    style Helper fill:#2196F3
    style Model fill:#FF9800
```

### Request Flow

```mermaid
sequenceDiagram
    participant C as Client
    participant A as LLMApi
    participant H as AutoApiHelper
    participant O as Ollama
    participant M as llama3

    C->>A: GET/POST/PUT/DELETE /api/auto/**
    A->>H: Extract context (method, path, body, shape)
    H->>H: Generate random seed + timestamp
    H->>H: Build prompt with randomness
    H-->>A: Prompt + temperature=1.2
    A->>O: POST /v1/chat/completions
    O->>M: Run inference
    M-->>O: Generated JSON
    O-->>A: Response
    A-->>C: JSON Response
```

### Shape Control Flow

```mermaid
flowchart TD
    Start[Request Arrives] --> CheckQuery{Shape in<br/>Query Param?}
    CheckQuery -->|Yes| UseQuery[Use Query Shape]
    CheckQuery -->|No| CheckHeader{Shape in<br/>Header?}
    CheckHeader -->|Yes| UseHeader[Use Header Shape]
    CheckHeader -->|No| CheckBody{Shape in<br/>Body Field?}
    CheckBody -->|Yes| UseBody[Use Body Shape]
    CheckBody -->|No| NoShape[No Shape Constraint]

    UseQuery --> BuildPrompt[Build Prompt]
    UseHeader --> BuildPrompt
    UseBody --> BuildPrompt
    NoShape --> BuildPrompt

    BuildPrompt --> AddRandom[Add Random Seed<br/>+ Timestamp]
    AddRandom --> SendLLM[Send to LLM]

    style UseQuery fill:#4CAF50
    style UseHeader fill:#4CAF50
    style UseBody fill:#4CAF50
    style NoShape fill:#FFC107
```

**Projects:**
- `mostlylucid.mockllmapi`: NuGet package library
- `LLMApi`: Demo application
- `LLMApi.Tests`: xUnit test suite (13 tests)

## Why Use mostlylucid.mockllmapi?

- **Rapid Prototyping**: Frontend development without waiting for backend
- **Demos**: Show realistic data flows without hardcoded fixtures
- **Testing**: Generate varied test data for edge cases
- **API Design**: Experiment with response shapes before implementing
- **Learning**: Example of LLM integration in .NET minimal APIs
- **Zero Maintenance**: No database, no state, no mock data files to maintain

## Building the NuGet Package

```bash
cd mostlylucid.mockllmapi
dotnet pack -c Release
```

Package will be in `bin/Release/mostlylucid.mockllmapi.{version}.nupkg`

## Contributing

This is a sample project demonstrating LLM-powered mock APIs. Feel free to fork and customize!

## License

This is free and unencumbered software released into the public domain. See [LICENSE](LICENSE) for details or visit [unlicense.org](https://unlicense.org).



  