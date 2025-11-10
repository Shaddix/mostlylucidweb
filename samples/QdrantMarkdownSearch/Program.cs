using System.Diagnostics;
using Grpc.Net.Client;
using Qdrant.Client;
using QdrantMarkdownSearch.Models;
using QdrantMarkdownSearch.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Qdrant client with gRPC
var qdrantEndpoint = builder.Configuration["Qdrant:Endpoint"] ?? "http://localhost:6333";
builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var channel = GrpcChannel.ForAddress(qdrantEndpoint);
    return new QdrantClient(channel);
});

// Register services
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<IVectorSearchService, QdrantVectorSearchService>();

// Register the background indexing service
builder.Services.AddHostedService<MarkdownIndexingService>();

var app = builder.Build();

// Simple home page with instructions
app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <title>Qdrant Markdown Search</title>
        <style>
            body { font-family: system-ui; max-width: 800px; margin: 50px auto; padding: 20px; line-height: 1.6; }
            h1 { color: #2563eb; }
            .endpoint { background: #f3f4f6; padding: 10px; border-radius: 5px; margin: 10px 0; }
            code { background: #e5e7eb; padding: 2px 6px; border-radius: 3px; }
            .search-box { margin: 20px 0; }
            input[type="text"] { width: 100%; padding: 12px; font-size: 16px; border: 2px solid #e5e7eb; border-radius: 5px; }
            button { background: #2563eb; color: white; padding: 12px 24px; border: none; border-radius: 5px; cursor: pointer; font-size: 16px; }
            button:hover { background: #1d4ed8; }
            .results { margin-top: 20px; }
            .result { border: 1px solid #e5e7eb; padding: 15px; margin: 10px 0; border-radius: 5px; }
            .result h3 { margin-top: 0; color: #1f2937; }
            .score { color: #6b7280; font-size: 14px; }
        </style>
    </head>
    <body>
        <h1>🔍 Qdrant Markdown Search Sample</h1>
        <p>A self-hosted semantic search engine powered by Qdrant and Ollama.</p>

        <h2>Try It Out</h2>
        <div class="search-box">
            <input type="text" id="searchInput" placeholder="Search for anything in the markdown files..." />
            <button onclick="search()">Search</button>
        </div>

        <div id="results" class="results"></div>

        <h2>Available Endpoints</h2>
        <div class="endpoint">
            <strong>GET /api/search</strong><br>
            Search the indexed documents<br>
            Query params: <code>query</code> (required), <code>limit</code> (optional, default 10)
        </div>

        <div class="endpoint">
            <strong>GET /api/stats</strong><br>
            Get indexing statistics
        </div>

        <h2>How It Works</h2>
        <ol>
            <li>Markdown files are read from the <code>MarkdownDocs</code> directory</li>
            <li>Ollama generates embeddings (768-dimensional vectors) locally</li>
            <li>Qdrant stores the vectors and metadata</li>
            <li>Search queries are converted to vectors and matched by semantic similarity</li>
        </ol>

        <p>
            <strong>100% self-hosted, 0% API costs!</strong><br>
            All processing happens on your machine.
        </p>

        <script>
            async function search() {
                const query = document.getElementById('searchInput').value;
                if (!query) return;

                const resultsDiv = document.getElementById('results');
                resultsDiv.innerHTML = '<p>Searching...</p>';

                try {
                    const response = await fetch(`/api/search?query=${encodeURIComponent(query)}&limit=5`);
                    const data = await response.json();

                    if (data.results.length === 0) {
                        resultsDiv.innerHTML = '<p>No results found.</p>';
                        return;
                    }

                    let html = `<h3>Found ${data.count} results (${data.searchTimeMs}ms)</h3>`;

                    data.results.forEach(result => {
                        const preview = result.content.substring(0, 200) + '...';
                        html += `
                            <div class="result">
                                <h3>${result.title}</h3>
                                <p>${preview}</p>
                                <p class="score">Score: ${(result.score * 100).toFixed(1)}% | File: ${result.fileName}</p>
                            </div>
                        `;
                    });

                    resultsDiv.innerHTML = html;
                } catch (error) {
                    resultsDiv.innerHTML = '<p style="color: red;">Error: ' + error.message + '</p>';
                }
            }

            document.getElementById('searchInput').addEventListener('keypress', (e) => {
                if (e.key === 'Enter') search();
            });
        </script>
    </body>
    </html>
    """, "text/html"));

// Search API endpoint
app.MapGet("/api/search", async (
    string query,
    int limit,
    IVectorSearchService vectorSearch,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.BadRequest(new { error = "Query parameter is required" });
    }

    limit = limit <= 0 ? 10 : Math.Min(limit, 50); // Cap at 50 results

    var sw = Stopwatch.StartNew();
    var results = await vectorSearch.SearchAsync(query, limit, ct);
    sw.Stop();

    var response = new SearchResponse
    {
        Query = query,
        Results = results,
        Count = results.Count,
        SearchTimeMs = sw.ElapsedMilliseconds
    };

    return Results.Ok(response);
});

// Stats endpoint
app.MapGet("/api/stats", async (IVectorSearchService vectorSearch, CancellationToken ct) =>
{
    var count = await vectorSearch.GetDocumentCountAsync(ct);

    return Results.Ok(new
    {
        documentCount = count,
        collectionName = builder.Configuration["Qdrant:CollectionName"],
        vectorSize = builder.Configuration["Qdrant:VectorSize"],
        embeddingModel = builder.Configuration["Ollama:Model"]
    });
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
