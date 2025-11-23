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
                // Check for high-confidence auto-redirect for first-time typos
                var autoRedirectResult = await TryAutoRedirectAsync(statusCodeReExecuteFeature, cancellationToken);
                if (autoRedirectResult != null)
                {
                    return autoRedirectResult;
                }

                var model = await CreateNotFoundModel(statusCodeReExecuteFeature, cancellationToken);
                return View("NotFound", model);
            case 500:
                return View("ServerError");
            default:
                return View("Error");
        }
    }

    private async Task<IActionResult?> TryAutoRedirectAsync(
        IStatusCodeReExecuteFeature? statusCodeReExecuteFeature,
        CancellationToken cancellationToken)
    {
        if (slugSuggestionService == null || statusCodeReExecuteFeature == null)
        {
            return null;
        }

        var originalPath = statusCodeReExecuteFeature.OriginalPath ?? string.Empty;
        var pathSegments = originalPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Only handle blog posts: /blog/{slug} or /blog/{language}/{slug}
        if (pathSegments.Length < 2 || !pathSegments[0].Equals("blog", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string slug;
        var language = "en";

        if (pathSegments.Length == 2)
        {
            slug = pathSegments[1];
        }
        else if (pathSegments.Length >= 3)
        {
            language = pathSegments[1];
            slug = pathSegments[2];
        }
        else
        {
            return null;
        }

        try
        {
            // First check for learned redirects (user previously clicked a suggestion)
            // These get 301 Permanent Redirect as they're confirmed patterns
            var learnedTargetSlug = await slugSuggestionService.GetAutoRedirectSlugAsync(slug, language, cancellationToken);

            if (!string.IsNullOrWhiteSpace(learnedTargetSlug))
            {
                var redirectUrl = language == "en"
                    ? $"/blog/{learnedTargetSlug}"
                    : $"/blog/{language}/{learnedTargetSlug}";

                logger.LogInformation(
                    "Learned auto-redirect (301): {OriginalPath} -> {RedirectUrl}",
                    originalPath, redirectUrl);

                return RedirectPermanent(redirectUrl);
            }

            // Then check for high-confidence first-time matches
            // These get 302 Temporary Redirect until the pattern is confirmed by user clicks
            var firstTimeTargetSlug = await slugSuggestionService.GetFirstTimeAutoRedirectSlugAsync(slug, language, cancellationToken);

            if (!string.IsNullOrWhiteSpace(firstTimeTargetSlug))
            {
                var redirectUrl = language == "en"
                    ? $"/blog/{firstTimeTargetSlug}"
                    : $"/blog/{language}/{firstTimeTargetSlug}";

                logger.LogInformation(
                    "First-time auto-redirect (302): {OriginalPath} -> {RedirectUrl}",
                    originalPath, redirectUrl);

                return Redirect(redirectUrl);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for auto-redirect for path: {Path}", originalPath);
        }

        return null;
    }

    private async Task<NotFoundModel> CreateNotFoundModel(
        IStatusCodeReExecuteFeature? statusCodeReExecuteFeature,
        CancellationToken cancellationToken)
    {
        var model = new NotFoundModel
        {
            OriginalPath = statusCodeReExecuteFeature?.OriginalPath ?? string.Empty,
            SuggestionsWithScores = new List<Mostlylucid.Models.Error.SuggestionWithScore>()
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
                var suggestionsWithScores = await slugSuggestionService.GetSuggestionsWithScoreAsync(slug, language, 5, cancellationToken);
                model.SuggestionsWithScores = suggestionsWithScores.Select(s => new Mostlylucid.Models.Error.SuggestionWithScore
                {
                    Post = s.Post,
                    Score = s.Score
                }).ToList();
                logger.LogInformation("Found {Count} suggestions for slug: {Slug}", model.SuggestionsWithScores.Count, slug);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating slug suggestions for path: {Path}", model.OriginalPath);
        }

        return model;
    }
}