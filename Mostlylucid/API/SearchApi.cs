using System.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.SemanticSearch.Config;
using Mostlylucid.SemanticSearch.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Markdown;
using Serilog.Events;
using Umami.Net.Models;

namespace Mostlylucid.API;

[ApiController]
[Route("api")]
public class SearchApi(
    BlogSearchService searchService,
    UmamiBackgroundSender umamiBackgroundSender,
    SearchService indexService,
    ISemanticSearchService semanticSearchService,
    SemanticSearchConfig semanticSearchConfig) : ControllerBase
{
    private const int RrfConstant = 60; // Reciprocal Rank Fusion constant

    [HttpGet]
    [Route("osearch/{query}")]
    [ValidateAntiForgeryToken]
    public async Task<JsonHttpResult<List<BlogSearchService.SearchResults>>> OpenSearch(string query,
        string language = MarkdownBaseService.EnglishLanguage)
    {
        var results = await indexService.GetSearchResults(language, query);

        var host = Request.Host.Value;
        var output = results.Select(x => new BlogSearchService.SearchResults(x.Title.Trim(), x.Slug,
            Url.ActionLink("Show", "Blog", new { slug = x.Slug }, "https", host))).ToList();
        return TypedResults.Json(output);
    }

    [HttpGet]
    [Route("search/{query}")]
    [OutputCache(Duration = 3600, VaryByQueryKeys = new[] { "query" })]
    public async Task<Results<JsonHttpResult<List<BlogSearchService.SearchResults>>, BadRequest<string>>>
        Search(string query)
    {
        using var activity = Log.Logger.StartActivity("Search {query}", query);
        try
        {
            var host = Request.Host.Value;
            List<BlogSearchService.SearchResults> output;

            // Use hybrid search if semantic search is enabled
            if (semanticSearchConfig.Enabled)
            {
                output = await HybridSearchAsync(query, host);
            }
            else
            {
                // Fallback to full-text search only
                output = await FullTextSearchAsync(query, host);
            }

            var encodedQuery = HttpUtility.UrlEncode(query);
            await umamiBackgroundSender.Track("searchEvent", new UmamiEventData { { "query", encodedQuery } });

            activity?.Activity?.SetTag("Results", output.Count);
            activity?.Complete();
            return TypedResults.Json(output);
        }
        catch (Exception e)
        {
            activity.Complete(LogEventLevel.Error, e);
            Log.Error(e, "Error in search");
            return TypedResults.BadRequest("Error in search");
        }
    }

    private async Task<List<BlogSearchService.SearchResults>> FullTextSearchAsync(string query, string host)
    {
        List<(string Title, string Slug)> posts;
        if (!query.Contains(' '))
            posts = await searchService.GetSearchResultForComplete(query);
        else
            posts = await searchService.GetSearchResultForQuery(query);

        return posts.Select(x => new BlogSearchService.SearchResults(
            x.Title.Trim(),
            x.Slug,
            Url.ActionLink("Show", "Blog", new { slug = x.Slug }, "https", host))).ToList();
    }

    private async Task<List<BlogSearchService.SearchResults>> HybridSearchAsync(string query, string host)
    {
        // Run both searches in parallel
        var fullTextTask = GetFullTextResultsAsync(query);
        var semanticTask = semanticSearchService.SearchAsync(query, limit: 20);

        await Task.WhenAll(fullTextTask, semanticTask);

        var fullTextResults = await fullTextTask;
        var semanticResults = await semanticTask;

        // Apply Reciprocal Rank Fusion to combine results
        var rrfScores = new Dictionary<string, (double Score, string Title, string Slug)>();

        // Score full-text results
        for (int i = 0; i < fullTextResults.Count; i++)
        {
            var (title, slug) = fullTextResults[i];
            var key = slug.ToLowerInvariant();
            var rrfScore = 1.0 / (RrfConstant + i + 1);

            if (rrfScores.TryGetValue(key, out var existing))
            {
                rrfScores[key] = (existing.Score + rrfScore, title, slug);
            }
            else
            {
                rrfScores[key] = (rrfScore, title, slug);
            }
        }

        // Score semantic results
        for (int i = 0; i < semanticResults.Count; i++)
        {
            var result = semanticResults[i];
            var key = result.Slug.ToLowerInvariant();
            var rrfScore = 1.0 / (RrfConstant + i + 1);

            if (rrfScores.TryGetValue(key, out var existing))
            {
                rrfScores[key] = (existing.Score + rrfScore, existing.Title, existing.Slug);
            }
            else
            {
                rrfScores[key] = (rrfScore, result.Title, result.Slug);
            }
        }

        // Sort by combined RRF score and return top results
        return rrfScores.Values
            .OrderByDescending(x => x.Score)
            .Take(15)
            .Select(x => new BlogSearchService.SearchResults(
                x.Title.Trim(),
                x.Slug,
                Url.ActionLink("Show", "Blog", new { slug = x.Slug }, "https", host)))
            .ToList();
    }

    private async Task<List<(string Title, string Slug)>> GetFullTextResultsAsync(string query)
    {
        if (!query.Contains(' '))
            return await searchService.GetSearchResultForComplete(query);
        else
            return await searchService.GetSearchResultForQuery(query);
    }
}