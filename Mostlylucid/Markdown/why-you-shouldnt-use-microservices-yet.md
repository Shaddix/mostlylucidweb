# Why You Probably Shouldn't Use Microservices (Yet)

<!--category-- Architecture, Microservices, Opinion -->
<datetime class="hidden">2025-12-29T20:00</datetime>

Microservices have become the default "serious system" architecture.

If you want to sound mature, you talk about service meshes, event buses, distributed tracing, and "independent deployability". If you want to look enterprise-ready, you draw boxes until the diagram looks like a bowl of spaghetti someone threw at a whiteboard.

But here's the corrective most juniors aren't being given:

**Microservices are not an application architecture upgrade. They are an organisational scaling strategy.**

If you don't already have the organisational pain they solve, adopting them early doesn't make you future-proof. It makes you present-broken.

This isn't a theoretical argument. It's an engineering trade-off, and like all trade-offs, it only makes sense when you understand the tax you're paying and what you're getting in return.

I've been as guilty as anyone of "doing microservices" without fully internalising the organisational overhead they entail. It's easy to get caught up in the vocabulary and forget that the real challenge is managing complexity across teams and systems.

In the end it comes down to the oldest pattern in software engineering: **KISS** - keep it simple, stupid.

[TOC]

## The Microservices Myth

Microservices are sold as a default architecture because the narrative is seductive:

* Small services are "cleaner"
* Distributed systems are "modern"
* Autonomy is "free"
* You can "scale later" because you started "right"

This framing is backwards.

Microservices aren't primarily about code structure. They're about **who can change what, without talking to whom, and how often**.

If you don't have:

* Multiple teams shipping independently
* Conflicting priorities
* Coordination bottlenecks
* Deployment contention
* Real ownership boundaries

…then you're not solving an organisational problem. You're buying one.

Architecture is ultimately about enabling people, not moving boxes around.

## The Diagram That Should Be a Warning Label

You've seen this diagram.

<p>
![A cautionary microservices architecture diagram](bad_microservices.jpg?format=webp&height=600)
</p>

This is not an "example architecture to copy". It's a warning label.

Courses often teach microservices as diagrams because diagrams are easier than teaching operational responsibility.

The important reframing is this: that diagram is not a target state. It's a **cost surface**.

Every box is a commitment:

* A runtime to operate
* A deployment to manage
* A contract to version
* A failure mode you now own
* An on-call story you can't ignore

The arrows look impressive. They also hide reality.

Because each arrow is:

* Latency
* Partial failure
* Retries
* Timeouts
* Trace context propagation
* "Works in staging" lies

If an architecture requires this on day one, you are not building a product. You are building a platform.

## The Hidden Cost Model of Microservices

Like all architectural decisions, microservices come with a **complexity tax** (see [Playing with Code: complexity tax](/blog/playing-with-code-efficient-agile). The difference is that this tax compounds across every service boundary, every deployment, and every failure mode.

The costs are rarely explicit. They're usually disguised as "best practice".

### Operational Overhead

Each service wants:

* Its own build pipeline
* Its own deployment configuration
* Its own monitoring and alerting
* Its own logging, dashboards, runbooks
* Its own security posture (secrets, auth, ingress)

If a team can't do this comfortably, it doesn't have microservices. It has **a distributed monolith with extra steps**.

### Latency and Failure Multiplication

In a monolith, a call is a function call.

In microservices, a "call" is a negotiated truce between:

* Networks
* Load balancers
* TLS
* Auth middleware
* Serialization
* Timeouts
* Retries
* Backpressure

Failure modes multiply:

* One downstream is slow → upstream threads pile up
* Retries amplify load → you DDoS yourself politely
* A single dependency flaps → three services degrade "randomly"

You don't debug bugs anymore. You debug **system states**.

### The Serialization Tax Nobody Talks About

Here's a cost that's almost never discussed in microservices advocacy: **serialization and deserialization (SerDe) overhead**.

Every service boundary means:

```csharp
// Monolith: direct object reference
var user = _userService.GetUser(userId);  // < 1μs
var tier = user.Tier;                      // Memory access

// Microservices: serialize → network → deserialize
var json = JsonSerializer.Serialize(user);        // ~50μs for a complex object
var bytes = Encoding.UTF8.GetBytes(json);         // ~10μs
// ... send over network ...
var responseJson = await response.Content.ReadAsStringAsync();  // ~20μs
var user = JsonSerializer.Deserialize<User>(responseJson);      // ~70μs
var tier = user.Tier;                                           // Finally
```

**Per call, that's ~150μs of pure CPU overhead before the network even gets involved.**

Now multiply that across a call chain:

* Order service deserializes request (150μs)
* Order service serializes User service request (150μs)
* User service deserializes request (150μs)
* User service serializes response (150μs)
* Order service deserializes User response (150μs)
* Order service serializes Inventory request (150μs)
* …and so on

**In a 5-service chain, you're spending ~1.5ms just on SerDe** - and that's optimistic JSON serialization. If you're using XML, Protocol Buffers with reflection, or inefficient serializers, multiply that by 2-10x.

Yes, you can reduce this with techniques like [JSON source generation in .NET](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation):

```csharp
[JsonSerializable(typeof(User))]
internal partial class UserJsonContext : JsonSerializerContext { }

// ~30-40% faster than reflection-based serialization
var json = JsonSerializer.Serialize(user, UserJsonContext.Default.User);
```

But you're still doing serialization. You've just made the tax slightly cheaper. In a monolith, the tax is zero.

#### A Real Example: The SerDe Disaster

I once profiled an address search system with this architecture (all in Kubernetes containers):

```
ASP.NET API → Go Service (fan-out) → ASP.NET Search Service → Elasticsearch
```

The Go service handled fan-out to multiple data sources and aggregated results. Each request meant multiple serialization hops across container boundaries.

**The breakdown for a typical address search:**

* ASP.NET API: Serialize search request → JSON (80μs)
* Network: ASP.NET → Go (varies by cluster load)
* Go Service: Deserialize request (40μs)
* Go Service: Fan out - serialize requests to multiple backends (60μs × N backends)
* Network: Go → ASP.NET Search (varies)
* Search Service: Deserialize request (90μs)
* Search Service: Build Elasticsearch query, serialize (120μs)
* Network: Search → Elasticsearch (varies)
* Elasticsearch: Deserialize query, execute, serialize results (actual search ~5ms, SerDe ~200μs)
* Network: Elasticsearch → Search (varies)
* Search Service: Deserialize ES response (300μs), map to domain model, serialize (180μs)
* Network: Search → Go (varies)
* Go Service: Deserialize responses from all backends (150μs × N), aggregate, serialize (80μs)
* Network: Go → ASP.NET (varies)
* ASP.NET API: Deserialize final response (120μs)

**For a 3-backend fan-out:**
* Total SerDe overhead: ~2ms before Elasticsearch even started
* Network overhead between containers: ~2-3ms
* Actual search + business logic: ~6ms

**We were spending nearly as much time on SerDe as we were on actual searching.**

The cost wasn't the Go service itself - fan-out made sense for the use case. The cost was the **service boundaries**. Every container border meant serialize → network → deserialize.

In a monolith doing the same fan-out logic:
* Same Elasticsearch queries
* Same aggregation logic  
* Zero inter-service SerDe (just method calls)
* ~40% less total latency

This cost:

* Scales with object complexity (nested objects, collections, polymorphism)
* Compounds with call depth (service chains)
* Burns CPU on every request (can't cache it away)
* Increases GC pressure (allocation churn from string/byte arrays)
* **Adds up fast when you're doing hundreds of requests per second**

In a monolith, this cost is zero. The object is already in memory.

### The Performance Myth: Throughput vs. Scalability

Here's a common sales pitch: "Microservices improve performance."

**This is backwards.**

Microservices are **slower** for the same compute resources. Let's be precise about what we mean:

**Throughput** (requests per second per CPU core):
- **Monolith**: Higher - zero SerDe, zero network hops, direct method calls
- **Microservices**: Lower - SerDe overhead, network latency, container orchestration

**Scalability** (ability to handle more total requests by adding resources):
- **Monolith**: Limited by vertical scaling (bigger machines)
- **Microservices**: Horizontal - add more instances of bottleneck services

Microservices let you **scale independently**. If your inventory service needs 10x resources but your user service doesn't, you can scale them separately. That's valuable **when you need it**.

But it's not a performance optimization. It's a **scaling strategy**. And it comes with a tax.

#### Modern Monoliths Can Scale Too

The argument "microservices scale better" made more sense in 2010 when servers had 4-8 cores.

Modern servers have 32-128 cores. A single machine can run thousands of concurrent operations efficiently.

If your monolith is built with modern concurrent execution patterns, it can handle massive throughput on a single deployment. For example, using patterns like [ephemeral execution coordinators](/blog/ephemeral-execution-library), you can:

```csharp
// Process thousands of concurrent operations in a monolith
services.AddEphemeralWorkCoordinator<TranslationRequest>(
    async (request, ct) => await TranslateAsync(request, ct),
    new EphemeralOptions { MaxConcurrency = Environment.ProcessorCount * 4 });

// Bounded, observable, efficient concurrent processing
// No network overhead, no SerDe, no container orchestration
// Can handle 10k+ req/sec on a single machine
```

This gives you:
- **Parallelism**: Work runs concurrently across all cores
- **Observability**: Track pending/active/failed operations
- **Backpressure**: Bounded queues prevent memory exhaustion
- **Zero overhead**: No serialization, no network, no distributed tracing

When you **do** need to scale beyond one machine, you can:
1. Run multiple instances behind a load balancer (stateless horizontal scaling)
2. Use an outbox pattern for async work distribution
3. Partition by key (customer ID, region, etc.) across instances

You're still running a monolith. You've just deployed it multiple times.

**The point**: Don't split into microservices for "performance". Split when **independent scaling of specific components** justifies the operational overhead.

### Cognitive Load

Microservices don't reduce complexity. They redistribute it.

The "simple service" still needs context:

* Who calls it?
* What does "correct" mean?
* What happens when it's down?
* What's the rollback plan?
* What's the dependency graph?

People underestimate this because diagrams hide it.

### Versioning and Contract Drift

Every boundary becomes a contract.  
Every contract becomes:

* Versioning rules
* Compatibility guarantees
* Schema evolution constraints
* Coordination overhead you pretended you didn't have

You can dodge this for a while with "just deploy everything together".

That's the punchline: if you deploy everything together, you built a monolith. Just a worse one.

### Tooling Before Value

The early spend is always the same:

* Service discovery
* Secrets management
* Distributed tracing
* Centralized logging
* Metrics and alerting
* CI/CD templates
* Local dev story that isn't painful
* Dependency management
* A way to run everything without crying

None of this ships product value.

And most teams do it before they've proven the product deserves the complexity.

This is the central mistake: **you pay the ceremony tax before you've earned the value**. Like [daily standups that waste time without delivering alignment](/blog/agile-standups-ceremony-tax), microservices can become ritual architecture: impressive to look at, expensive to maintain, and disconnected from the actual problem you're solving.

Taken together, these costs don't disappear - they compound. And they compound whether or not the organisation needed microservices in the first place.

## What People Forget: Humans and Teams

Architecture exists to help groups of humans change software safely.

Not to impress other engineers.  
Not to satisfy a diagram.  
Not to justify a platform team you don't have.

Team size and deployment cadence matter more than the pattern catalogue.

* If one team owns the whole roadmap, a monolith is usually faster and safer.
* If you still understand the system end-to-end, microservices will reduce that clarity.
* If you don't have stable ownership boundaries, microservices will force coordination through APIs - which is the slowest coordination mechanism available.

**Conway's Law isn't optional. It's physics.**

Your architecture will mirror your communication structure whether you like it or not.

Microservices don't create autonomy. They require it.

## In Defence of the Modular Monolith

Most systems should start as a monolith. But not a sloppy one.

A **modular monolith** is:

* One deployment unit
* One runtime
* One place to debug
* One consistent view of state
* But with real internal boundaries

It means:

* Domain modules with explicit dependencies
* Clear ownership in code
* No "everything references everything"
* Contracts enforced inside the codebase

The point isn't to stay monolithic forever. The point is to **earn boundaries before you make them remote**.

A modular monolith forces you to learn the skills microservices pretend to solve:

* Domain modelling (real boundaries, not folder boundaries)
* Separation of concerns (not "we made a service")
* Testability
* Change discipline
* Knowing what your system actually does

**If you can't build a clean modular monolith, microservices won't save you. They'll just distribute the mess.**

### Good Monolith Design: Patterns That Scale Later

The right monolith design lets you **defer the microservices decision** without locking yourself in.

#### The Outbox Pattern: Async Without Distribution

One of the best examples is the **outbox pattern** - a way to achieve eventual consistency and async processing *inside* a monolith, with a clean path to distribution later.

Instead of:

```csharp
// Tightly coupled synchronous code
public async Task PlaceOrder(Order order)
{
    await _orderRepo.Save(order);
    await _emailService.SendConfirmation(order);  // Blocks on external service
    await _inventoryService.Reserve(order.Items); // Blocks on another service
}
```

You write:

```csharp
// Outbox pattern: write events to a table
public async Task PlaceOrder(Order order)
{
    await using var transaction = await _db.Database.BeginTransactionAsync();
    
    // Save order
    await _orderRepo.Save(order);
    
    // Write events to outbox table (same transaction)
    _db.OutboxMessages.Add(new OutboxMessage
    {
        EventType = "OrderPlaced",
        Payload = JsonSerializer.Serialize(order),
        CreatedAt = DateTime.UtcNow
    });
    
    await _db.SaveChangesAsync();
    await transaction.CommitAsync();
    
    // Background worker picks up outbox events and publishes them
}
```

**What this gives you:**

* **Transactional consistency**: Events and data commit together or not at all
* **Decoupling**: Email/inventory logic runs async, doesn't block order creation
* **Reliability**: Events persist even if downstream systems are down
* **Clean extraction path**: Later, swap the outbox worker for Kafka/RabbitMQ without changing order logic

The [SegmentCommerce sample project](/blog/zero-pii-customer-intelligence-part2) demonstrates this pattern in production:

```csharp
// Mostlylucid.SegmentCommerce/Services/Queue/PostgresOutbox.cs
public class PostgresOutbox
{
    public async Task PublishAsync<T>(string eventType, T payload)
    {
        var message = new OutboxMessage
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        };
        
        _db.OutboxMessages.Add(message);
        // Caller commits transaction
    }
}

// Background service
public class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pending = await _db.OutboxMessages
                .Where(m => !m.Processed)
                .OrderBy(m => m.CreatedAt)
                .Take(100)
                .ToListAsync();
                
            foreach (var message in pending)
            {
                await ProcessMessage(message);
                message.Processed = true;
            }
            
            await _db.SaveChangesAsync();
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

This runs in a single app today. When you need to scale:

1. Replace the background worker with a message bus consumer
2. Publish outbox events to Kafka/RabbitMQ instead of processing locally
3. **Order placement code doesn't change**

You've built microservices-ready infrastructure without paying the distribution tax.

#### Other Patterns Worth Doing Early

* **CQRS (light)**: Separate read/write models even if they share a DB
* **Event sourcing (selective)**: For audit-critical entities only
* **Feature flags**: Decouple deploy from release
* **Background jobs**: Don't block requests, process async

None of these require distribution. All of them make distribution easier later.

## When Microservices Actually Make Sense

Microservices make sense when the constraints are real, not aspirational.

The critical question isn't "should we use microservices?" It's: **do the benefits outweigh the operational tax?**

Like choosing between [small local models and frontier LLMs](/blog/small-models-not-budget-option), this is about understanding where complexity belongs and what failure modes you can afford.

### Good Triggers

* **Team contention is measurable**: Teams block each other weekly
* **Independent deploys are necessary**: Release cadence differs per domain
* **Scale is uneven**: One subsystem needs 10x resources and isolation
* **Failure isolation matters**: One component must not take down the rest
* **Regulatory/data isolation is mandatory**: Boundaries aren't negotiable
* **Org structure is already multi-team**: Ownership exists and is stable

### Bad Triggers

* "We might scale"
* "Netflix does it"
* "Best practice"
* "It'll look good"
* "Our monolith is messy, so we'll fix it by splitting it"

**If your reason contains the words *might*, *eventually*, or *future-proof*, it's probably not a reason.**

## The Technical Reality: What Microservices Actually Cost

Let's get concrete. Here's what changes when you split a monolith into services.

### Call Chains and Latency

**Monolith:**

```csharp
public async Task<Order> PlaceOrder(OrderRequest request)
{
    var user = await _userService.GetUser(request.UserId);
    var inventory = await _inventoryService.CheckStock(request.Items);
    var price = _pricingService.Calculate(request.Items, user.Tier);
    
    var order = new Order { /* ... */ };
    await _orderRepository.Save(order);
    await _emailService.SendConfirmation(order);
    
    return order;
}
```

Total latency: ~50ms (in-process calls + 2 DB queries)

**Microservices:**

```csharp
public async Task<Order> PlaceOrder(OrderRequest request)
{
    // HTTP call to User Service (network + TLS + serialization)
    var user = await _httpClient.GetAsync<User>($"http://user-service/users/{request.UserId}");
    
    // HTTP call to Inventory Service
    var inventory = await _httpClient.PostAsync<StockResult>(
        "http://inventory-service/check", request.Items);
    
    // HTTP call to Pricing Service
    var price = await _httpClient.PostAsync<PriceResult>(
        "http://pricing-service/calculate", 
        new { items = request.Items, tier = user.Tier });
    
    // HTTP call to Order Service
    var order = await _httpClient.PostAsync<Order>(
        "http://order-service/orders", request);
    
    // Event published to message bus
    await _messageBus.Publish(new OrderPlaced { OrderId = order.Id });
    
    return order;
}
```

Total latency: ~400ms (even optimistic 50–80ms per HTTP call in real environments + message bus publish ~20ms)

**What you gained:**

* Each service can deploy independently
* Inventory and Pricing can scale separately from Orders
* Failure in Email doesn't block order creation (async event)

**What you paid:**

* ~8x latency increase
* 4 new failure points (any service can be down/slow)
* Network, DNS, TLS overhead on every call
* **Serialization/deserialization overhead** (see earlier section)
* Need for retry logic, circuit breakers, timeouts
* Distributed tracing to debug failures

### Error Handling Explosion

**Monolith error handling:**

```csharp
try
{
    var order = await PlaceOrder(request);
    return Ok(order);
}
catch (InsufficientStockException)
{
    return BadRequest(new { error = "Out of stock" });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Order placement failed");
    return StatusCode(500);
}
```

**Microservices error handling:**

```csharp
try
{
    // Call User Service
    var user = await _httpClient.GetAsync<User>($"http://user-service/users/{request.UserId}");
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    return NotFound(new { error = "User not found" });
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
{
    // Retry with exponential backoff?
    // Circuit breaker opened?
    // Fail fast or degrade gracefully?
    _logger.LogWarning("User service unavailable, retrying...");
    await Task.Delay(TimeSpan.FromMilliseconds(100));
    // ... retry logic ...
}
catch (TaskCanceledException ex)
{
    // Timeout - was the request processed? Do we retry?
    _logger.LogError("User service timeout");
    return StatusCode(503, new { error = "Service temporarily unavailable" });
}

try
{
    var inventory = await _httpClient.PostAsync<StockResult>(
        "http://inventory-service/check", request.Items);
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
{
    // Business error from remote service
    var errorDetails = await ex.Content.ReadAsAsync<ErrorResponse>();
    return BadRequest(new { error = errorDetails.Message });
}
catch (HttpRequestException ex)
{
    // Which service failed? Network issue? Service down?
    // Do we have a fallback? Cached data? Fail fast?
    _logger.LogError(ex, "Inventory service failed");
    // Maybe try a backup instance?
    // ... more retry logic ...
}

// And repeat for every service call...
```

Every network call introduces:

* Timeout scenarios
* Network failures
* Service unavailability
* Partial failures (request sent, response not received)
* Retry amplification (your retry might trigger their retry)
* Distributed transaction nightmares

### Deployment Coordination

**Monolith deployment:**

```bash
# Build
dotnet publish -c Release

# Run migrations
dotnet ef database update

# Deploy
docker push myapp:v1.2.3
kubectl set image deployment/myapp myapp=myapp:v1.2.3

# Rollback if needed
kubectl rollout undo deployment/myapp
```

**Microservices deployment:**

You're changing the order structure. Now `Order` includes `UserTier` directly (denormalized for performance).

```bash
# 1. Deploy Pricing Service v2 (understands both old and new Order format)
kubectl set image deployment/pricing-service pricing-service=pricing:v2.0.0

# 2. Wait for rollout and monitor
kubectl rollout status deployment/pricing-service
# Check metrics - is v2 working with old order format?

# 3. Deploy Order Service v2 (starts sending new format)
kubectl set image deployment/order-service order-service=order:v2.0.0

# 4. Monitor for errors
# If Pricing v2 has a bug, you can't just rollback Order service
# You have to rollback both in reverse order

# 5. Deploy User Service v2 (stops including tier in response)
# But wait - old Order Service instances might still be running!
# Need zero-downtime rollout or maintain backward compat

# 6. Eventually clean up old code paths in Pricing Service v3
# (6 months later because you're scared to remove backward compat)
```

With microservices, every change crosses multiple services. Contract evolution requires:

* Backward compatibility windows
* Feature flags across services
* Coordinated rollouts
* Extended testing matrices (Service A v1 + Service B v2, Service A v2 + Service B v1, etc.)

None of this is impossible. But it requires discipline, tooling, and experience that most teams only earn after years of pain.

### The Debugging Experience

**Monolith bug report:** "Order placement fails for premium users"

```csharp
// Set breakpoint in PlaceOrder
// Step through each call
// User tier is null - ah, there's the bug
// Fix, test, deploy
```

**Microservices bug report:** "Order placement fails for premium users"

```bash
# 1. Check logs - which service failed?
kubectl logs -l app=order-service --tail=100

# 2. Oh, it's calling Pricing service. Check those logs
kubectl logs -l app=pricing-service --tail=100

# 3. Grep for correlation ID across all services
stern -l app=order-service,app=pricing-service,app=user-service \
  | grep "correlation-id-xyz"

# 4. Reconstruct the call chain from distributed traces
# Open Jaeger/Zipkin, find the trace
# User service returned 200 OK
# Pricing service returned 400 Bad Request
# Error: "user.tier is required"

# 5. Check User service - did it send tier?
# Look at schema version - ah, User v1.2 stopped sending tier
# When did that deploy? Was Pricing service updated?

# 6. Check API contracts
# Pricing expects tier, User stopped sending it
# Who approved this change?

# 7. Fix requires coordinating two teams
# Can't just deploy a fix - need contract negotiation
```

This is the reality: **bugs that were 5-minute fixes become multi-team investigations**.

If this sounds extreme, good. Microservices are extreme engineering.

## How to Evolve Safely: Monolith → Microservices

Microservices are a one-way door. Treat them like one.

The safe path looks boring:

### 1. Prove Boundaries First

If you can't draw the boundary inside the monolith (with enforceable dependencies), you can't extract it safely.

Get the seams right locally first.

### 2. Extract Reads Before Writes

Reads are easier:

* Fewer invariants
* Fewer consistency requirements
* Fewer rollback nightmares

Move read models out first if you need separation.

### 3. Prefer Async Before Sync

Synchronous service calls create dependency chains.

Async messaging creates:

* Buffering
* Resilience
* Decoupling you can actually survive

Start by separating events and facts, not by slicing endpoints.

### 4. Strangle, Don't Rewrite

No big bang.  
No "we'll rewrite it properly".

Slice off a boundary, measure it, own it, and keep going.

**If you can't explain why a service exists independently, it shouldn't exist at all.**

## The Takeaway: Microservices Only Pay If You Understand the Trade-Offs

Microservices are not a starting point. They are an outcome.

**Complexity should be earned.** Distributed systems are not a badge - they are a debt instrument.

The trade-off equation is simple:

```
Microservices Value = (Team Autonomy + Independent Scaling + Failure Isolation)
                     - (Latency Tax + SerDe Tax + Operational Overhead + Coordination Cost)
```

For most teams - especially early-stage or single-team products - the right side dominates. You pay enormous costs for benefits you don't need yet.

But when you have:

* 5+ teams stepping on each other's toes
* Deployment queues measured in days
* Subsystems with wildly different scaling needs
* Regulatory requirements for data isolation

…then the left side starts to win. The tax becomes justified.

### A Decision Framework

Ask these questions in order:

1. **Can one team still own this end-to-end?**  
   → Yes: stay monolith. No: consider splitting.

2. **Are teams blocked by each other's release cycles?**  
   → Yes: microservices might help. No: coordination is working.

3. **Do we have the operational muscle to run distributed systems?**  
   → No: build it first. Yes: proceed carefully.

4. **Can we trace, debug, and deploy multiple services without heroics?**  
   → No: you're not ready. Yes: you might be.

5. **Have we proven the boundaries in code first?**  
   → No: fix your monolith structure. Yes: extraction is safer.

If you can't get past question 3 with confidence, you're not ready for microservices - and that's fine. **Boring architecture is a competitive advantage.**

Most successful products were built as monoliths first: GitHub, Shopify, Stack Overflow, Basecamp. They evolved into distributed systems only when the organisational pain demanded it.

Your job isn't to build the most impressive architecture. It's to deliver value while minimising accidental complexity.

And no customer has ever paid extra because your system used Kafka.

Sometimes that means microservices. Usually, it means a well-structured monolith and the discipline to keep it that way.
