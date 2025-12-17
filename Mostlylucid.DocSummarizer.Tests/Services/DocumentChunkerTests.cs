using Xunit;
using Mostlylucid.DocSummarizer.Models;
using Mostlylucid.DocSummarizer.Services;

namespace Mostlylucid.DocSummarizer.Tests.Services;

public class DocumentChunkerTests
{
    private readonly DocumentChunker _chunker = new();

    [Fact]
    public void ChunkByStructure_WithValidMarkdown_ReturnsChunks()
    {
        // Arrange
        var markdown = @"
# Main Heading
This is the main content.

## Sub Heading
This is sub content.

### Sub Sub Heading
This is sub sub content.

Regular paragraph without heading.
";

        // Act
        var chunks = _chunker.ChunkByStructure(markdown);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("Main Heading", chunks[0].Heading);
        Assert.Equal(1, chunks[0].HeadingLevel);
        Assert.Equal("Sub Heading", chunks[1].Heading);
        Assert.Equal(2, chunks[1].HeadingLevel);
        Assert.Equal("Sub Sub Heading", chunks[2].Heading);
        Assert.Equal(3, chunks[2].HeadingLevel);
    }

    [Fact]
    public void ChunkByStructure_WithEmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var markdown = "";

        // Act
        var chunks = _chunker.ChunkByStructure(markdown);

        // Assert
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkByStructure_WithNoHeadings_ReturnsSingleChunk()
    {
        // Arrange
        var markdown = "This is a regular paragraph without any headings.";

        // Act
        var chunks = _chunker.ChunkByStructure(markdown);

        // Assert
        Assert.Single(chunks);
        Assert.Equal(string.Empty, chunks[0].Heading);
        Assert.Equal(0, chunks[0].HeadingLevel);
    }

    [Fact]
    public void ChunkByStructure_WithMultipleSections_ReturnsCorrectOrder()
    {
        // Arrange
        var markdown = @"
# First Section
Content for first section.

# Second Section
Content for second section.

## Subsection
Content for subsection.
";

        // Act
        var chunks = _chunker.ChunkByStructure(markdown);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("First Section", chunks[0].Heading);
        Assert.Equal(1, chunks[0].HeadingLevel);
        Assert.Equal("Second Section", chunks[1].Heading);
        Assert.Equal(1, chunks[1].HeadingLevel);
        Assert.Equal("Subsection", chunks[2].Heading);
        Assert.Equal(2, chunks[2].HeadingLevel);
    }

    [Fact]
    public void ChunkByStructure_WithCodeBlocks_DoesNotMistakeAsHeadings()
    {
        // Arrange
        var markdown = @"
# Main Heading
Some content.

```csharp
// This is code
var x = 1;
```

## Another Heading
More content.
";

        // Act
        var chunks = _chunker.ChunkByStructure(markdown);

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.Equal("Main Heading", chunks[0].Heading);
        Assert.Equal("Another Heading", chunks[1].Heading);
    }
}