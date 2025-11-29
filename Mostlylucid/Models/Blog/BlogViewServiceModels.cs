using Mostlylucid.Services.Markdown;

namespace Mostlylucid.Models.Blog;

/// <summary>
/// Request model for index page.
/// </summary>
public record BlogIndexRequest(
    int Page = 1,
    int PageSize = 20,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string Language = MarkdownBaseService.EnglishLanguage,
    string OrderBy = "date",
    string OrderDir = "desc",
    string? Order = null,
    string? Category = null);

/// <summary>
/// Result model for index page.
/// </summary>
public class BlogIndexResult
{
    public PostListViewModel Posts { get; set; } = new();
    public bool ShouldRedirectToSinglePost { get; set; }
    public string? SinglePostSlug { get; set; }
    public string? SinglePostLanguage { get; set; }
}

/// <summary>
/// Result model for date range.
/// </summary>
public record DateRangeResult(string MinDate, string MaxDate);

/// <summary>
/// User info for populating view models.
/// </summary>
public record UserInfo(bool LoggedIn, bool IsAdmin, string? Name, string? Email, string? AvatarUrl);
