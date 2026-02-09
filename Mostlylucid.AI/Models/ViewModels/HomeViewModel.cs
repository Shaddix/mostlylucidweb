using Mostlylucid.Shared.Models.Blog;

namespace Mostlylucid.AI.Models.ViewModels;

public class HomeViewModel : AIBaseViewModel
{
    public List<PostListModel> RecentArticles { get; set; } = new();
}
