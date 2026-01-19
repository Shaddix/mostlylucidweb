using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Mostlylucid.AI.Models.ViewModels;
using Mostlylucid.AI.Services;
using Mostlylucid.Shared.Config;

namespace Mostlylucid.AI.Controllers;

public class AIBaseController : Controller
{
    protected readonly AIBaseControllerService BaseControllerService;
    protected readonly ILogger Logger;
    protected readonly IAIArticleService AIArticleService;
    protected readonly AnalyticsSettings AnalyticsSettings;

    public AIBaseController(AIBaseControllerService baseControllerService, ILogger logger)
    {
        BaseControllerService = baseControllerService;
        Logger = logger;
        AIArticleService = baseControllerService.AIArticleService;
        AnalyticsSettings = baseControllerService.AnalyticsSettings;
    }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
        if (!Request.IsHtmx())
        {
            ViewBag.UmamiPath = AnalyticsSettings.UmamiPath;
            ViewBag.UmamiWebsiteId = AnalyticsSettings.WebsiteId;
            ViewBag.UmamiScript = AnalyticsSettings.UmamiScript;
        }

        base.OnActionExecuting(filterContext);
    }

    protected void PopulateAnalytics(AIBaseViewModel model)
    {
        model.UmamiPath = AnalyticsSettings.UmamiPath;
        model.UmamiWebsiteId = AnalyticsSettings.WebsiteId;
        model.UmamiScript = AnalyticsSettings.UmamiScript;
    }
}
