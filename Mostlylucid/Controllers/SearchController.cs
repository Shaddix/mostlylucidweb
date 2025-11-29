using System.ComponentModel.DataAnnotations;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Mapper;
using Mostlylucid.Models.Blog;
using Mostlylucid.Models.Search;
using Mostlylucid.Services;
using Mostlylucid.Services.Blog;
using Mostlylucid.Shared.Models;
using Mostlylucid.SemanticSearch.Services;

namespace Mostlylucid.Controllers;

[Route("search")]
public class SearchController(
    BaseControllerService baseControllerService,
    BlogSearchService searchService,
    ISemanticSearchService semanticSearchService,
    ILogger<SearchController> logger)
    : BaseController(baseControllerService, logger)
{
    [HttpGet]
    [Route("")]
    [OutputCache(Duration = 3600, Tags = new[] { "blog" }, VaryByHeaderNames = new[] { "hx-request", "pagerequest" }, VaryByQueryKeys = new[] { "query", "page", "pageSize", "language", "dateRange", "startDate", "endDate", "order" })]
    public async Task<IActionResult> Search(
        string? query,
        int page = 1,
        int pageSize = 10,
        string? language = null,
        DateRangeOption dateRange = DateRangeOption.AllTime,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string order = "date_desc",
        [FromHeader] bool pagerequest = false)
    {
        try
        {
            // Calculate date range based on option
            var (calculatedStartDate, calculatedEndDate) = CalculateDateRange(dateRange, startDate, endDate);

            // Get available languages for the filter dropdown
            var availableLanguages = await searchService.GetAvailableLanguagesAsync();

            if (string.IsNullOrEmpty(query?.Trim()))
            {
                var emptyModel = new SearchResultsModel
                {
                    Query = query,
                    SearchResults = new(),
                    AvailableLanguages = availableLanguages,
                    Filters = new SearchFilters
                    {
                        Language = language,
                        DateRange = dateRange,
                        StartDate = calculatedStartDate,
                        EndDate = calculatedEndDate
                    }
                };
                if (Request.IsHtmx()) return PartialView("SearchResults", emptyModel);
                return View("SearchResults", emptyModel);
            }

            var searchResults = await searchService.HybridSearchWithPagingAsync(
                query,
                language,
                calculatedStartDate,
                calculatedEndDate,
                page,
                pageSize,
                order);

            var searchModel = new SearchResultsModel
            {
                Query = query,
                SearchResults = searchResults.ToPostListViewModel(),
                AvailableLanguages = availableLanguages,
                Filters = new SearchFilters
                {
                    Language = language,
                    DateRange = dateRange,
                    StartDate = calculatedStartDate,
                    EndDate = calculatedEndDate
                }
            };
            searchModel = await PopulateBaseModel(searchModel);

            // Build link URL with current filters
            var linkUrl = Url.Action("Search", "Search", new { query, language, dateRange, startDate, endDate, order });
            searchModel.SearchResults.LinkUrl = linkUrl;

            if (pagerequest && Request.IsHtmx()) return PartialView("_SearchResultsPartial", searchModel.SearchResults);

            if (Request.IsHtmx()) return PartialView("SearchResults", searchModel);
            return View("SearchResults", searchModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query '{Query}'", query);

            // Return empty results instead of 500
            var errorModel = new SearchResultsModel
            {
                Query = query,
                SearchResults = new(),
                AvailableLanguages = new List<string> { "en" },
                Filters = new SearchFilters
                {
                    Language = language,
                    DateRange = dateRange
                }
            };

            if (Request.IsHtmx()) return PartialView("SearchResults", errorModel);
            return View("SearchResults", errorModel);
        }
    }

    private static (DateTime? StartDate, DateTime? EndDate) CalculateDateRange(DateRangeOption dateRange, DateTime? startDate, DateTime? endDate)
    {
        var now = DateTime.UtcNow;
        return dateRange switch
        {
            DateRangeOption.LastWeek => (now.AddDays(-7), now),
            DateRangeOption.LastMonth => (now.AddMonths(-1), now),
            DateRangeOption.LastYear => (now.AddYears(-1), now),
            DateRangeOption.Custom => (startDate, endDate),
            _ => (null, null) // AllTime - no date filter
        };
    }

    [HttpGet]
    [Route("{query}")]
    public  IActionResult InitialSearch([FromRoute] string query)
    {
        return RedirectToAction("Search", new { query });
    }

    [HttpGet]
    [Route("semantic")]
    [OutputCache(Duration = 3600, Tags = new[] { "blog" }, VaryByQueryKeys = new[] {"query", "limit"})]
    public async Task<IActionResult> SemanticSearch(string? query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query cannot be empty");
        }

        var results = await semanticSearchService.SearchAsync(query, limit);

        if (Request.IsHtmx())
        {
            return PartialView("_SemanticSearchResults", results);
        }

        return Json(results);
    }

    [HttpGet]
    [Route("related/{slug}/{language}")]
    [OutputCache(Duration = 7200, Tags = new[] { "blog" }, VaryByRouteValueNames = new[] {"slug", "language"})]
    public async Task<IActionResult> RelatedPosts(string slug, string language, int limit = 5)
    {
        var semanticResults = await semanticSearchService.GetRelatedPostsAsync(slug, limit);

        // Enrich with details from PostgreSQL
        var enrichedResults = new List<RelatedPostViewModel>();
        if (semanticResults.Count > 0)
        {
            var slugs = semanticResults.Select(r => r.Slug).ToList();
            var posts = await BlogViewService.GetPostsBySlugAsync(slugs, language);

            foreach (var sr in semanticResults)
            {
                var post = posts.FirstOrDefault(p => p.Slug == sr.Slug);
                if (post != null)
                {
                    enrichedResults.Add(new RelatedPostViewModel
                    {
                        Slug = sr.Slug,
                        Title = post.Title,
                        PublishedDate = post.PublishedDate,
                        Categories = post.Categories,
                        Score = sr.Score
                    });
                }
            }
        }

        if (Request.IsHtmx())
        {
            return PartialView("_RelatedPosts", enrichedResults);
        }

        return Json(enrichedResults);
    }

}