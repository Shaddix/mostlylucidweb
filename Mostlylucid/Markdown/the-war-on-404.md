# The War on 404

<!--category-- ASP.NET, Middleware, C# -->
<datetime class="hidden">2025-05-23T09:00</datetime>

## The War On 404: Making Old Stuff Work Again

When you've been blogging since 2004 (yes, really), you accumulate a lot of digital detritus. I recently imported my old posts from 2004-2009 and discovered that approximately *everything* was broken. External links pointing to sites that vanished a decade ago, internal links to slugs that no longer exist, the whole lot. Rather than manually fixing thousands of links (life's too short), I built a system to handle it automatically.

This article covers my approach to keeping old content alive:
- **Outgoing links**: `BrokenLinkArchiveMiddleware` - replaces dead external links with archive.org snapshots
- **Incoming links**: `SlugRedirectMiddleware` - learns from user behaviour to redirect mistyped or old URLs
- **The learning system**: How the site gets smarter over time
- **Background processing**: Checking links without blocking requests

[TOC]

## The Problem with Old Content

Here's the thing about the internet: it's not permanent. That excellent blog post you linked to in 2006? Gone. That documentation site? Restructured three times. Your own URL scheme from before you settled on a proper slugging convention? Embarrassing.

```mermaid
graph TD
    A[User requests old post] --> B{Link valid?}
    B -->|Yes| C[Happy user]
    B -->|No| D[404 Error]
    D --> E[Frustrated user]
    E --> F[User leaves]

    style D stroke:#ef4444,stroke-width:3px
    style F stroke:#ef4444,stroke-width:3px
    style C stroke:#10b981,stroke-width:3px
```

The naive approach is to fix links manually. But when you've got hundreds of posts with thousands of links, that's not on. We need automation.

## Architecture Overview

The system has two main components working in tandem:

```mermaid
flowchart TB
    subgraph Incoming["Incoming Requests"]
        A[User Request] --> B[SlugRedirectMiddleware]
        B --> C{Known redirect?}
        C -->|Yes| D[301 Permanent Redirect]
        C -->|No| E[Continue to App]
        E --> F{Page exists?}
        F -->|No| G[404 Handler]
        G --> H[Show suggestions]
        H --> I[User clicks suggestion]
        I --> J[Learn redirect]
    end

    subgraph Outgoing["Outgoing Links"]
        K[Page Renders] --> L[BrokenLinkArchiveMiddleware]
        L --> M[Extract all links]
        M --> N[Register for checking]
        L --> O[Replace broken links]
        O --> P[Archive.org URLs]
        O --> Q[Semantic search results]
        O --> R[Remove dead links]
    end

    style D stroke:#10b981,stroke-width:3px
    style P stroke:#3b82f6,stroke-width:3px
    style Q stroke:#8b5cf6,stroke-width:3px
```

## Part 1: Handling Outgoing Links

### The BrokenLinkArchiveMiddleware

This middleware intercepts HTML responses and does three things:
1. Extracts all links for background checking
2. Replaces known broken external links with archive.org versions
3. Uses semantic search to find replacements for broken internal links

The key insight here is that we want to find archive.org snapshots from *around the time the post was written*. A snapshot from 2024 of a 2006 article might reference completely different content. So we look up the blog post's publish date and ask archive.org for the closest snapshot.

Here's the core structure:

```csharp
public partial class BrokenLinkArchiveMiddleware(
    RequestDelegate next,
    ILogger<BrokenLinkArchiveMiddleware> logger,
    IServiceScopeFactory serviceScopeFactory)
{
    public async Task InvokeAsync(
        HttpContext context,
        IBrokenLinkService? brokenLinkService,
        ISemanticSearchService? semanticSearchService)
    {
        // Only process HTML responses for blog pages
        if (!ShouldProcessRequest(context))
        {
            await next(context);
            return;
        }

        // Capture the response so we can modify it
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await next(context);

        // Process the HTML response
        if (IsSuccessfulHtmlResponse(context, responseBody))
        {
            var html = await ReadResponseAsync(responseBody);
            html = await ProcessLinksAsync(html, context, brokenLinkService, semanticSearchService);
            await WriteModifiedResponseAsync(originalBodyStream, html, context);
        }
        else
        {
            await CopyOriginalResponseAsync(responseBody, originalBodyStream);
        }
    }
}
```

### Extracting Links

We use a generated regex to pull out all `href` attributes. The `[GeneratedRegex]` attribute in .NET gives us compile-time regex generation, which is both faster and allocation-free:

```csharp
[GeneratedRegex(@"<a[^>]*\shref\s*=\s*[""']([^""']+)[""'][^>]*>",
    RegexOptions.IgnoreCase | RegexOptions.Compiled)]
private static partial Regex HrefRegex();

private List<string> ExtractAllLinks(string html, HttpRequest request)
{
    var links = new List<string>();
    var matches = HrefRegex().Matches(html);

    foreach (Match match in matches)
    {
        var href = match.Groups[1].Value;

        // Skip special links (anchors, mailto, etc.)
        if (SkipPatterns.Any(p => href.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            continue;

        if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == "http" || uri.Scheme == "https")
                links.Add(href);
        }
        else if (href.StartsWith("/"))
        {
            // Convert relative URLs to absolute for tracking
            var baseUri = new UriBuilder(request.Scheme, request.Host.Host,
                request.Host.Port ?? (request.Scheme == "https" ? 443 : 80));
            links.Add(new Uri(baseUri.Uri, href).ToString());
        }
    }

    return links.Distinct().ToList();
}
```

### Registering Links for Background Checking

We don't want to block the response while checking links. Instead, we fire off a background task:

```csharp
var allLinks = ExtractAllLinks(html, context.Request);
var sourcePageUrl = context.Request.Path.Value;

if (allLinks.Count > 0)
{
    // Fire and forget - don't block the response
    _ = Task.Run(async () =>
    {
        using var scope = serviceScopeFactory.CreateScope();
        var scopedService = scope.ServiceProvider.GetRequiredService<IBrokenLinkService>();
        await scopedService.RegisterUrlsAsync(allLinks, sourcePageUrl);
    });
}
```

Note the `IServiceScopeFactory` - we need a new scope because the original request's scoped services will be disposed before our background task completes.

### Replacing Broken Links

Once we know a link is broken and have an archive.org replacement, we swap them out with a helpful tooltip:

```csharp
foreach (var (originalUrl, archiveUrl) in archiveMappings)
{
    if (html.Contains(originalUrl))
    {
        var tooltipText = $"Original link ({originalUrl}) is dead - archive.org version used";
        var originalPattern = $"href=\"{originalUrl}\"";
        var archivePattern = $"href=\"{archiveUrl}\" class=\"tooltip tooltip-warning\" " +
                            $"data-tip=\"{tooltipText}\" data-original-url=\"{originalUrl}\"";

        html = html.Replace(originalPattern, archivePattern);
    }
}
```

For internal broken links, we try semantic search first:

```csharp
if (isInternal && semanticSearchService != null)
{
    var replacement = await TryFindSemanticReplacementAsync(
        brokenUrl, semanticSearchService, request, cancellationToken);

    if (replacement != null)
    {
        html = ReplaceHref(html, brokenUrl, replacement);
        continue;
    }
}

// No replacement found - convert to plain text
html = RemoveHref(html, brokenUrl);
```

## Part 2: Background Link Checking

The `BrokenLinkCheckerBackgroundService` runs hourly to check links and fetch archive.org URLs.

```mermaid
sequenceDiagram
    participant BG as Background Service
    participant DB as Database
    participant Web as External Sites
    participant Archive as Archive.org CDX API

    loop Every Hour
        BG->>DB: Get unchecked links (batch of 20)
        loop For each link
            BG->>Web: HEAD request
            Web-->>BG: Status code
            BG->>DB: Update link status
        end

        BG->>DB: Get broken links needing archive
        loop For each broken link
            BG->>DB: Look up source post publish date
            BG->>Archive: CDX API query (filtered by date)
            Archive-->>BG: Closest snapshot
            BG->>DB: Store archive URL
        end
    end
```

### The Archive.org CDX API

Archive.org provides a CDX (Capture Index) API that lets us query for snapshots. The clever bit is filtering by date:

```csharp
private async Task<string?> GetArchiveUrlAsync(
    string originalUrl,
    DateTime? beforeDate,
    CancellationToken cancellationToken)
{
    var queryParams = new List<string>
    {
        $"url={Uri.EscapeDataString(originalUrl)}",
        "output=json",
        "fl=timestamp,original,statuscode",
        "filter=statuscode:200",  // Only successful responses
        "limit=1"
    };

    // Find snapshot closest to the blog post's publish date
    if (beforeDate.HasValue)
    {
        queryParams.Add($"to={beforeDate.Value:yyyyMMdd}");
        queryParams.Add("sort=closest");
        queryParams.Add($"closest={beforeDate.Value:yyyyMMdd}");
    }

    var apiUrl = $"https://web.archive.org/cdx/search/cdx?{string.Join("&", queryParams)}";

    // ... fetch and parse response

    return $"https://web.archive.org/web/{timestamp}/{original}";
}
```

This means if I wrote a post in 2008 linking to some resource, I'll get the archive.org snapshot from around 2008, not a modern version that might be completely different.

### Link Status Tracking

We track links with a proper entity:

```csharp
[Table("broken_links", Schema = "mostlylucid")]
public class BrokenLinkEntity
{
    public int Id { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string? ArchiveUrl { get; set; }
    public bool IsBroken { get; set; } = false;
    public int? LastStatusCode { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public int ConsecutiveFailures { get; set; } = 0;
    public string? SourcePageUrl { get; set; }  // For publish date lookup
}
```

## Part 3: Handling Incoming Requests

Now for the other side of the coin: people requesting URLs that don't exist.

### The Learning System

When someone hits a 404, we show them suggestions using fuzzy string matching and (optionally) semantic search. If they click a suggestion, we record that. After enough clicks with sufficient confidence, we start auto-redirecting.

```mermaid
stateDiagram-v2
    [*] --> RequestReceived
    RequestReceived --> CheckAutoRedirect

    CheckAutoRedirect --> Redirect301: Has learned redirect
    CheckAutoRedirect --> Continue: No auto-redirect

    Continue --> PageFound: Exists
    Continue --> NotFound: 404

    NotFound --> CheckHighConfidence
    CheckHighConfidence --> Redirect302: Score >= 0.85 & gap >= 0.15
    CheckHighConfidence --> ShowSuggestions: Lower confidence

    ShowSuggestions --> UserClicks
    UserClicks --> RecordClick
    RecordClick --> UpdateWeight
    UpdateWeight --> CheckThreshold

    CheckThreshold --> EnableAutoRedirect: Weight >= 5 & confidence >= 70%
    CheckThreshold --> [*]: Below threshold

    EnableAutoRedirect --> [*]
```

### The SlugRedirectMiddleware

This runs early in the pipeline, before the 404 handler:

```csharp
public class SlugRedirectMiddleware(
    RequestDelegate next,
    ILogger<SlugRedirectMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISlugSuggestionService? slugSuggestionService)
    {
        if (context.Request.Path.StartsWithSegments("/blog", StringComparison.OrdinalIgnoreCase))
        {
            if (slugSuggestionService != null)
            {
                var (slug, language) = ExtractSlugAndLanguage(context.Request.Path);

                var targetSlug = await slugSuggestionService.GetAutoRedirectSlugAsync(
                    slug, language, context.RequestAborted);

                if (!string.IsNullOrWhiteSpace(targetSlug))
                {
                    var redirectUrl = language == "en"
                        ? $"/blog/{targetSlug}"
                        : $"/blog/{language}/{targetSlug}";

                    // 301 Permanent Redirect - this is a learned, confident redirect
                    context.Response.Redirect(redirectUrl, permanent: true);
                    return;
                }
            }
        }

        await next(context);
    }
}
```

### Similarity Scoring

The suggestion service uses Levenshtein distance (edit distance) plus some heuristics:

```csharp
private double CalculateSimilarity(string source, string target)
{
    if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        return 1.0;

    source = source.ToLowerInvariant();
    target = target.ToLowerInvariant();

    // Levenshtein distance converted to similarity (0-1)
    var distance = CalculateLevenshteinDistance(source, target);
    var maxLength = Math.Max(source.Length, target.Length);
    var levenshteinSimilarity = 1.0 - (double)distance / maxLength;

    // Bonus if one string contains the other
    var substringBonus = (source.Contains(target) || target.Contains(source)) ? 0.2 : 0.0;

    // Bonus for common prefix (catches typos at the end)
    var prefixLength = GetCommonPrefixLength(source, target);
    var prefixBonus = (double)prefixLength / Math.Min(source.Length, target.Length) * 0.1;

    return Math.Min(1.0, levenshteinSimilarity + substringBonus + prefixBonus);
}
```

### Learning from User Behaviour

When a user clicks a suggestion, we record it and update confidence scores:

```csharp
public async Task RecordSuggestionClickAsync(
    string requestedSlug,
    string clickedSlug,
    string language,
    int suggestionPosition,
    double originalScore,
    CancellationToken cancellationToken = default)
{
    var redirect = await _context.SlugRedirects
        .FirstOrDefaultAsync(r =>
            r.FromSlug == normalizedRequestedSlug &&
            r.ToSlug == normalizedClickedSlug &&
            r.Language == language,
            cancellationToken);

    if (redirect == null)
    {
        redirect = new SlugRedirectEntity
        {
            FromSlug = normalizedRequestedSlug,
            ToSlug = normalizedClickedSlug,
            Language = language,
            Weight = 1
        };
        _context.SlugRedirects.Add(redirect);
    }
    else
    {
        redirect.Weight++;
        redirect.LastClickedAt = DateTimeOffset.UtcNow;
    }

    redirect.UpdateConfidenceScore();
    await _context.SaveChangesAsync(cancellationToken);
}
```

The confidence score calculation considers both clicks and impressions:

```csharp
public void UpdateConfidenceScore()
{
    var total = Weight + ShownCount;
    ConfidenceScore = total > 0 ? (double)Weight / total : 0.0;

    // Enable auto-redirect after 5+ clicks with 70%+ confidence
    if (Weight >= AutoRedirectWeightThreshold &&
        ConfidenceScore >= AutoRedirectConfidenceThreshold)
    {
        AutoRedirect = true;
    }
}
```

### Two-Tier Auto-Redirect

We have two levels of automatic redirects:

1. **First-time high-confidence (302)**: If the similarity score is >= 0.85 AND there's a significant gap to the second-best match, we redirect immediately with a 302. This catches obvious typos.

2. **Learned redirects (301)**: After 5+ clicks with 70%+ confidence, we use a permanent 301 redirect. This is the "the humans have spoken" case.

```csharp
public async Task<string?> GetFirstTimeAutoRedirectSlugAsync(
    string requestedSlug,
    string language,
    CancellationToken cancellationToken = default)
{
    var suggestions = await GetSuggestionsWithScoreAsync(requestedSlug, language, 2, cancellationToken);

    if (suggestions.Count == 0) return null;

    var topMatch = suggestions[0];

    // Need high confidence
    if (topMatch.Score < 0.85) return null;

    // If there's only one suggestion with high score, redirect
    if (suggestions.Count == 1) return topMatch.Post.Slug;

    // Multiple suggestions - only redirect if there's a clear winner
    var scoreGap = topMatch.Score - suggestions[1].Score;
    if (scoreGap >= 0.15)
        return topMatch.Post.Slug;

    return null;  // Too close to call - show suggestions instead
}
```

## Putting It All Together

The middleware registration order matters. Slug redirects need to run before the static file middleware and before MVC routing:

```csharp
// In Program.cs
app.UseSlugRedirect();  // Check for learned redirects early
app.UseStaticFiles();
app.UseRouting();
// ... other middleware
app.UseBrokenLinkArchive();  // Process outgoing links in responses
```

The flow looks like this:

```mermaid
flowchart LR
    subgraph Request["Request Processing"]
        direction TB
        A[Request] --> B[SlugRedirectMiddleware]
        B --> C[Static Files]
        C --> D[Routing]
        D --> E[MVC/Endpoints]
    end

    subgraph Response["Response Processing"]
        direction TB
        E --> F[BrokenLinkArchiveMiddleware]
        F --> G[Link Extraction]
        G --> H[Link Replacement]
        H --> I[Response]
    end

    subgraph Background["Background Processing"]
        direction TB
        J[BrokenLinkCheckerService] --> K[Check URLs]
        K --> L[Fetch Archive.org]
        L --> M[Update Database]
    end

    G -.->|Register links| J

    style B stroke:#3b82f6,stroke-width:3px
    style F stroke:#8b5cf6,stroke-width:3px
    style J stroke:#f59e0b,stroke-width:3px
```

## Conclusion

This system has been running for a while now and it's rather satisfying watching it learn. The database gradually fills up with redirect mappings, broken links get archive.org replacements, and users rarely see a naked 404 anymore.

The key principles:
- **Don't block requests** - use background processing for expensive operations
- **Learn from users** - their clicks are valuable signals
- **Be conservative with auto-redirects** - only redirect when you're confident
- **Time-aware archiving** - find archive.org snapshots from when the content was written
- **Graceful degradation** - if services aren't available, just continue normally

It's not perfect - some links are genuinely gone forever, and semantic search doesn't always find the right replacement. But it's a damn sight better than leaving thousands of broken links lying about.

## Coming Soon: Semantic Search Deep Dive

This week (w/c 24th November 2025) I'll be publishing my semantic search series which goes into much more detail about how the vector-based search works under the hood. That series will cover embedding generation, Qdrant vector storage, and how it all ties into this 404 handling system. If you're curious about how `ISemanticSearchService.SearchAsync` actually finds "similar" content, that's where you'll want to look.

The complete source is in the repository if you want to nick any of it for your own projects.
