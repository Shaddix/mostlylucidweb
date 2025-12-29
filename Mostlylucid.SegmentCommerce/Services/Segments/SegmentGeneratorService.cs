using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;

namespace Mostlylucid.SegmentCommerce.Services.Segments;

/// <summary>
/// Generates dynamic segments from data patterns using LLM for naming.
/// </summary>
public interface ISegmentGeneratorService
{
    /// <summary>
    /// Generate segments from current category/product data.
    /// </summary>
    Task<List<SegmentEntity>> GenerateCategorySegmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Generate behavioral segments from profile patterns.
    /// </summary>
    Task<List<SegmentEntity>> GenerateBehavioralSegmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Regenerate names/descriptions for existing segments using LLM.
    /// </summary>
    Task RegenerateSegmentNamesAsync(CancellationToken ct = default);

    /// <summary>
    /// Seed default segments if none exist.
    /// </summary>
    Task SeedDefaultSegmentsAsync(CancellationToken ct = default);
}

public class SegmentGeneratorService : ISegmentGeneratorService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<SegmentGeneratorService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SegmentGeneratorService(
        SegmentCommerceDbContext db,
        HttpClient httpClient,
        IConfiguration config,
        ILogger<SegmentGeneratorService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<List<SegmentEntity>> GenerateCategorySegmentsAsync(CancellationToken ct = default)
    {
        var segments = new List<SegmentEntity>();

        // Get distinct categories with product counts
        var categories = await _db.Products
            .Where(p => p.Status == ProductStatus.Active)
            .GroupBy(p => p.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .Where(x => x.Count >= 3) // Only categories with enough products
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        foreach (var cat in categories)
        {
            var slug = $"interested-in-{cat.Category}";
            
            // Check if already exists
            if (await _db.Segments.AnyAsync(s => s.Slug == slug, ct))
                continue;

            // Generate name via LLM
            var (name, description, icon) = await GenerateSegmentNameAsync(
                cat.Category,
                SegmentType.CategoryBased,
                ct);

            var segment = new SegmentEntity
            {
                Slug = slug,
                Name = name,
                Description = description,
                Icon = icon,
                Color = GetColorForCategory(cat.Category),
                Type = SegmentType.CategoryBased,
                Rules =
                [
                    new SegmentRuleData
                    {
                        RuleType = "CategoryInterest",
                        Field = $"interests.{cat.Category}",
                        Operator = "gte",
                        Value = 0.4,
                        Weight = 1.0,
                        Description = $"Interest in {cat.Category} > 40%"
                    }
                ],
                RuleCombination = RuleCombinationType.Weighted,
                MembershipThreshold = 0.35,
                Tags = ["category", cat.Category],
                IsSystem = true,
                LlmModel = _config["Ollama:Model"] ?? "llama3.2"
            };

            segments.Add(segment);
            _db.Segments.Add(segment);
        }

        await _db.SaveChangesAsync(ct);
        return segments;
    }

    public async Task<List<SegmentEntity>> GenerateBehavioralSegmentsAsync(CancellationToken ct = default)
    {
        var segments = new List<SegmentEntity>();

        // Define behavioral segment templates
        var templates = new[]
        {
            new BehavioralTemplate("high-value", SegmentType.Behavioral, new[]
            {
                new SegmentRuleData { RuleType = "Statistic", Field = "totalPurchases", Operator = "gte", Value = 3, Weight = 0.4 },
                new SegmentRuleData { RuleType = "PriceRange", Field = "priceRange", Operator = "between", Value = "100-10000", Weight = 0.3 },
                new SegmentRuleData { RuleType = "Recency", Field = "lastSeen", Operator = "lt", Value = 30, Weight = 0.3 }
            }),
            new BehavioralTemplate("cart-abandoner", SegmentType.Behavioral, new[]
            {
                new SegmentRuleData { RuleType = "Statistic", Field = "totalCartAdds", Operator = "gte", Value = 3, Weight = 0.5 },
                new SegmentRuleData { RuleType = "Statistic", Field = "totalPurchases", Operator = "lt", Value = 2, Weight = 0.5 }
            }),
            new BehavioralTemplate("bargain-seeker", SegmentType.PriceBased, new[]
            {
                new SegmentRuleData { RuleType = "PriceRange", Field = "priceRange", Operator = "between", Value = "0-75", Weight = 0.5 },
                new SegmentRuleData { RuleType = "Trait", Field = "traits.prefersDeals", Operator = "eq", Value = true, Weight = 0.5 }
            }),
            new BehavioralTemplate("new-visitor", SegmentType.Lifecycle, new[]
            {
                new SegmentRuleData { RuleType = "Statistic", Field = "totalSessions", Operator = "lte", Value = 2, Weight = 0.5 },
                new SegmentRuleData { RuleType = "Statistic", Field = "totalPurchases", Operator = "eq", Value = 0, Weight = 0.5 }
            }),
            new BehavioralTemplate("loyal-customer", SegmentType.Lifecycle, new[]
            {
                new SegmentRuleData { RuleType = "Statistic", Field = "totalPurchases", Operator = "gte", Value = 5, Weight = 0.4 },
                new SegmentRuleData { RuleType = "Statistic", Field = "totalSessions", Operator = "gte", Value = 10, Weight = 0.3 },
                new SegmentRuleData { RuleType = "Recency", Field = "lastSeen", Operator = "lt", Value = 14, Weight = 0.3 }
            }),
            new BehavioralTemplate("researcher", SegmentType.Behavioral, new[]
            {
                new SegmentRuleData { RuleType = "Statistic", Field = "totalSignals", Operator = "gte", Value = 20, Weight = 0.5 },
                new SegmentRuleData { RuleType = "Statistic", Field = "totalPurchases", Operator = "lte", Value = 3, Weight = 0.5 }
            })
        };

        foreach (var template in templates)
        {
            if (await _db.Segments.AnyAsync(s => s.Slug == template.Slug, ct))
                continue;

            var (name, description, icon) = await GenerateSegmentNameAsync(
                template.Slug,
                template.Type,
                ct);

            var segment = new SegmentEntity
            {
                Slug = template.Slug,
                Name = name,
                Description = description,
                Icon = icon,
                Color = GetColorForBehavior(template.Slug),
                Type = template.Type,
                Rules = template.Rules.ToList(),
                RuleCombination = RuleCombinationType.Weighted,
                MembershipThreshold = 0.4,
                Tags = ["behavioral", template.Type.ToString().ToLowerInvariant()],
                IsSystem = true,
                LlmModel = _config["Ollama:Model"] ?? "llama3.2"
            };

            segments.Add(segment);
            _db.Segments.Add(segment);
        }

        await _db.SaveChangesAsync(ct);
        return segments;
    }

    public async Task RegenerateSegmentNamesAsync(CancellationToken ct = default)
    {
        var segments = await _db.Segments.ToListAsync(ct);

        foreach (var segment in segments)
        {
            var (name, description, icon) = await GenerateSegmentNameAsync(
                segment.Slug,
                segment.Type,
                ct);

            segment.Name = name;
            segment.Description = description;
            segment.Icon = icon;
            segment.UpdatedAt = DateTime.UtcNow;
            segment.LlmModel = _config["Ollama:Model"] ?? "llama3.2";
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SeedDefaultSegmentsAsync(CancellationToken ct = default)
    {
        if (await _db.Segments.AnyAsync(ct))
        {
            _logger.LogDebug("Segments already exist, skipping seed");
            return;
        }

        _logger.LogInformation("Seeding default segments...");

        // Generate category-based segments from existing products
        await GenerateCategorySegmentsAsync(ct);

        // Generate behavioral segments
        await GenerateBehavioralSegmentsAsync(ct);

        _logger.LogInformation("Segment seeding complete");
    }

    private async Task<(string Name, string Description, string Icon)> GenerateSegmentNameAsync(
        string slugOrCategory,
        SegmentType type,
        CancellationToken ct)
    {
        // Check if LLM naming is enabled (disabled by default for faster startup)
        var useLlmNaming = _config.GetValue("Segments:UseLlmNaming", false);
        if (!useLlmNaming)
        {
            _logger.LogDebug("LLM naming disabled, using template names for segment: {Slug}", slugOrCategory);
            return (
                FallbackName(slugOrCategory),
                FallbackDescription(slugOrCategory),
                FallbackIcon(type)
            );
        }

        var ollamaUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ollama:Model"] ?? "llama3.2";

        var prompt = type switch
        {
            SegmentType.CategoryBased => 
                $"Generate a short, catchy marketing segment name for shoppers interested in \"{slugOrCategory}\" products.\n\n" +
                "Return JSON only:\n" +
                "{\n" +
                "  \"name\": \"2-4 word segment name (e.g., 'Tech Enthusiasts', 'Fashion Forward')\",\n" +
                "  \"description\": \"One sentence describing who belongs to this segment\",\n" +
                "  \"icon\": \"Single emoji that represents this segment\"\n" +
                "}",
            _ => 
                $"Generate a short, catchy marketing segment name for the behavioral pattern: \"{slugOrCategory.Replace("-", " ")}\".\n\n" +
                "Return JSON only:\n" +
                "{\n" +
                "  \"name\": \"2-4 word segment name (e.g., 'Deal Hunters', 'Loyal VIPs')\",\n" +
                "  \"description\": \"One sentence describing who belongs to this segment\",\n" +
                "  \"icon\": \"Single emoji that represents this segment\"\n" +
                "}"
        };

        try
        {
            var request = new
            {
                model,
                prompt,
                stream = false
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // Timeout for LLM

            var response = await _httpClient.PostAsJsonAsync(
                $"{ollamaUrl}/api/generate",
                request,
                cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(JsonOptions, cts.Token);
                if (result?.Response != null)
                {
                    var parsed = ParseLlmJson(result.Response);
                    if (parsed != null)
                    {
                        return (
                            parsed.Name ?? FallbackName(slugOrCategory),
                            parsed.Description ?? FallbackDescription(slugOrCategory),
                            parsed.Icon ?? "👤"
                        );
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM request timed out for segment: {Slug}, using fallback", slugOrCategory);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("LLM not available ({Message}), using fallback names", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate segment name via LLM, using fallback");
        }

        // Fallback if LLM fails
        return (
            FallbackName(slugOrCategory),
            FallbackDescription(slugOrCategory),
            FallbackIcon(type)
        );
    }

    private static SegmentNameResponse? ParseLlmJson(string response)
    {
        // Try to extract JSON from response
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            try
            {
                return JsonSerializer.Deserialize<SegmentNameResponse>(jsonStr, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string FallbackName(string slug)
    {
        var words = slug.Replace("-", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    private static string FallbackDescription(string slug)
    {
        return $"Shoppers matching the {slug.Replace("-", " ")} pattern.";
    }

    private static string FallbackIcon(SegmentType type) => type switch
    {
        SegmentType.CategoryBased => "🏷️",
        SegmentType.Behavioral => "📊",
        SegmentType.Lifecycle => "🔄",
        SegmentType.PriceBased => "💰",
        _ => "👤"
    };

    private static string GetColorForCategory(string category) => category.ToLowerInvariant() switch
    {
        "tech" or "electronics" => "#3b82f6",
        "fashion" or "clothing" => "#ec4899",
        "home" or "furniture" => "#14b8a6",
        "sport" or "fitness" => "#06b6d4",
        "food" or "grocery" => "#22c55e",
        "beauty" or "cosmetics" => "#f472b6",
        "toys" or "games" => "#f59e0b",
        "books" or "media" => "#8b5cf6",
        _ => "#6366f1"
    };

    private static string GetColorForBehavior(string slug) => slug switch
    {
        "high-value" => "#8b5cf6",
        "cart-abandoner" => "#ef4444",
        "bargain-seeker" => "#22c55e",
        "new-visitor" => "#f59e0b",
        "loyal-customer" => "#eab308",
        "researcher" => "#6366f1",
        _ => "#64748b"
    };

    private record BehavioralTemplate(string Slug, SegmentType Type, SegmentRuleData[] Rules);
    private record OllamaResponse(string Response);
    private record SegmentNameResponse(string? Name, string? Description, string? Icon);
}
