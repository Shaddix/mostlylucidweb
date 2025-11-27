# Table of Contents Extension Enhancements (v1.1.0)

## Overview

Version 1.1.0 brings major improvements to the TOC (Table of Contents) extension, focusing on usability, flexibility, and robustness.

## Key Features

### 1. Smart Heading Level Auto-Detection

**Problem Solved**: Previously, if your document started at H2 (common in blog posts), the TOC would be empty because it was looking for H1 headers by default.

**Solution**: The TOC extension now automatically detects the lowest heading level in your document and adjusts accordingly.

**Example**:
```markdown
[TOC]

## Introduction
Some intro text.

## Getting Started
### Installation
### Configuration

## Conclusion
Final thoughts.
```

**Result**:
- The TOC detects that the document starts at H2
- Automatically adjusts minimum level from H1 to H2
- Generates a complete TOC with all sections
- Includes HTML comment: `<!-- [TOC] Auto-adjusted minLevel from 1 to 2 (document starts at H2) -->`

### 2. Flexible Syntax Parsing

**New Syntax Options** for the `cssclass` attribute:

| Syntax | Description | Version |
|--------|-------------|---------|
| `[TOC cssclass="my-class"]` | Double quotes | v1.0.0 |
| `[TOC cssclass='my-class']` | Single quotes | v1.1.0+ |
| `[TOC cssclass=my-class]` | No quotes | v1.1.0+ |
| `[TOC  cssclass = "my-class" ]` | Extra whitespace | v1.1.0+ |

**Whitespace Handling**:
- Leading whitespace before `[TOC]` is now ignored
- Trailing whitespace after `[TOC]` is now ignored
- Extra spaces around the `=` sign are handled

**Updated Regex Pattern**:
```csharp
^\s*\[TOC(?:\s+cssclass\s*=\s*["']?([a-zA-Z0-9_-]+)["']?)?\s*\]\s*$
```

### 3. Improved Error Handling

**Empty Documents**:
```markdown
Some text without headings.

[TOC]

More text.
```
**Output**: `<!-- [TOC] No headings found (max level: 6). Document has X descendants, Y are HeadingBlocks -->`

**Missing Document**:
**Output**: `<!-- [TOC] ERROR: Could not find document -->`

**No Headings in Range**:
**Output**: `<!-- [TOC] No headings found after filtering (effective range: X-Y) -->`

### 4. Performance Improvements

**Before v1.1.0**:
- Debug console logging throughout the code
- Multiple passes over the document tree
- Verbose output in production

**After v1.1.0**:
- All debug console logging removed
- Single-pass heading collection and filtering
- Clean HTML output with optional debug comments
- More efficient heading processing

### 5. Pipeline Order Requirement (Breaking Change)

**IMPORTANT**: As of v1.1.0, the `UseToc()` extension MUST be registered last in your Markdig pipeline.

**Correct Usage**:
```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .Use<FetchMarkdownExtension>()
    .Use<ImgExtension>()
    .Use<LinkRewriteExtension>()
    .UseToc()  // MUST be last!
    .Build();
```

**Why**: This ensures proper interaction with other Markdig extensions and guarantees that the TOC sees the final document structure.

## Test Coverage

Comprehensive test coverage added for all new features:

### New Test Cases:
1. **Whitespace Handling**:
   - `TocExtension_WithTrailingSpaces_Matches`
   - `TocExtension_WithLeadingSpaces_Matches`

2. **Flexible Syntax**:
   - `TocExtension_SingleQuotes_UsesCustomClass`
   - `TocExtension_NoQuotes_UsesCustomClass`
   - `TocExtension_ExtraSpacesAroundEquals_UsesCustomClass`

3. **Auto-Adjustment**:
   - `TocExtension_DocumentStartsAtH2_AutoAdjustsMinLevel`
   - `TocExtension_DocumentStartsAtH3_AutoAdjustsMinLevel`
   - `TocExtension_DocumentWithH1_NoAutoAdjustment`

4. **Edge Cases**:
   - `TocExtension_EmptyDocument_RendersNothing`

## Migration Guide

### From v1.0.0 to v1.1.0

**Breaking Change**: Pipeline order
```csharp
// ❌ OLD - May cause issues
var pipeline = new MarkdownPipelineBuilder()
    .UseToc()
    .UseAdvancedExtensions()
    .Build();

// ✅ NEW - Correct order
var pipeline = new MarkdownPipelineBuilder()
    .UseAdvancedExtensions()
    .UseToc()  // Last!
    .Build();
```

**No other breaking changes** - All existing `[TOC]` syntax continues to work.

## Usage Examples

### Example 1: Blog Post (Starts at H2)
```markdown
# My Blog Post Title

[TOC]

## Introduction
Welcome to my blog post...

## Main Content
### Subsection A
### Subsection B

## Conclusion
Thanks for reading!
```

**Output**: TOC automatically includes Introduction, Main Content (with subsections), and Conclusion.

### Example 2: Custom CSS Class
```markdown
[TOC cssclass='my-custom-toc']

## Section 1
## Section 2
```

**Output**:
```html
<nav class="my-custom-toc" aria-label="Table of Contents">
  <ul>
    <li><a href="#section-1">Section 1</a></li>
    <li><a href="#section-2">Section 2</a></li>
  </ul>
</nav>
```

### Example 3: Flexible Whitespace
```markdown
  [TOC  cssclass = "docs-toc"  ]

## Getting Started
## Advanced Topics
```

**Output**: Works perfectly despite extra whitespace!

## Benefits

1. **Better User Experience**: No more empty TOCs for blog posts
2. **More Flexible**: Multiple syntax options for cssclass
3. **Cleaner Output**: Removed debug logging
4. **Better Debugging**: Informative HTML comments when things go wrong
5. **Production Ready**: Optimized performance and robust error handling

## Technical Details

### Auto-Adjustment Algorithm

```csharp
// 1. Collect all headings within max level
var allHeadings = document.Descendants()
    .OfType<HeadingBlock>()
    .Where(h => h.Level <= obj.MaxLevel)
    .ToList();

// 2. Find the minimum heading level present
var actualMinLevel = allHeadings.Min(h => h.Level);

// 3. Use the higher of configured min or detected min
var effectiveMinLevel = Math.Max(obj.MinLevel, actualMinLevel);

// 4. Filter headings by effective range
var filteredHeadings = allHeadings
    .Where(h => h.Level >= effectiveMinLevel && h.Level <= obj.MaxLevel)
    .ToList();
```

### Debug Comments

In development, you'll see helpful comments:
- `<!-- [TOC] Auto-adjusted minLevel from 1 to 2 (document starts at H2) -->`
- `<!-- [TOC] No headings found (max level: 6). Document has 10 descendants, 0 are HeadingBlocks -->`

These help diagnose issues without cluttering production logs.

## Resources

- [Main README](./README.md) - Full package documentation
- [Release Notes](./release-notes.txt) - Version history
- [Integration Guide](./INTEGRATION_GUIDE.md) - Integration examples
- [GitHub Repository](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.Markdig.FetchExtension)

## Feedback

Found a bug or have a feature request? [Open an issue on GitHub](https://github.com/scottgal/mostlylucidweb/issues)
