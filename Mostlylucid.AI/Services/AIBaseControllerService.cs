using Mostlylucid.Blog.ViewServices;
using Mostlylucid.Shared.Config;

namespace Mostlylucid.AI.Services;

public class AIBaseControllerService
{
    public IBlogViewService BlogViewService { get; }
    public AnalyticsSettings AnalyticsSettings { get; }
    public IAIArticleService AIArticleService { get; }

    public AIBaseControllerService(
        IBlogViewService blogViewService,
        AnalyticsSettings analyticsSettings,
        IAIArticleService aiArticleService)
    {
        BlogViewService = blogViewService;
        AnalyticsSettings = analyticsSettings;
        AIArticleService = aiArticleService;
    }
}
