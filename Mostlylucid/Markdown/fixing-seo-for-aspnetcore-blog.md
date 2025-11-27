# Why You Didn't Find This on Google (And How I Fixed It)

<!--category-- ASP.NET Core, SEO -->
<datetime class="hidden">2025-11-26T14:00</datetime>

I've been running this blog for a while now, writing detailed technical articles on ASP.NET Core, Entity Framework, HTMX, and all sorts of .NET goodness. Yet my search rankings were... underwhelming. After finally investigating why, I discovered I'd been sabotaging my own SEO with some rookie mistakes. Here's what was wrong and how I fixed it.

> NOTE: This has yet to be released, the next release is HUGE (too big really!) so need to get my ducks in a row to get it all out there. BUT here's what I did...and what WILL arrive at some point!
[TOC]

# The Problem: Every Page Looked Identical to Google

When Google crawls your site, it looks at several key signals to understand what each page is about. I was sending Google the same signals for every single page.

## The Smoking Gun: Static Meta Descriptions

Here's what my `_Layout.cshtml` looked like:

```html
<meta name="description" content="Scott Galloway is a lead developer and software engineer with a passion for building web applications.">
<meta property="og:description" content="Scott Galloway is a lead developer and software engineer with a passion for building web applications.">
```

Every. Single. Page. The same description. My article about [Background Services in ASP.NET Core](/blog/background-services-in-aspnetcore-part1)? Same description as my homepage. My deep dive into [Entity Framework](/blog/addingaboraboragaboragotoaboraboref)? Same description.

Google sees 200+ pages with identical descriptions and thinks "this site has duplicate content issues" or "this site doesn't care about providing useful information." Either way, rankings suffer.

## Missing Canonical URLs

I have a multilingual blog with translations. The same content exists at:
- `/blog/my-article` (English)
- `/blog/my-article/fr` (French)
- `/blog/my-article/de` (German)

Without canonical URLs, Google might see these as duplicate content, diluting the SEO value across multiple URLs instead of consolidating it on the primary page.

## No Structured Data

Google's search results can show rich snippets - author information, publish dates, article previews. But only if you tell Google about this data using [JSON-LD structured data](https://developers.google.com/search/docs/appearance/structured-data/article). I wasn't.

## Static Social Images

Every page shared the same `og:image`. While this doesn't directly affect Google rankings, it impacts click-through rates when articles are shared on social media - which indirectly affects SEO through engagement signals.

# The Fixes

## 1. Dynamic Meta Descriptions

The first fix was making the layout support dynamic descriptions with a sensible fallback:

```html
@{
    var currentUrl = $"https://{Context.Request.Host}{Context.Request.Path}";
    var defaultDescription = "Scott Galloway is a lead developer and software engineer with a passion for building web applications.";
    var pageDescription = ViewBag.Description as string ?? defaultDescription;
}

<!-- Canonical URL -->
<link rel="canonical" href="@currentUrl" />

<!-- Facebook Meta Tags -->
<meta property="og:url" content="@currentUrl">
<meta property="og:type" content="@(ViewBag.OgType ?? "website")">
<meta property="og:title" content="@ViewBag.Title">
<meta property="og:description" content="@pageDescription">
<meta property="og:site_name" content="mostlylucid" />

<!-- Twitter Meta Tags -->
<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="@ViewBag.Title">
<meta name="twitter:description" content="@pageDescription">

<!-- Meta Description -->
<meta name="description" content="@pageDescription" />

<!-- Article metadata for blog posts -->
@if (ViewBag.PublishedDate != null)
{
    <meta property="article:published_time" content="@(((DateTime)ViewBag.PublishedDate).ToString("yyyy-MM-ddTHH:mm:ssZ"))" />
    <meta property="article:author" content="Scott Galloway" />
}
@if (ViewBag.Categories != null)
{
    foreach (var category in ViewBag.Categories)
    {
        <meta property="article:tag" content="@category" />
    }
}
```

Now the layout reads `ViewBag.Description` if set, falling back to a default. The canonical URL is automatically set to the current page URL.

## 2. Auto-Generated Descriptions for Blog Posts

Each blog post now generates its description from the first 155 characters of content. In my `Post.cshtml`:

```html
@using Mostlylucid.Shared.Helpers
@model Mostlylucid.Models.Blog.BlogPostViewModel

@{
    Layout = "_Layout";
    ViewBag.Title = $"{Model.Title} ({Model.Language.ConvertCodeToLanguage()})";

    // Generate description from plain text content (first 155 chars, truncate at word boundary)
    var plainText = Model.PlainTextContent ?? "";
    var description = plainText.Length > 155
        ? plainText.Substring(0, plainText.LastIndexOf(' ', 155)) + "..."
        : plainText;
    description = description.Replace("\n", " ").Replace("\r", "").Trim();
    ViewBag.Description = description;

    // Set article metadata
    ViewBag.OgType = "article";
    ViewBag.PublishedDate = Model.PublishedDate;
    ViewBag.Categories = Model.Categories;

    // Build canonical URL (without language suffix for English)
    var canonicalUrl = Model.Language == "en"
        ? $"https://{Context.Request.Host}/blog/{Model.Slug}"
        : $"https://{Context.Request.Host}/blog/{Model.Slug}/{Model.Language}";
}
```

The key points here:

1. **Truncate at word boundary** - We find the last space before 155 characters to avoid cutting words in half
2. **Strip newlines** - Meta descriptions should be single-line
3. **Set `og:type` to "article"** - Tells social platforms this is an article, not a generic webpage
4. **Pass article metadata** - Published date and categories flow through to the layout

## 3. JSON-LD Structured Data

This is the big one for rich snippets. Add a JSON-LD script block to your blog post view:

```html
<!-- JSON-LD Structured Data for Blog Post -->
<script type="application/ld+json">
{
    "@@context": "https://schema.org",
    "@@type": "BlogPosting",
    "headline": "@Model.Title",
    "description": "@description",
    "datePublished": "@Model.PublishedDate.ToString("yyyy-MM-ddTHH:mm:ssZ")",
    @if (Model.UpdatedDate.HasValue)
    {
        @:"dateModified": "@Model.UpdatedDate.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")",
    }
    "author": {
        "@@type": "Person",
        "name": "Scott Galloway",
        "url": "https://mostlylucid.net/blog/aboutme"
    },
    "publisher": {
        "@@type": "Organization",
        "name": "Mostly Lucid",
        "logo": {
            "@@type": "ImageObject",
            "url": "https://mostlylucid.net/img/logo.svg"
        }
    },
    "mainEntityOfPage": {
        "@@type": "WebPage",
        "@@id": "@canonicalUrl"
    },
    "wordCount": @Model.WordCount,
    "inLanguage": "@Model.Language",
    "keywords": "@string.Join(", ", Model.Categories)",
    "image": "https://mostlylucid.net/img/social2.jpg"
}
</script>
```

Note the `@@` escaping for the `@` symbol in Razor views - JSON-LD uses `@context` and `@type` which would otherwise be interpreted as Razor syntax.

This structured data tells Google:
- **What type of content this is** (BlogPosting)
- **Who wrote it** (Person with a URL to learn more)
- **When it was published and modified**
- **What topics it covers** (keywords from categories)
- **How long it is** (wordCount)
- **What language it's in**

Google can use this to show rich snippets in search results, including author information, publish dates, and more.

## 4. Unique Descriptions for Key Pages

Don't forget your static pages. Each should have a unique, relevant description:

```html
<!-- Home page -->
@{
    ViewBag.Title = "Mostly Lucid - Scott Galloway's Developer Blog";
    ViewBag.Description = "Technical blog covering ASP.NET Core, C#, Entity Framework, HTMX, Docker, and modern web development. Practical tutorials, NuGet packages, and open source projects.";
}

<!-- Blog index -->
@{
    ViewBag.Title = "Blog Posts";
    ViewBag.Description = "Technical articles on ASP.NET Core, C#, Entity Framework, Docker, and modern web development. Practical tutorials and real-world examples from a lead developer.";
}

<!-- Contact page -->
@{
    ViewBag.Title = "Contact Scott Galloway";
    ViewBag.Description = "Get in touch with Scott Galloway. Questions about ASP.NET Core, C#, web development, or collaboration opportunities? Send me a message.";
}

<!-- Search page -->
@{
    ViewBag.Title = "Search Results";
    ViewBag.Description = "Search through technical articles on ASP.NET Core, C#, Entity Framework, and web development. Find tutorials, guides, and solutions.";
}
```

# Other SEO Essentials

## Sitemap

You need a sitemap. Here's a simple controller that generates one dynamically:

```csharp
public class SiteMapController(
    IBlogViewService blogViewService,
    IHttpContextAccessor httpContextAccessor) : Controller
{
    [HttpGet]
    [ResponseCache(Duration = 43200)] // Cache for 12 hours
    public async Task<IActionResult> Index()
    {
        var pages = await blogViewService.GetPosts();
        var siteUrl = $"https://{httpContextAccessor.HttpContext?.Request.Host}";

        XNamespace sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var feed = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(sitemap + "urlset",
                from page in pages
                select new XElement(sitemap + "url",
                    new XElement(sitemap + "loc", $"{siteUrl}/blog/{page.Slug}"),
                    new XElement(sitemap + "lastmod", page.PublishedDate.ToString("yyyy-MM-dd")),
                    new XElement(sitemap + "changefreq", "weekly"),
                    new XElement(sitemap + "priority", "0.8")
                )
            )
        );

        return Content(feed.ToString(), "text/xml");
    }
}
```

Register the route:

```csharp
app.MapControllerRoute(
    name: "sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "SiteMap", action = "Index" });
```

## robots.txt

Tell search engines where your sitemap is and what to crawl:

```csharp
app.MapGet("/robots.txt", async context =>
{
    var siteUrl = $"https://{context.Request.Host}";
    var robotsTxt = $"""
        User-agent: *
        Allow: /

        Sitemap: {siteUrl}/sitemap.xml
        """;

    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync(robotsTxt);
});
```

## RSS Feed

Many developers use RSS readers. Having an RSS feed also helps with discoverability:

```html
<link rel="alternate" type="application/atom+xml"
      title="RSS Feed for mostlylucid.net"
      href="https://mostlylucid.net/rss" />
```

# What About Images?

You might be wondering about generating unique OG images for each post. For a technical blog, it's probably not worth the effort. Your traffic comes from:

- **Google search** - descriptions matter more than images
- **RSS feeds** - no images
- **Hacker News / Reddit** - thumbnails barely visible
- **Direct links** - developers sharing URLs

A consistent branded image is fine for recognition. Custom image generation matters more for visual content, news sites, or marketing pages competing for clicks on Facebook and Twitter.

If you did want to generate them, you could use [ImageSharp](https://docs.sixlabors.com/articles/imagesharp/) (which I already use for image processing) to overlay text on a template:

```csharp
public async Task<string> GenerateOgImage(string title, string slug)
{
    using var image = await Image.LoadAsync("wwwroot/img/og-template.png");

    var font = SystemFonts.CreateFont("Arial", 48, FontStyle.Bold);

    image.Mutate(x => x.DrawText(
        new RichTextOptions(font)
        {
            Origin = new PointF(50, 200),
            WrappingLength = 1100
        },
        title,
        Color.White));

    var outputPath = $"wwwroot/og/{slug}.png";
    await image.SaveAsPngAsync(outputPath);
    return $"/og/{slug}.png";
}
```

But for a technical blog? Ship what you've got.

# Testing Your SEO

## Google Rich Results Test

Use [Google's Rich Results Test](https://search.google.com/test/rich-results) to validate your structured data. Paste a URL and it will tell you if your JSON-LD is valid and what rich results you're eligible for.

## View Page Source

The simplest test - view the page source and check:
- Is the `<meta name="description">` unique for this page?
- Is there a `<link rel="canonical">`?
- Is there a `<script type="application/ld+json">` block?

## Google Search Console

After deploying, submit your sitemap to [Google Search Console](https://search.google.com/search-console). You can also use the URL Inspection tool to see exactly how Google sees your pages and request re-indexing.

# Summary

The fixes were straightforward:

| Problem | Fix |
|---------|-----|
| Static meta descriptions | Dynamic `ViewBag.Description` with auto-generation from content |
| No canonical URLs | Added `<link rel="canonical">` to layout |
| No structured data | JSON-LD `BlogPosting` schema on each post |
| Missing article metadata | `article:published_time`, `article:author`, `article:tag` meta tags |
| Static page descriptions | Unique descriptions for Home, Blog, Contact, Search pages |

The key insight: **Google can't read your mind**. If every page has the same description, Google has no way to know what makes each page unique and valuable. Tell Google what each page is about, and you'll rank better.

Give it a few weeks for Google to re-crawl your site, and you should see improvements. SEO is a long game, but these fundamentals are the foundation everything else builds on.

# Further Reading

- [Google's SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide)
- [Schema.org BlogPosting](https://schema.org/BlogPosting)
- [Open Graph Protocol](https://ogp.me/)
- [Google Rich Results Test](https://search.google.com/test/rich-results)
- [Google Search Console](https://search.google.com/search-console)
