# Mostlylucid.Markdig.FetchExtension

A Markdig extension that enables fetching remote Markdown at render time using a simple inline directive:

```markdown
<fetch markdownurl="https://example.com/README.md" pollfrequency="12h"/>
```

Features:

- Custom inline parser and renderer for a `<fetch>` tag
- DI-friendly: resolves an `IMarkdownFetchService` from the configured `IServiceProvider`
- Caching/refresh cadence is controlled by the `pollfrequency` attribute (in hours)

## Getting started

1) Register your own implementation of `IMarkdownFetchService` in the app's DI container.

```csharp
public class MyMarkdownFetchService : IMarkdownFetchService
{
    public async Task<MarkdownFetchResult> FetchMarkdownAsync(string url, int pollFrequencyHours, int blogPostId)
    {
        // Fetch your remote content here
        var content = await new HttpClient().GetStringAsync(url);
        return new MarkdownFetchResult { Success = true, Content = content };
    }
}
```

```csharp
builder.Services.AddScoped<IMarkdownFetchService, MyMarkdownFetchService>();
```

2) Configure the extension with your application's `IServiceProvider` at startup (after building the host):

```csharp
FetchMarkdownExtension.ConfigureServiceProvider(app.Services);
```

3) Add the extension to your Markdig pipeline:

```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Use<FetchMarkdownExtension>()
    .Build();
```

Now any `<fetch markdownurl="..." pollfrequency="12h"/>` directive in your Markdown will be resolved to the fetched
content rendered as HTML.

## Notes

- The interface includes a `blogPostId` argument to support callers that track fetches against an entity. If you don't
  have one, pass `0` or ignore it in your implementation.
- If the service isn't configured or fetching fails, the renderer emits an HTML comment with the error so your page
  still renders.

## License

MIT