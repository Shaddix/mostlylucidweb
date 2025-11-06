# Mostlylucid.Markdig.FetchExtension.SqlServer

SQL Server storage plugin for Mostlylucid.Markdig.FetchExtension.

## Overview

This plugin provides a SQL Server-based storage backend for the Markdig Fetch Extension. It persists fetched markdown content to a SQL Server database, enabling multi-server deployments with shared cache in Microsoft-centric environments.

## Installation

```bash
dotnet add package Mostlylucid.Markdig.FetchExtension.SqlServer
```

## Quick Start

```csharp
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Markdig.FetchExtension.SqlServer;
using Markdig;
using Microsoft.Extensions.DependencyInjection;

// Setup DI
var services = new ServiceCollection();
services.AddLogging();
services.AddSqlServerMarkdownFetch("Server=localhost;Database=MyApp;Integrated Security=true;TrustServerCertificate=true");
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

Provide a standard SQL Server connection string:

```csharp
// Windows Authentication (Integrated Security)
services.AddSqlServerMarkdownFetch(
    "Server=localhost;Database=MyApp;Integrated Security=true;TrustServerCertificate=true");

// SQL Server Authentication
services.AddSqlServerMarkdownFetch(
    "Server=localhost;Database=MyApp;User Id=sa;Password=YourPassword;TrustServerCertificate=true");

// Azure SQL Database
services.AddSqlServerMarkdownFetch(
    "Server=myserver.database.windows.net;Database=MyApp;User Id=admin@myserver;Password=pass;Encrypt=true");

// From configuration
services.AddSqlServerMarkdownFetch(
    Configuration.GetConnectionString("MarkdownCache"));
```

### ASP.NET Core Integration

```csharp
// In Program.cs
builder.Services.AddSqlServerMarkdownFetch(
    builder.Configuration.GetConnectionString("MarkdownCache"));

// After building the app
var app = builder.Build();
app.Services.EnsureMarkdownCacheDatabase();
```

## Database Schema

The plugin creates a table in the default `dbo` schema:

```sql
CREATE TABLE MarkdownCache (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Url NVARCHAR(2048) NOT NULL,
    BlogPostId INT NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    LastFetchedAt DATETIMEOFFSET NOT NULL,
    CacheKey NVARCHAR(128) NOT NULL
);

CREATE UNIQUE INDEX IX_MarkdownCache_CacheKey ON MarkdownCache(CacheKey);
CREATE INDEX IX_MarkdownCache_Url_BlogPostId ON MarkdownCache(Url, BlogPostId);
```

## Features

- **Multi-Server Support** - Shared cache across application instances
- **Enterprise Ready** - SQL Server's ACID guarantees
- **High Availability** - Always On, Failover Clustering
- **Azure Integration** - Works with Azure SQL Database
- **Stale-while-revalidate** - Returns cached content if fetch fails
- **Multi-post Support** - Same URL can have different cache per blog post
- **Automatic Schema Creation** - No manual database setup required

## When to Use SQL Server Storage

**Best for:**
- Microsoft/Azure-centric environments
- Applications already using SQL Server
- Enterprise deployments with existing SQL Server infrastructure
- Azure App Service + Azure SQL Database deployments
- High-availability requirements with Always On

**Consider alternatives if:**
- Single-server with simple needs (use SQLite)
- Cost-conscious deployments (PostgreSQL is free)
- You don't have SQL Server infrastructure

## Production Considerations

### Connection Pooling

Connection pooling is enabled by default in SQL Server:

```csharp
services.AddSqlServerMarkdownFetch(
    "Server=myserver;Database=MyApp;User Id=user;Password=pass;" +
    "Min Pool Size=5;Max Pool Size=100;Connection Lifetime=300");
```

### Migrations

For production deployments, use Entity Framework migrations instead of `EnsureCreated()`:

```bash
dotnet ef migrations add InitialCreate --context MarkdownCacheDbContext
dotnet ef database update --context MarkdownCacheDbContext
```

### Performance Tuning

1. **Indexes**: The plugin creates appropriate indexes by default
2. **Connection pooling**: Enabled by default
3. **Columnstore**: For very large caches, consider columnstore indexes
4. **Partitioning**: Consider table partitioning by date for massive scale
5. **Read replicas**: Use Always On read replicas for read scaling

### High Availability

SQL Server supports various HA configurations:
- Always On Availability Groups
- Failover Cluster Instances (FCI)
- Database Mirroring (legacy)
- Azure SQL Database built-in HA

### Azure SQL Database

When using Azure SQL Database:

```csharp
services.AddSqlServerMarkdownFetch(
    "Server=tcp:myserver.database.windows.net,1433;" +
    "Database=MyApp;" +
    "User ID=admin@myserver;" +
    "Password=YourPassword;" +
    "Encrypt=true;" +
    "TrustServerCertificate=false;" +
    "Connection Timeout=30;");
```

**Azure-specific tips:**
- Use service tiers appropriate for your workload (S3+ recommended)
- Enable Query Store for performance monitoring
- Configure firewall rules to allow your app servers
- Consider using Managed Identity for authentication
- Use elastic pools for multiple databases

## Docker Deployment

Example `docker-compose.yml` for SQL Server:

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "YourStrong!Passw0rd"
      MSSQL_PID: "Developer"
    volumes:
      - sqlserver_data:/var/opt/mssql
    ports:
      - "1433:1433"

  app:
    image: myapp:latest
    environment:
      ConnectionStrings__MarkdownCache: "Server=sqlserver;Database=MyApp;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true"
    depends_on:
      - sqlserver

volumes:
  sqlserver_data:
```

## Connection String Authentication Options

### Windows Authentication (Recommended for on-premises)
```csharp
"Server=myserver;Database=MyApp;Integrated Security=true"
```

### SQL Server Authentication
```csharp
"Server=myserver;Database=MyApp;User Id=myuser;Password=mypass"
```

### Azure Managed Identity (Recommended for Azure)
```csharp
services.AddSqlServerMarkdownFetch(connectionString:
    new SqlConnectionStringBuilder
    {
        DataSource = "myserver.database.windows.net",
        InitialCatalog = "MyApp",
        Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
    }.ConnectionString);
```

## Requirements

- .NET 9.0+
- Microsoft.EntityFrameworkCore.SqlServer 9.0+
- SQL Server 2016+ or Azure SQL Database

## License

MIT
