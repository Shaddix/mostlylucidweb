using Microsoft.AspNetCore.Mvc;

namespace Mostlylucid.SegmentCommerce.Controllers.Api;

/// <summary>
/// Generates SVG placeholder images for products that don't have generated images.
/// This avoids external dependencies on picsum.photos or other placeholder services.
/// </summary>
[ApiController]
[Route("api/placeholder")]
public class PlaceholderController : ControllerBase
{
    private static readonly Dictionary<string, (string bg, string fg, string icon)> CategoryStyles = new()
    {
        ["tech"] = ("#1a365d", "#90cdf4", "cpu"),
        ["fashion"] = ("#742a2a", "#fed7d7", "shirt"),
        ["home"] = ("#22543d", "#9ae6b4", "home"),
        ["sport"] = ("#744210", "#faf089", "activity"),
        ["books"] = ("#553c9a", "#d6bcfa", "book"),
        ["food"] = ("#7b341e", "#fbd38d", "coffee")
    };

    /// <summary>
    /// Generates an SVG placeholder image for a product.
    /// </summary>
    [HttpGet("{category}/{productName}")]
    [ResponseCache(Duration = 86400)] // Cache for 1 day
    public IActionResult GetPlaceholder(string category, string productName, [FromQuery] int w = 400, [FromQuery] int h = 400)
    {
        var (bg, fg, icon) = CategoryStyles.GetValueOrDefault(category.ToLower(), ("#4a5568", "#cbd5e0", "box"));
        
        // Create a deterministic "random" pattern based on product name hash
        var hash = GetStableHash(productName);
        var pattern = GeneratePattern(hash, w, h, fg);
        var iconSvg = GetIconSvg(icon, w, h, fg);
        
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">
              <rect width="100%" height="100%" fill="{bg}"/>
              {pattern}
              {iconSvg}
              <text x="50%" y="85%" text-anchor="middle" fill="{fg}" font-family="system-ui, sans-serif" font-size="14" opacity="0.7">
                {TruncateText(Uri.UnescapeDataString(productName), 30)}
              </text>
            </svg>
            """;

        return Content(svg, "image/svg+xml");
    }

    private static string GeneratePattern(int hash, int w, int h, string color)
    {
        var lines = new System.Text.StringBuilder();
        var random = new Random(hash);
        
        // Generate subtle geometric pattern
        for (int i = 0; i < 8; i++)
        {
            var x1 = random.Next(0, w);
            var y1 = random.Next(0, h);
            var x2 = random.Next(0, w);
            var y2 = random.Next(0, h);
            var opacity = 0.05 + random.NextDouble() * 0.1;
            
            lines.AppendLine($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{color}\" stroke-opacity=\"{opacity:F2}\" stroke-width=\"1\"/>");
        }
        
        // Add some circles for visual interest
        for (int i = 0; i < 5; i++)
        {
            var cx = random.Next(w / 4, 3 * w / 4);
            var cy = random.Next(h / 4, 3 * h / 4);
            var r = random.Next(20, 60);
            var opacity = 0.03 + random.NextDouble() * 0.07;
            
            lines.AppendLine($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{r}\" fill=\"{color}\" fill-opacity=\"{opacity:F2}\"/>");
        }
        
        return lines.ToString();
    }

    private static string GetIconSvg(string icon, int w, int h, string color)
    {
        var cx = w / 2;
        var cy = h / 2 - 20;
        var size = Math.Min(w, h) / 4;
        
        return icon switch
        {
            "cpu" => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <rect x="0" y="0" width="{size}" height="{size}" rx="4" fill="none" stroke="{color}" stroke-width="2" opacity="0.5"/>
                  <rect x="{size * 0.25}" y="{size * 0.25}" width="{size * 0.5}" height="{size * 0.5}" fill="{color}" opacity="0.3"/>
                  <line x1="-8" y1="{size * 0.3}" x2="0" y2="{size * 0.3}" stroke="{color}" stroke-width="2" opacity="0.5"/>
                  <line x1="-8" y1="{size * 0.7}" x2="0" y2="{size * 0.7}" stroke="{color}" stroke-width="2" opacity="0.5"/>
                  <line x1="{size}" y1="{size * 0.3}" x2="{size + 8}" y2="{size * 0.3}" stroke="{color}" stroke-width="2" opacity="0.5"/>
                  <line x1="{size}" y1="{size * 0.7}" x2="{size + 8}" y2="{size * 0.7}" stroke="{color}" stroke-width="2" opacity="0.5"/>
                </g>
                """,
            "shirt" => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <path d="M{size * 0.3},0 L0,{size * 0.3} L{size * 0.2},{size * 0.4} L{size * 0.2},{size} L{size * 0.8},{size} L{size * 0.8},{size * 0.4} L{size},{size * 0.3} L{size * 0.7},0 L{size * 0.6},{size * 0.15} L{size * 0.4},{size * 0.15} Z" 
                        fill="{color}" opacity="0.3" stroke="{color}" stroke-width="2" stroke-opacity="0.5"/>
                </g>
                """,
            "home" => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <path d="M{size / 2},0 L0,{size * 0.5} L{size * 0.15},{size * 0.5} L{size * 0.15},{size} L{size * 0.85},{size} L{size * 0.85},{size * 0.5} L{size},{size * 0.5} Z" 
                        fill="{color}" opacity="0.3" stroke="{color}" stroke-width="2" stroke-opacity="0.5"/>
                  <rect x="{size * 0.35}" y="{size * 0.6}" width="{size * 0.3}" height="{size * 0.4}" fill="{color}" opacity="0.5"/>
                </g>
                """,
            "activity" => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <polyline points="0,{size * 0.5} {size * 0.25},{size * 0.5} {size * 0.35},{size * 0.2} {size * 0.5},{size * 0.8} {size * 0.65},{size * 0.3} {size * 0.75},{size * 0.5} {size},{size * 0.5}"
                            fill="none" stroke="{color}" stroke-width="3" stroke-opacity="0.5"/>
                </g>
                """,
            "book" => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <rect x="0" y="0" width="{size * 0.45}" height="{size}" fill="{color}" opacity="0.3"/>
                  <rect x="{size * 0.55}" y="0" width="{size * 0.45}" height="{size}" fill="{color}" opacity="0.3"/>
                  <line x1="{size * 0.5}" y1="0" x2="{size * 0.5}" y2="{size}" stroke="{color}" stroke-width="2" opacity="0.5"/>
                </g>
                """,
            "coffee" => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <path d="M{size * 0.1},{size * 0.3} L{size * 0.2},{size} L{size * 0.8},{size} L{size * 0.9},{size * 0.3} Z" 
                        fill="{color}" opacity="0.3" stroke="{color}" stroke-width="2" stroke-opacity="0.5"/>
                  <ellipse cx="{size * 0.5}" cy="{size * 0.3}" rx="{size * 0.4}" ry="{size * 0.1}" fill="{color}" opacity="0.2"/>
                  <path d="M{size * 0.85},{size * 0.4} Q{size * 1.1},{size * 0.5} {size * 0.85},{size * 0.7}" 
                        fill="none" stroke="{color}" stroke-width="2" stroke-opacity="0.5"/>
                </g>
                """,
            _ => $"""
                <g transform="translate({cx - size / 2}, {cy - size / 2})">
                  <rect x="0" y="{size * 0.2}" width="{size}" height="{size * 0.8}" fill="{color}" opacity="0.3" stroke="{color}" stroke-width="2" stroke-opacity="0.5"/>
                  <polygon points="{size * 0.1},0 {size * 0.5},{size * 0.2} {size * 0.9},0 {size * 0.1},0" fill="{color}" opacity="0.2"/>
                </g>
                """
        };
    }

    /// <summary>
    /// Generates a circular SVG avatar placeholder for a seller.
    /// </summary>
    [HttpGet("seller/{sellerName}")]
    [ResponseCache(Duration = 86400)] // Cache for 1 day
    public IActionResult GetSellerPlaceholder(string sellerName, [FromQuery] int w = 48, [FromQuery] int h = 48)
    {
        var name = Uri.UnescapeDataString(sellerName);
        var initials = GetInitials(name);
        var hash = GetStableHash(name);
        
        // Generate a nice color based on the name hash
        var colors = new[]
        {
            ("#3182ce", "#ebf8ff"), // blue
            ("#38a169", "#f0fff4"), // green
            ("#d69e2e", "#fffff0"), // yellow
            ("#e53e3e", "#fff5f5"), // red
            ("#805ad5", "#faf5ff"), // purple
            ("#dd6b20", "#fffaf0"), // orange
            ("#319795", "#e6fffa"), // teal
            ("#d53f8c", "#fff5f7")  // pink
        };
        var (bg, fg) = colors[hash % colors.Length];
        
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">
              <circle cx="{w / 2}" cy="{h / 2}" r="{Math.Min(w, h) / 2}" fill="{bg}"/>
              <text x="50%" y="50%" text-anchor="middle" dy="0.35em" fill="{fg}" font-family="system-ui, sans-serif" font-size="{Math.Min(w, h) / 2.5}" font-weight="600">
                {System.Security.SecurityElement.Escape(initials)}
              </text>
            </svg>
            """;

        return Content(svg, "image/svg+xml");
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
        
        return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}";
    }

    private static int GetStableHash(string input)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
            {
                hash = hash * 31 + c;
            }
            return Math.Abs(hash);
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return System.Security.SecurityElement.Escape(text);
        return System.Security.SecurityElement.Escape(text[..(maxLength - 3)]) + "...";
    }
}
