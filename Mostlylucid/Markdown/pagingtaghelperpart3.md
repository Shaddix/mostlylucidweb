# A Paging View Component ASP.NET Core Tag Helper (Part 2, PageSize)

<datetime class="hidden">2025-03-25T22:12</datetime>
<!--category-- ASP.NET Core, TagHelper, PagingTagHelper -->

# Introduction
As part of my ongoing sage with my paging tag helper I have now separated out PageSize into its own tag helper. This is to make the tag helper more flexible and to allow for more complex paging scenarios.

**Again; these are all works-in progress. I'm already [up to 0.9.0](https://www.nuget.org/packages/mostlylucid.pagingtaghelper#readme-body-tab) at the time of writing but USE AT YOUR OWN RISK. It's free but NOT YET of release quality IMHO (I don't even have [full samples](https://taghelpersample.mostlylucid.net/PageSizeWithHTMX) yet!)**

See the [code for the tag helper here](https://github.com/scottgal/mostlylucid.pagingtaghelper/blob/main/mostlylucid.pagingtaghelper/Components/PageSizeTagHelper.cs).

[TOC]

# The Page Size Tag Helper
As with my other taghelpers this is designed for a use-case I often encounter; how to easily switch page size in results lists.
 At first glance this seems fairly straightforward but it can become TRICKY once you add HTMX into the mix.
 
## Requirements
So for my use case the requirements were the following
1. Allows easy use for teh developer (as much as possible is configured using a simple page model)
2. Supports updating the page size using HTMX - meaning it's able to post back *while retaining all query string parameters*. 
This is important as it allows the user to change the page size without losing any other filters they have applied.
3. Has a somewhat dynamic range of page sizes (including an 'All' option).

## The Tag Helper
With this in mind I designed the TagHelper to be as follows:

```html
<page-size
    hx-target="#list"
    hx-swap="innerHTML"
    model="Model">
    
</page-size>
<div id="list">
    <partial name="_ResultsList" model="Model"/>
</div>
```

So in this case again it takes my standard paging model:
```csharp
public interface IPagingModel
{
    public int Page { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; }

    public ViewType ViewType { get; set; }
    
    public string LinkUrl { get; set; }
}
```
Which provides the `Page`, `TotalItems`, `PageSize`, `ViewType` and `LinkUrl` for the paging.

You can also specify these in the form of attributes to the model:
```html

<page-size
    hx-target="#list"
    hx-swap="innerHTML"
    total-items="100"
    page-size="10"
    view-type="DaisyAndTailwind">
</page-size>
```
Where the `hx-target` here is the target for the HTMX request and the `hx-swap` is the target for the response.

In this case it'll use the specified `PageSize` and `TotalItems` and `ViewType` to render the page size selector.

## JS Shenanigans
Unfortunately this time around I had a requirement I couldn't handle purely server side. Namely I wanted to preserve query string paramaters in the pagesize change request. 
To achieve this I have two pieces of JS which will load into the control depending on HTMX being used oor not

```csharp
@if (Model.UseHtmx)
{
@Html.HTMXPageSizeChange()
}
else
{
    @Html.PageSizeOnchangeSnippet()
}
```

If HTMX is being used it renders this into the page:
```javascript
(() => {
    if (window.__pageSizeListenerAdded) return;

    document.addEventListener('htmx:configRequest', event => {
        const { elt } = event.detail;
        if (elt?.matches('[name="pageSize"]')) {
            const params = new URLSearchParams(window.location.search);
            params.set('pageSize', elt.value); // This will update or add the pageSize param
            event.detail.parameters = Object.fromEntries(params.entries());
        }
    });
    window.__pageSizeListenerAdded = true;
})();
```

Which is a simple piece of JavaScript which will inject current querystring parameters into the request.

Alternatively if HTMX is NOT enabled it will render this:

```javascript
(function attachPageSizeListener() {
    // Ensure we only attach once if this script is included multiple times
    if (window.__pageSizeListenerAttached) return;
    window.__pageSizeListenerAttached = true;

    // Wait for the DOM to be fully loaded
    document.addEventListener("DOMContentLoaded", () => {
        document.body.addEventListener("change", handlePageSizeChange);
    });

    function handlePageSizeChange(event) {
        // Check if the changed element is a page-size <select> inside .page-size-container
        const select = event.target.closest(".page-size-container select[name='pageSize']");
        if (!select) return;

        const container = select.closest(".page-size-container");
        if (!container) return;

        // Default to "true" if there's no .useHtmx input
        const useHtmx = container.querySelector("input.useHtmx")?.value === "true";
        if (useHtmx) {
            // If using HTMX, we do nothing—HTMX will handle the request
            return;
        }

        // Either use a linkUrl from the container or the current page URL
        const linkUrl = container.querySelector("input.linkUrl")?.value || window.location.href;
        const url = new URL(linkUrl, window.location.origin);

        // Copy existing query params from current location
        const existingParams = new URLSearchParams(window.location.search);
        for (const [key, value] of existingParams.entries()) {
            url.searchParams.set(key, value);
        }

        // If user picked the same pageSize as what's already in the URL, do nothing
        if (url.searchParams.get("pageSize") === select.value) {
            return;
        }

        // Update the pageSize param
        url.searchParams.set("pageSize", select.value);

        // Redirect
        window.location.href = url.toString();
    }
})();
```
One change I'll lilely make is changing the name of the 'flag' variable; `__pageSizeListenerAttached ` as it's a bit too generic AND I use it for both HTMX and non-HTMX requests.

## The CODE
The tag helper itself is pretty simple, the main code just populates a few properties and then renders the view Component. 

```csharp
 public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // We want to render a <div> by default
        output.TagName = "div";

        // Remove any leftover attributes that we handle ourselves
        RemoveUnwantedAttributes(output);

        // Determine final PagerId
        var pagerId = PagerId ?? PageSizeModel?.PagerId ?? $"pager-{Guid.NewGuid():N}";
        // Assign ID to the outer div
        output.Attributes.SetAttribute("id", pagerId);
        // Build or fallback to a link URL
        var finalLinkUrl = BuildLinkUrl();
        if (string.IsNullOrEmpty(finalLinkUrl))
        {
            // If we can't build a URL, show a fallback message or short-circuit
            output.Content.SetHtmlContent(
                "<p style=\"color:red\">No valid link URL found for PageSize control.</p>");
            return;
        }

        var finalPageSize = PageSize ?? PageSizeModel?.PageSize ?? 10;
        // Clamp the page size to MaxPageSize

        var finalTotalItems = TotalItems ?? Model?.TotalItems ?? PageSizeModel?.TotalItems ?? 0;
        if (finalTotalItems == 0) throw new ArgumentNullException(nameof(finalTotalItems), "TotalItems is required");
        var maxPageSize = Math.Min(finalTotalItems, MaxPageSize);

        // Fallback to model's properties if not set
        var finalViewType = PageSizeModel?.ViewType ?? Model?.ViewType ?? ViewType;

        var pageSizeSteps = new int[] { 10, 25, 50, 75, 100, 125, 150, 200, 250, 500, 1000 };

        
        if (!string.IsNullOrEmpty(PageSizeSteps))
            pageSizeSteps = PageSizeSteps.Split(',').Select(s =>

            {
                if (int.TryParse(s, out var result))
                    return result;
                else
                {
                    throw new ArgumentException("Invalid page size step", nameof(PageSizeSteps));
                }
            }).ToArray();


        var pageSizes = CalculatePageSizes(finalTotalItems, maxPageSize, pageSizeSteps);

        var useHtmx = PageSizeModel?.UseHtmx ?? UseHtmx;
        var useLocalView = PageSizeModel?.UseLocalView ?? UseLocalView;

        // Acquire the IViewComponentHelper
        var viewComponentHelper = ViewContext.HttpContext.RequestServices
            .GetRequiredService<IViewComponentHelper>();
        ((IViewContextAware)viewComponentHelper).Contextualize(ViewContext);

        // Construct the PageSizeModel (if not provided) with final settings
        var pagerModel = new PageSizeModel
        {
            ViewType = finalViewType,
            UseLocalView = useLocalView,
            UseHtmx = useHtmx,
            PageSizes = pageSizes,
            PagerId = pagerId,
            Model = Model,
            LinkUrl = finalLinkUrl,
            MaxPageSize = maxPageSize,
            PageSize = finalPageSize,
            TotalItems = finalTotalItems
        };

        // Safely invoke the "PageSize" view component
        try
        {
            var result = await viewComponentHelper.InvokeAsync("PageSize", pagerModel);
            output.Content.SetHtmlContent(result);
        }
        catch (Exception ex)
        {
            // Optional: Log or display an error
            output.Content.SetHtmlContent(
                $"<p class=\"text-red-500\">Failed to render PageSize component: {ex.Message}</p>"
            );
        }
    }
```
One tricky piece is correctly handling the `PageSizeSteps` attribute. This is a comma separated list of page sizes which the user can select from. OR you can pass in your own steps. You can also specify the MaxPageSize which is the maximum page size that can be selected. 


```csharp
    private List<int> CalculatePageSizes(int totalItems, int maxxPageSize, int[] pageSizeSteps)
    {
        var pageSizes = new List<int>();

        // 1. Include all fixed steps up to both TotalItems and MaxPageSize
        foreach (var step in pageSizeSteps)
        {
            if (step <= totalItems && step <= maxxPageSize)
                pageSizes.Add(step);
        }

        // 2. If TotalItems exceeds the largest fixed step, keep doubling
        int lastFixedStep = pageSizeSteps.Last();
        if (totalItems > lastFixedStep)
        {
            int next = lastFixedStep;
            while (next < totalItems && next < maxxPageSize)
            {
                next *= 2; // double the step
                if (next <= totalItems && next <= maxxPageSize)
                {
                    pageSizes.Add(next);
                }
            }
        }

        // 3. Include TotalItems if it's not already in the list
        if (totalItems <= maxxPageSize && !pageSizes.Contains(totalItems))
        {
            pageSizes.Add(totalItems);
        }

        pageSizes.Sort();

        return pageSizes;
    }

```

As with my other tag helper I use the TagHelper->ViewComponent pattern to render the view component. This allows me to keep the tag helper simple and the view component complex. It also permits you to change views for different experiences; or to even inject your own view and use that (using `use-local-view`).

```csharp

public class PageSizeViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(PageSizeModel model)
        {
            if(model.Model != null)
            {
                model.LinkUrl ??= model.Model.LinkUrl;
                
                if(model.Model is IPagingSearchModel searchModel)
                {
                    model.SearchTerm ??= searchModel.SearchTerm;
                }
            }
            
            var viewName = "Components/Pager/Default";

            var useLocalView = model.UseLocalView;

            return (useLocalView, model.ViewType) switch
            {
                (true, ViewType.Custom) when ViewExists(viewName) => View(viewName, model),
                (true, ViewType.Custom) when !ViewExists(viewName) => throw new ArgumentException("View not found: " + viewName),
                (false, ViewType.Bootstrap) => View("/Areas/Components/Views/PageSize/BootstrapView.cshtml", model),
                (false, ViewType.Plain) => View("/Areas/Components/Views/PageSize/PlainView.cshtml", model),
                (false, ViewType.TailwindAndDaisy) => View("/Areas/Components/Views/PageSize/Default.cshtml", model),
                _ => View("/Areas/Components/Views/PageSize/Default.cshtml", model)
            };

            // If the view exists in the app, use it; otherwise, use the fallback RCL view
        }
        /// <summary>
        /// Checks if a view exists in the consuming application.
        /// </summary>
        private bool ViewExists(string viewName)
        {
            var result = ViewEngine.FindView(ViewContext, viewName, false);
            return result.Success;
        }
    }
```

# In Conclusion
Well that's it for the PageSize tag helper. I'm still working on the documentation and examples but the code is gettign better daily. 