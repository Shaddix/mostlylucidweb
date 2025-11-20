using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Models.Error;
using Mostlylucid.Services;
using Mostlylucid.Services.Blog;

namespace Mostlylucid.Controllers;

public class ErrorController(
    BaseControllerService baseControllerService,
    ILogger<ErrorController> logger,
    ISlugSuggestionService? slugSuggestionService = null) : BaseController(baseControllerService, logger)
{
    [Route("/error/{statusCode}")]
    [HttpGet]
    public async Task<IActionResult> HandleError(int statusCode, CancellationToken cancellationToken = default)
    {
        // Retrieve the original request information
        var statusCodeReExecuteFeature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

        if (statusCodeReExecuteFeature != null)
        {
            // Access the original path and query string that caused the error
            var originalPath = statusCodeReExecuteFeature.OriginalPath;
            var originalQueryString = statusCodeReExecuteFeature.OriginalQueryString;

            // Optionally log the original URL or pass it to the view
            ViewData["OriginalUrl"] = $"{originalPath}{originalQueryString}";
            ViewData["StatusCode"] = statusCode;
        }

        // Handle specific status codes and return corresponding views
        switch (statusCode)
        {
            case 404:
                var model = await CreateNotFoundModel(statusCodeReExecuteFeature, cancellationToken);
                return View("NotFound", model);
            case 500:
                return View("ServerError");
            default:
                return View("Error");
        }
    }

    private async Task<NotFoundModel> CreateNotFoundModel(
        IStatusCodeReExecuteFeature? statusCodeReExecuteFeature,
        CancellationToken cancellationToken)
    {
        var model = new NotFoundModel
        {
            OriginalPath = statusCodeReExecuteFeature?.OriginalPath ?? string.Empty,
            Suggestions = new List<Mostlylucid.Shared.Models.Blog.PostListModel>()
        };

        if (slugSuggestionService == null || string.IsNullOrWhiteSpace(model.OriginalPath))
        {
            return model;
        }

        try
        {
            // Extract slug from path (e.g., /blog/my-post-slug or /blog/en/my-post-slug)
            var pathSegments = model.OriginalPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            string? slug = null;
            var language = "en"; // Default language

            // Pattern: /blog/{slug} or /blog/{language}/{slug}
            if (pathSegments.Length >= 2 && pathSegments[0].Equals("blog", StringComparison.OrdinalIgnoreCase))
            {
                if (pathSegments.Length == 2)
                {
                    // /blog/{slug}
                    slug = pathSegments[1];
                }
                else if (pathSegments.Length >= 3)
                {
                    // /blog/{language}/{slug}
                    language = pathSegments[1];
                    slug = pathSegments[2];
                }
            }

            if (!string.IsNullOrWhiteSpace(slug))
            {
                logger.LogInformation("Searching for suggestions for slug: {Slug} in language: {Language}", slug, language);
                model.Suggestions = await slugSuggestionService.GetSlugSuggestionsAsync(slug, language, 5, cancellationToken);
                logger.LogInformation("Found {Count} suggestions for slug: {Slug}", model.Suggestions.Count, slug);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating slug suggestions for path: {Path}", model.OriginalPath);
        }

        return model;
    }
}