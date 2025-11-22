using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.Controllers;
using Mostlylucid.Mapper;
using Mostlylucid.Models.Comments;
using Mostlylucid.Services;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Shared.Config.Markdown;
using MarkdownBaseService = Mostlylucid.Services.Markdown.MarkdownBaseService;

namespace Mostlylucidblog.Controllers;

[Route("blog")]
public class BlogController(BaseControllerService baseControllerService,
    IBlogViewService blogViewService,
    CommentViewService commentViewService,
    MarkdownRenderingService markdownRenderingService,
    MarkdownConfig markdownConfig,
    ILogger<BlogController> logger) : BaseController(baseControllerService, logger)
{
    // Temporarily disabled for development - re-enable for production
    [ResponseCache(Duration = 300, VaryByHeader = "hx-request", VaryByQueryKeys = new[] { "page", "pageSize", nameof(startDate), nameof(endDate), nameof(language), nameof(orderBy), nameof(orderDir) },
        Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request" },
        VaryByQueryKeys = new[] { nameof(page), nameof(pageSize), nameof(startDate), nameof(endDate), nameof(language), nameof(orderBy), nameof(orderDir) })]
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20, DateTime? startDate = null, DateTime? endDate = null,
        string language = MarkdownBaseService.EnglishLanguage, string orderBy = "date", string orderDir = "desc")
    {
        var posts = await blogViewService.GetPagedPosts(page, pageSize, language: language, startDate: startDate, endDate: endDate);
        // Set LinkUrl to preserve filters and options if present
        posts.LinkUrl = Url.Action("Index", "Blog", new { startDate, endDate, language, orderBy, orderDir });
        if (Request.IsHtmx()) return PartialView("_BlogSummaryList", posts);
        return View("Index", posts);
    }

    [HttpGet("calendar-days")]
    [ResponseCache(Duration = 300, VaryByHeader = "hx-request", VaryByQueryKeys = new[] { nameof(year), nameof(month), nameof(language) }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = 1800, VaryByHeaderNames = new[] { "hx-request" }, VaryByQueryKeys = new[] { nameof(year), nameof(month), nameof(language) })]
    public async Task<IActionResult> CalendarDays(int year, int month, string language = MarkdownBaseService.EnglishLanguage)
    {
        if (year < 2000 || month < 1 || month > 12) return BadRequest("Invalid year or month");
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var posts = await blogViewService.GetPostsForRange(start, end, language: language);
        if (posts is null) return Json("");
        var dates = posts
            .Select(p => p.PublishedDate.Date)
            .Distinct()
            .OrderBy(d => d)
            .Select(d => d.ToString("yyyy-MM-dd"))
            .ToList();
        return Json(new { dates });
    }

    [HttpGet("date-range")]
    [ResponseCache(Duration = 3600, VaryByHeader = "hx-request", VaryByQueryKeys = new[] { nameof(language) }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = 7200, VaryByHeaderNames = new[] { "hx-request" }, VaryByQueryKeys = new[] { nameof(language) })]
    public async Task<IActionResult> DateRange(string language = MarkdownBaseService.EnglishLanguage)
    {
        var allPosts = await blogViewService.GetAllPosts();
        if (allPosts is null || !allPosts.Any())
        {
            // Return default range if no posts
            return Json(new
            {
                minDate = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd"),
                maxDate = DateTime.UtcNow.ToString("yyyy-MM-dd")
            });
        }

        // Filter by language if specified
        var posts = allPosts;
        if (!string.IsNullOrEmpty(language) && language != MarkdownBaseService.EnglishLanguage)
        {
            posts = allPosts.Where(p => p.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // If no posts in that language, fall back to all posts
        if (!posts.Any())
        {
            posts = allPosts;
        }

        var minDate = posts.Min(p => p.PublishedDate.Date);
        var maxDate = posts.Max(p => p.PublishedDate.Date);

        return Json(new
        {
            minDate = minDate.ToString("yyyy-MM-dd"),
            maxDate = maxDate.ToString("yyyy-MM-dd")
        });
    }

    [Route("{slug}")]
    [HttpGet]
    // Temporarily disabled for development - re-enable for production
    [ResponseCache(Duration = 300, VaryByHeader = "hx-request",
        VaryByQueryKeys = new[] { nameof(slug), nameof(language) }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request" },
        VaryByQueryKeys = new[] { nameof(slug), nameof(language) })]
    public async Task<IActionResult> Show(string slug, string language = MarkdownBaseService.EnglishLanguage)
    {
        if(slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            slug = slug[..^3];
        
        // Normalize slug: convert underscores/spaces to hyphens and lowercase for consistent lookup
        slug = slug.Replace('_', '-').Replace(' ', '-').ToLowerInvariant();
        
        var post = await blogViewService.GetPost(slug, language);
        if (post == null) return NotFound();

        var user = await GetUserInfo();
        post.Authenticated = user.LoggedIn;
        post.Name = user.Name;
        post.Email = user.Email;
        post.AvatarUrl = user.AvatarUrl;
        var commentViewList = new CommentViewList
        {
            PostId = int.Parse(post.Id),
            IsAdmin = user.IsAdmin
        };

        if (user.IsAdmin)
            commentViewList.Comments = await commentViewService.GetAllComments(int.Parse(post.Id));
        else
            commentViewList.Comments = await commentViewService.GetApprovedComments(int.Parse(post.Id));

        commentViewList.Comments.ForEach(x => x.IsAdmin = user.IsAdmin);
        post.Comments = commentViewList;

        // Determine previous (newer) and next (older) posts within the same language
        var allInLanguage = await blogViewService.GetPostsForLanguage(language: post.Language);
        if (allInLanguage?.Any() == true)
        {
            var index = allInLanguage.FindIndex(p => p.Slug.Equals(post.Slug, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                // previous in time (newer) would be at index - 1 because list is ordered DESC (newest first)
                if (index - 1 >= 0)
                    post.PreviousPost = allInLanguage[index - 1];
                // next in time (older) would be at index + 1
                if (index + 1 < allInLanguage.Count)
                    post.NextPost = allInLanguage[index + 1];
            }
        }

        if (Request.IsHtmx()) return PartialView("_PostPartial", post);
        return View("Post", post);
    }

    [Route("category/{category}")]
    [HttpGet]
    // Temporarily disabled for development - re-enable for production
    [ResponseCache(Duration = 300, VaryByHeader = "hx-request",
        VaryByQueryKeys = new[] { nameof(category), nameof(page), nameof(pageSize) },
        Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request" },
        VaryByQueryKeys = new[] { nameof(category), nameof(page), nameof(pageSize) })]
    public async Task<IActionResult> Category(string category, int page = 1, int pageSize = 10)
    {
        ViewBag.Category = category;
        var posts = await blogViewService.GetPostsByCategory(category, page, pageSize);
        var user = await GetUserInfo();
        posts.Authenticated = user.LoggedIn;
        posts.Name = user.Name;
        posts.AvatarUrl = user.AvatarUrl;
        posts.LinkUrl = Url.Action("Category", "Blog");
        ViewBag.Title = category + " - Blog";
        if (Request.IsHtmx()) return PartialView("_BlogSummaryList", posts);
        return View("Index", posts);
    }

    [Route("language/{slug}/{language}")]
    [HttpGet]
    public async Task<IActionResult> Compat(string slug, string language)
    {
        return await Show(slug, language);
    }


    [Route("{language}/{slug}")]
    [HttpGet]
    // Temporarily disabled for development - re-enable for production
    [ResponseCache(Duration = 300, VaryByHeader = "hx-request",
        VaryByQueryKeys = new[] { nameof(slug), nameof(language) }, Location = ResponseCacheLocation.Any)]
    [OutputCache(Duration = 3600, VaryByHeaderNames = new[] { "hx-request" },
        VaryByQueryKeys = new[] { nameof(slug), nameof(language) })]
    public async Task<IActionResult> Language(string slug, string language)
    {

        return await Show(slug, language);
    }

    /// <summary>
    /// View a draft post - NOT indexed, NOT cached, NOT listed anywhere.
    /// For author preview only.
    /// </summary>
    [Route("drafts/{slug}")]
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Draft(string slug)
    {
        if (slug.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            slug = slug[..^3];

        slug = slug.Replace('_', '-').Replace(' ', '-').ToLowerInvariant();

        var draftsPath = Path.Combine(markdownConfig.MarkdownPath, "drafts");
        var filePath = Path.Combine(draftsPath, $"{slug}.md");

        if (!System.IO.File.Exists(filePath))
            return NotFound($"Draft '{slug}' not found");

        try
        {
            var markdown = await System.IO.File.ReadAllTextAsync(filePath);
            var fileInfo = new FileInfo(filePath);
            var blogPost = markdownRenderingService.GetPageFromMarkdown(markdown, fileInfo.LastWriteTimeUtc, filePath);

            var post = blogPost.ToViewModel();
            post.Categories = blogPost.Categories.ToArray();

            // Mark as draft for UI indication
            ViewBag.IsDraft = true;

            if (Request.IsHtmx()) return PartialView("_PostPartial", post);
            return View("Post", post);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading draft {Slug}", slug);
            return StatusCode(500, "Error reading draft");
        }
    }
}