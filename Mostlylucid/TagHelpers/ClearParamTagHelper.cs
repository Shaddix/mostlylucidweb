using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Mostlylucid.TagHelpers;

[HtmlTargetElement("clear-param")]
public class ClearParamTagHelper : TagHelper
{
    [HtmlAttributeName("name")]
    public string? Name { get; set; }

    [HtmlAttributeName("all")]
    public bool All { get; set; } = false;

    [HtmlAttributeName("target")]
    public string Target { get; set; } = "#content";

    [HtmlAttributeName("exclude")]
    public string Exclude { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "a";
        output.Attributes.SetAttribute("x-data", "window.queryParamClearer({})");

        if (All)
        {
            output.Attributes.SetAttribute("x-all", "true");
        }
        else if (!string.IsNullOrEmpty(Name))
        {
            output.Attributes.SetAttribute("x-param", Name);
        }

        if (!string.IsNullOrEmpty(Exclude))
        {
            output.Attributes.SetAttribute("x-exclude", Exclude);
        }

        output.Attributes.SetAttribute("data-target", Target);
        output.Attributes.SetAttribute("x-on:click.prevent", "clearParam($event)");

        // Add styling classes
        const string cssClasses = "btn btn-outline btn-sm text-gray-700 border-gray-300 dark:text-gray-100 dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700 transition-all";
        output.Attributes.SetAttribute("class", cssClasses);

        // If no content was provided, set default text
        if (output.Content.IsEmptyOrWhiteSpace)
        {
            output.Content.SetContent(All ? "Clear All" : "Clear");
        }
    }
}
