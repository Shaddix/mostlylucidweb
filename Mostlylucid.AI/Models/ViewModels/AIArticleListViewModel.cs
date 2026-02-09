using mostlylucid.pagingtaghelper.Models;
using Mostlylucid.Shared.Models.Blog;

namespace Mostlylucid.AI.Models.ViewModels;

public class AIArticleListViewModel : AIBaseViewModel, IPagingModel
{
    public ViewType ViewType { get; set; } = ViewType.TailwindAndDaisy;
    public string LinkUrl { get; set; } = "/articles";
    public int Page { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public List<PostListModel> Data { get; set; } = new();
}
