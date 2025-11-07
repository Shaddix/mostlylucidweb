using Markdig.Parsers;
using Markdig.Syntax;

namespace Mostlylucid.Markdig.FetchExtension.Models;

/// <summary>
/// Block element representing a Table of Contents placeholder
/// </summary>
public class TocBlock : Block
{
    public TocBlock(BlockParser? parser) : base(parser)
    {
    }

    /// <summary>
    /// Minimum heading level to include (default: 1 = H1)
    /// </summary>
    public int MinLevel { get; set; } = 1;

    /// <summary>
    /// Maximum heading level to include (default: 6 = H6)
    /// </summary>
    public int MaxLevel { get; set; } = 6;

    /// <summary>
    /// CSS class to apply to the TOC container
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Title to display above TOC (optional)
    /// </summary>
    public string? Title { get; set; }
}
