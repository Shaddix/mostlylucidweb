using System.Text.RegularExpressions;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Mostlylucid.Markdig.FetchExtension.Models;

namespace Mostlylucid.Markdig.FetchExtension.Parsers;

/// <summary>
/// Parser for [TOC] markers in markdown
/// Supports: [TOC], [TOC cssclass="my-class"], [TOC cssclass='my-class'], [TOC cssclass=my-class]
/// </summary>
public partial class TocBlockParser : BlockParser
{
    [GeneratedRegex(@"^\s*\[TOC(?:\s+cssclass\s*=\s*[""']?([a-zA-Z0-9_-]+)[""']?)?\s*\]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TocRegex();

    public TocBlockParser()
    {
        OpeningCharacters = new[] { '[' };
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
        {
            return BlockState.None;
        }
    
        var line = processor.Line;
        var startPosition = line.Start;

        // Quick check - must start with [TOC
        if (line.Length < 5 || line.Text[startPosition] != '[')
            return BlockState.None;

        // Get the full line text
        var lineText = line.ToString().Trim();

        var match = TocRegex().Match(lineText);
 
        if (!match.Success)
            return BlockState.None;

        // Parse optional CSS class from cssclass="..." attribute
        // Group 1 contains the class name if present
        var minLevel = 1;
        var maxLevel = 6;
        string? cssClass = null;

        if (match.Groups[1].Success && !string.IsNullOrEmpty(match.Groups[1].Value))
        {
            cssClass = match.Groups[1].Value;
        }

        // Create the TOC block
        var tocBlock = new TocBlock(this)
        {
            MinLevel = minLevel,
            MaxLevel = maxLevel,
            CssClass = cssClass,
            Column = processor.Column,
            Span = new SourceSpan(startPosition, line.End)
        };

        processor.NewBlocks.Push(tocBlock);

        // Consume the line
        processor.GoToColumn(processor.Column + line.End - line.Start + 1);

        return BlockState.BreakDiscard;
    }

    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        return BlockState.None;
    }
}
