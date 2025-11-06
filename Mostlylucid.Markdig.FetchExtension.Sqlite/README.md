# Mostlylucid.Markdig.FetchExtension.Sqlite

SQLite storage plugin for Mostlylucid.Markdig.FetchExtension.

## Overview

This plugin provides a SQLite-based storage backend for the Markdig Fetch Extension. It persists fetched markdown content to a SQLite database, allowing the cache to survive application restarts.

## Installation

```bash
dotnet add package Mostlylucid.Markdig.FetchExtension.Sqlite
```

## Quick Start

```csharp
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Markdig.FetchExtension.Sqlite;
using Markdig;
using Microsoft.Extensions.DependencyInjection;

// Setup DI
var services = new ServiceCollection();
services.AddLogging();
services.AddSqliteMarkdownFetch("Data Source=markdown-cache.db");
var serviceProvider = services.BuildServiceProvider();

// Ensure database is created
serviceProvider.EnsureMarkdownCacheDatabase();

// Configure extension
FetchMarkdownExtension.ConfigureServiceProvider(serviceProvider);

// Create pipeline
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Use<FetchMarkdownExtension>()
    .Build();

// Use it!
var markdown = @"
# My Document
<fetch markdownurl=""https://raw.githubusercontent.com/user/repo/main/README.md"" pollfrequency=""24""/>
";

var html = Markdown.ToHtml(markdown, pipeline);
```

## Configuration Options

### Connection String

By default, the database file is `markdown-cache.db` in the current directory. You can customize this:

```csharp
// Relative path
services.AddSqliteMarkdownFetch("Data Source=./cache/markdown.db");

// Absolute path
services.AddSqliteMarkdownFetch("Data Source=C:/MyApp/Data/markdown-cache.db");

// In-memory database (useful for testing)
services.AddSqliteMarkdownFetch("Data Source=:memory:");
```

### ASP.NET Core Integration

```csharp
// In Program.cs
builder.Services.AddSqliteMarkdownFetch("Data Source=./Data/markdown-cache.db");

// After building the app
var app = builder.Build();
app.Services.EnsureMarkdownCacheDatabase();
```

## Database Schema

The plugin creates a single table:

```sql
CREATE TABLE MarkdownCacheEntry (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Url TEXT(2048) NOT NULL,
    BlogPostId INTEGER NOT NULL,
    Content TEXT NOT NULL,
    LastFetchedAt TEXT NOT NULL,
    CacheKey TEXT(128) NOT NULL
);

CREATE UNIQUE INDEX IX_MarkdownCacheEntry_CacheKey ON MarkdownCacheEntry(CacheKey);
CREATE INDEX IX_MarkdownCacheEntry_Url_BlogPostId ON MarkdownCacheEntry(Url, BlogPostId);
```

## Features

- **Persistent Storage** - Cache survives application restarts
- **Lightweight** - Single SQLite file, no server required
- **Stale-while-revalidate** - Returns cached content if fetch fails
- **Multi-post Support** - Same URL can have different cache per blog post
- **Automatic Schema Creation** - No manual database setup required

## When to Use SQLite Storage

**Best for:**
- Single-server applications
- Small to medium traffic sites
- Development and testing
- Applications that need simple persistence

**Consider alternatives if:**
- Multi-server deployment (use PostgreSQL or SQL Server)
- High concurrency requirements (database providers handle locks better)
- You need centralized cache management

## Troubleshooting

### Database Locked Errors

SQLite uses file-based locking. If you see "database is locked" errors:

1. Ensure only one application instance accesses the database
2. Consider using a server-based database (PostgreSQL/SQL Server) for multi-server scenarios
3. Use connection string parameter: `Data Source=cache.db;Pooling=True;`

### Performance

For best performance:
- Use SSD storage for the database file
- Enable WAL mode: `Data Source=cache.db;Journal Mode=WAL;`
- Keep database file on local disk (not network share)

## Requirements

- .NET 9.0+
- Microsoft.EntityFrameworkCore.Sqlite 9.0+

## License

MIT
