using FluentAssertions;
using Mostlylucid.BlogLLM.Services;
using Xunit;

namespace Mostlylucid.BlogLLM.Tests;

public class MarkdownParserTests
{
    private readonly MarkdownParserService _parser;
    private readonly string _testFilePath;

    public MarkdownParserTests()
    {
        _parser = new MarkdownParserService();
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.md");
    }

    [Fact]
    public void ParseFile_ShouldExtractTitle()
    {
        // Arrange
        var markdown = "# Test Blog Post\n\nContent here...";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document = _parser.ParseFile(_testFilePath);

        // Assert
        document.Title.Should().Be("Test Blog Post");

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void ParseFile_ShouldExtractCategories()
    {
        // Arrange
        var markdown = @"# Title

<!-- category -- AI, LLM, Tutorial -->

Content...";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document = _parser.ParseFile(_testFilePath);

        // Assert
        document.Categories.Should().Equal("AI", "LLM", "Tutorial");

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void ParseFile_ShouldExtractPublishedDate()
    {
        // Arrange
        var markdown = @"# Title

<!-- category -- Test -->
<datetime class=""hidden"">2025-01-15T12:30</datetime>

Content...";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document = _parser.ParseFile(_testFilePath);

        // Assert
        document.PublishedDate.Year.Should().Be(2025);
        document.PublishedDate.Month.Should().Be(1);
        document.PublishedDate.Day.Should().Be(15);

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void ParseFile_ShouldExtractLanguageFromFilename()
    {
        // Arrange
        var spanishFilePath = Path.Combine(Path.GetTempPath(), "test-post.es.md");
        var markdown = "# Test\n\nContent...";
        File.WriteAllText(spanishFilePath, markdown);

        // Act
        var document = _parser.ParseFile(spanishFilePath);

        // Assert
        document.Language.Should().Be("es");

        // Cleanup
        File.Delete(spanishFilePath);
    }

    [Fact]
    public void ParseFile_ShouldExtractSections()
    {
        // Arrange
        var markdown = @"# Main Title

## Introduction

This is the intro.

## Section 1

Content for section 1.

### Subsection 1.1

Nested content.

## Section 2

More content.";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document = _parser.ParseFile(_testFilePath);

        // Assert
        document.Sections.Should().HaveCount(4);
        document.Sections[0].Heading.Should().Be("Introduction");
        document.Sections[0].Level.Should().Be(2);

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void ParseFile_ShouldExtractCodeBlocks()
    {
        // Arrange
        var markdown = @"# Title

## Code Example

```csharp
public class Test
{
    public string Name { get; set; }
}
```

Some text after.";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document = _parser.ParseFile(_testFilePath);

        // Assert
        var section = document.Sections.First(s => s.Heading == "Code Example");
        section.CodeBlocks.Should().HaveCount(1);
        section.CodeBlocks[0].Language.Should().Be("csharp");
        section.CodeBlocks[0].Code.Should().Contain("public class Test");

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void ParseFile_ShouldCountWords()
    {
        // Arrange
        var markdown = "# Title\n\nThis is a test with ten words in total content.";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document = _parser.ParseFile(_testFilePath);

        // Assert
        document.WordCount.Should().BeGreaterThan(0);

        // Cleanup
        File.Delete(_testFilePath);
    }

    [Fact]
    public void ParseFile_ShouldGenerateContentHash()
    {
        // Arrange
        var markdown = "# Title\n\nContent";
        File.WriteAllText(_testFilePath, markdown);

        // Act
        var document1 = _parser.ParseFile(_testFilePath);

        // Modify content
        File.WriteAllText(_testFilePath, "# Title\n\nDifferent Content");
        var document2 = _parser.ParseFile(_testFilePath);

        // Assert
        document1.ContentHash.Should().NotBe(document2.ContentHash);

        // Cleanup
        File.Delete(_testFilePath);
    }
}
