using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.Shared.Models;

namespace Mostlylucid.AI.Services;

public interface IAIArticleService
{
    Task<AIArticleListViewModel> GetArticlesAsync(int page = 1, int pageSize = 10, string? language = null);
    Task<BlogPostDto?> GetArticleBySlugAsync(string slug, string? language = null);
}
