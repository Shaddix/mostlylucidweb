# Mostlylucid.MinimalBlog - How Simple Can a Blog Really Be?

<!--category-- ASP.NET, Markdown, Blogging -->
<datetime class="hidden">2025-11-30T12:00</datetime>

## Introduction

If you've been following this blog, you might have noticed that my main blogging platform (the one you're reading right now) is... let's call it "enthusiastically engineered." We've got PostgreSQL databases, full-text search with GIN indexes, automated translation to 12 languages, Hangfire job scheduling, Prometheus metrics, Serilog tracing, HTMX interactions, and enough Docker containers to make a ship jealous.

But what if you just want to write a blog? What if you don't need all that complexity? What if you just want to drop markdown files in a folder and have them appear on the web?

That's exactly why I created **Mostlylucid.MinimalBlog** - a demonstration of just how simple a functional, modern blog platform can be. This is the anti-thesis of my main blog: deliberately minimal, purposefully simple, and refreshingly straightforward.

[TOC]

## The Philosophy: Less is More

The entire Mostlylucid.MinimalBlog project is designed around one principle: **keep it simple**. Here's what that means in practice:

- **No database** - Just markdown files in a folder
- **No build pipeline** - No npm, no webpack, no complicated asset processing
- **No JavaScript framework** - Just plain HTML and CSS
- **Minimal dependencies** - Just ASP.NET 9.0 and Markdig
- **One service** - A single `MarkdownBlogService` that does everything

The entire project consists of:
- 1 `.csproj` file
- 1 service class (~120 lines)
- 1 MetaWeblog API implementation (for Markdown Monster integration)
- 4 Razor pages
- 1 CSS file (~55 lines)
- 1 configuration file

That's it. No complexity. No over-engineering. Just what you need to blog.

## Project Structure

Let's look at how the project is organized:

```
Mostlylucid.MinimalBlog/
├── Pages/
│   ├── Index.cshtml              # Homepage with post list
│   ├── Post.cshtml                # Individual post page
│   ├── Categories.cshtml          # List of all categories
│   ├── Category.cshtml            # Posts in a category
│   ├── _Layout.cshtml             # Shared layout
│   ├── _ViewImports.cshtml        # Shared imports
│   └── _ViewStart.cshtml          # Layout selection
├── wwwroot/
│   └── css/
│       └── site.css               # All the CSS you need
├── MarkdownBlogService.cs         # Core blog logic
├── MetaWeblogService.cs           # XML-RPC for external editors
├── Program.cs                     # Application setup
├── appsettings.json               # Configuration
└── Mostlylucid.MinimalBlog.csproj # Project file
```

## The Heart: MarkdownBlogService

The core of the blog is the `MarkdownBlogService` class. It's remarkably simple - just 120 lines of code that handle:

1. Reading markdown files from a directory
2. Parsing metadata (title, categories, publish date)
3. Converting markdown to HTML using Markdig
4. Caching everything in memory

Here's how it works:

### Loading Posts

The service scans a configured directory for `.md` files and loads them all into memory:

```csharp
private List<BlogPost> LoadAllPosts()
{
    if (!Directory.Exists(_markdownPath)) return [];

    return Directory.GetFiles(_markdownPath, "*.md", SearchOption.TopDirectoryOnly)
        .Where(f => Path.GetFileName(f).Count(c => c == '.') == 1) // Only base .md files
        .Select(ParseFile)
        .Where(p => p is { IsHidden: false })
        .OrderByDescending(p => p!.PublishedDate)
        .ToList()!;
}
```

Notice the clever filtering: `Count(c => c == '.') == 1` ensures we only get base `.md` files, not translated versions like `post.ar.md` or `post.de.md` (in case you want to add translations later).

### Parsing Metadata

Each markdown file follows a simple convention:

```markdown
# Post Title

<!-- category -- Category1, Category2 -->
<datetime class="hidden">2024-11-30T12:00</datetime>

Your content here...
```

The parser extracts this metadata using regular expressions and the Markdig AST:

```csharp
private BlogPost? ParseFile(string filePath)
{
    var markdown = File.ReadAllText(filePath);
    var slug = Path.GetFileNameWithoutExtension(filePath);
    var document = Markdown.Parse(markdown, _pipeline);

    // Extract title from first H1
    var title = document.Descendants<HeadingBlock>()
        .FirstOrDefault(h => h.Level == 1)?
        .Inline?.FirstChild?.ToString() ?? slug;

    // Extract categories: <!-- category -- Cat1, Cat2 -->
    var categoryMatch = CategoryRegex().Match(markdown);
    var categories = categoryMatch.Success
        ? categoryMatch.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries)
        : [];

    // Extract date: <datetime class="hidden">2024-01-01T00:00</datetime>
    var dateMatch = DateTimeRegex().Match(markdown);
    var publishedDate = dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var dt)
        ? dt : File.GetCreationTimeUtc(filePath);

    return new BlogPost
    {
        Slug = slug,
        Title = title,
        Categories = categories,
        PublishedDate = publishedDate,
        HtmlContent = Markdown.ToHtml(markdown, _pipeline),
        IsHidden = markdown.Contains("<hidden")
    };
}
```

### Caching Strategy

Every method in the service uses `IMemoryCache` to avoid re-reading and re-parsing files on every request:

```csharp
public IReadOnlyList<BlogPost> GetAllPosts()
{
    return cache.GetOrCreate("all_posts", entry =>
    {
        entry.SetOptions(CacheOptions);
        return LoadAllPosts();
    }) ?? [];
}
```

Cache entries have a 30-minute sliding expiration and 2-hour absolute expiration. Simple, effective.

## Application Setup: Program.cs

The entire application setup is just 43 lines of code:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(b => b.Expire(TimeSpan.FromMinutes(10)));
    options.AddPolicy("Blog", b => b.Expire(TimeSpan.FromHours(1)).Tag("blog"));
});
builder.Services.AddSingleton<MarkdownBlogService>();
builder.Services.AddSingleton<MetaWeblogService>();

var app = builder.Build();

// Serve images from configured path
var imagesPath = builder.Configuration["ImagesPath"] ?? "wwwroot/images";
var imagesDir = Path.IsPathRooted(imagesPath) ? imagesPath : Path.Combine(app.Environment.ContentRootPath, imagesPath);
Directory.CreateDirectory(imagesDir);

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesDir),
    RequestPath = "/images"
});

app.UseOutputCache();

// MetaWeblog XML-RPC endpoint
app.MapPost("/metaweblog", async (HttpContext ctx, MetaWeblogService svc) =>
{
    ctx.Response.ContentType = "text/xml";
    var response = await svc.HandleRequestAsync(ctx.Request.Body);
    await ctx.Response.WriteAsync(response);
});

app.MapRazorPages();

app.Run();
```

Key features:
- **Output caching** for performance
- **Static file serving** for images (configurable path)
- **MetaWeblog API** endpoint for writing with external editors like Markdown Monster
- **Singleton services** since they cache everything anyway

## The UI: Simple Razor Pages

The UI is pure server-rendered HTML with minimal CSS. Let's look at the homepage:

```cshtml
@page
@using Mostlylucid.MinimalBlog
@inject MarkdownBlogService Blog
@{
    ViewData["Title"] = "Home";
    var posts = Blog.GetAllPosts();
}

<ul class="post-list">
    @foreach (var post in posts)
    {
        <li>
            <time datetime="@post.PublishedDate:yyyy-MM-dd">@post.PublishedDate.ToString("MMM d, yyyy")</time>
            <h2><a href="/post/@post.Slug">@post.Title</a></h2>
            @if (post.Categories.Length > 0)
            {
                <span class="cats">@string.Join(", ", post.Categories)</span>
            }
        </li>
    }
</ul>
```

No JavaScript. No HTMX. No Alpine.js. Just good old HTML rendered on the server. Fast, accessible, and it works without JavaScript enabled.

The individual post page is equally simple:

```cshtml
@page "/post/{slug}"
@using Mostlylucid.MinimalBlog
@using Microsoft.AspNetCore.OutputCaching
@attribute [OutputCache(PolicyName = "Blog")]
@inject MarkdownBlogService Blog
@{
    var slug = RouteData.Values["slug"]?.ToString() ?? "";
    var post = Blog.GetPost(slug);

    if (post == null)
    {
        Response.StatusCode = 404;
        ViewData["Title"] = "Not Found";
    }
    else
    {
        ViewData["Title"] = post.Title;
    }
}

@if (post == null)
{
    <h1>Post Not Found</h1>
    <p><a href="/">Back to home</a></p>
}
else
{
    <article>
        <h1>@post.Title</h1>
        <div class="meta">
            <time datetime="@post.PublishedDate:yyyy-MM-dd">@post.PublishedDate.ToString("MMMM d, yyyy")</time>
            @if (post.Categories.Length > 0)
            {
                <span> &bull; </span>
                @foreach (var cat in post.Categories)
                {
                    <a href="/category/@cat">@cat</a>
                    if (cat != post.Categories.Last())
                    {
                        <span>, </span>
                    }
                }
            }
        </div>
        <div class="content">
            @Html.Raw(post.HtmlContent)
        </div>
    </article>
}
```

Notice the `[OutputCache]` attribute on the page - this caches the entire rendered HTML for an hour, making subsequent requests blazingly fast.

## Styling: 55 Lines of CSS

The entire visual design is handled by a single CSS file with just 55 lines. It uses CSS custom properties for theming and creates a clean, dark GitHub-inspired look:

```css
:root {
  --bg: #0d1117;
  --bg-card: #161b22;
  --text: #c9d1d9;
  --text-muted: #8b949e;
  --accent: #58a6ff;
  --border: #30363d;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
  background: var(--bg);
  color: var(--text);
  line-height: 1.6;
  max-width: 48rem;
  margin: 0 auto;
  padding: 2rem 1rem;
}

/* ... more styles ... */
```

No preprocessor. No build step. No thousands of utility classes. Just clean, readable CSS that works.

## Bonus Feature: MetaWeblog API

For writers who prefer dedicated markdown editors like [Markdown Monster](https://markdownmonster.west-wind.com/), the project includes a full MetaWeblog API implementation. This XML-RPC API allows external editors to:

- List posts
- Create new posts
- Edit existing posts
- Delete posts
- Upload images
- Retrieve categories

The implementation is in `MetaWeblogService.cs` and handles the complete XML-RPC protocol, parsing requests and generating responses. This means you can write your blog posts in your favorite editor and publish them directly to your blog.

## Configuration

The entire configuration file is just 14 lines:

```json
{
  "MarkdownPath": "../Mostlylucid/Markdown",
  "ImagesPath": "wwwroot/images",
  "MetaWeblog": {
    "Username": "admin",
    "Password": "changeme",
    "BlogUrl": "http://localhost:5000"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- `MarkdownPath` - where your markdown files live
- `ImagesPath` - where images are stored
- `MetaWeblog` - credentials for external editor access

## Running the Blog

To run the blog:

```bash
cd Mostlylucid.MinimalBlog
dotnet run
```

That's it. No `npm install`, no database migrations, no Docker containers. Just `dotnet run`.

Visit `http://localhost:5000` and you'll see your blog with all markdown files from the configured path.

## Creating Content

To create a new blog post:

1. Create a new `.md` file in your configured `MarkdownPath`
2. Add the standard metadata:
   ```markdown
   # Your Post Title

   <!-- category -- YourCategory, AnotherCategory -->
   <datetime class="hidden">2024-11-30T12:00</datetime>

   Your content here...
   ```
3. Save the file
4. The cache will expire within 30 minutes (or restart the app)

To add images, simply place them in your configured `ImagesPath` directory and reference them in your markdown:

```markdown
![Alt text](your-image.jpg)
```

## What's Missing (On Purpose)

This minimal blog intentionally doesn't include:

- **Comments** - Use a third-party service if needed
- **Search** - Keep your content organized with categories
- **Tags** - Categories are sufficient for small blogs
- **RSS/Atom** - Simple to add if you need it
- **Authentication** - MetaWeblog API uses basic auth only
- **Analytics** - Add JavaScript snippet if desired
- **SEO optimization** - Works fine with basic meta tags
- **Responsive images** - Browser handles it
- **Dark/light theme toggle** - One theme is enough

These features are all *possible* to add, but they're not included by default because most small blogs don't need them.

## Performance Characteristics

Despite its simplicity, this blog is **fast**:

- **Memory caching** means no file I/O after first load
- **Output caching** means no Razor rendering after first request
- **No database** means no query overhead
- **No JavaScript** means faster page loads
- **Simple CSS** means minimal stylesheet parsing

For a small to medium blog (under 1000 posts), this architecture will outperform most database-backed blog platforms.

## When to Use This vs. the Full Mostlylucid Blog

Use **Mostlylucid.MinimalBlog** when:
- You're starting a personal blog
- You have fewer than 500 posts
- You don't need multiple languages
- You want to keep things simple
- You're comfortable with markdown files
- You don't need comments or other interactive features

Use the **full Mostlylucid platform** when:
- You need multilingual support
- You want automated translation
- You need full-text search
- You want comments and user interaction
- You need analytics and metrics
- You want a complex categorization system
- You're building a content management system

## Conclusion: Simplicity as a Feature

In the modern web development world, we often reach for complex solutions by default. Need a blog? Better set up a database, configure an ORM, set up migrations, add caching, implement search, configure background jobs...

But sometimes the simple solution is the *right* solution. Mostlylucid.MinimalBlog proves that you can build a functional, fast, and maintainable blog platform with:

- **342 lines of C#** (MarkdownBlogService + MetaWeblogService + Program.cs)
- **~120 lines of Razor markup** (4 pages)
- **55 lines of CSS**
- **1 NuGet dependency** (Markdig)

That's **less than 520 lines of code total** for a complete blogging platform.

The project serves as both a functional blog platform and a reminder: before you add complexity, ask yourself if you really need it. Sometimes a folder full of markdown files is all you need.

You can find the complete source code in the [Mostlylucid.MinimalBlog directory](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.MinimalBlog) of the main repository.

Happy blogging! 🚀
