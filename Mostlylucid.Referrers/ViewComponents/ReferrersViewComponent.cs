using Microsoft.AspNetCore.Mvc;
using Mostlylucid.Referrers.Models;
using Mostlylucid.Referrers.Services;

namespace Mostlylucid.Referrers.ViewComponents;

/// <summary>
/// ViewComponent for displaying referrers to a blog post
/// </summary>
public class ReferrersViewComponent : ViewComponent
{
    private readonly IReferrerService _referrerService;

    public ReferrersViewComponent(IReferrerService referrerService)
    {
        _referrerService = referrerService;
    }

    /// <summary>
    /// Renders the referrers for a specific post
    /// </summary>
    /// <param name="postSlug">The slug of the blog post</param>
    /// <param name="showEmpty">Whether to render anything when there are no referrers</param>
    public async Task<IViewComponentResult> InvokeAsync(string postSlug, bool showEmpty = false)
    {
        var referrers = await _referrerService.GetReferrersForPostAsync(postSlug);

        if (!showEmpty && referrers.Referrers.Count == 0)
        {
            return Content(string.Empty);
        }

        return View(referrers);
    }
}

/// <summary>
/// ViewComponent for displaying top referrers across all posts
/// </summary>
public class TopReferrersViewComponent : ViewComponent
{
    private readonly IReferrerService _referrerService;

    public TopReferrersViewComponent(IReferrerService referrerService)
    {
        _referrerService = referrerService;
    }

    /// <summary>
    /// Renders the top referrers across all posts
    /// </summary>
    /// <param name="limit">Maximum number of referrers to display</param>
    public async Task<IViewComponentResult> InvokeAsync(int limit = 10)
    {
        var referrers = await _referrerService.GetTopReferrersAsync(limit);
        return View(referrers);
    }
}
