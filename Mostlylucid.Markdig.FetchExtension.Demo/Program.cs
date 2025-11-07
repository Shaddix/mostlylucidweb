using Markdig;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Markdig.FetchExtension.Processors;
using Mostlylucid.Markdig.FetchExtension.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddInMemoryMarkdownFetch();

var app = builder.Build();

FetchMarkdownExtension.ConfigureServiceProvider(app.Services);

// Order matters: UseDefaultFiles must come before UseStaticFiles
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/render", ([FromBody] RenderRequest request, IServiceProvider sp) =>
{
    var preprocessor = new MarkdownFetchPreprocessor(sp);
    var processed = preprocessor.Preprocess(request.Markdown);

    // v1.1.0: UseToc() MUST be last in the pipeline!
    var pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseToc()  // MUST be last!
        .Build();

    var html = Markdown.ToHtml(processed, pipeline);
    return Results.Json(new { html });
});

app.MapGet("/cache", (IServiceProvider sp) =>
{
    var fetchService = sp.GetRequiredService<IMarkdownFetchService>();
    if (fetchService is Mostlylucid.Markdig.FetchExtension.Storage.ICacheInspector inspector)
    {
        var entries = inspector.GetAllCachedEntries();
        return Results.Json(entries);
    }
    return Results.Json(new[] { new { error = "Cache inspection not supported by this storage provider" } });
});

app.Run();

record RenderRequest(string Markdown);
