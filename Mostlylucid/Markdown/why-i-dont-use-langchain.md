# Why I Don't Use LangChain (and What I Do Instead)

<!--category-- AI, Architecture, LLM, Agents, Systems Design, C# -->
<datetime class="hidden">2025-12-18T10:00</datetime>

I'm a .NET developer. When I started building LLM-powered systems, everyone pointed me toward LangChain. "It's the standard," they said. "All the examples use it." And they were right — if you're in the Python ecosystem, LangChain is everywhere.

But here's the thing: I don't avoid LangChain because it's bad. I avoid it because it solves problems I already solve more explicitly, and for my use cases — C#, local inference, privacy, determinism — frameworks add friction rather than value.

This isn't an anti-LangChain post. It's a post about understanding what problems frameworks solve, and realizing you might not need them.

**Thesis: If you understand the problems LangChain solves, you don't need LangChain.**

[TOC]

## What LangChain Actually Does Well

Let's be fair first. LangChain excels at several things:

**Rapid prototyping** - You can have a working demo in minutes. The getting-started examples are genuinely good.

**Python ecosystem integration** - If you're already in the Python/Jupyter/pandas world, LangChain glues everything together seamlessly.

**Lowering the barrier** - For people new to LLMs, it provides useful abstractions: prompt templates, tool calling patterns, memory management, vector DB integrations.

LangChain is an **integration accelerator**, not an AI requirement. It speeds up the path from "I have an idea" to "I have a demo." That's valuable.

But it's also where the problems start for me as a C# developer building production systems.

## The Problems LangChain Solves

Before dismissing a framework, you need to understand what problems it's solving. LangChain addresses these real issues:

1. **Context construction** - Building coherent prompts from schema, samples, history, and constraints
2. **Tool orchestration** - Managing multiple tool calls in sequence with conditional logic
3. **State management** - Maintaining conversation context across multiple turns
4. **Retry and error handling** - Recovering gracefully when the LLM generates invalid output
5. **Multi-step reasoning** - Breaking complex tasks into sequential steps (the "agent" pattern)
6. **Observability** - Tracking what actually happened during execution

These are legitimate problems. The question is: do you need a framework to solve them?

## Where LangChain Starts to Hurt

For my work — building production .NET systems with local LLMs, strict privacy requirements, and deterministic behavior — LangChain introduces friction in several areas.

### Hidden State and Implicit Control Flow

LangChain manages memory and context for you. That sounds convenient until you need to debug why your prompt is 10,000 tokens longer than expected, or why the LLM suddenly has access to conversation history you thought you'd cleared.

The framework concatenates prompts, manages memory, and handles execution order implicitly. When something breaks, you're debugging the framework's behavior, not your code's behavior.

### Framework-Coupled Thinking

Once you adopt LangChain, you start designing **for LangChain**. Your architecture becomes coupled to the framework's abstractions: chains, agents, retrievers, memory buffers.

This isn't unique to LangChain — all frameworks do this. But in a fast-moving field like LLMs, where the right abstractions aren't settled yet, coupling to a framework's worldview is risky.

### The Python Impedance Mismatch

LangChain assumes:
- Long-lived processes (notebook-style workflows)
- Mutable global state
- Python's dynamic typing and duck typing
- Blocking I/O patterns

As a .NET developer, I assume:
- Request-scoped lifetimes (ASP.NET Core patterns)
- Immutable or explicitly-managed state
- Strong typing and compile-time safety
- Async/await everywhere

The [LangChain .NET ports](https://github.com/tryAGI/LangChain) exist, but they're playing catch-up with the Python version, and the abstractions still feel foreign to idiomatic C#.

### Production Reality Gaps

When you move from prototype to production, you need:

- **Determinism** - Same input should produce predictable behavior
- **Validation** - Ensure the LLM's output is safe before executing it
- **Sandboxing** - Limit what generated code can actually do
- **Cost control** - Track token usage and impose limits
- **Local inference** - Run models offline without cloud dependencies

LangChain optimizes for iteration speed, not production hardening. That's fine for demos; it's a problem for production.

## What I Build Instead

Here's the mental model I use: **LLMs are reasoning engines, not execution engines.**

### LLMs Do:
- **Interpretation** - Understanding user intent from natural language
- **Planning** - Breaking complex tasks into steps
- **Translation** - Converting intent into structured formats (SQL, JSON, function calls)

### LLMs Do NOT:
- **Compute aggregates** - Summing 100,000 rows
- **Scan datasets** - Searching through large files
- **Own state** - Maintaining long-term memory

The principle: **LLMs reason. Engines compute.**

This separation drives everything I build.

### Explicit Context, Not Magic Memory

Instead of framework-managed memory, I build context explicitly per request:

```csharp
public class QueryContext
{
    public List<ColumnInfo> Schema { get; set; }
    public List<Dictionary<string, string>> SampleRows { get; set; }
    public List<ConversationTurn> History { get; set; }
    public string UserQuestion { get; set; }
}
```

Every prompt construction is visible. I know exactly what's being sent to the LLM because I built the string myself:

```csharp
private string BuildPrompt(QueryContext context)
{
    var sb = new StringBuilder();
    sb.AppendLine("You are a SQL expert. Generate a query based on:");
    sb.AppendLine();
    
    // Schema
    sb.AppendLine("Schema:");
    foreach (var col in context.Schema)
        sb.AppendLine($"  - {col.Name}: {col.Type}");
    
    // History (if any)
    if (context.History.Any())
    {
        sb.AppendLine("\nPrevious conversation:");
        foreach (var turn in context.History.TakeLast(3))
            sb.AppendLine($"  Q: {turn.Question} → SQL: {turn.Sql}");
    }
    
    // Current question
    sb.AppendLine($"\nQuestion: {context.UserQuestion}");
    sb.AppendLine("Generate SQL (no explanation, just the query):");
    
    return sb.ToString();
}
```

No hidden state. No magic concatenation. Just explicit string building. When it's wrong, I know why.

### Deterministic Execution Layers

Instead of letting the LLM execute anything, I use it to generate **intent**, then execute that intent through deterministic engines:

- **SQL engines** (DuckDB) - For data queries
- **Search engines** (Lucene, Postgres full-text) - For document retrieval
- **Rule engines** - For business logic
- **Domain services** - For validated operations

The LLM generates SQL. DuckDB executes it. The LLM never sees the data:

```csharp
// LLM generates intent
var sql = await GenerateSqlAsync(context);

// Validate before execution
var error = ValidateSql(connection, sql);
if (error != null)
{
    // Retry with error feedback
    sql = await GenerateSqlAsync(context, previousError: error);
}

// Execute in sandboxed engine
var results = ExecuteQuery(connection, sql);
```

This is safer, faster, and debuggable. The LLM can't accidentally run `DROP TABLE` because I validate the SQL first. The LLM can't leak data because it never sees the data — only the schema.

## A Concrete Example: CSV Analysis Without Frameworks

I recently wrote about [analyzing large CSV files with local LLMs](https://mostlylucid.net/blog/analysing-large-csv-files-with-local-llms). The architecture:

**User Question → LLM → SQL → DuckDB → Results**

The LLM receives:
- The CSV schema (column names and types)
- 3 sample rows (to understand data format)
- The user's question

The LLM generates:
- A DuckDB SQL query

The system then:
- Validates the SQL using `EXPLAIN` (catches syntax errors without executing)
- Executes the query against the CSV file
- Returns results to the user

The LLM never sees the actual data. It only sees structure.

This is what LangChain would call an "agent" — a system that uses an LLM to generate actions, validates them, executes them, and potentially retries on failure.

Except I built it in ~200 lines of C# with no framework:

```csharp
public class CsvQueryService
{
    private readonly OllamaApiClient _ollama;
    private readonly string _model;
    
    public async Task<QueryResult> QueryAsync(string csvPath, string question)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        
        // 1. Build context
        var context = BuildContext(connection, csvPath, question);
        
        // 2. Generate SQL
        var sql = await GenerateSqlAsync(context);
        
        // 3. Validate
        var error = ValidateSql(connection, sql);
        if (error != null)
        {
            // Retry once with error feedback
            sql = await GenerateSqlAsync(context, error);
        }
        
        // 4. Execute
        return ExecuteQuery(connection, sql);
    }
}
```

That's it. No chains, no agents framework, no magic. Just explicit orchestration of LLM → validation → execution.

## What Is an Agent, Really?

The term "agent" gets thrown around constantly, usually to mean "anything involving an LLM." Let's be precise.

An agent is:
- **A loop** - It runs multiple iterations
- **With state** - It remembers what it's tried
- **With tools** - It can take actions in the world
- **With feedback** - It observes results and adjusts

An agent is **not a library**. It's a pattern.

My agent pattern in C#:

```csharp
public class Agent
{
    private readonly List<ConversationTurn> _history = new();
    
    public async Task<string> RunAsync(string goal)
    {
        while (!IsGoalAchieved(goal))
        {
            // 1. Generate next action based on history
            var action = await GenerateActionAsync(goal, _history);
            
            // 2. Validate before executing
            if (!IsActionSafe(action))
            {
                _history.Add(new ConversationTurn 
                { 
                    Action = action, 
                    Result = "REJECTED: Unsafe action" 
                });
                continue;
            }
            
            // 3. Execute through deterministic tool
            var result = await ExecuteActionAsync(action);
            
            // 4. Record and continue
            _history.Add(new ConversationTurn { Action = action, Result = result });
        }
        
        return GenerateSummary(_history);
    }
}
```

This is an agent. It's a loop with state, tools, and feedback. I wrote it in 30 lines. I didn't need a framework.

## Where Microsoft's Agent Framework Fits

To be fair to the .NET ecosystem, Microsoft has released the [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview) that's purpose-built for .NET developers building production AI systems.

### What the Microsoft Agent Framework Gets Right

The framework (formerly known as Microsoft.Extensions.AI) provides:

- **Explicit orchestration** - You control the agent loop, not the framework
- **Strong typing** - Compile-time safety for tool definitions and function calling
- **First-class observability** - Built-in telemetry, logging, and distributed tracing via OpenTelemetry
- **Enterprise boundaries** - Designed for production .NET systems with proper DI, configuration, and lifecycle management
- **Multi-model support** - Abstractions over OpenAI, Azure OpenAI, Ollama, and other providers
- **Semantic Kernel integration** - Works with Microsoft's broader AI stack

Key components:
- `IChatClient` - Unified interface for chat completions
- `IEmbeddingGenerator` - Vector embeddings across providers
- `AIFunction` - Type-safe function calling
- Middleware pipeline - For logging, retry, caching, telemetry

**Example:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddChatClient(builder => 
    builder.UseOllama("llama3.2")
           .UseOpenTelemetry()
           .UseLogging());

var app = builder.Build();

app.MapPost("/chat", async (IChatClient client, string message) =>
{
    var response = await client.CompleteAsync(message);
    return response.Content;
});
```

### Where I Still Stay Lower-Level

Even with Microsoft's framework, I prefer to keep core orchestration explicit:

**I don't want:**
- **Opaque planners** - The framework autonomously deciding which tool to call
- **Implicit tool selection** - Magic routing based on natural language descriptions
- **Hidden retry logic** - Framework-managed error recovery I can't inspect

**I want:**
- **Visible loops** - I see every iteration in my code
- **Testable steps** - I can unit test the decision logic
- **Replaceable components** - I can swap the LLM, the tools, the validation layer
- **Explicit state** - I know exactly what's in context

Microsoft's Agent Framework is closer to how I think than LangChain. It respects .NET patterns, uses dependency injection properly, and doesn't fight the ecosystem. But I still prefer writing the orchestration myself.

**When to use the Microsoft Agent Framework:**

- Building chat applications with function calling
- Need multi-model support (switch between OpenAI, Azure, Ollama)
- Want enterprise features (telemetry, logging, distributed tracing)
- Working in a team that prefers framework consistency
- Building on top of Semantic Kernel

**When to go framework-less:**

- You need full control over the agent loop
- You're building custom reasoning patterns
- You want zero abstraction overhead
- You're optimizing for specific use cases (like CSV analysis or web scraping)
- You want to understand exactly how it works

The framework doesn't eliminate architectural decisions. You still choose what to put in context, how to chunk data, and when to retry. It just makes the plumbing easier.

## Why This Scales Better Long-Term

Framework-less systems age better for several reasons:

**Performance** - No abstraction overhead. My CSV query service runs sub-100ms because there's no framework between the LLM and DuckDB.

**Cost predictability** - I control exactly what goes to the LLM. No hidden prompt inflation from framework-managed memory.

**Debuggability** - When something breaks, I'm debugging my code, not reverse-engineering a framework's magic.

**Privacy** - For systems with strict data residency requirements, knowing exactly what leaves the machine matters.

**Offline scenarios** - Edge devices, air-gapped networks, regulated environments. Frameworks assume internet access and cloud services.

**Regulatory compliance** - In finance, healthcare, and government, you often need to explain and audit every decision. "The framework did it" isn't an acceptable answer.

The more constrained your environment, the more you want explicit control.

## When I Would Use LangChain

To disarm critics: there are legitimate cases where I'd reach for LangChain.

**Hackathons** - Speed to demo matters more than architecture.

**Throwaway POCs** - If you're validating an idea and plan to rewrite for production anyway.

**Python-heavy teams** - If your team is already fluent in Python, the ecosystem fit is strong.

**Teaching concepts** - LangChain's abstractions can help beginners understand the agent pattern before building their own.

Knowing when **not** to use something is as valuable as knowing when to use it.

## The Broader Pattern: Frameworks vs. First Principles

This isn't really about LangChain. It's about the tradeoff between frameworks and first-principles engineering.

Frameworks accelerate familiar problems. If you're building the 100th CRUD API, reach for Entity Framework or Dapper. The patterns are settled.

But LLM-powered systems? The right abstractions aren't settled yet. We don't know if "chains" or "agents" or "retrievers" are the right mental models. We're still figuring it out.

In that environment, I prefer to build close to the metal:
- LLMs via direct API calls (`OllamaSharp`, OpenAI SDK)
- Prompt construction via explicit string building
- Validation via domain-specific logic
- Execution via purpose-built engines (SQL, search, etc.)

As a .NET developer, I have strong opinions about how systems should be built: explicit lifetimes, strong typing, async all the way down, dependency injection for testability.

LangChain's abstractions don't map cleanly to those opinions. So I don't use it.

## The Takeaway

If you're a .NET developer looking at LangChain and wondering "Do I need this?", here's my answer:

**You need to solve the problems LangChain solves** - context management, tool orchestration, retry logic, observability.

**You don't need LangChain to solve them** - Especially if you value explicitness, strong typing, and production hardening over rapid prototyping.

The principle I build on:

**"LLMs reason. Engines compute. Orchestration is yours to own."**

Or more simply:

**"If you understand the problems a framework solves, you often don't need the framework."**

Build systems that make sense in your ecosystem, with your constraints, using your language's idioms. For me, that's C#, strong typing, explicit control flow, and deterministic execution layers.

For you, it might be different. And that's fine.

The goal isn't to avoid frameworks. The goal is to choose them consciously, understanding both what they provide and what they cost.

---

**Further Reading:**
- [Analyzing Large CSV Files with Local LLMs in C#](/blog/analysing-large-csv-files-with-local-llms) - A concrete example of LLM + SQL without frameworks
- [Fetching and Analysing Web Content with LLMs](/blog/fetching-and-analysing-web-content-with-llms) - Web scraping and analysis without frameworks
- [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview) - Official Microsoft agent framework for .NET
- [Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/) - Foundational AI abstractions for .NET
- [Semantic Kernel](https://github.com/microsoft/semantic-kernel) - Microsoft's LLM orchestration SDK
- [LangChain Documentation](https://python.langchain.com/) - To understand what you're choosing not to use
- [OllamaSharp](https://github.com/awaescher/OllamaSharp) - C# client for local LLM inference