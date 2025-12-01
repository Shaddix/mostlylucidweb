# mostlylucid.MinimalBlog - How Simple Can an ASP.NET Blog Really Be?

<!--category-- ASP.NET, Markdown, Blogging -->
<datetime class="hidden">2025-12-01T12:00</datetime>

## Introduction

If you've been following this blog, you might have noticed that my main blogging platform is... let's call it "enthusiastically engineered." PostgreSQL AND vector databases, semantic AND full-text search with GIN indexes, automated translation to 14 languages, multiple hosted services, Hangfire job scheduling, Prometheus metrics, Serilog tracing, HTMX interactions, usign my own nuget packages, and enough Docker containers to make a ship jealous.

**That's entirely deliberate.** This site is my living lab - a playground where I experiment with technologies, test deployment strategies, measure performance characteristics, and build reusable packages. It's *supposed* to be over-engineered because that's how I learn: by solving problems that most blogs don't actually have, then packaging those solutions as open-source libraries others can use.

But here's the thing: **you probably don't need any of that to run a blog.**

That's why I created **mostlylucid.MinimalBlog** - to show what happens when you strip away all the experimentation and focus on the absolute essentials. No database. No build pipeline. No complexity. Just markdown files in a folder, appearing on the web. This is what a blog looks like when you're not using it as a laboratory.

> NOTE: See the end of the article for a link to the source, I plan on releasing this as a [nuget package](https://www.nuget.org/packages?q=mostlylucid&includeComputedFrameworks=true&prerel=true&sortby=relevance) as soon as I get time to ensure it's 100% reliable and it's perf isn't TOO awful (so look for k6 testing articles soon!). 

[TOC]

## The Philosophy: Less is More

The entire project is designed around one principle: **keep it simple**. No database, no build pipeline, no JavaScript framework. Just ASP.NET 9.0, Markdig for markdown parsing, and about 500 lines of code total. That's it.
NOTE: You COULD even do this client side by using the likes of [markdown-it](https://github.com/markdown-it/markdown-it) then just have the server site map static `.md` files and make it even SIMPLER but...well this is an ASP.NET blog (kinda sorta 🤓).

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

The core of the blog is the `MarkdownBlogService` class. It's remarkably simple-just 120 lines of code that handle:

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

The entire application setup is just 43 lines: Razor Pages, memory cache, output cache, two singleton services, static file serving, and a MetaWeblog XML-RPC endpoint. Everything cached as singletons because nothing changes unless files are modified.

## The UI: Simple Razor Pages

The UI is pure server-rendered HTML. No JavaScript, no HTMX, no Alpine.js. The homepage lists posts, the post page renders `@Html.Raw(post.HtmlContent)` with an `[OutputCache]` attribute for hour-long HTML caching. Four pages total, each under 30 lines.

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

## Using as a NuGet Package

> As mentioned above it will SOON be available but not yet :)

The blog is now available as a NuGet package, making it trivial to add to any ASP.NET Core application:

```bash
dotnet add package mostlylucid.MinimalBlog
```

Then in your `Program.cs`:

```csharp
builder.Services.AddRazorPages();
builder.Services.AddMinimalBlog(options =>
{
    options.MarkdownPath = "Markdown";
    options.ImagesPath = "wwwroot/images";
    options.EnableMetaWeblog = false; // Optional, defaults to true
});

var app = builder.Build();

app.UseStaticFiles();
app.UseMinimalBlog();
app.MapRazorPages();
app.Run();
```

That's it - just two method calls (`AddMinimalBlog` and `UseMinimalBlog`) and you have a working blog.

## Running the Sample Project

To run the included sample project:

```bash
cd Mostlylucid.MinimalBlog
dotnet run
```

Visit `http://localhost:5000` and you'll see the blog with markdown files from the configured path.

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
- You just want to write and publish

Use the **full Mostlylucid platform** when:
- You're using your blog as a **learning laboratory** for new technologies
- You want to experiment with deployment strategies, monitoring, and performance optimization
- You need specific features like multilingual support, full-text search, or comments
- You're building packages and need a real-world testbed
- You're documenting complex technical implementations
- The journey of building the platform is as valuable as the content it hosts

## Conclusion: Simplicity as a Feature

In the modern web development world, we often reach for complex solutions by default. Need a blog? Better set up a database, configure an ORM, set up migrations, add caching, implement search, configure background jobs...

But sometimes the simple solution is the *right* solution. Mostlylucid.MinimalBlog proves that you can build a functional, fast, and maintainable blog platform with:

- **342 lines of C#** (MarkdownBlogService + MetaWeblogService + Program.cs)
- **~120 lines of Razor markup** (4 pages)
- **55 lines of CSS**
- **1 NuGet dependency** (Markdig)

That's **less than 520 lines of code total** for a complete blogging platform.

The project serves as both a functional blog platform and a reminder: before you add complexity, ask yourself if you really need it. Sometimes a folder full of markdown files is all you need.

You can find the complete source code in the [Mostlylucid.MinimalBlog directory](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.MinimalBlog) of the main repository. I'll release the nuget package as soon as I'm happy with the code. 

Happy blogging! 
