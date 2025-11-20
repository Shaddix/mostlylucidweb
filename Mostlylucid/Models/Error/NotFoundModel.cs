using Mostlylucid.Shared.Models.Blog;

namespace Mostlylucid.Models.Error;

public class NotFoundModel
{
    public string OriginalPath { get; set; } = string.Empty;
    public List<PostListModel> Suggestions { get; set; } = new();
}