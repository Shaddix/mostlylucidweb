using Microsoft.AspNetCore.Mvc.Filters;

namespace Mostlylucid.SkipAttribute;

public class TestActionFilter  :ActionFilterAttribute
{
    
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var skipFilter = context.ActionDescriptor.EndpointMetadata.FirstOrDefault(x => x is SkipActionFilter) as SkipActionFilter;
        
        if(skipFilter is null || !skipFilter.SkipFilter)
        {
            return;
        }
        if (skipFilter is { SkipFilter: true })
        {
            return;
        }
        base.OnActionExecuting(context);
    }
    
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        base.OnActionExecuted(context);
    }
    
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        base.OnResultExecuting(context);
    }
    
    public override void OnResultExecuted(ResultExecutedContext context)
    {
        base.OnResultExecuted(context);
    }
    
}