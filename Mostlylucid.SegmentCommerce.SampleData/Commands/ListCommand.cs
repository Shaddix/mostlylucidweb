using System.ComponentModel;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class ListSettings : CommandSettings
{
    [CommandOption("-c|--category <CATEGORY>")]
    [Description("Show details for a specific category")]
    public string? Category { get; set; }

    [CommandOption("--json")]
    [Description("Output as JSON")]
    public bool Json { get; set; }
}

public class ListCommand : Command<ListSettings>
{
    private readonly GadgetTaxonomy _taxonomy;

    public ListCommand(GadgetTaxonomy taxonomy)
    {
        _taxonomy = taxonomy;
    }

    public override int Execute(CommandContext context, ListSettings settings)
    {
        if (settings.Json)
        {
            OutputJson(settings);
            return 0;
        }

        if (!string.IsNullOrEmpty(settings.Category))
        {
            ShowCategoryDetails(settings.Category);
        }
        else
        {
            ShowAllCategories();
        }

        return 0;
    }

    private void ShowAllCategories()
    {
        AnsiConsole.Write(new Rule("[bold cyan]Gadget Taxonomy[/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine($"[dim]Version: {_taxonomy.Version}[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Category")
            .AddColumn("Subcategories")
            .AddColumn("Product Types")
            .AddColumn("Price Range");

        foreach (var (slug, category) in _taxonomy.Categories)
        {
            var subcatCount = category.Subcategories.Count;
            var productCount = category.Subcategories.Values.Sum(s => s.Products.Count);

            table.AddRow(
                $"[bold]{category.DisplayName}[/] [dim]({slug})[/]",
                subcatCount.ToString(),
                productCount.ToString(),
                $"£{category.PriceRange.Min:F2} - £{category.PriceRange.Max:F2}");
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Use --category <slug> to see details for a specific category[/]");
    }

    private void ShowCategoryDetails(string categorySlug)
    {
        if (!_taxonomy.Categories.TryGetValue(categorySlug, out var category))
        {
            AnsiConsole.MarkupLine($"[red]Unknown category: {categorySlug}[/]");
            AnsiConsole.MarkupLine($"Available: {string.Join(", ", _taxonomy.Categories.Keys)}");
            return;
        }

        AnsiConsole.Write(new Rule($"[bold cyan]{category.DisplayName}[/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine($"[dim]{category.Description}[/]");
        AnsiConsole.MarkupLine($"Price range: £{category.PriceRange.Min:F2} - £{category.PriceRange.Max:F2}");
        AnsiConsole.WriteLine();

        foreach (var (subcatSlug, subcategory) in category.Subcategories)
        {
            AnsiConsole.MarkupLine($"[bold yellow]{subcategory.DisplayName}[/]");

            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Type")
                .AddColumn("Variants")
                .AddColumn("Features")
                .AddColumn("Colours")
                .AddColumn("Brands");

            foreach (var product in subcategory.Products)
            {
                table.AddRow(
                    $"[bold]{product.Type}[/]",
                    string.Join(", ", product.Variants.Take(3)) + (product.Variants.Count > 3 ? $" (+{product.Variants.Count - 3})" : ""),
                    string.Join(", ", product.Features.Take(3)) + (product.Features.Count > 3 ? $" (+{product.Features.Count - 3})" : ""),
                    string.Join(", ", product.Colours.Take(3)) + (product.Colours.Count > 3 ? $" (+{product.Colours.Count - 3})" : ""),
                    string.Join(", ", product.Brands.Take(2)) + (product.Brands.Count > 2 ? $" (+{product.Brands.Count - 2})" : ""));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    private void OutputJson(ListSettings settings)
    {
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        string json;

        if (string.IsNullOrEmpty(settings.Category))
        {
            json = System.Text.Json.JsonSerializer.Serialize(_taxonomy.Categories, options);
        }
        else
        {
            var category = _taxonomy.Categories.GetValueOrDefault(settings.Category);
            json = System.Text.Json.JsonSerializer.Serialize(category, options);
        }

        Console.WriteLine(json);
    }
}
