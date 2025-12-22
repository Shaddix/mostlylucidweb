using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mostlylucid.SegmentCommerce.Models;

public sealed record AttributeSet(
    IReadOnlyList<string> Sizes,
    IReadOnlyList<string> Shapes,
    IReadOnlyList<string> Colours);

public sealed record GadgetCategoryAttributes(
    string DisplayName,
    IReadOnlyDictionary<string, AttributeSet> Subcategories);

public sealed record GadgetAttributeCatalogue(
    IReadOnlyDictionary<string, GadgetCategoryAttributes> Categories);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(GadgetAttributeCatalogue))]
public partial class GadgetAttributeCatalogueJsonContext : JsonSerializerContext;
