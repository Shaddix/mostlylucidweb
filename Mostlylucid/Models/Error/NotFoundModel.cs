using Mostlylucid.Shared.Models.Blog;

namespace Mostlylucid.Models.Error;

public class NotFoundModel
{
    public string OriginalPath { get; set; } = string.Empty;
    public List<SuggestionWithScore> SuggestionsWithScores { get; set; } = new();

    // Convenience property for backwards compatibility
    public List<PostListModel> Suggestions => SuggestionsWithScores.Select(s => s.Post).ToList();
}

public class SuggestionWithScore
{
    public PostListModel Post { get; set; } = new();
    public double Score { get; set; }
}