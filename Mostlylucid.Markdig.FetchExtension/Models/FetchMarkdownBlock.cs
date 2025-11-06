using Markdig.Parsers;
using Markdig.Syntax;

namespace Mostlylucid.Markdig.FetchExtension.Models;

/// <summary>
///     Block element to represent a fetch directive when used as a block (on its own line).
/// </summary>
public class FetchMarkdownBlock : LeafBlock
{
    public FetchMarkdownBlock(BlockParser? parser) : base(parser)
    {
    }

    public string Url { get; set; } = string.Empty;
    public int PollFrequencyHours { get; set; }
    public string FetchedContent { get; set; } = string.Empty;
    public bool FetchSuccessful { get; set; }
}