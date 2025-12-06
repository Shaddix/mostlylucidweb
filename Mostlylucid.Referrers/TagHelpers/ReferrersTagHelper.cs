using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Mostlylucid.Referrers.Services;

namespace Mostlylucid.Referrers.TagHelpers;

/// <summary>
/// Tag helper for displaying referrers to a blog post
/// Usage: <referrers post-slug="my-post" show-empty="false" />
/// </summary>
[HtmlTargetElement("referrers")]
public class ReferrersTagHelper : TagHelper
{
    private readonly IReferrerService _referrerService;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <summary>
    /// The slug of the blog post to show referrers for
    /// </summary>
    [HtmlAttributeName("post-slug")]
    public string PostSlug { get; set; } = string.Empty;

    /// <summary>
    /// Whether to show the element even when there are no referrers
    /// </summary>
    [HtmlAttributeName("show-empty")]
    public bool ShowEmpty { get; set; } = false;

    /// <summary>
    /// CSS class to apply to the container
    /// </summary>
    [HtmlAttributeName("class")]
    public string CssClass { get; set; } = "referrers-list";

    public ReferrersTagHelper(IReferrerService referrerService)
    {
        _referrerService = referrerService;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(PostSlug))
        {
            output.SuppressOutput();
            return;
        }

        var referrers = await _referrerService.GetReferrersForPostAsync(PostSlug);

        if (!ShowEmpty && referrers.Referrers.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.Attributes.SetAttribute("class", CssClass);

        var content = new TagBuilder("div");

        if (referrers.Referrers.Count > 0)
        {
            var header = new TagBuilder("h4");
            header.AddCssClass("referrers-header text-sm font-semibold mb-2");
            header.InnerHtml.Append("Linked from:");
            content.InnerHtml.AppendHtml(header);

            var list = new TagBuilder("ul");
            list.AddCssClass("referrers-items list-none p-0 m-0");

            foreach (var referrer in referrers.Referrers)
            {
                var item = new TagBuilder("li");
                item.AddCssClass("referrer-item text-sm py-1");

                var link = new TagBuilder("a");
                link.Attributes["href"] = referrer.Url;
                link.Attributes["rel"] = "nofollow noopener";
                link.Attributes["target"] = "_blank";
                link.AddCssClass("referrer-link text-primary hover:underline");
                link.InnerHtml.Append(referrer.DisplayName);
                item.InnerHtml.AppendHtml(link);

                if (referrer.HitCount > 1)
                {
                    var count = new TagBuilder("span");
                    count.AddCssClass("referrer-count text-xs text-base-content/60 ml-2");
                    count.InnerHtml.Append($"({referrer.HitCount})");
                    item.InnerHtml.AppendHtml(count);
                }

                list.InnerHtml.AppendHtml(item);
            }

            content.InnerHtml.AppendHtml(list);
        }
        else if (ShowEmpty)
        {
            var empty = new TagBuilder("p");
            empty.AddCssClass("referrers-empty text-sm text-base-content/60");
            empty.InnerHtml.Append("No referrers yet.");
            content.InnerHtml.AppendHtml(empty);
        }

        output.Content.SetHtmlContent(content.InnerHtml);
    }
}

/// <summary>
/// Tag helper for displaying top referrers across all posts
/// Usage: <top-referrers limit="10" />
/// </summary>
[HtmlTargetElement("top-referrers")]
public class TopReferrersTagHelper : TagHelper
{
    private readonly IReferrerService _referrerService;

    /// <summary>
    /// Maximum number of referrers to display
    /// </summary>
    [HtmlAttributeName("limit")]
    public int Limit { get; set; } = 10;

    /// <summary>
    /// CSS class to apply to the container
    /// </summary>
    [HtmlAttributeName("class")]
    public string CssClass { get; set; } = "top-referrers-list";

    public TopReferrersTagHelper(IReferrerService referrerService)
    {
        _referrerService = referrerService;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var referrers = await _referrerService.GetTopReferrersAsync(Limit);

        if (referrers.Count == 0)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "div";
        output.Attributes.SetAttribute("class", CssClass);

        var content = new TagBuilder("div");

        var header = new TagBuilder("h4");
        header.AddCssClass("top-referrers-header text-sm font-semibold mb-2");
        header.InnerHtml.Append("Top Referrers:");
        content.InnerHtml.AppendHtml(header);

        var list = new TagBuilder("ul");
        list.AddCssClass("top-referrers-items list-none p-0 m-0");

        foreach (var referrer in referrers)
        {
            var item = new TagBuilder("li");
            item.AddCssClass("referrer-item text-sm py-1");

            var link = new TagBuilder("a");
            link.Attributes["href"] = referrer.Url;
            link.Attributes["rel"] = "nofollow noopener";
            link.Attributes["target"] = "_blank";
            link.AddCssClass("referrer-link text-primary hover:underline");
            link.InnerHtml.Append(referrer.DisplayName);
            item.InnerHtml.AppendHtml(link);

            var count = new TagBuilder("span");
            count.AddCssClass("referrer-count text-xs text-base-content/60 ml-2");
            count.InnerHtml.Append($"({referrer.HitCount})");
            item.InnerHtml.AppendHtml(count);

            list.InnerHtml.AppendHtml(item);
        }

        content.InnerHtml.AppendHtml(list);
        output.Content.SetHtmlContent(content.InnerHtml);
    }
}
