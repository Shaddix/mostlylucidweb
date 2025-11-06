Mostlylucid.Markdig.FetchExtension
===================================

A Markdig extension for fetching and embedding remote Markdown at render time.

QUICK START
-----------

1. Install the package:
   dotnet add package Mostlylucid.Markdig.FetchExtension

2. Configure services:
   services.AddInMemoryMarkdownFetch();  // or AddFileBasedMarkdownFetch()

3. Setup extension:
   FetchMarkdownExtension.ConfigureServiceProvider(serviceProvider);

4. Create pipeline:
   var pipeline = new MarkdownPipelineBuilder()
       .UseAdvancedExtensions()
       .Use<FetchMarkdownExtension>()
       .Build();

USAGE
-----

Fetch remote content:
<fetch markdownurl="https://example.com/README.md" pollfrequency="24"/>

With link rewriting (for GitHub repos):
<fetch markdownurl="https://raw.githubusercontent.com/user/repo/main/README.md"
       pollfrequency="24"
       transformlinks="true"/>

Show metadata summary:
<fetch markdownurl="https://api.example.com/docs.md"
       pollfrequency="12"
       showsummary="true"
       summarytemplate="Updated {age} | Status: {status}"/>

Separate summary tag:
<fetch markdownurl="https://example.com/docs.md" pollfrequency="24"/>
...
<fetch-summary url="https://example.com/docs.md" template="Last updated {age}"/>

STORAGE OPTIONS
---------------

In-Memory: services.AddInMemoryMarkdownFetch()
File-Based: services.AddFileBasedMarkdownFetch("./cache")
SQLite: services.AddSqliteMarkdownFetch("Data Source=cache.db")
PostgreSQL: services.AddPostgresMarkdownFetch(connectionString)
SQL Server: services.AddSqlServerMarkdownFetch(connectionString)

DATABASE PLUGINS (separate packages)
-------------------------------------
- Mostlylucid.Markdig.FetchExtension.Sqlite
- Mostlylucid.Markdig.FetchExtension.Postgres
- Mostlylucid.Markdig.FetchExtension.SqlServer

FEATURES
--------
- Smart caching with configurable poll frequency
- Stale-while-revalidate (returns cache if fetch fails)
- Automatic link rewriting for GitHub content
- Flexible metadata summaries with template support
- Multiple storage backends
- Full DI support

DOCUMENTATION
-------------
https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.Markdig.FetchExtension

LICENSE: Unlicense (public domain)
