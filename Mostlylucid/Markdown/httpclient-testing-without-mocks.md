# Unit Testing HttpClient WITHOUT Mocks

<datetime class="hidden">2025-01-27T07:00</datetime>
<!--category-- xUnit, Unit Testing, HttpClient -->

## Introduction

When testing code that uses `HttpClient`, the traditional approach involves mocking `HttpMessageHandler` using frameworks like Moq. While this works, it can be verbose, ceremony-heavy, and frankly a bit ugly. There's a cleaner alternative: using `DelegatingHandler` to create test handlers that behave like real HTTP endpoints.

In this post I'll show you why you might skip the mocks entirely and use `DelegatingHandler` for more readable, maintainable, and compact test code.

[TOC]

## The Problem with Mocking HttpMessageHandler

Here's what typical `HttpMessageHandler` mocking looks like with Moq:

```csharp
var mockHandler = new Mock<HttpMessageHandler>();
mockHandler.Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(x => x.RequestUri.ToString().Contains("api/send")),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync((HttpRequestMessage request, CancellationToken cancellationToken) =>
    {
        var requestBody = request.Content?.ReadAsStringAsync(cancellationToken).Result;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(requestBody ?? "No content", Encoding.UTF8, "application/json")
        };
    });

var client = new HttpClient(mockHandler.Object);
```

This has several issues:

1. **Verbose** - Lots of boilerplate for what should be simple behaviour
2. **Protected method ceremony** - You need `Protected()` and `ItExpr` because `SendAsync` is protected
3. **Hard to read** - The actual test logic is buried in setup ceremony
4. **Not reusable** - Each test needs similar setup code
5. **Brittle** - Easy to get the string-based method name wrong

## The DelegatingHandler Alternative

`DelegatingHandler` is a built-in .NET class designed for exactly this purpose - intercepting HTTP requests before they hit the network. It's what middleware like retry handlers, logging handlers, and authentication handlers use in production.

Here's the same functionality using `DelegatingHandler`:

```csharp
public class EchoHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : "No content";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }
}
```

Using it:

```csharp
var client = new HttpClient(new EchoHandler());
```

That's it. No mocking frameworks, no protected method gymnastics, no string-based method names.

## A Real-World Example: Translation Service Handler

Here's a more sophisticated example from a translation service test handler:

```csharp
public class TranslateDelegatingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var absPath = request.RequestUri?.AbsolutePath;
        var method = request.Method;

        return absPath switch
        {
            "/translate" when method == HttpMethod.Post => await HandleTranslate(request),
            "/translate" => new HttpResponseMessage(HttpStatusCode.OK),
            "/health" => new HttpResponseMessage(HttpStatusCode.OK),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
    }

    private static async Task<HttpResponseMessage> HandleTranslate(HttpRequestMessage request)
    {
        var content = await request.Content!.ReadFromJsonAsync<TranslateRequest>();

        // Simulate error for specific test case
        if (content?.TargetLanguage == "xx")
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var response = new TranslateResponse("es", new[] { "Texto traducido" });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };
    }
}
```

This handler:
- Routes different paths to different behaviours
- Deserializes request content to make decisions
- Returns appropriate error codes for specific scenarios
- Is completely readable and self-documenting

## Setting Up with Dependency Injection

When using `IHttpClientFactory` (which you should be), integrating test handlers is straightforward:

```csharp
public static IServiceCollection SetupTestServices(DelegatingHandler handler)
{
    var services = new ServiceCollection();

    services.AddHttpClient<ITranslationService, TranslationService>(client =>
    {
        client.BaseAddress = new Uri("https://test.local");
    })
    .ConfigurePrimaryHttpMessageHandler(() => handler);

    return services;
}
```

Then in your tests:

```csharp
[Fact]
public async Task Translate_ReturnsTranslatedText()
{
    var services = SetupTestServices(new TranslateDelegatingHandler());
    var provider = services.BuildServiceProvider();
    var service = provider.GetRequiredService<ITranslationService>();

    var result = await service.TranslateAsync("Hello", "es");

    Assert.Equal("Texto traducido", result);
}

[Fact]
public async Task Translate_InvalidLanguage_ThrowsException()
{
    var services = SetupTestServices(new TranslateDelegatingHandler());
    var provider = services.BuildServiceProvider();
    var service = provider.GetRequiredService<ITranslationService>();

    await Assert.ThrowsAsync<HttpRequestException>(
        () => service.TranslateAsync("Hello", "xx"));
}
```

## Advanced Pattern: Configurable Handlers

For more flexibility, you can create handlers that accept configuration:

```csharp
public class ConfigurableHandler : DelegatingHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _routes;

    public ConfigurableHandler()
    {
        _routes = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>();
    }

    public ConfigurableHandler WithRoute(string path, HttpStatusCode status)
    {
        _routes[path] = _ => Task.FromResult(new HttpResponseMessage(status));
        return this;
    }

    public ConfigurableHandler WithRoute(string path, object responseBody)
    {
        _routes[path] = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseBody)
        });
        return this;
    }

    public ConfigurableHandler WithRoute(
        string path,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _routes[path] = handler;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        if (_routes.TryGetValue(path, out var handler))
            return await handler(request);

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
```

Usage:

```csharp
var handler = new ConfigurableHandler()
    .WithRoute("/api/users", new[] { new User("Alice"), new User("Bob") })
    .WithRoute("/api/health", HttpStatusCode.OK)
    .WithRoute("/api/error", HttpStatusCode.InternalServerError);

var client = new HttpClient(handler);
```

## Why Choose DelegatingHandler Over Mocks?

| Aspect | Moq-based Mocking | DelegatingHandler |
|--------|-------------------|-------------------|
| **Lines of code** | Many | Few |
| **Readability** | Low (ceremony heavy) | High (just C#) |
| **Reusability** | Poor | Excellent |
| **Debugging** | Harder (mock magic) | Easy (step through) |
| **Refactoring** | Brittle | Robust |
| **Learning curve** | Steeper (Moq APIs) | Minimal |
| **Dependencies** | Requires Moq | None (built-in) |

## When Mocking Still Makes Sense

To be fair, there are scenarios where Moq-style mocking might still be appropriate:

1. **One-off simple responses** - If you need a single-response handler once, inline Moq might be quicker
2. **Verification** - Moq's `Verify()` is useful for asserting calls were made
3. **Existing codebase** - If your team already has extensive Moq infrastructure

For verification, you can add it to DelegatingHandler too:

```csharp
public class VerifyingHandler : DelegatingHandler
{
    public List<HttpRequestMessage> ReceivedRequests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ReceivedRequests.Add(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
```

## Conclusion

Using `DelegatingHandler` for HttpClient testing gives you:

- **Compact code** - No mocking framework ceremony
- **Readable tests** - Just regular C# classes
- **Reusable handlers** - Share across test classes
- **Easy debugging** - Set breakpoints, step through code
- **Zero dependencies** - It's built into .NET

Next time you reach for `Mock<HttpMessageHandler>`, consider whether a simple `DelegatingHandler` would serve you better. Your future self (and your teammates) will thank you for the cleaner, more maintainable test code.

See the test projects in this solution for real-world examples of this pattern in action.
