# Mostlylucid.MinimalBlog

A minimal, file-based markdown blog for ASP.NET Core 9.0. Just point to a folder of markdown files and go. No database, no build pipeline, no complexity.

## Features

- **Simple** - About 500 lines of code total
- **File-based** - Just markdown files in a folder
- **Fast** - Memory and output caching built-in
- **Clean UI** - GitHub-inspired dark theme
- **MetaWeblog API** - Optional support for Markdown Monster and other editors
- **Zero config** - Works out of the box with sensible defaults

## Quick Start

### 1. Install the package

```bash
dotnet add package Mostlylucid.MinimalBlog
```

### 2. Add to your Program.cs

```csharp
using Mostlylucid.MinimalBlog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMinimalBlog(options =>
{
    options.MarkdownPath = "Markdown"; // Where your .md files are
});

var app = builder.Build();

app.UseStaticFiles();
app.UseMinimalBlog();
app.MapRazorPages();

app.Run();
```

### 3. Create your first post

Create a file `Markdown/hello-world.md`:

```markdown
# Hello World

<!-- category -- Getting Started -->
<datetime class="hidden">2024-11-30T12:00</datetime>

Welcome to my new blog!
```

### 4. Run it

```bash
dotnet run
```

Visit `http://localhost:5000` and you're done!

## Configuration Options

```csharp
builder.Services.AddMinimalBlog(options =>
{
    // Path to markdown files (default: "Markdown")
    options.MarkdownPath = "Posts";

    // Path to images (default: "wwwroot/images")
    options.ImagesPath = "wwwroot/blog-images";

    // Enable MetaWeblog API for external editors (default: true)
    options.EnableMetaWeblog = true;

    // MetaWeblog API credentials (only if enabled)
    options.MetaWeblogUsername = "admin";
    options.MetaWeblogPassword = "your-secure-password";
    options.BlogUrl = "https://yourblog.com";
});
```

## Markdown Format

Each markdown file should follow this convention:

```markdown
# Post Title

<!-- category -- Category1, Category2 -->
<datetime class="hidden">2024-11-30T12:00</datetime>

Your content here...
```

- **Title**: First `# Heading` in the file
- **Categories**: HTML comment `<!-- category -- Cat1, Cat2 -->`
- **Date**: `<datetime class="hidden">YYYY-MM-DDTHH:mm</datetime>`

## Using with Markdown Monster

The optional MetaWeblog API lets you write and publish posts using [Markdown Monster](https://markdownmonster.west-wind.com/):

1. Enable MetaWeblog in your configuration
2. In Markdown Monster: Tools → Weblog Publishing → Configure Weblog
3. Select "MetaWeblog API"
4. Enter your blog URL + `/metaweblog` as the endpoint

## Performance

- **Memory caching**: Markdown files parsed once, cached for 30 minutes
- **Output caching**: Rendered HTML cached for 1 hour
- **No database**: No query overhead
- **No JavaScript**: Faster page loads

For small to medium blogs (under 1000 posts), this will outperform most database-backed platforms.

## What's Included

- 4 Razor Pages (Index, Post, Categories, Category)
- 55 lines of CSS (dark GitHub-inspired theme)
- MarkdownBlogService (~120 lines)
- MetaWeblogService (~220 lines, optional)
- Extension methods for easy setup

## What's Not Included

- Comments (use a third-party service)
- Search (categories are sufficient for small blogs)
- RSS/Atom feeds (easy to add if needed)
- Multiple themes
- Admin UI

## License

This is free and unencumbered software released into the public domain (Unlicense).

## Source Code

Part of the [mostlylucidweb](https://github.com/scottgal/mostlylucidweb) repository.

Read the [full article](https://mostlylucid.net/blog/minimalblog-introduction) about why this exists.
