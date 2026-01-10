using Mostlylucid.Models.Blog;
using Mostlylucid.Shared.Entities;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.Models.Search;

public class SearchResultsModel : BaseViewModel
{
    public string? Query { get; set; }
    public PostListViewModel SearchResults { get; set; } = new();

    // Filter options
    public SearchFilters Filters { get; set; } = new();
    public List<string> AvailableLanguages { get; set; } = new();
    public List<CategoryWithCount> AllCategories { get; set; } = new();
}

public class SearchFilters
{
    public string? Language { get; set; }
    public DateRangeOption DateRange { get; set; } = DateRangeOption.AllTime;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public enum DateRangeOption
{
    AllTime,
    LastWeek,
    LastMonth,
    LastYear,
    Custom
}