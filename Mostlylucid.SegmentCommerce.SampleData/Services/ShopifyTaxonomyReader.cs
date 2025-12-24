using System.Text.Json;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Reads and provides access to Shopify taxonomy data.
/// </summary>
public class ShopifyTaxonomyReader
{
    private ShopifyTaxonomy? _taxonomy;
    private readonly string _dataPath;

    public ShopifyTaxonomyReader(string dataPath = @"D:\segmentdata")
    {
        _dataPath = dataPath;
    }

    /// <summary>
    /// Load the taxonomy from the JSON file.
    /// </summary>
    public async Task<ShopifyTaxonomy> LoadTaxonomyAsync(CancellationToken cancellationToken = default)
    {
        if (_taxonomy != null)
            return _taxonomy;

        var filePath = Path.Combine(_dataPath, "categories.json");
        
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Shopify taxonomy file not found: {filePath}");
        }

        AnsiConsole.MarkupLine($"[blue]Loading Shopify taxonomy from[/] {Markup.Escape(filePath)}");

        await using var stream = File.OpenRead(filePath);
        _taxonomy = await JsonSerializer.DeserializeAsync<ShopifyTaxonomy>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize Shopify taxonomy");

        AnsiConsole.MarkupLine($"[green]Loaded[/] {_taxonomy.Verticals.Count} verticals with {_taxonomy.GetAllCategories().Count()} total categories");

        return _taxonomy;
    }

    /// <summary>
    /// Get random leaf categories for product generation.
    /// </summary>
    public async Task<List<ShopifyCategory>> GetRandomCategoriesAsync(
        int count, 
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        var taxonomy = await LoadTaxonomyAsync(cancellationToken);
        return taxonomy.GetRandomCategories(count, random);
    }

    /// <summary>
    /// Get all vertical names.
    /// </summary>
    public async Task<List<string>> GetVerticalNamesAsync(CancellationToken cancellationToken = default)
    {
        var taxonomy = await LoadTaxonomyAsync(cancellationToken);
        return taxonomy.Verticals.Select(v => v.Name).ToList();
    }

    /// <summary>
    /// Get random categories from specific verticals.
    /// </summary>
    public async Task<List<ShopifyCategory>> GetRandomCategoriesFromVerticalsAsync(
        IEnumerable<string> verticalNames,
        int categoriesPerVertical,
        Random? random = null,
        CancellationToken cancellationToken = default)
    {
        var taxonomy = await LoadTaxonomyAsync(cancellationToken);
        random ??= Random.Shared;
        
        var result = new List<ShopifyCategory>();
        
        foreach (var verticalName in verticalNames)
        {
            var vertical = taxonomy.Verticals.FirstOrDefault(v => 
                v.Name.Equals(verticalName, StringComparison.OrdinalIgnoreCase));
            
            if (vertical == null)
            {
                AnsiConsole.MarkupLine($"[yellow]Vertical not found: {Markup.Escape(verticalName)}[/]");
                continue;
            }

            var leaves = vertical.Categories.Where(c => c.Children.Count == 0).ToList();
            var selected = leaves
                .OrderBy(_ => random.Next())
                .Take(categoriesPerVertical)
                .ToList();
            
            result.AddRange(selected);
        }

        return result;
    }

    /// <summary>
    /// Get category statistics.
    /// </summary>
    public async Task<TaxonomyStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var taxonomy = await LoadTaxonomyAsync(cancellationToken);
        var allCategories = taxonomy.GetAllCategories().ToList();
        var leafCategories = taxonomy.GetLeafCategories().ToList();

        return new TaxonomyStats
        {
            Version = taxonomy.Version,
            TotalVerticals = taxonomy.Verticals.Count,
            TotalCategories = allCategories.Count,
            LeafCategories = leafCategories.Count,
            MaxDepth = allCategories.Max(c => c.Level),
            VerticalStats = taxonomy.Verticals.Select(v => new VerticalStats
            {
                Name = v.Name,
                Prefix = v.Prefix,
                TotalCategories = v.Categories.Count,
                LeafCategories = v.Categories.Count(c => c.Children.Count == 0)
            }).ToList()
        };
    }
}

/// <summary>
/// Statistics about the loaded taxonomy.
/// </summary>
public class TaxonomyStats
{
    public string Version { get; set; } = string.Empty;
    public int TotalVerticals { get; set; }
    public int TotalCategories { get; set; }
    public int LeafCategories { get; set; }
    public int MaxDepth { get; set; }
    public List<VerticalStats> VerticalStats { get; set; } = [];
}

/// <summary>
/// Statistics for a single vertical.
/// </summary>
public class VerticalStats
{
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int TotalCategories { get; set; }
    public int LeafCategories { get; set; }
}
