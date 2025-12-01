# Data Access in .NET: Comparing ORMs and Mapping Strategies with PostgreSQL

<!-- category -- .NET, PostgreSQL, EF Core, Dapper, Performance -->
<datetime class="hidden">2025-12-01T14:00</datetime>

When building .NET applications, one of the most important architectural decisions you'll make is how to handle data access and object mapping. The .NET ecosystem offers a rich variety of approaches, from full-featured ORMs to bare-metal SQL execution. Each approach comes with its own trade-offs in terms of performance, developer productivity, type safety, and maintainability.

In this comprehensive guide, we'll explore the most popular data access patterns in .NET. While we use PostgreSQL with Npgsql in our examples (since that's what powers this blog), the concepts, patterns, and trade-offs apply equally to SQL Server, MySQL, SQLite, and other relational databases. The principles remain the same - only the SQL dialect and some specific features differ.

## Table of Contents

## The Spectrum of Data Access Approaches

The .NET data access landscape can be visualized as a spectrum:

```
Full Abstraction                                      Full Control
     ↓                                                      ↓
[EF Core] → [EF Core Raw SQL] → [Dapper] → [Npgsql ADO.NET]
```

As you move from left to right, you gain performance and control but lose convenience and automatic features. Let's examine each approach in detail.

### Data Access Flow Comparison

Here's a visual comparison of how each approach handles a typical query:

```mermaid
graph TB
    subgraph "EF Core Flow"
        A1[LINQ Query] -->|Compile| B1[Expression Tree]
        B1 -->|Translate| C1[SQL Query]
        C1 -->|Execute| D1[PostgreSQL]
        D1 -->|Results| E1[DbDataReader]
        E1 -->|Materialize| F1[Tracked Entities]
        F1 -->|Return| G1[Application]
    end

    subgraph "Dapper Flow"
        A2[SQL String] -->|Parameterize| B2[DbCommand]
        B2 -->|Execute| C2[PostgreSQL]
        C2 -->|Results| D2[DbDataReader]
        D2 -->|Map| E2[POCOs]
        E2 -->|Return| F2[Application]
    end

    subgraph "Raw Npgsql Flow"
        A3[SQL + Parameters] -->|Build Command| B3[NpgsqlCommand]
        B3 -->|Execute| C3[PostgreSQL]
        C3 -->|Results| D3[NpgsqlDataReader]
        D3 -->|Manual Mapping| E3[Objects]
        E3 -->|Return| F3[Application]
    end

    style A1 stroke:#2563eb,stroke-width:2px
    style B1 stroke:#2563eb,stroke-width:2px
    style C1 stroke:#2563eb,stroke-width:2px
    style D1 stroke:#2563eb,stroke-width:2px
    style E1 stroke:#2563eb,stroke-width:2px
    style F1 stroke:#2563eb,stroke-width:2px
    style G1 stroke:#2563eb,stroke-width:2px

    style A2 stroke:#059669,stroke-width:2px
    style B2 stroke:#059669,stroke-width:2px
    style C2 stroke:#059669,stroke-width:2px
    style D2 stroke:#059669,stroke-width:2px
    style E2 stroke:#059669,stroke-width:2px
    style F2 stroke:#059669,stroke-width:2px

    style A3 stroke:#dc2626,stroke-width:2px
    style B3 stroke:#dc2626,stroke-width:2px
    style C3 stroke:#dc2626,stroke-width:2px
    style D3 stroke:#dc2626,stroke-width:2px
    style E3 stroke:#dc2626,stroke-width:2px
    style F3 stroke:#dc2626,stroke-width:2px
```

### Performance vs Developer Productivity Trade-off

```mermaid
graph LR
    A[High Productivity<br/>Low Performance] --> B[EF Core<br/>Full Tracking]
    B --> C[EF Core<br/>No Tracking]
    C --> D[EF Core<br/>Raw SQL]
    D --> E[Dapper]
    E --> F[Raw Npgsql]
    F --> G[Low Productivity<br/>High Performance]

    style A stroke:#2563eb,stroke-width:2px
    style B stroke:#2563eb,stroke-width:2px
    style C stroke:#3b82f6,stroke-width:2px
    style D stroke:#059669,stroke-width:2px
    style E stroke:#059669,stroke-width:2px
    style F stroke:#dc2626,stroke-width:2px
    style G stroke:#dc2626,stroke-width:2px
```

## Entity Framework Core: The Full-Featured ORM

Entity Framework Core is Microsoft's flagship ORM, providing a complete abstraction over your database. It supports PostgreSQL through the Npgsql.EntityFrameworkCore.PostgreSQL provider.

### Key Features

- **Change Tracking**: Automatically tracks entity changes and generates appropriate SQL
- **Migrations**: Code-first schema management and version control
- **LINQ Provider**: Type-safe queries using C# language constructs
- **Lazy/Eager Loading**: Flexible loading strategies for related entities
- **Advanced PostgreSQL Features**: Full-text search, JSON columns, arrays, range types
- **Interceptors and Events**: Extensibility points for cross-cutting concerns

### Example: Basic CRUD with EF Core

```csharp
public class BlogDbContext : DbContext
{
    public DbSet<BlogPost> BlogPosts { get; set; }
    public DbSet<Comment> Comments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql("Host=localhost;Database=blog;Username=postgres;Password=secret");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // PostgreSQL-specific: Full-text search
        modelBuilder.Entity<BlogPost>()
            .HasGeneratedTsVectorColumn(
                p => p.SearchVector,
                "english",
                p => new { p.Title, p.Content })
            .HasIndex(p => p.SearchVector)
            .HasMethod("GIN");

        // PostgreSQL array type
        modelBuilder.Entity<BlogPost>()
            .Property(p => p.Tags)
            .HasPostgresArrayConversion(
                tag => tag.ToLowerInvariant(),
                tag => tag);
    }
}

public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string[] Tags { get; set; }
    public NpgsqlTsVector SearchVector { get; set; }
    public List<Comment> Comments { get; set; }
}

// Usage
public class BlogService
{
    private readonly BlogDbContext _context;

    public async Task<List<BlogPost>> GetRecentPostsAsync(int count)
    {
        return await _context.BlogPosts
            .Include(p => p.Comments)
            .OrderByDescending(p => p.PublishedDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<BlogPost>> SearchPostsAsync(string searchTerm)
    {
        return await _context.BlogPosts
            .Where(p => p.SearchVector.Matches(EF.Functions.ToTsQuery("english", searchTerm)))
            .ToListAsync();
    }

    public async Task AddPostAsync(BlogPost post)
    {
        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync();
    }
}
```

### EF Core with Raw SQL

EF Core also supports raw SQL queries when you need more control:

```csharp
public async Task<List<BlogPost>> GetPostsByComplexCriteriaAsync()
{
    var searchTerm = "postgresql";

    return await _context.BlogPosts
        .FromSqlInterpolated($@"
            SELECT * FROM ""BlogPosts""
            WHERE ""SearchVector"" @@ to_tsquery('english', {searchTerm})
            AND array_length(""Tags"", 1) > 3
            ORDER BY ts_rank(""SearchVector"", to_tsquery('english', {searchTerm})) DESC
        ")
        .ToListAsync();
}

// Or with DbDataReader for maximum control
public async Task<List<PostStatistics>> GetPostStatisticsAsync()
{
    using var command = _context.Database.GetDbConnection().CreateCommand();
    command.CommandText = @"
        SELECT
            DATE_TRUNC('month', ""PublishedDate"") as Month,
            COUNT(*) as PostCount,
            AVG(ARRAY_LENGTH(""Tags"", 1)) as AvgTags
        FROM ""BlogPosts""
        GROUP BY DATE_TRUNC('month', ""PublishedDate"")
        ORDER BY Month DESC";

    await _context.Database.OpenConnectionAsync();

    var results = new List<PostStatistics>();
    using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        results.Add(new PostStatistics
        {
            Month = reader.GetDateTime(0),
            PostCount = reader.GetInt32(1),
            AverageTags = reader.GetDouble(2)
        });
    }

    return results;
}
```

### When to Use EF Core

**✅ Use EF Core When:**

- Building a new application with evolving schema requirements
- You need strong typing and compile-time query validation
- Migrations and schema versioning are important
- Your team prefers working with objects over SQL
- You're using complex domain models with relationships
- Development speed is more critical than raw performance
- You need cross-database portability (though PostgreSQL-specific features lock you in)

**❌ Avoid EF Core When:**

- Maximum performance is critical (high-throughput APIs, batch processing)
- You have complex, hand-tuned SQL queries
- Your queries don't map well to object graphs
- You need fine-grained control over every SQL statement
- Memory usage is a critical constraint (change tracking overhead)
- You're working with legacy schemas that don't map to conventions

### EF Core SQL Generation: Understanding What Gets Executed

One of the most important aspects of using EF Core effectively is understanding what SQL it generates. EF Core has significantly improved SQL generation over the years, but it's critical to verify the queries being sent to PostgreSQL.

#### Viewing Generated SQL

```csharp
// Enable sensitive data logging and detailed errors (development only!)
optionsBuilder
    .UseNpgsql(connectionString)
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors()
    .LogTo(Console.WriteLine, LogLevel.Information);

// Or use logging to see SQL
public class BlogService
{
    private readonly BlogDbContext _context;
    private readonly ILogger<BlogService> _logger;

    public async Task<List<BlogPost>> GetPostsAsync()
    {
        var query = _context.BlogPosts
            .Where(p => p.PublishedDate > DateTime.UtcNow.AddDays(-30))
            .OrderByDescending(p => p.PublishedDate);

        // View the SQL before execution
        var sql = query.ToQueryString();
        _logger.LogInformation("Executing query: {Sql}", sql);

        return await query.ToListAsync();
    }
}
```

#### Example: Simple Query

**C# LINQ:**
```csharp
var recentPosts = await _context.BlogPosts
    .Where(p => p.CategoryId == 5)
    .OrderByDescending(p => p.PublishedDate)
    .Take(10)
    .ToListAsync();
```

**Generated SQL (EF Core 8+):**
```sql
SELECT b."Id", b."Title", b."Content", b."CategoryId", b."PublishedDate"
FROM "BlogPosts" AS b
WHERE b."CategoryId" = @__categoryId_0
ORDER BY b."PublishedDate" DESC
LIMIT @__p_1
```

Notice how EF Core 8+ generates clean, efficient SQL with proper parameterization.

#### Example: Join with Include (Before EF Core 5)

**C# Code:**
```csharp
var posts = await _context.BlogPosts
    .Include(p => p.Category)
    .Include(p => p.Comments)
    .ToListAsync();
```

**Old SQL (EF Core 3.1 - Cartesian Explosion):**
```sql
SELECT b."Id", b."Title", c."Id", c."Name", cm."Id", cm."Content"
FROM "BlogPosts" AS b
LEFT JOIN "Categories" AS c ON b."CategoryId" = c."Id"
LEFT JOIN "Comments" AS cm ON b."Id" = cm."BlogPostId"
ORDER BY b."Id", c."Id"
```

This creates a **Cartesian product** - if a post has 10 comments, that row is repeated 10 times!

#### Example: Split Queries (EF Core 5+)

**C# Code with Split Query:**
```csharp
var posts = await _context.BlogPosts
    .Include(p => p.Category)
    .Include(p => p.Comments)
    .AsSplitQuery()  // ← This is the key!
    .ToListAsync();
```

**Generated SQL (Multiple Queries):**
```sql
-- Query 1: Get posts and categories
SELECT b."Id", b."Title", b."Content", c."Id", c."Name"
FROM "BlogPosts" AS b
LEFT JOIN "Categories" AS c ON b."CategoryId" = c."Id"

-- Query 2: Get comments for those posts
SELECT cm."Id", cm."Content", cm."BlogPostId"
FROM "Comments" AS cm
INNER JOIN (
    SELECT b."Id"
    FROM "BlogPosts" AS b
) AS t ON cm."BlogPostId" = t."Id"
ORDER BY t."Id"
```

This eliminates the Cartesian product and is often **much faster** for collections!

#### Example: Filtered Include (EF Core 5+)

**C# Code:**
```csharp
var posts = await _context.BlogPosts
    .Include(p => p.Comments.Where(c => c.IsApproved))
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT b."Id", b."Title", b."Content", t."Id", t."Content", t."IsApproved"
FROM "BlogPosts" AS b
LEFT JOIN (
    SELECT c."Id", c."Content", c."IsApproved", c."BlogPostId"
    FROM "Comments" AS c
    WHERE c."IsApproved" = TRUE
) AS t ON b."Id" = t."BlogPostId"
ORDER BY b."Id"
```

#### Example: JSON Column Queries (EF Core 7+)

**C# Code:**
```csharp
public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; }
    public PostMetadata Metadata { get; set; }  // Stored as JSONB
}

public class PostMetadata
{
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
    public List<string> RelatedTags { get; set; }
}

// Query JSON properties
var featuredPosts = await _context.BlogPosts
    .Where(p => p.Metadata.IsFeatured)
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT b."Id", b."Title", b."Metadata"
FROM "BlogPosts" AS b
WHERE b."Metadata" ->> 'IsFeatured' = 'true'
```

EF Core 7+ can translate JSON property access to PostgreSQL JSON operators!

#### Example: Bulk Update (EF Core 7+ ExecuteUpdate)

**Old Way (Inefficient):**
```csharp
var posts = await _context.BlogPosts
    .Where(p => p.CategoryId == 5)
    .ToListAsync();

foreach (var post in posts)
{
    post.IsArchived = true;
}

await _context.SaveChangesAsync();  // Generates N UPDATE statements!
```

**New Way (EF Core 7+):**
```csharp
await _context.BlogPosts
    .Where(p => p.CategoryId == 5)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(p => p.IsArchived, true));
```

**Generated SQL (Single Query!):**
```sql
UPDATE "BlogPosts" AS b
SET "IsArchived" = TRUE
WHERE b."CategoryId" = 5
```

This is a **massive** improvement - one SQL statement instead of N!

#### Example: Bulk Delete (EF Core 7+)

**Old Way:**
```csharp
var oldPosts = await _context.BlogPosts
    .Where(p => p.PublishedDate < DateTime.UtcNow.AddYears(-5))
    .ToListAsync();

_context.BlogPosts.RemoveRange(oldPosts);
await _context.SaveChangesAsync();  // N DELETE statements
```

**New Way:**
```csharp
await _context.BlogPosts
    .Where(p => p.PublishedDate < DateTime.UtcNow.AddYears(-5))
    .ExecuteDeleteAsync();
```

**Generated SQL:**
```sql
DELETE FROM "BlogPosts" AS b
WHERE b."PublishedDate" < @__p_0
```

#### Example: Complex Aggregation

**C# Code:**
```csharp
var categoryStats = await _context.Categories
    .Select(c => new CategoryStats
    {
        CategoryName = c.Name,
        PostCount = c.BlogPosts.Count(),
        LatestPostDate = c.BlogPosts.Max(p => p.PublishedDate),
        AverageComments = c.BlogPosts.Average(p => p.Comments.Count)
    })
    .ToListAsync();
```

**Generated SQL (EF Core 8):**
```sql
SELECT c."Name" AS "CategoryName",
       COUNT(*)::int AS "PostCount",
       MAX(b."PublishedDate") AS "LatestPostDate",
       COALESCE(AVG((
           SELECT COUNT(*)::int
           FROM "Comments" AS c0
           WHERE b."Id" = c0."BlogPostId"
       ))::double precision, 0.0) AS "AverageComments"
FROM "Categories" AS c
LEFT JOIN "BlogPosts" AS b ON c."Id" = b."CategoryId"
GROUP BY c."Id", c."Name"
```

#### PostgreSQL Full-Text Search

**C# Code:**
```csharp
var searchResults = await _context.BlogPosts
    .Where(p => p.SearchVector.Matches(EF.Functions.ToTsQuery("english", "postgresql & performance")))
    .OrderByDescending(p => p.SearchVector.Rank(EF.Functions.ToTsQuery("english", "postgresql & performance")))
    .Take(20)
    .ToListAsync();
```

**Generated SQL:**
```sql
SELECT b."Id", b."Title", b."Content", b."SearchVector"
FROM "BlogPosts" AS b
WHERE b."SearchVector" @@ to_tsquery('english', @__searchTerm_0)
ORDER BY ts_rank(b."SearchVector", to_tsquery('english', @__searchTerm_0)) DESC
LIMIT 20
```

### ⚠️ Critical EF Core Warnings and Pitfalls

#### 1. Change Tracking Memory Leaks

**The Problem:**
```csharp
// ❌ DANGER: This can cause memory leaks!
public class PostCache
{
    private readonly BlogDbContext _context;
    private List<BlogPost> _cachedPosts;

    public PostCache(BlogDbContext context)
    {
        _context = context;
    }

    public async Task LoadCacheAsync()
    {
        // These entities are now tracked by the context
        _cachedPosts = await _context.BlogPosts.ToListAsync();

        // The DbContext holds references to these entities FOREVER
        // They can never be garbage collected while the context lives!
    }
}
```

**Why it's a problem:**
- Tracked entities remain in memory for the lifetime of the `DbContext`
- The change tracker maintains references, preventing garbage collection
- Long-lived contexts (e.g., singletons) = memory leak
- In ASP.NET Core, context is scoped by default (good!)
- But if you cache tracked entities, you're in trouble

**The Solution:**
```csharp
public async Task LoadCacheAsync()
{
    // ✅ Use AsNoTracking() for read-only queries
    _cachedPosts = await _context.BlogPosts
        .AsNoTracking()
        .ToListAsync();

    // Or detach entities after loading
    var posts = await _context.BlogPosts.ToListAsync();
    foreach (var post in posts)
    {
        _context.Entry(post).State = EntityState.Detached;
    }
    _cachedPosts = posts;
}
```

#### 2. Proxy Generation and Lazy Loading Dangers

**The Problem:**
```csharp
// ❌ Enable lazy loading
optionsBuilder
    .UseNpgsql(connectionString)
    .UseLazyLoadingProxies();  // Convenient but dangerous!

public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; }
    public virtual Category Category { get; set; }  // Virtual = proxy
    public virtual List<Comment> Comments { get; set; }
}

// Somewhere in your code
var posts = await _context.BlogPosts.ToListAsync();

foreach (var post in posts)
{
    Console.WriteLine(post.Category.Name);  // N+1 query here!
    Console.WriteLine(post.Comments.Count);  // Another N+1 query!
}
```

**What happens:**
1. First query loads all posts
2. For **each post**, accessing `Category` triggers a database query
3. For **each post**, accessing `Comments` triggers another query
4. If you have 100 posts, you just executed **201 queries**!

**The Solution:**
```csharp
// ✅ Explicit eager loading
var posts = await _context.BlogPosts
    .Include(p => p.Category)
    .Include(p => p.Comments)
    .ToListAsync();

// Or use split queries for better performance
var posts = await _context.BlogPosts
    .Include(p => p.Category)
    .Include(p => p.Comments)
    .AsSplitQuery()
    .ToListAsync();

// Or use projection to DTOs
var posts = await _context.BlogPosts
    .Select(p => new PostDto
    {
        Title = p.Title,
        CategoryName = p.Category.Name,
        CommentCount = p.Comments.Count
    })
    .ToListAsync();
```

#### 3. DbContext Lifetime Issues

**The Problem:**
```csharp
// ❌ NEVER do this - singleton DbContext
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<BlogDbContext>();  // WRONG!
}

// ❌ Also wrong - storing context in static field
public static class DataAccess
{
    private static BlogDbContext _context = new BlogDbContext();

    public static async Task<BlogPost> GetPostAsync(int id)
    {
        return await _context.BlogPosts.FindAsync(id);
    }
}
```

**Why it's wrong:**
- `DbContext` is **not thread-safe**
- Concurrent requests will cause data corruption
- Change tracker grows indefinitely
- Connection pool exhaustion
- Stale data from cache

**The Solution:**
```csharp
// ✅ Use scoped lifetime (default in ASP.NET Core)
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContext<BlogDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// ✅ Or use DbContext factory for background services
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContextFactory<BlogDbContext>(options =>
        options.UseNpgsql(connectionString));
}

public class BlogBackgroundService
{
    private readonly IDbContextFactory<BlogDbContext> _contextFactory;

    public async Task ProcessPostsAsync()
    {
        // Create a new context for this operation
        using var context = await _contextFactory.CreateDbContextAsync();

        var posts = await context.BlogPosts.ToListAsync();
        // Process posts...
    }
}
```

#### 4. Unintended Includes in Navigation Properties

**The Problem:**
```csharp
public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; }
    public List<Comment> Comments { get; set; }
}

// You query one post...
var post = await _context.BlogPosts.FirstAsync();

// Add a new comment
var newComment = new Comment { Content = "Great post!" };
post.Comments.Add(newComment);

await _context.SaveChangesAsync();

// ❌ EF Core saves the comment, BUT...
// If Comments wasn't loaded, you just lost all existing comments!
// The collection is empty, so EF thinks there are no other comments
```

**The Solution:**
```csharp
// ✅ Always load navigation properties before modifying
var post = await _context.BlogPosts
    .Include(p => p.Comments)
    .FirstAsync(p => p.Id == postId);

post.Comments.Add(newComment);
await _context.SaveChangesAsync();

// Or add directly to the DbSet
_context.Comments.Add(new Comment
{
    BlogPostId = postId,
    Content = "Great post!"
});
await _context.SaveChangesAsync();
```

#### 5. Async vs Sync Mixing

**The Problem:**
```csharp
// ❌ Mixing sync and async - deadlock risk!
public async Task<BlogPost> GetPostAsync(int id)
{
    var post = _context.BlogPosts
        .Where(p => p.Id == id)
        .FirstOrDefault();  // Sync method in async context!

    return post;
}

// ❌ Even worse - blocking async code
public BlogPost GetPost(int id)
{
    return _context.BlogPosts
        .FirstOrDefaultAsync(p => p.Id == id)
        .Result;  // DEADLOCK RISK!
}
```

**The Solution:**
```csharp
// ✅ Use async all the way
public async Task<BlogPost> GetPostAsync(int id)
{
    return await _context.BlogPosts
        .FirstOrDefaultAsync(p => p.Id == id);
}

// ✅ Or use sync all the way (not recommended for ASP.NET Core)
public BlogPost GetPost(int id)
{
    return _context.BlogPosts
        .FirstOrDefault(p => p.Id == id);
}
```

### Performance Characteristics

- **Query Performance**: 20-50% overhead compared to Dapper for simple queries
- **Memory Usage**: Higher due to change tracking and proxy generation
- **First Query**: Slow (query compilation and caching)
- **Subsequent Queries**: Faster due to compiled query cache
- **Inserts/Updates**: Automatic change tracking adds overhead
- **Bulk Operations**: Poor performance with default methods (consider EFCore.BulkExtensions or ExecuteUpdate/ExecuteDelete in EF Core 7+)

## Dapper: The Micro-ORM

Dapper is a lightweight, high-performance micro-ORM created by Stack Overflow. It provides a thin layer over ADO.NET, handling the tedious work of mapping query results to objects while giving you full SQL control.

### Key Features

- **Raw SQL**: You write all SQL yourself
- **High Performance**: Minimal overhead over ADO.NET
- **Multi-Mapping**: Map complex queries to multiple related types
- **Parameter Handling**: Automatic parameterization prevents SQL injection
- **Transaction Support**: Full control over transactions
- **Simplicity**: No configuration, no change tracking, no magic

### Example: Dapper with PostgreSQL

```csharp
using Npgsql;
using Dapper;

public class DapperBlogRepository
{
    private readonly string _connectionString;

    public DapperBlogRepository(string connectionString)
    {
        _connectionString = connectionString;

        // Configure Dapper to work with PostgreSQL naming conventions
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task<IEnumerable<BlogPost>> GetRecentPostsAsync(int count)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            SELECT id, title, content, tags, published_date, category_id
            FROM blog_posts
            ORDER BY published_date DESC
            LIMIT @Count";

        return await connection.QueryAsync<BlogPost>(sql, new { Count = count });
    }

    // Multi-mapping: Join posts with comments
    public async Task<IEnumerable<BlogPost>> GetPostsWithCommentsAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            SELECT
                p.id, p.title, p.content,
                c.id, c.author, c.content, c.blog_post_id
            FROM blog_posts p
            LEFT JOIN comments c ON p.id = c.blog_post_id
            ORDER BY p.published_date DESC";

        var postDict = new Dictionary<int, BlogPost>();

        await connection.QueryAsync<BlogPost, Comment, BlogPost>(
            sql,
            (post, comment) =>
            {
                if (!postDict.TryGetValue(post.Id, out var existingPost))
                {
                    existingPost = post;
                    existingPost.Comments = new List<Comment>();
                    postDict.Add(post.Id, existingPost);
                }

                if (comment != null)
                {
                    existingPost.Comments.Add(comment);
                }

                return existingPost;
            },
            splitOn: "id"
        );

        return postDict.Values;
    }

    // Full-text search with PostgreSQL
    public async Task<IEnumerable<BlogPost>> SearchPostsAsync(string searchTerm)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        const string sql = @"
            SELECT
                id,
                title,
                content,
                ts_rank(search_vector, query) as rank
            FROM blog_posts,
                 to_tsquery('english', @SearchTerm) query
            WHERE search_vector @@ query
            ORDER BY rank DESC";

        return await connection.QueryAsync<BlogPost>(sql, new { SearchTerm = searchTerm });
    }

    // Bulk insert with PostgreSQL COPY
    public async Task BulkInsertPostsAsync(IEnumerable<BlogPost> posts)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var writer = await connection.BeginBinaryImportAsync(
            "COPY blog_posts (title, content, tags, published_date) FROM STDIN (FORMAT BINARY)"
        );

        foreach (var post in posts)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(post.Title);
            await writer.WriteAsync(post.Content);
            await writer.WriteAsync(post.Tags, NpgsqlDbType.Array | NpgsqlDbType.Text);
            await writer.WriteAsync(post.PublishedDate);
        }

        await writer.CompleteAsync();
    }

    // Using stored procedures/functions
    public async Task<int> GetPostCountByDateRangeAsync(DateTime start, DateTime end)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<int>(
            "SELECT count_posts_by_date_range(@Start, @End)",
            new { Start = start, End = end }
        );
    }

    // Transaction support
    public async Task TransferPostToCategoryAsync(int postId, int newCategoryId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await connection.ExecuteAsync(
                "UPDATE blog_posts SET category_id = @CategoryId WHERE id = @PostId",
                new { CategoryId = newCategoryId, PostId = postId },
                transaction
            );

            await connection.ExecuteAsync(
                "INSERT INTO category_history (post_id, category_id, changed_at) VALUES (@PostId, @CategoryId, @ChangedAt)",
                new { PostId = postId, CategoryId = newCategoryId, ChangedAt = DateTime.UtcNow },
                transaction
            );

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Advanced Dapper Techniques

```csharp
// Custom type handlers for PostgreSQL types
public class PostgresArrayTypeHandler : SqlMapper.TypeHandler<string[]>
{
    public override void SetValue(IDbDataParameter parameter, string[] value)
    {
        parameter.Value = value;
        ((NpgsqlParameter)parameter).NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text;
    }

    public override string[] Parse(object value)
    {
        return (string[])value;
    }
}

// Register custom handlers
SqlMapper.AddTypeHandler(new PostgresArrayTypeHandler());

// Dynamic parameters for complex scenarios
public async Task<IEnumerable<BlogPost>> SearchWithDynamicFiltersAsync(SearchCriteria criteria)
{
    var parameters = new DynamicParameters();
    var conditions = new List<string>();

    if (!string.IsNullOrEmpty(criteria.SearchTerm))
    {
        conditions.Add("search_vector @@ to_tsquery('english', @SearchTerm)");
        parameters.Add("SearchTerm", criteria.SearchTerm);
    }

    if (criteria.CategoryIds?.Any() == true)
    {
        conditions.Add("category_id = ANY(@CategoryIds)");
        parameters.Add("CategoryIds", criteria.CategoryIds);
    }

    if (criteria.FromDate.HasValue)
    {
        conditions.Add("published_date >= @FromDate");
        parameters.Add("FromDate", criteria.FromDate.Value);
    }

    var whereClause = conditions.Any()
        ? "WHERE " + string.Join(" AND ", conditions)
        : "";

    var sql = $@"
        SELECT id, title, content, published_date
        FROM blog_posts
        {whereClause}
        ORDER BY published_date DESC";

    using var connection = new NpgsqlConnection(_connectionString);
    return await connection.QueryAsync<BlogPost>(sql, parameters);
}
```

### When to Use Dapper

**✅ Use Dapper When:**

- Performance is critical
- You're comfortable writing SQL
- You have complex queries that don't map well to LINQ
- You need fine-grained control over SQL generation
- You're working with an existing database schema
- You want minimal overhead and dependencies
- Memory usage is a concern
- You need to leverage PostgreSQL-specific features extensively

**❌ Avoid Dapper When:**

- Your team is not comfortable with SQL
- You need automatic change tracking
- You want schema migrations as code
- You have a rapidly evolving schema
- You need cross-database portability
- You prefer LINQ over SQL syntax

### Performance Characteristics

- **Query Performance**: 5-15% overhead over raw ADO.NET
- **Memory Usage**: Minimal - no change tracking or proxies
- **Bulk Operations**: Excellent with PostgreSQL COPY
- **First Query**: Fast - no compilation step
- **Developer Productivity**: Requires more SQL knowledge

## Raw ADO.NET with Npgsql

For maximum control and performance, you can use Npgsql directly without any ORM layer.

### Example: Pure Npgsql

```csharp
public class NpgsqlBlogRepository
{
    private readonly string _connectionString;

    public async Task<List<BlogPost>> GetRecentPostsAsync(int count)
    {
        var posts = new List<BlogPost>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(
            "SELECT id, title, content, tags, published_date FROM blog_posts ORDER BY published_date DESC LIMIT @count",
            connection
        );

        command.Parameters.AddWithValue("count", count);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            posts.Add(new BlogPost
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                Tags = reader.GetFieldValue<string[]>(3),
                PublishedDate = reader.GetDateTime(4)
            });
        }

        return posts;
    }

    // Using prepared statements for repeated queries
    public async Task<BlogPost> GetPostByIdAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(
            "SELECT id, title, content FROM blog_posts WHERE id = $1",
            connection
        );

        command.Parameters.AddWithValue(id);
        await command.PrepareAsync(); // Prepared statement

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new BlogPost
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Content = reader.GetString(2)
            };
        }

        return null;
    }

    // Working with PostgreSQL JSONB
    public async Task<Dictionary<string, object>> GetPostMetadataAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(
            "SELECT metadata FROM blog_posts WHERE id = $1",
            connection
        );

        command.Parameters.AddWithValue(id);

        var json = await command.ExecuteScalarAsync() as string;
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    }
}
```

### When to Use Raw Npgsql

**✅ Use Raw Npgsql When:**

- You need absolute maximum performance
- You're building a high-throughput system
- You need precise control over every database operation
- You're working with PostgreSQL-specific features not well-supported by ORMs
- You want zero abstraction overhead
- You're building a data-intensive background processor

**❌ Avoid Raw Npgsql When:**

- Development speed is more important than raw performance
- Your team prefers higher-level abstractions
- You have complex domain models
- You need rapid prototyping

## Object Mapping Libraries

When working with Dapper or raw ADO.NET, you often need to map between different object representations (DTOs, entities, view models). Several libraries can help automate this.

### Mapster: High-Performance Mapping

Mapster is a fast, convention-based object mapper that uses source generation for optimal performance.

```csharp
// Install: Mapster and Mapster.Tool
using Mapster;

public class BlogPostDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Summary { get; set; }
    public List<string> CategoryNames { get; set; }
}

public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public List<Category> Categories { get; set; }
}

// Configuration
public class MappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<BlogPost, BlogPostDto>()
            .Map(dest => dest.Summary, src => src.Content.Substring(0, 200))
            .Map(dest => dest.CategoryNames, src => src.Categories.Select(c => c.Name).ToList());
    }
}

// Usage
public class BlogService
{
    public async Task<List<BlogPostDto>> GetPostsAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var posts = await connection.QueryAsync<BlogPost>(@"
            SELECT p.id, p.title, p.content,
                   c.id, c.name
            FROM blog_posts p
            LEFT JOIN post_categories pc ON p.id = pc.post_id
            LEFT JOIN categories c ON pc.category_id = c.id
        ");

        // Map to DTOs
        return posts.Adapt<List<BlogPostDto>>();
    }
}
```

### AutoMapper: Mature Convention-Based Mapper

AutoMapper is the most popular mapping library, though slower than Mapster.

```csharp
// Install: AutoMapper and AutoMapper.Extensions.Microsoft.DependencyInjection
using AutoMapper;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<BlogPost, BlogPostDto>()
            .ForMember(d => d.Summary, opt => opt.MapFrom(s => s.Content.Substring(0, 200)))
            .ForMember(d => d.CategoryNames, opt => opt.MapFrom(s => s.Categories.Select(c => c.Name)));

        // Reverse map
        CreateMap<BlogPostDto, BlogPost>()
            .ForMember(d => d.Content, opt => opt.Ignore());
    }
}

// Registration in Program.cs
services.AddAutoMapper(typeof(MappingProfile));

// Usage
public class BlogService
{
    private readonly IMapper _mapper;

    public BlogService(IMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<List<BlogPostDto>> GetPostsAsync()
    {
        var posts = await _repository.GetAllPostsAsync();
        return _mapper.Map<List<BlogPostDto>>(posts);
    }
}
```

### Manual Mapping: Full Control

Sometimes the best approach is explicit manual mapping:

```csharp
public static class BlogPostMapper
{
    public static BlogPostDto ToDto(this BlogPost post)
    {
        return new BlogPostDto
        {
            Id = post.Id,
            Title = post.Title,
            Summary = post.Content.Length > 200
                ? post.Content.Substring(0, 200) + "..."
                : post.Content,
            CategoryNames = post.Categories?.Select(c => c.Name).ToList() ?? new List<string>()
        };
    }

    public static List<BlogPostDto> ToDtoList(this IEnumerable<BlogPost> posts)
    {
        return posts.Select(p => p.ToDto()).ToList();
    }
}

// Usage
var posts = await _repository.GetAllPostsAsync();
var dtos = posts.ToDtoList();
```

### When to Use Mapping Libraries

**✅ Use Mapster/AutoMapper When:**

- You have many similar objects to map
- Mapping logic is convention-based (same property names)
- You want to centralize mapping configuration
- You need projection to DTOs from large entities

**✅ Use Manual Mapping When:**

- Mapping logic is complex and custom
- Performance is absolutely critical
- You have few mappings
- You want full transparency and control

## Hybrid Approaches: Best of Both Worlds

In real applications, you often want to use different approaches for different scenarios within the same application.

### CQRS Pattern: Separating Reads and Writes

```mermaid
graph TB
    Client[Client Application]

    subgraph "Write Side"
        WriteAPI[Write API / Commands]
        EFCore[EF Core Context]
        WriteDB[(PostgreSQL<br/>Write Operations)]
    end

    subgraph "Read Side"
        ReadAPI[Read API / Queries]
        Dapper[Dapper Repository]
        ReadDB[(PostgreSQL<br/>Read Operations)]
    end

    Client -->|Create/Update/Delete| WriteAPI
    WriteAPI --> EFCore
    EFCore -->|Change Tracking<br/>Validation<br/>Business Logic| WriteDB

    Client -->|Query/Search| ReadAPI
    ReadAPI --> Dapper
    Dapper -->|Optimized SQL<br/>DTOs<br/>No Tracking| ReadDB

    WriteDB -.->|Same Database| ReadDB

    style Client stroke:#6366f1,stroke-width:2px
    style WriteAPI stroke:#2563eb,stroke-width:2px
    style EFCore stroke:#2563eb,stroke-width:2px
    style WriteDB stroke:#2563eb,stroke-width:2px
    style ReadAPI stroke:#059669,stroke-width:2px
    style Dapper stroke:#059669,stroke-width:2px
    style ReadDB stroke:#059669,stroke-width:2px
```

This pattern leverages:
- **EF Core** for writes: Change tracking, validation, and business rules
- **Dapper** for reads: Maximum query performance and flexibility
- Same database, different access patterns optimized for each use case

### EF Core for Writes, Dapper for Reads (CQRS Pattern)

```csharp
// Commands: Use EF Core for change tracking and validation
public class BlogCommandService
{
    private readonly BlogDbContext _context;

    public async Task<int> CreatePostAsync(BlogPost post)
    {
        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync();
        return post.Id;
    }

    public async Task UpdatePostAsync(BlogPost post)
    {
        _context.BlogPosts.Update(post);
        await _context.SaveChangesAsync();
    }
}

// Queries: Use Dapper for read performance
public class BlogQueryService
{
    private readonly string _connectionString;

    public async Task<BlogPostDto> GetPostBySlugAsync(string slug)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        return await connection.QueryFirstOrDefaultAsync<BlogPostDto>(@"
            SELECT
                p.id,
                p.title,
                p.slug,
                p.content,
                p.published_date,
                c.name as category_name,
                COUNT(cm.id) as comment_count
            FROM blog_posts p
            LEFT JOIN categories c ON p.category_id = c.id
            LEFT JOIN comments cm ON p.id = cm.post_id
            WHERE p.slug = @Slug
            GROUP BY p.id, p.title, p.slug, p.content, p.published_date, c.name",
            new { Slug = slug }
        );
    }
}
```

### EF Core with Raw SQL for Complex Queries

```csharp
public class BlogService
{
    private readonly BlogDbContext _context;

    // Simple queries: Use EF Core LINQ
    public async Task<List<BlogPost>> GetPostsByCategoryAsync(int categoryId)
    {
        return await _context.BlogPosts
            .Where(p => p.CategoryId == categoryId)
            .Include(p => p.Comments)
            .ToListAsync();
    }

    // Complex analytics: Use raw SQL
    public async Task<List<PostAnalytics>> GetPostAnalyticsAsync()
    {
        return await _context.Database
            .SqlQuery<PostAnalytics>($@"
                WITH post_metrics AS (
                    SELECT
                        p.id,
                        p.title,
                        COUNT(DISTINCT c.id) as comment_count,
                        COUNT(DISTINCT v.id) as view_count,
                        AVG(c.sentiment_score) as avg_sentiment
                    FROM blog_posts p
                    LEFT JOIN comments c ON p.id = c.post_id
                    LEFT JOIN post_views v ON p.id = v.post_id
                    WHERE p.published_date >= NOW() - INTERVAL '30 days'
                    GROUP BY p.id, p.title
                )
                SELECT * FROM post_metrics
                ORDER BY view_count DESC
            ")
            .ToListAsync();
    }
}
```

### Dapper with Mapster for Complex Mappings

```csharp
public class BlogRepository
{
    private readonly string _connectionString;

    public async Task<List<BlogPostViewModel>> GetPostsWithRelatedDataAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);

        // Complex SQL that's hard to express in LINQ
        var posts = await connection.QueryAsync<BlogPostRaw>(@"
            SELECT
                p.*,
                jsonb_agg(DISTINCT jsonb_build_object('id', c.id, 'name', c.name)) as categories,
                jsonb_agg(DISTINCT jsonb_build_object('id', t.id, 'name', t.name)) as tags,
                (
                    SELECT COUNT(*)
                    FROM comments cm
                    WHERE cm.post_id = p.id AND cm.status = 'approved'
                ) as approved_comment_count
            FROM blog_posts p
            LEFT JOIN post_categories pc ON p.id = pc.post_id
            LEFT JOIN categories c ON pc.category_id = c.id
            LEFT JOIN post_tags pt ON p.id = pt.post_id
            LEFT JOIN tags t ON pt.tag_id = t.id
            GROUP BY p.id
        ");

        // Use Mapster to map to view model
        return posts.Adapt<List<BlogPostViewModel>>();
    }
}
```

## Performance Comparison

Let's look at real-world performance benchmarks for common operations with PostgreSQL:

### Benchmark: Reading 1000 Records

```
BenchmarkDotNet Results (Lower is Better):

Method                    | Mean       | Allocated
------------------------- |----------- |-----------
EF Core (No Tracking)    | 12.34 ms   | 2.4 MB
EF Core (With Tracking)  | 15.67 ms   | 4.8 MB
Dapper                   | 8.21 ms    | 1.8 MB
Raw Npgsql               | 7.45 ms    | 1.2 MB
```

### Benchmark: Inserting 1000 Records

```
Method                    | Mean       | Allocated
------------------------- |----------- |-----------
EF Core (SaveChanges)    | 245.3 ms   | 15.2 MB
EF Core (BulkInsert)     | 42.1 ms    | 8.4 MB
Dapper (Loop)            | 189.7 ms   | 2.1 MB
Npgsql COPY              | 18.3 ms    | 0.8 MB
```

### Benchmark: Complex Join Query

```
Method                    | Mean       | Allocated
------------------------- |----------- |-----------
EF Core (Include)        | 28.5 ms    | 5.2 MB
EF Core (Split Query)    | 24.1 ms    | 4.8 MB
Dapper (Multi-Map)       | 16.8 ms    | 3.1 MB
Raw Npgsql               | 15.2 ms    | 2.4 MB
```

### Key Takeaways

1. **Raw Npgsql is fastest** but requires the most code
2. **Dapper offers excellent performance** with minimal abstraction cost
3. **EF Core no-tracking queries** are reasonable for most scenarios
4. **Bulk operations** show the biggest performance gaps
5. **Memory allocations** follow a similar pattern to execution time

## Decision Matrix: Which Approach to Use

Here's a practical decision tree for choosing your data access strategy:

### Use EF Core When:

- ✅ Building a new application with evolving requirements
- ✅ Domain-driven design with rich entity models
- ✅ You need migrations and schema management
- ✅ Team is more comfortable with C# than SQL
- ✅ Read/write ratio is balanced
- ✅ Query performance within 20-50% of optimal is acceptable
- ✅ You want change tracking and unit of work pattern

### Use Dapper When:

- ✅ Performance is important but not critical
- ✅ You have complex queries that don't map well to LINQ
- ✅ You're comfortable writing SQL
- ✅ You need fine control over SQL generation
- ✅ Working with existing database schemas
- ✅ Read-heavy workloads with simple writes
- ✅ You want minimal abstraction overhead

### Use Raw Npgsql When:

- ✅ Maximum performance is critical
- ✅ Building high-throughput data processors
- ✅ Working extensively with PostgreSQL-specific features
- ✅ Batch operations and bulk imports
- ✅ Every millisecond and megabyte matters
- ✅ You need absolute control

### Hybrid Approach When:

- ✅ Different parts of application have different needs
- ✅ CQRS pattern (EF Core for writes, Dapper for reads)
- ✅ Most queries use EF Core, but a few need raw SQL
- ✅ You want gradual migration between approaches
- ✅ Large, complex applications with varied requirements

## Real-World Example: Blog Platform Data Access

Let's see how you might structure a real blog platform using a hybrid approach:

```csharp
// Entities (shared between all approaches)
public class BlogPost
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Slug { get; set; }
    public string Content { get; set; }
    public string ContentHash { get; set; }
    public DateTime PublishedDate { get; set; }
    public int CategoryId { get; set; }
    public NpgsqlTsVector SearchVector { get; set; }

    public Category Category { get; set; }
    public List<Comment> Comments { get; set; }
}

// EF Core context for schema management and writes
public class BlogDbContext : DbContext
{
    public DbSet<BlogPost> BlogPosts { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Comment> Comments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("blog");

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.ToTable("blog_posts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.SearchVector).HasMethod("GIN");

            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ContentHash).HasMaxLength(64);

            entity.HasGeneratedTsVectorColumn(
                e => e.SearchVector,
                "english",
                e => new { e.Title, e.Content }
            );

            entity.HasOne(e => e.Category)
                .WithMany(c => c.BlogPosts)
                .HasForeignKey(e => e.CategoryId);
        });
    }
}

// Write repository using EF Core
public class BlogWriteRepository
{
    private readonly BlogDbContext _context;
    private readonly ILogger<BlogWriteRepository> _logger;

    public BlogWriteRepository(BlogDbContext context, ILogger<BlogWriteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CreatePostAsync(BlogPost post)
    {
        _context.BlogPosts.Add(post);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created blog post {PostId} with title {Title}", post.Id, post.Title);

        return post.Id;
    }

    public async Task UpdatePostAsync(BlogPost post)
    {
        var existing = await _context.BlogPosts.FindAsync(post.Id);

        if (existing == null)
            throw new InvalidOperationException($"Post {post.Id} not found");

        existing.Title = post.Title;
        existing.Content = post.Content;
        existing.ContentHash = post.ContentHash;
        existing.CategoryId = post.CategoryId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated blog post {PostId}", post.Id);
    }

    public async Task DeletePostAsync(int id)
    {
        var post = await _context.BlogPosts.FindAsync(id);

        if (post != null)
        {
            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted blog post {PostId}", id);
        }
    }
}

// Read repository using Dapper for performance
public class BlogReadRepository
{
    private readonly string _connectionString;
    private readonly ILogger<BlogReadRepository> _logger;

    public BlogReadRepository(IConfiguration configuration, ILogger<BlogReadRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    public async Task<BlogPostDto> GetPostBySlugAsync(string slug)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var post = await connection.QueryFirstOrDefaultAsync<BlogPostDto>(@"
            SELECT
                p.id,
                p.title,
                p.slug,
                p.content,
                p.published_date,
                c.id as category_id,
                c.name as category_name,
                (SELECT COUNT(*) FROM comments WHERE blog_post_id = p.id) as comment_count
            FROM blog.blog_posts p
            INNER JOIN blog.categories c ON p.category_id = c.id
            WHERE p.slug = @Slug",
            new { Slug = slug }
        );

        if (post != null)
        {
            _logger.LogInformation("Retrieved blog post by slug {Slug}", slug);
        }

        return post;
    }

    public async Task<List<BlogPostSummaryDto>> GetRecentPostsAsync(int page, int pageSize)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var posts = await connection.QueryAsync<BlogPostSummaryDto>(@"
            SELECT
                p.id,
                p.title,
                p.slug,
                LEFT(p.content, 200) as summary,
                p.published_date,
                c.name as category_name
            FROM blog.blog_posts p
            INNER JOIN blog.categories c ON p.category_id = c.id
            ORDER BY p.published_date DESC
            LIMIT @PageSize OFFSET @Offset",
            new { PageSize = pageSize, Offset = (page - 1) * pageSize }
        );

        return posts.ToList();
    }

    public async Task<List<BlogPostDto>> SearchPostsAsync(string searchTerm)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var posts = await connection.QueryAsync<BlogPostDto>(@"
            SELECT
                p.id,
                p.title,
                p.slug,
                p.content,
                p.published_date,
                c.name as category_name,
                ts_rank(p.search_vector, query) as relevance_score
            FROM blog.blog_posts p
            INNER JOIN blog.categories c ON p.category_id = c.id,
                 to_tsquery('english', @SearchTerm) query
            WHERE p.search_vector @@ query
            ORDER BY relevance_score DESC
            LIMIT 50",
            new { SearchTerm = searchTerm }
        );

        _logger.LogInformation("Searched posts with term {SearchTerm}, found {Count} results",
            searchTerm, posts.Count());

        return posts.ToList();
    }
}

// Analytics repository using raw Npgsql for maximum performance
public class BlogAnalyticsRepository
{
    private readonly string _connectionString;

    public BlogAnalyticsRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<List<PostAnalytics>> GetTopPostsAsync(int days, int limit)
    {
        var analytics = new List<PostAnalytics>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(@"
            SELECT
                p.id,
                p.title,
                p.slug,
                COUNT(DISTINCT pv.id) as view_count,
                COUNT(DISTINCT c.id) as comment_count,
                AVG(pv.time_on_page) as avg_time_on_page
            FROM blog.blog_posts p
            LEFT JOIN blog.post_views pv ON p.id = pv.post_id
                AND pv.viewed_at >= NOW() - INTERVAL '@days days'
            LEFT JOIN blog.comments c ON p.id = c.blog_post_id
            WHERE p.published_date >= NOW() - INTERVAL '@days days'
            GROUP BY p.id, p.title, p.slug
            ORDER BY view_count DESC
            LIMIT @limit",
            connection
        );

        command.Parameters.AddWithValue("days", days);
        command.Parameters.AddWithValue("limit", limit);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            analytics.Add(new PostAnalytics
            {
                PostId = reader.GetInt32(0),
                Title = reader.GetString(1),
                Slug = reader.GetString(2),
                ViewCount = reader.GetInt64(3),
                CommentCount = reader.GetInt64(4),
                AverageTimeOnPage = reader.IsDBNull(5) ? 0 : reader.GetDouble(5)
            });
        }

        return analytics;
    }
}

// Service layer orchestrating different repositories
public class BlogService
{
    private readonly BlogWriteRepository _writeRepo;
    private readonly BlogReadRepository _readRepo;
    private readonly BlogAnalyticsRepository _analyticsRepo;

    public BlogService(
        BlogWriteRepository writeRepo,
        BlogReadRepository readRepo,
        BlogAnalyticsRepository analyticsRepo)
    {
        _writeRepo = writeRepo;
        _readRepo = readRepo;
        _analyticsRepo = analyticsRepo;
    }

    // Write operations use EF Core
    public Task<int> CreatePostAsync(BlogPost post) => _writeRepo.CreatePostAsync(post);
    public Task UpdatePostAsync(BlogPost post) => _writeRepo.UpdatePostAsync(post);
    public Task DeletePostAsync(int id) => _writeRepo.DeletePostAsync(id);

    // Read operations use Dapper
    public Task<BlogPostDto> GetPostBySlugAsync(string slug) => _readRepo.GetPostBySlugAsync(slug);
    public Task<List<BlogPostSummaryDto>> GetRecentPostsAsync(int page, int pageSize)
        => _readRepo.GetRecentPostsAsync(page, pageSize);
    public Task<List<BlogPostDto>> SearchPostsAsync(string searchTerm)
        => _readRepo.SearchPostsAsync(searchTerm);

    // Analytics use raw Npgsql
    public Task<List<PostAnalytics>> GetTopPostsAsync(int days, int limit)
        => _analyticsRepo.GetTopPostsAsync(days, limit);
}
```

## PostgreSQL-Specific Considerations

When working with PostgreSQL, certain features deserve special attention:

### Full-Text Search

```csharp
// EF Core
var posts = await _context.BlogPosts
    .Where(p => p.SearchVector.Matches(EF.Functions.ToTsQuery("english", searchTerm)))
    .OrderByDescending(p => p.SearchVector.Rank(EF.Functions.ToTsQuery("english", searchTerm)))
    .ToListAsync();

// Dapper
var posts = await connection.QueryAsync<BlogPost>(@"
    SELECT *, ts_rank(search_vector, query) as rank
    FROM blog_posts,
         to_tsquery('english', @SearchTerm) query
    WHERE search_vector @@ query
    ORDER BY rank DESC",
    new { SearchTerm = searchTerm });
```

### JSON/JSONB Columns

```csharp
// EF Core
modelBuilder.Entity<BlogPost>()
    .Property(p => p.Metadata)
    .HasColumnType("jsonb");

var posts = await _context.BlogPosts
    .Where(p => EF.Functions.JsonContains(p.Metadata, "{\"featured\": true}"))
    .ToListAsync();

// Dapper
var posts = await connection.QueryAsync<BlogPost>(@"
    SELECT * FROM blog_posts
    WHERE metadata @> @Metadata::jsonb",
    new { Metadata = "{\"featured\": true}" });
```

### Arrays

```csharp
// EF Core
modelBuilder.Entity<BlogPost>()
    .Property(p => p.Tags)
    .HasColumnType("text[]");

var posts = await _context.BlogPosts
    .Where(p => p.Tags.Contains("postgresql"))
    .ToListAsync();

// Dapper
var posts = await connection.QueryAsync<BlogPost>(@"
    SELECT * FROM blog_posts
    WHERE @Tag = ANY(tags)",
    new { Tag = "postgresql" });
```

### COPY for Bulk Inserts

```csharp
// Only available with raw Npgsql
public async Task BulkInsertAsync(IEnumerable<BlogPost> posts)
{
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();

    using var writer = await connection.BeginBinaryImportAsync(
        "COPY blog_posts (title, content, published_date) FROM STDIN (FORMAT BINARY)"
    );

    foreach (var post in posts)
    {
        await writer.StartRowAsync();
        await writer.WriteAsync(post.Title);
        await writer.WriteAsync(post.Content);
        await writer.WriteAsync(post.PublishedDate);
    }

    await writer.CompleteAsync();
}
```

## Best Practices Summary

### General Guidelines

1. **Start with EF Core** for new projects unless you have specific performance requirements
2. **Profile before optimizing** - don't assume you need Dapper/raw SQL
3. **Use hybrid approaches** - combine strengths of different tools
4. **Keep data access logic isolated** - repository pattern helps switching implementations
5. **Use connection pooling** - configure properly for your workload
6. **Leverage PostgreSQL features** - don't abstract away powerful database capabilities

### EF Core Specific

1. **Use AsNoTracking()** for read-only queries
2. **Avoid N+1 queries** - use Include() or split queries appropriately
3. **Use compiled queries** for repeated query patterns
4. **Consider AsSplitQuery()** for complex includes
5. **Use batching** for multiple inserts/updates
6. **Project to DTOs** early to reduce data transfer

### Dapper Specific

1. **Use parameterized queries** to prevent SQL injection
2. **Consider query result caching** for expensive, repeated queries
3. **Use multi-mapping** for joins instead of multiple round-trips
4. **Reuse connections** from connection pool
5. **Consider Dapper.Contrib** for simple CRUD operations

### Performance Tips

1. **Index your queries** - analyze slow queries with EXPLAIN ANALYZE
2. **Use prepared statements** for repeated queries
3. **Batch operations** when possible
4. **Use COPY** for bulk inserts in PostgreSQL
5. **Monitor connection pool** - configure min/max pool size
6. **Consider read replicas** for read-heavy workloads

## Conclusion

Choosing the right data access approach for your .NET application with PostgreSQL isn't about finding the "best" tool - it's about matching the right tool to your specific needs:

- **EF Core** excels at rapid development, domain modeling, and applications where developer productivity trumps raw performance
- **Dapper** provides an excellent middle ground with near-optimal performance and reasonable abstraction
- **Raw Npgsql** delivers maximum performance and control for data-intensive operations
- **Mapping libraries** like Mapster and AutoMapper reduce boilerplate when working with lower-level data access

In practice, the most successful applications often use a **hybrid approach**, leveraging the strengths of each tool where appropriate. Use EF Core for your domain logic and writes, Dapper for complex read queries, and raw Npgsql for bulk operations and analytics.

The key is to:

1. **Understand your requirements** - performance, development speed, team skills
2. **Profile your application** - identify actual bottlenecks, not assumed ones
3. **Choose pragmatically** - use the simplest tool that meets your needs
4. **Remain flexible** - you can mix approaches within the same application

Remember: premature optimization is the root of all evil, but so is building a system that can't scale when needed. Start simple, measure performance, and optimize where it matters.

Happy coding with PostgreSQL and .NET! 🐘🚀
