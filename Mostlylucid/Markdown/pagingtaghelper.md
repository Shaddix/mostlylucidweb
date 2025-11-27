# A Paging View Component ASP.NET Core Tag Helper (Part 1 the Bare-Bones)

<datetime class="hidden">2025-03-11T17:12</datetime>
<!--category-- ASP.NET Core, TagHelper, PagingTagHelper -->

# Introduction

A work project the other day necessitated implementing paging form results. My go-to paging tag helper has always been the [pagination Tag Helper by Darrel O'Neill ](https://github.com/darrel-oneil/PaginationTagHelper) as I wrote about [here](https://www.mostlylucid.net/blog/addpagingwithhtmx) however for whatever reason it's just *stopped working* for me. So instead of trying to puzzle through what looks like an abandoned project at this point I decided to build one myself.

As usual you can get the source for this [on my GitHub](https://github.com/scottgal/mostlylucid.pagingtaghelper)

I have a sample site for this project [hosted here](https://taghelpersample.mostlylucid.net/)

This has samples of the output like this:

![Search Demo With Tag Helperg](taghelpersearch.png?width=500&format=webp)

[TOC]

# Requirements
For this tag helper I had a few requirements:
1. Should work seamlessly with [Tailwind](https://tailwindcss.com/) and [DaisyUI](https://daisyui.com/); my preferred CSS frameworks.
2. Should work with HTMX without causing any problems.
3. Should have a pagesize dropdown which uses HTMX to flip (so if you DON'T use HTMX it should still work but you need to add a button).
4. Should be easy to configure and use
   1. Accepts a paging model so it's simple to use in a Razor page
   2. Should be able to be configured with a few simple parameters
5. Should be a nuget package so all you great people can play with it.
6. Should be BOTH a ViewComponent and a TagHelper so it can be used in both Razor pages and views; with that in mind it should also have an overridable `Dafault.cshtml` view.
7. Works with a simple search function.

In future I'll add the capability to:

1. Add custom CSS to avoid being tied to DaisyUI and Tailwind~~ I have added this capability already see the demo here: [https://taghelpersample.mostlylucid.net/Home/PlainView](https://taghelpersample.mostlylucid.net/Home/PlainView)
2. The ability to specify page sizes
3. The ability to add a custom JS call to the page size dropdown (to allow you to NOT use HTMX).
4. Use Alpine to make the pager more active and responsive (as I did in my [previous article](https://www.mostlylucid.net/blog/addpagingwithhtmx)).


# Installation
The taghelper is now a shiny new Nuget package so you can install it with the following command:

```bash
dotnet add package mostlylucid.pagingtaghelper
```
You'd then add the tag helper to your `_ViewImports.cshtml` file like so:

```razor
@addTagHelper *, mostlylucid.pagingtaghelper
```
Then you can just start using it; I provide some helper classes which you can use to configre it such as 

## `IPagingModel`

This is the 'basic stuff' you need to get started. It's a simple interface that you can implement on your model to get the paging working.  Note that `ViewType` is optional here it defaults to `TailwindANdDaisy` but you can set it to `Custom`, `Plain` or `Bootstrap` if you want to use a different view. 

```csharp
public enum ViewType
{
TailwindANdDaisy,
Custom,
Plain,
Bootstrap
}
```
OR you can even specify a custom View by using the TagHelper's `use-local-view` property.



```csharp
namespace mostlylucid.pagingtaghelper.Models;

public interface IPagingModel
{
    public int Page { get; set; }
    public int TotalItems { get; set; }
    public int PageSize { get; set; }

    public ViewType ViewType { get; set; }
    
    public string LinkUrl { get; set; }
}
namespace mostlylucid.pagingtaghelper.Models;

public interface IPagingSearchModel : IPagingModel
{
    public string? SearchTerm { get; set; }
}

```

I have also implement these in the project to provide a baseline:

```csharp
public abstract class BasePagerModel : IPagingModel
{
    public int Page { get; set; } = 1;
    public int TotalItems { get; set; } = 0;
    public int PageSize { get; set; } = 10;
    public ViewType ViewType { get; set; } = ViewType.TailwindANdDaisy;

    public string LinkUrl { get; set; } = "";

}
public abstract class BasePagerSearchMdodel : BasePagerModel, IPagingSearchModel
{
    public string? SearchTerm { get; set; }
}
```

I'll cover the search functionality in a future article.. 

# The TagHelper

I then worked out what I would like the TagHelper to look like in use:

```html
<paging
    x-ref="pager"
    hx-boost="true"
    hx-indicator="#loading-modal"
    hx-target="#user-list "
    hx-swap="show:none"
    model="Model"
    pages-to-display="10"
    hx-headers='{"pagerequest": "true"}'>
</paging>
```

Here you can see I set a few HTMX parameters and the model to use for the paging. I also set the number of pages to display and the headers to send with the request (this allows me to use HTMX to populate the page).

The component also has a BUNCH of other config elements which I'll work through in future articles. As you can see therer's a LOT of possible configuration. 

```html
<paging css-class=""
        first-last-navigation=""
        first-page-text=""
        next-page-aria-label=""
        next-page-text=""
        page=""
        pages-to-display=""
        page-size=""
        previous-page-text=""
        search-term=""
        skip-forward-back-navigation=""
        skip-back-text=""
        skip-forward-text="true"
        total-items=""
        link-url=""
        last-page-text=""
        show-pagesize=""
        use-htmx=""
        use-local-view=""
        view-type="Bootstrap"
        htmx-target=""
        id=""
></paging>
```

The TagHelper is pretty simple but has a bunch of properties enabling the user to customize the behaviour (you can see this below in the [view](#the-view) ) aside from the properties (which I won't paste here for brevity) the code is fairly straightforward:


```csharp
    /// <summary>
    /// Processes the tag helper to generate the pagination component.
    /// </summary>
    /// <param name="context">The tag helper context.</param>
    /// <param name="output">The tag helper output.</param>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
   
        output.TagName = "div";
        
        //Remove all the properties that are not needed for the rendered content.
        output.Attributes.RemoveAll("page");
        output.Attributes.RemoveAll("link-url");
        output.Attributes.RemoveAll("page-size");
        output.Attributes.RemoveAll("total-items");
        output.Attributes.RemoveAll("pages-to-display");
        output.Attributes.RemoveAll("css-class");
        output.Attributes.RemoveAll("first-page-text");
        output.Attributes.RemoveAll("previous-page-text");
        output.Attributes.RemoveAll("skip-back-text");
        output.Attributes.RemoveAll("skip-forward-text");
        output.Attributes.RemoveAll("next-page-text");
        output.Attributes.RemoveAll("next-page-aria-label");
        output.Attributes.RemoveAll("last-page-text");
        output.Attributes.RemoveAll("first-last-navigation");
        output.Attributes.RemoveAll("skip-forward-back-navigation");
        output.Attributes.RemoveAll("model");
        output.Attributes.RemoveAll("show-pagesize");
        output.Attributes.RemoveAll("pagingmodel");
        output.Attributes.RemoveAll("use-local-view");
        
        var pagerId =  PagerId ?? $"pager-{Guid.NewGuid():N}";
        var linkUrl = LinkUrl ?? ViewContext.HttpContext.Request.Path;
        PageSize = Model?.PageSize ?? PageSize ?? 10;
        Page = Model?.Page ?? Page ?? 1;
        ViewType = Model?.ViewType ?? ViewType;
        TotalItems = Model?.TotalItems ?? TotalItems ?? 0;
        if(Model is IPagingSearchModel searchModel)
            SearchTerm = searchModel?.SearchTerm ?? SearchTerm ?? "";
        output.Attributes.SetAttribute("id", pagerId);
        var viewComponentHelper = (IViewComponentHelper)ViewContext.HttpContext.RequestServices.GetService(typeof(IViewComponentHelper))!;
        ((IViewContextAware)viewComponentHelper).Contextualize(ViewContext);

        var pagerModel = PagerModel ?? new PagerViewModel()
        {
            
            ViewType = ViewType,
            UseLocalView = UseLocalView,
            UseHtmx = UseHtmx,
            PagerId = pagerId,
            SearchTerm = SearchTerm,
            ShowPageSize = ShowPageSize,
            Model = Model,
            LinkUrl = linkUrl,
            Page = Page,
            PageSize = PageSize,
            TotalItems = TotalItems,
            PagesToDisplay = PagesToDisplay,
            CssClass = CssClass,
            FirstPageText = FirstPageText,
            PreviousPageText = PreviousPageText,
            SkipBackText = SkipBackText,
            SkipForwardText = SkipForwardText,
            NextPageText = NextPageText,
            NextPageAriaLabel = NextPageAriaLabel,
            LastPageText = LastPageText,
            FirstLastNavigation = FirstLastNavigation,
            SkipForwardBackNavigation = SkipForwardBackNavigation,
            HtmxTarget = HtmxTarget,
            
        };

        var result = await viewComponentHelper.InvokeAsync("Pager", pagerModel);
        output.Content.SetHtmlContent(result);
    }
```
It comprises of these steps:

1. Set the output tag name to `div`; this is the container for the pager.
2. Remove all the properties that are not needed for the rendered content (but leave all the user provided ones, this allows for simple customization).
3. Set the pagerId to a random GUID if not provided (this is really used for custom code, you CAN specify the ID or just let this code take care of it). 
4. Set the linkUrl to the current path if not provided - this allows you to override this if you want to use a different URL.
5. Set the PageSize, Page, ViewType, TotalItems and SearchTerm to the model if provided or the default if not. This enables us to just pass in the `IPagingModel` and have the pager work with no further configuration. 
6. Set the ID attribute to the pagerId.
7. Get the ViewComponentHelper from the DI container and contextualize it with the current ViewContext.
8. Create a new `PagerViewModel` with the properties set to the values we have or the defaults if not provided.
9. Invoke the `Pager` ViewComponent with the `PagerViewModel` and set the output content to the result.

Again all pretty simple. 


# The ViewComponent
## The View
The view for the ViewComponent is pretty simple; it's just a loop through the pages and a few links to the first, last, next and previous pages. 
<details>
  <summary>Complete source code for the Default TailwindUI & Daisy view</summary>

```csharp
@model mostlylucid.pagingtaghelper.Components.PagerViewModel
@{
    var totalPages = (int)Math.Ceiling((double)Model.TotalItems! / (double)Model.PageSize!);
    var pageSizes = new List<int>();
    if (Model.ShowPageSize)
    {
        // Build a dynamic list of page sizes.

        // Fixed steps as a starting point.
        int[] fixedSteps = { 10, 25, 50, 75, 100, 125, 150, 200, 250, 500, 1000 };

        // Add only those fixed steps that are less than or equal to TotalItems.
        foreach (var step in fixedSteps)
        {
            if (step <= Model.TotalItems)
            {
                pageSizes.Add(step);
            }
        }

        // If TotalItems is greater than the largest fixed step,
        // add additional steps by doubling until reaching TotalItems.
        if (Model.TotalItems > fixedSteps.Last())
        {
            int next = fixedSteps.Last();
            while (next < Model.TotalItems)
            {
                next *= 2;
                // Only add if it doesn't exceed TotalItems.
                if (next < Model.TotalItems)
                {
                    pageSizes.Add(next);
                }
            }

            // Always include the actual TotalItems as the maximum option.
            if (!pageSizes.Contains(Model.TotalItems.Value))
            {
                pageSizes.Add(Model.TotalItems.Value);
            }
        }
    }
}
@if (totalPages > 1)
{
    <div class="@Model.CssClass flex items-center justify-center" id="pager-container">
        @if (Model.ShowPageSize)
        {
            var pagerId = Model.PagerId;
            var htmxAttributes = Model.UseHtmx
                ? $"hx-get=\"{Model.LinkUrl}\" hx-trigger=\"change\" hx-include=\"#{pagerId} [name='page'], #{pagerId} [name='search']\" hx-push-url=\"true\""
                : "";


            <!-- Preserve current page -->
            <input type="hidden" name="page" value="@Model.Page"/>
            <input type="hidden" name="search" value="@Model.SearchTerm"/>
            <input type="hidden" class="useHtmx" value="@Model.UseHtmx.ToString().ToLowerInvariant()"/>
            if (!Model.UseHtmx)
            {
                <input type="hidden" class="linkUrl" value="@Model.LinkUrl"/>
            }

            <!-- Page size select with label -->
            <div class="flex items-center mr-8">
                <label for="pageSize-@pagerId" class="text-sm text-gray-600 mr-2">Page size:</label>
                <select id="pageSize-@pagerId"
                        name="pageSize"
                        class="border rounded select select-primary select-sm pt-0 mt-0 min-w-[80px] pr-4"
                        @Html.Raw(htmxAttributes)>
                    @foreach (var option in pageSizes.ToList())
                    {
                        var optionString = option.ToString();
                        if (option == Model.PageSize)
                        {
                            <option value="@optionString" selected="selected">@optionString</option>
                        }
                        else
                        {
                            <option value="@optionString">@optionString</option>
                        }
                    }
                </select>
            </div>
        }

        @* "First" page link *@
        @if (Model.FirstLastNavigation && Model.Page > 1)
        {
            var href = $"{Model.LinkUrl}?page=1&pageSize={Model.PageSize}";
            if (!string.IsNullOrEmpty(Model.SearchTerm))
            {
                href += $"&search={Model.SearchTerm}";
            }

            <a class="btn btn-sm"
               href="@href">
                @Model.FirstPageText
            </a>
        }

        @* "Previous" page link *@
        @if (Model.Page > 1)
        {
            var href = $"{Model.LinkUrl}?page={Model.Page - 1}&pageSize={Model.PageSize}";
            if (!string.IsNullOrEmpty(Model.SearchTerm))
            {
                href += $"&search={Model.SearchTerm}";
            }

            <a class="btn btn-sm"
               href="@href">
                @Model.PreviousPageText
            </a>
        }

        @* Optional skip back indicator *@
        @if (Model.SkipForwardBackNavigation && Model.Page > Model.PagesToDisplay)
        {
            <a class="btn btn-sm btn-disabled">
                @Model.SkipBackText
            </a>
        }

        @* Determine visible page range *@
        @{
            int halfDisplay = Model.PagesToDisplay / 2;
            int startPage = Math.Max(1, Model.Page.Value - halfDisplay);
            int endPage = Math.Min(totalPages, startPage + Model.PagesToDisplay - 1);
            startPage = Math.Max(1, endPage - Model.PagesToDisplay + 1);
        }
        @for (int i = startPage; i <= endPage; i++)
        {
            var href = $"{Model.LinkUrl}?page={i}&pageSize={Model.PageSize}";
            if (!string.IsNullOrEmpty(Model.SearchTerm))
            {
                href += $"&search={Model.SearchTerm}";
            }

            <a data-page="@i" class="btn btn-sm mr-2 @(i == Model.Page ? "btn-active" : "")"
               href="@href">
                @i
            </a>
        }

        @* Optional skip forward indicator *@
        @if (Model.SkipForwardBackNavigation && Model.Page < totalPages - Model.PagesToDisplay + 1)
        {
            <a class="btn btn-sm btn-disabled mr-2">
                @Model.SkipForwardText
            </a>
        }

        @* "Next" page link *@
        @if (Model.Page < totalPages)
        {
            var href = $"{Model.LinkUrl}?page={Model.Page + 1}&pageSize={Model.PageSize}";
            if (!string.IsNullOrEmpty(Model.SearchTerm))
            {
                href += $"&search={Model.SearchTerm}";
            }

            <a class="btn btn-sm mr-2"
               href="@href"
               aria-label="@Model.NextPageAriaLabel">
                @Model.NextPageText
            </a>
        }

        @* "Last" page link *@
        @if (Model.FirstLastNavigation && Model.Page < totalPages)
        {
            var href = $"{Model.LinkUrl}?page={totalPages}&pageSize={Model.PageSize}";
            if (!string.IsNullOrEmpty(Model.SearchTerm))
            {
                href += $"&search={Model.SearchTerm}";
            }

            <a class="btn btn-sm"
               href="@href">
                @Model.LastPageText
            </a>
        }

        <!-- Page info text with left margin for separation -->
        <div class="text-sm text-neutral-500 ml-8">
            Page @Model.Page of @totalPages (Total items: @Model.TotalItems)
        </div>
    </div>
}
```
</details>

This is broken up into a few sections:
1. The page size dropdown
2. The first, last, next and previous links
3. The skip back and skip forward links
4. The page links
5. The page info text

## The Page Size Dropdown
One thing I was  missing from the original tag helper was a page size dropdown.
This is a simple select list, you can see I first start out by defining `fixedSteps` which are just a few fixed steps I want to use for the dropdown. I then loop through these and add them to the list. A habit I always have is having an 'all' option so I add the total items to the list if it's not already there. 

```csharp
@{
    var totalPages = (int)Math.Ceiling((double)Model.TotalItems! / (double)Model.PageSize!);
    var pageSizes = new List<int>();
    if (Model.ShowPageSize)
    {
        // Build a dynamic list of page sizes.

        // Fixed steps as a starting point.
        int[] fixedSteps = { 10, 25, 50, 75, 100, 125, 150, 200, 250, 500, 1000 };

        // Add only those fixed steps that are less than or equal to TotalItems.
        foreach (var step in fixedSteps)
        {
            if (step <= Model.TotalItems)
            {
                pageSizes.Add(step);
            }
        }

        // If TotalItems is greater than the largest fixed step,
        // add additional steps by doubling until reaching TotalItems.
        if (Model.TotalItems > fixedSteps.Last())
        {
            int next = fixedSteps.Last();
            while (next < Model.TotalItems)
            {
                next *= 2;
                // Only add if it doesn't exceed TotalItems.
                if (next < Model.TotalItems)
                {
                    pageSizes.Add(next);
                }
            }

            // Always include the actual TotalItems as the maximum option.
            if (!pageSizes.Contains(Model.TotalItems.Value))
            {
                pageSizes.Add(Model.TotalItems.Value);
            }
        }
    }
}
```

I then render this out to the page

```csharp
  @if (Model.ShowPageSize)
        {
            var pagerId = Model.PagerId;
            var htmxAttributes = Model.UseHtmx
                ? $"hx-get=\"{Model.LinkUrl}\" hx-trigger=\"change\" hx-include=\"#{pagerId} [name='page'], #{pagerId} [name='search']\" hx-push-url=\"true\""
                : "";


            <!-- Preserve current page -->
            <input type="hidden" name="page" value="@Model.Page"/>
            <input type="hidden" name="search" value="@Model.SearchTerm"/>
            <input type="hidden" class="useHtmx" value="@Model.UseHtmx.ToString().ToLowerInvariant()"/>
            if (!Model.UseHtmx)
            {
                <input type="hidden" class="linkUrl" value="@Model.LinkUrl"/>
            }

            <!-- Page size select with label -->
            <div class="flex items-center mr-8">
                <label for="pageSize-@pagerId" class="text-sm text-gray-600 mr-2">Page size:</label>
                <select id="pageSize-@pagerId"
                        name="pageSize"
                        class="border rounded select select-primary select-sm pt-0 mt-0 min-w-[80px] pr-4"
                        @Html.Raw(htmxAttributes)>
                    @foreach (var option in pageSizes.ToList())
                    {
                        var optionString = option.ToString();
                        if (option == Model.PageSize)
                        {
                            <option value="@optionString" selected="selected">@optionString</option>
                        }
                        else
                        {
                            <option value="@optionString">@optionString</option>
                        }
                    }
                </select>
            </div>
        }
```

You can see I optionally use some HTMX attributes to pass the page size to the server and update the page while retaining the current page and search parameter (if any). 

Additionally if you specif `use-htmx=false` as a parameter on the tag helper it won't output these but instead will allow you to use some JS I provide as an HTML Helper to update the page size.

```csharp
@Html.PageSizeOnchangeSnippet()
    

```

This is a simple script that will update the page size and reload the page (note this doesn't yet work for Plain CSS / Bootstrap as I need to work out the property names etc). 

```javascript
document.addEventListener("DOMContentLoaded", function () {
    document.body.addEventListener("change", function (event) {
        const selectElement = event.target.closest("#pager-container select[name='pageSize']");
        if (!selectElement) return;

        const pagerContainer = selectElement.closest("#pager-container");
        const useHtmxInput = pagerContainer.querySelector("input.useHtmx");
        const useHtmx = useHtmxInput ? useHtmxInput.value === "true" : true; // default to true

        if (!useHtmx) {
            const pageInput = pagerContainer.querySelector("[name='page']");
            const searchInput = pagerContainer.querySelector("[name='search']");

            const page = pageInput ? pageInput.value : "1";
            const search = searchInput ? searchInput.value : "";
            const pageSize = selectElement.value;
            const linkUrl =  pagerContainer.querySelector("input.linkUrl").value ?? "";
            
            const url = new URL(linkUrl, window.location.origin);
            url.searchParams.set("page", page);
            url.searchParams.set("pageSize", pageSize);

            if (search) {
                url.searchParams.set("search", search);
            }

            window.location.href = url.toString();
        }
    });
});

````

# HTMX integration
The HTMX integration is pretty simple as HTMX is cascading to child elements we can define the HTMX parameters on the parent element and they will be inherited.

  - `hx-boost="true"` - this uses the nifty [hx-boost feature](https://htmx.org/attributes/hx-boost/) to intercept the click event and send the request via HTMX.
  - hx-indicator="#loading-modal" - this has a loading modal that will show while the request is being processed.
  - hx-target="#user-list" - this is the element the response will swap out, in this case the user list. Note: This currently includes the Pager for simplicity; you CAN make this more active using Alpine (as in my [earlier article](https://www.mostlylucid.net/blog/addpagingwithhtmx) ) but it was out of scope this time. 
  - hx-swap="show:none"

## The Loading Modal
This is pretty simple and uses DaisyUI, boxicons and Tailwind to create a simple loading modal.

```html
<div id="loading-modal" class="modal htmx-indicator">
    <div class="modal-box flex flex-col items-center justify-center">
        <h2 class="text-lg font-semibold">Loading...</h2>
        <i class="bx bx-loader bx-spin text-3xl mt-2"></i>
    </div>
</div>

```
hx-indicator="#loading-modal" then specifies that when an HTMX request is performed it shoould show then hide this modal.


# Future Features
So that's part 1, there's obviously a LOT more to cover and I will in future articles; including the sample site, alternative views, the search functionality and the custom CSS.