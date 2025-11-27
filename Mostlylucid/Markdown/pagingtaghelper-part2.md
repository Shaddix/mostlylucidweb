# A Paging View Component ASP.NET Core Tag Helper (Part 1.1, kinda sorta...A Flippy Tag Helper)

<datetime class="hidden">2025-03-17T22:12</datetime>
<!--category-- ASP.NET Core, TagHelper, PagingTagHelper -->

# Introduction
So while building out a project I originally built the [paging tag helper for ](https://www.mostlylucid.net/blog/category/PagingTagHelper) I also cam across ANOTHER need. A way to easily build sorting functionality for a table of results with HtMX support.

So...I present to you the Flippy Table Header tag helper thingy. 
This is just a quick article with a LOT of code. You can always see [the samples here](https://taghelpersample.mostlylucid.net/). And the source code as ever [is here](https://github.com/scottgal/mostlylucid.pagingtaghelper).

I'm pretty awful building examples (it's a slog innit) but I'll try to add more and more as I go along.
To install:

```bash
dotnet add package mostlylucid.pagingtaghelper
```

![Flippy TagHelper](flippyheaders.png?width=1000&format=webp)


[TOC]

# The Flippy Tag Helper
So what is this thing? Well in short it's a way to generate a table header (or anywhere else really) which will allow you to sort a table of results.

At it's simplest (and without HTMX integration) it lets you do this. 

```html
    <sortable-header column="@nameof(FakeDataModel.CompanyCity)"
                             current-order-by="@Model.OrderBy"
                             descending="@Model.Descending"
                             controller="Home"
                             use-htmx="false"
                             action="PageSortTagHelperNoHtmx"
                       >@Html.DisplayNameFor(x => x.Data.First().CompanyCity)
</sortable-header>
```
Here you can see you specify the column name, the test for the header and a place to post back (thanks to [JetBrains.Annotations](https://www.nuget.org/packages/Jetbrains.Annotations) which among many other things, which I've barely scratched the surface of give intellisense for the Controller and Action names).

This will generate a link which will post back to the `PageSortTagHelperNoHtmx` action on the `Home` controller with the column name and the current sort order and any other parameters in the URL (controlled with the `auto-append-querystring` attribute. This lets you easily get a useful postback link, you'll see below I made it pretty flexible where you can specify href / action & controller and get a link back with the querystring parameters appended.

```csharp
    private void AddQueryStringParameters(TagHelperOutput output, bool newDescending)
    {
        string? href = "";

        // If Controller and Action are provided, generate URL dynamically
        if (!string.IsNullOrEmpty(Controller) && !string.IsNullOrEmpty(Action))
        {
            href = Url.ActionLink(Action, Controller);
          
        }
        else if (output.Attributes.ContainsName("href")) // If href is manually set, use it
        {
            href = output.Attributes["href"].Value?.ToString() ?? "";
        }
        if(string.IsNullOrEmpty(href)) throw new ArgumentException("No href was provided or could be generated");
        
        // If AutoAppend is false or href is still empty, don't modify anything
        if (!AutoAppend && !string.IsNullOrWhiteSpace(href))
        {
            output.Attributes.RemoveAll("href");
            output.Attributes.SetAttribute("href", href);
            return;
        }

        // Parse the existing URL to append query parameters
        var queryStringBuilder = QueryString.Empty
            .Add("orderBy", Column)
            .Add("descending", newDescending.ToString().ToLowerInvariant());

        // Preserve existing query parameters from the current request
        foreach (var key in ViewContext.HttpContext.Request.Query.Keys)
        {
            var keyLower = key.ToLowerInvariant();
            if (keyLower != "orderby" && keyLower != "descending") // Avoid duplicating orderBy params
            {
             queryStringBuilder=   queryStringBuilder.Add(key, ViewContext.HttpContext.Request.Query[key]!);
            }
        }
href+= queryStringBuilder.ToString();
        
        // Remove old href and set the new one with the appended parameters
        output.Attributes.RemoveAll("href");
        output.Attributes.SetAttribute("href", href);
    }

```

This is the method which appends the querystring parameters to the URL. It's pretty simple, it generates a URL based on the `Controller` and `Action` attributes or if you've set the `href` attribute it will use that. If you've set `AutoAppend` to false it will just use the `href` attribute as is (meanign you can roll your own for specific use-cases). 

## Crazy Config
To make this as useful as possible with / without HTMX. with / without Tailwind & DaisyUI etc I've give you a BUNCH of properties to use to configure this relatively simple control 

```csharp
    [HtmlAttributeName("hx-controller")]
    [AspMvcController] // Enables IntelliSense for controller names
    public string? HXController { get; set; }

    [HtmlAttributeName("hx-action")]
    [AspMvcAction] // Enables IntelliSense for action names
    public string? HXAction { get; set; }
    
    
    [HtmlAttributeName("action")]
    [AspMvcAction]
    public string? Action { get; set; }
    
    [HtmlAttributeName("controller")]
    [AspMvcController]
    public string? Controller { get; set; }
    
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; }
    /// <summary>
    /// The column to sort by
    /// </summary>
    [HtmlAttributeName("column")] public string Column { get; set; } = string.Empty;

    /// <summary>
    /// Whether to auto-append any query string parameters
    /// </summary>
    [HtmlAttributeName("auto-append-querystring")] public bool AutoAppend { get; set; } = true;
    
    // <summary>
    /// Whether to use htmx ; specifcally used to set hx-vals
    /// </summary>
    [HtmlAttributeName("use-htmx")] public bool UseHtmx { get; set; } = true;

    /// <summary>
    /// The currently set order by column
    /// </summary>
    [HtmlAttributeName("current-order-by")]
    public string? CurrentOrderBy { get; set; }

    /// <summary>
    /// Sort direction, true for descending, false for ascending
    /// </summary>
    [HtmlAttributeName("descending")] public bool Descending { get; set; }

    
    /// <summary>
    ///  CSS class for the chevron up icon
    /// </summary>
    [HtmlAttributeName("chevron-up-class")]
    public string? ChevronUpClass { get; set; }
    
    /// <summary>
    ///  CSS class for the chevron down icon
    /// </summary>

    [HtmlAttributeName("chevron-down-class")]
    public string? ChevronDownClass { get; set; }
    
    /// <summary>
    /// The CSS class for the chevron when unsorted
    /// </summary>
    
    [HtmlAttributeName("chevron-unsorted-class")]
    public string? ChevronUnsortedClass { get; set; }

    /// <summary>
    /// The CSS class to use for the tag.
    /// </summary>
    [HtmlAttributeName("tag-class")] public string? TagClass { get; set; }
```

You can see here I have properties for PRETTY MUCH everything in the control (I'm not going to go through them all here they should be pretty self-explanatory).

# With HTMX
Now as you may have learned I'm an HTMX NUT so as usual this supports HTMX pretty seamlessly:
It DEFAULTS to `use-htmx` true which fills in the `hx-vals` attribute. Along with this I use the[ HTMX Tag Helpers](https://github.com/khalidabuhakmeh/Htmx.Net/tree/bbc9de911a723bff7cd0884aa566ed68502fa94b) to make the code as simple as possible. 

```html
            <sortable-header column="Name"
                             current-order-by="@Model.OrderBy"
                             descending="@Model.Descending"
                             hx-get
                             hx-route-pagesize="@Model.PageSize"
                             hx-route-page="@Model.Page"
                             hx-route-search="@Model.SearchTerm"
                             hx-controller="Home"
                             hx-action="PageSortTagHelper"
                             hx-indicator="#loading-modal"
                             hx-target="#list"
                             hx-push-url="true">@Html.DisplayNameFor(x => x.Data.First().Name)
            </sortable-header>

```

And well that's it reallty it just *works* with HTMX seamlessly. 

# The MVC Controller
I then post this back to a simple MVC Controller which generates some sample data:

```csharp
    [Route("PageSortTagHelper")]
    public async Task<IActionResult> PageSortTagHelper(string? search, int pageSize = 10, int page = 1, string? orderBy = "", bool descending = false)
    {
        var pagingModel = await SortResults(search, pageSize, page, orderBy, descending);

        if (Request.IsHtmxBoosted() || Request.IsHtmx())
        {
            return PartialView("_PageSortTagHelper", pagingModel);
        }
        return View("PageSortTagHelper", pagingModel);
    }
    
     private async Task<OrderedPagingViewModel> SortResults(string? search, int pageSize, int page, string? orderBy, bool descending)
    {
        search = search?.Trim().ToLowerInvariant();
        var fakeModel = await dataFakerService.GenerateData(1000);
        var results = new List<FakeDataModel>();

        if (!string.IsNullOrEmpty(search))
            results = fakeModel.Where(x => x.Name.ToLowerInvariant().Contains(search)
                                           || x.Description.ToLowerInvariant().Contains(search) ||
                                           x.CompanyAddress.ToLowerInvariant().Contains(search)
                                           || x.CompanyEmail.ToLowerInvariant().Contains(search)
                                           || x.CompanyCity.ToLowerInvariant().Contains(search)
                                           || x.CompanyCountry.ToLowerInvariant().Contains(search)
                                           || x.CompanyPhone.ToLowerInvariant().Contains(search)).ToList();
        else
        {
            results = fakeModel.ToList();
        }

        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            results = results.OrderByField(orderBy, descending).ToList();
        }

        var pagingModel = new OrderedPagingViewModel();
        pagingModel.TotalItems = results.Count();
        pagingModel.Page = page;
        pagingModel.SearchTerm = search;
        pagingModel.PageSize = pageSize;
        pagingModel.Data = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        pagingModel.OrderBy = orderBy;
        pagingModel.Descending = descending;
        return pagingModel;
    }

```
## The `OrderByField` Extension Method
Oh and I have a little extension method packaged in which applies a named order field to the data (or `IQueryable` etc). T|he whole extension method is below. You can see this has 3 methods, one for strong typed column names, one for IQueryable string column names and one for IEnumerables. 

```csharp
using System.Linq.Expressions;
using System.Reflection;

namespace mostlylucid.pagingtaghelper.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> OrderByField<T, TKey>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending = false)
    {
        return descending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);
    }


    public static IQueryable<T> OrderByField<T>(
        this IQueryable<T> source,
        string sortBy,
        bool descending = false)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return source; // No sorting applied

        var property =
            typeof(T).GetProperty(sortBy, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            throw new ArgumentException($"Property '{sortBy}' not found on type '{typeof(T)}'.");

        var param = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.MakeMemberAccess(param, property);
        var lambda = Expression.Lambda(propertyAccess, param);

        var methodName = descending ? "OrderByDescending" : "OrderBy";

        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(T), property.PropertyType },
            source.Expression,
            Expression.Quote(lambda)
        );

        return source.Provider.CreateQuery<T>(resultExpression);
    }
    
    public static IEnumerable<T> OrderByField<T>(
        this IEnumerable<T> source,
        string sortBy,
        bool descending = false)
    {
        var property = typeof(T).GetProperty(sortPropertyName(sortBy), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            throw new ArgumentException($"Property '{sortBy}' not found on type '{typeof(T)}'.");

        return descending
            ? source.OrderByDescending(x => property.GetValue(x, null))
            : source.OrderBy(x => property.GetValue(x, null));
    }

    // Helper methods for readability (optional)
    private static string sortPropertyName(string sortBy) => sortBy.Trim();
    private static string methodName(bool descending) => descending ? "OrderByDescending" : "OrderBy";
}
```

# In Conclusion
That's really it. It's just something I needed so I built it an stuck it in the nuget package.