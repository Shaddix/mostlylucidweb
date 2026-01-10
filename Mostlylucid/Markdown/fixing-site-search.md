# Fixing the Site's Search: PostgreSQL Full-Text Search (and Where It Breaks)

<!--category-- ASP.NET, PostgreSQL, Search, RRF -->
<datetime class="hidden">2026-01-14T12:00</datetime>

## Introduction

Search is one of those features that seems simple until you try to build it well. Users expect it to "just work" - but what does "just work" mean? It means handling typos, understanding acronyms, supporting technical terms with special characters (like "ASP.NET"), and providing relevant results even when the exact query doesn't match.

This article is not about building a search engine from scratch, but about fixing the sharp edges of PostgreSQL full-text search in a real production system - chronicling the journey from identifying issues to implementing solutions that handle edge cases and provide Google-style search operators.

The system also integrates semantic vector search using Qdrant and RRF ranking to combine keyword and conceptual matching. This semantic search infrastructure serves dual purposes: powering the user-facing search experience AND providing the retrieval foundation for the [Lawyer GPT](/blog/building-a-lawyer-gpt-for-your-blog-part1) RAG system (a writing assistant that helps draft new posts using past content as a knowledge base). See [Building a "Lawyer GPT" - Part 3](/blog/building-a-lawyer-gpt-for-your-blog-part3) for a deep dive into embeddings and vector databases.

## The Problems

### 1. Empty Results Showed Random Articles

When a search returned no results, the system was showing random old articles instead of helpful suggestions. This violated the principle of least surprise - users expected either relevant results or a clear "no match found" message with recent posts as suggestions. This made it look like the search had worked -  just badly.

**The Fix**: Modified `BlogSearchService.HybridSearchWithPagingAsync()` to detect when search returns zero results and fall back to showing recent posts ordered by date descending. Added a `NoMatchFound` flag to `BasePagingModel` so the UI can display an appropriate message like "No match found. Did you mean one of these?"

```csharp
// No match found - return recent posts as suggestions
if (noMatchFound)
{
    Log.Logger.Information("No search results for '{Query}', returning recent posts as suggestions", query);
    return await GetRecentPostsAsSuggestions(targetLanguage, startDate, endDate, page, pageSize);
}
```

### 2. Acronyms Like "DiSE" Weren't Matching

PostgreSQL's default English text search configuration technically indexes acronyms, but normalization and stemming make short, case-significant terms unreliable in practice. When you search for "DiSE", it gets lowercased to "dise" and then the English dictionary may discard it or assign low weight, causing full-text search to miss articles that clearly contain "DiSE" in the title and content.

**The Fix**: Added acronym detection (terms ≤6 characters with uppercase letters) and case-insensitive substring fallback using PostgreSQL's `ILIKE`. This supplements full-text search rather than replacing it (see Performance Considerations below for why this is acceptable):

```csharp
// Detect if query looks like an acronym or short term
var isAcronymLike = query.Length <= 6 && query.Any(char.IsUpper);

// Add substring search for acronyms
searchQuery = searchQuery.Where(x =>
    EF.Functions.ILike(x.Title, $"%{acronym}%")
    || EF.Functions.ILike(x.PlainTextContent, $"%{acronym}%"));
```

### 3. Technical Terms with Special Characters Broke Search

Searches for "ASP.NET" or "C#" would fail because PostgreSQL's text search parser treats periods and hash symbols as delimiters, splitting "ASP.NET" into "ASP" and "NET" as separate tokens. This meant searching for the exact term wouldn't work.

**The Fix**: Created a `SearchQueryParser` that recognizes common technical terms and replaces them with searchable versions:

```csharp
private static readonly Dictionary<string, string> TechnicalTerms = new(StringComparer.OrdinalIgnoreCase)
{
    ["asp.net"] = "aspnet",
    ["c#"] = "csharp",
    [".net"] = "dotnet",
    ["f#"] = "fsharp",
    ["node.js"] = "nodejs",
    // ... more terms
};
```

### 4. Queries Like "ASP.NET and Alpine" Didn't Work

PostgreSQL treats "and" as a stop word and removes it from searches. Combined with the special character issue, a natural query like "ASP.NET and Alpine" would essentially become a search for "Alpine" only.

**The Fix**: Implemented a Google-style query parser that handles stop words intelligently and supports advanced search operators.

## The Solutions

### Google-Style Search Operators

I implemented a `SearchQueryParser` class that parses queries with support for:

1. **Quoted Phrases**: `"exact match"` - searches for the exact phrase
2. **Excluded Terms**: `-unwanted` - excludes results containing this term
3. **Wildcards**: `ASP*` - matches "ASP", "ASPNET", "ASPNetCore", etc.
4. **Technical Terms**: Automatically handles "ASP.NET", "C#", ".NET" etc.

The parser uses a compiled regex pattern to tokenize the query:

```csharp
[GeneratedRegex(@"""([^""]+)""|(-)?(\S+)", RegexOptions.Compiled)]
private static partial Regex QueryTokenRegex();

public ParsedQuery Parse(string query)
{
    var matches = QueryTokenRegex().Matches(processedQuery);

    foreach (Match match in matches)
    {
        // Quoted phrase
        if (match.Groups[1].Success)
        {
            var phrase = match.Groups[1].Value.Trim();
            result.Phrases.Add(phrase);
            continue;
        }

        // Excluded term (starts with -)
        if (match.Groups[2].Success)
        {
            var term = match.Groups[3].Value.Trim();
            result.ExcludeTerms.Add(term.ToLowerInvariant());
            continue;
        }

        // Regular term or wildcard
        var token = match.Groups[3].Value.Trim();
        if (token.Contains('*'))
        {
            result.WildcardTerms.Add(token.Replace("*", ""));
        }
        else if (!StopWords.Contains(token))
        {
            result.IncludeTerms.Add(token.ToLowerInvariant());
        }
    }
}
```

### Building PostgreSQL tsquery

The parser outputs structured data that's then converted to PostgreSQL's tsquery syntax. We use `to_tsquery` because we're generating structured syntax - not `websearch_to_tsquery` because we've already parsed operators ourselves, giving us more control over how terms are combined.

```csharp
public string BuildTsQuery(ParsedQuery parsed)
{
    var queryParts = new List<string>();

    // Add include terms with AND
    foreach (var term in parsed.IncludeTerms)
    {
        queryParts.Add(term);
    }

    // Add wildcard terms with :* suffix
    foreach (var term in parsed.WildcardTerms)
    {
        queryParts.Add($"{term}:*");
    }

    return queryParts.Count > 0 ? string.Join(" & ", queryParts) : string.Empty;
}
```

> **Why not just use `websearch_to_tsquery`?**
> Because it still fails on technical terms, acronyms, and domain-specific syntax — and offers no hooks for hybrid ranking or fallbacks. By parsing ourselves, we can handle these edge cases before they reach PostgreSQL.

### Enhanced BuildSearchQuery

The `BuildSearchQuery` method was completely rewritten to use the parsed query structure. Note that `baseQuery` already filters by language, date range, and visibility - we're adding search-specific conditions on top:

```csharp
private IOrderedQueryable<BlogPostEntity> BuildSearchQuery(
    string query,
    string language,
    DateTime? startDate,
    DateTime? endDate,
    string order)
{
    var parsed = _queryParser.Parse(query);
    IQueryable<BlogPostEntity> searchQuery = baseQuery;

    // Handle phrases (exact substring matching)
    foreach (var phrase in parsed.Phrases)
    {
        searchQuery = searchQuery.Where(x =>
            EF.Functions.ILike(x.Title, $"%{phrase}%")
            || EF.Functions.ILike(x.PlainTextContent, $"%{phrase}%")
            || x.Categories.Any(c => EF.Functions.ILike(c.Name, $"%{phrase}%")));
    }

    // Build tsquery for include terms and wildcards
    var tsQuery = _queryParser.BuildTsQuery(parsed);

    // Apply full-text search if we have terms
    if (!string.IsNullOrWhiteSpace(tsQuery))
    {
        searchQuery = searchQuery.Where(x =>
            x.SearchVector.Matches(EF.Functions.ToTsQuery("english", tsQuery))
            || x.Categories.Any(c =>
                EF.Functions.ToTsVector("english", c.Name)
                    .Matches(EF.Functions.ToTsQuery("english", tsQuery))));
    }

    // Handle acronyms with case-insensitive substring search
    // This supplements full-text search (additive OR), not replaces it
    var acronymTerms = parsed.IncludeTerms
        .Concat(parsed.WildcardTerms)
        .Where(t => _queryParser.IsAcronymLike(t))
        .ToList();

    foreach (var acronym in acronymTerms)
    {
        searchQuery = searchQuery.Where(x =>
            EF.Functions.ILike(x.Title, $"%{acronym}%")
            || EF.Functions.ILike(x.PlainTextContent, $"%{acronym}%"));
    }

    // Handle excluded terms (must NOT contain these)
    foreach (var excludeTerm in parsed.ExcludeTerms)
    {
        searchQuery = searchQuery.Where(x =>
            !EF.Functions.ILike(x.Title, $"%{excludeTerm}%")
            && !EF.Functions.ILike(x.PlainTextContent, $"%{excludeTerm}%")
            && !x.Categories.Any(c => EF.Functions.ILike(c.Name, $"%{excludeTerm}%")));
    }

    return orderedQuery;
}
```

## Search Examples

Here are some examples of the improved search functionality:

### Basic Search
```
DiSE
```
✅ Now matches articles with "DiSE" in title or content (case-insensitive)

### Technical Terms
```
ASP.NET
```
✅ Finds articles about ASP.NET (automatically converted to "aspnet" for full-text search)

### Phrase Search
```
"semantic search"
```
✅ Finds exact phrase "semantic search" in articles

### Excluded Terms
```
ASP.NET -Core
```
✅ Finds ASP.NET articles but excludes those mentioning "Core"

### Wildcards
```
ASP*
```
✅ Matches "ASP", "ASPNET", "ASPNetCore", etc.

### Complex Query
```
"full text search" PostgreSQL -MySQL
```
✅ Finds articles with exact phrase "full text search" AND mentions of PostgreSQL, but EXCLUDING articles that mention MySQL

### Before and After Example

**Query**: `ASP.NET and Alpine`

**Before** (broken):
- "ASP.NET" split into "ASP" + "NET"
- "and" removed as stop word
- Result: Only searches for "Alpine"

**After** (fixed):
- "ASP.NET" converted to "aspnet"
- "and" intelligently preserved as part of user query intent
- "Alpine" searched normally
- Result: Finds articles about both ASP.NET and Alpine

## RRF Ranking Integration

The search integrates with Reciprocal Rank Fusion (RRF) from the lucidRAG implementation. RRF here is used for *ranking*, not recall - it combines already-retrieved results from BM25 full-text search and vector semantic search:

```csharp
// Fuse using RRF with category/freshness boosts
var fusedDtos = _ranker.FuseResults(bm25Results, vectorResults, query);
```

The RRF algorithm uses `1/(k+rank)` where k=60 to combine results from multiple sources, then applies boosts for:
- **Category match**: +2.0
- **Title match**: +1.0
- **Freshness**: Exponential decay over 1 year (+1.5 max)

For details on how semantic search works with embeddings and vector similarity, see [Building a "Lawyer GPT" - Part 3](/blog/building-a-lawyer-gpt-for-your-blog-part3). The same infrastructure powers both user-facing search and the RAG retrieval for AI-assisted writing.

## Technical Architecture

### Dependency Injection

The new components are registered as services:

```csharp
services.AddSingleton<SearchQueryParser>();
services.AddSingleton<SearchRanker>();
services.AddScoped<BlogSearchService>();
```

`SearchQueryParser` and `SearchRanker` are singletons because they're stateless, thread-safe, and can be reused across requests. `BlogSearchService` is scoped because it accesses the database context.

### Query Flow

1. **User enters search query** → SearchController
2. **Query parsing** → SearchQueryParser breaks down into structured components
3. **Semantic search (if enabled)** → Qdrant vector database with ONNX embeddings
4. **PostgreSQL full-text search (always available)** → BM25-based keyword matching
5. **RRF fusion** → Combines both result sets with category/freshness boosts
6. **No results** → Returns recent posts as suggestions

The same semantic search component is also used by the [Lawyer GPT](/blog/building-a-lawyer-gpt-for-your-blog-part1) system to retrieve relevant past blog posts for AI-assisted writing. See [Part 4](/blog/building-a-lawyer-gpt-for-your-blog-part4) for details on the ingestion pipeline.

### Database Schema

Search relies on a precomputed `SearchVector` column in the `BlogPosts` table:

```sql
ALTER TABLE mostlylucid."BlogPosts"
ADD COLUMN "SearchVector" tsvector
GENERATED ALWAYS AS (
    to_tsvector('english',
        coalesce("Title", '') || ' ' ||
        coalesce("PlainTextContent", '')
    )
) STORED;

CREATE INDEX idx_blog_posts_search_vector
ON mostlylucid."BlogPosts"
USING GIN ("SearchVector");
```

The GIN (Generalized Inverted Index) provides fast full-text search across large text corpora.

## Performance Considerations

### Why ILIKE for Acronyms?

While `ILIKE` (case-insensitive LIKE) is slower than full-text search, it's necessary for acronyms because:
1. Full-text search normalizes/stems terms, breaking short uppercase strings
2. Acronyms are typically short (≤6 characters), limiting the performance impact
3. The condition is only added when needed (detected acronyms)

This approach would not scale to arbitrary substring search across millions of rows — but for targeted acronym fallback it's appropriate.

### Query Optimization

The search builds WHERE clauses incrementally, allowing PostgreSQL's query planner to optimize:

```csharp
IQueryable<BlogPostEntity> searchQuery = baseQuery;

// Each filter is added conditionally
if (parsed.Phrases.Count > 0) { ... }
if (!string.IsNullOrWhiteSpace(tsQuery)) { ... }
if (acronymTerms.Count > 0) { ... }
```

This generates a single optimized SQL query rather than multiple round trips.

## Lessons Learned

1. **Don't assume full-text search handles everything**: Edge cases like acronyms and special characters need special handling.

2. **Combine multiple approaches**: Full-text search (BM25) + semantic search (vectors) + substring fallbacks provides better coverage than any single method.

3. **Google shaped user expectations**: Supporting quoted phrases, exclusions, and wildcards makes search feel natural because users are already familiar with these operators.

4. **Provide helpful fallbacks**: When search fails, don't show nothing - show recent posts and make it clear no match was found.

5. **Parse, don't hack**: A proper query parser is cleaner and more maintainable than a series of string manipulations.

## Future Enhancements

Possible improvements for the future, categorized by concern:

**Retrieval Improvements**:
- **Fuzzy matching**: Levenshtein distance for typo tolerance
- **Synonym expansion**: "blog post" → "article", "tutorial"

**Parsing Enhancements**:
- **Category filtering**: `category:ASP.NET` operator
- **Date range**: `after:2025-01-01` operator

**Ranking Refinements**:
- **Result highlighting**: Show matched portions of text in results
- **Click-through tracking**: Learn from user behavior to improve ranking

**Observability**:
- **Search analytics**: Track popular queries and failed searches to identify gaps

## Conclusion

Building good search is an iterative process. This implementation combines PostgreSQL full-text search with semantic vector search, custom parsing for technical terms, and intelligent fallbacks.

The key insight: no single approach handles all cases. PostgreSQL FTS excels at keyword matching, semantic search handles conceptual queries, and targeted fallbacks catch edge cases. The clean parsing layer ties it together, handling operators and technical terms before they reach the search engines.

The semantic search infrastructure detailed here serves dual purposes:
- **User-facing search**: Combines keyword and semantic matching for better results
- **RAG retrieval**: Powers the [Lawyer GPT](/blog/building-a-lawyer-gpt-for-your-blog-part1) writing assistant

For deeper dives into the underlying technology:
- [Part 3: Embeddings & Vector Databases](/blog/building-a-lawyer-gpt-for-your-blog-part3) - How semantic search works
- [Part 4: Ingestion Pipeline](/blog/building-a-lawyer-gpt-for-your-blog-part4) - Processing and indexing content

All the code for this implementation is available in the [blog's GitHub repository](https://github.com/scottgal/mostlylucidweb).
