using Markdig;
using Xunit;

namespace Mostlylucid.Markdig.FetchExtension.Tests;

public class TocExtensionTests
{
    [Fact]
    public void TocExtension_BasicToc_GeneratesNavWithLinks()
    {
        // Arrange
        var markdown = @"# Article Title

[TOC]

## Introduction
Some intro text.

## Getting Started
### Installation
Install instructions.

### Configuration
Config details.

## Conclusion
Final thoughts.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"ml_toc\"", result);
        Assert.Contains("<ul>", result);
        Assert.Contains("<li>", result);
        Assert.Contains("<a href=\"#introduction\">Introduction</a>", result);
        Assert.Contains("<a href=\"#getting-started\">Getting Started</a>", result);
        Assert.Contains("<a href=\"#installation\">Installation</a>", result);
        Assert.Contains("<a href=\"#configuration\">Configuration</a>", result);
        Assert.Contains("<a href=\"#conclusion\">Conclusion</a>", result);
        Assert.Contains("</nav>", result);
    }

    [Fact]
    public void TocExtension_CustomClass_UsesCustomClass()
    {
        // Arrange
        var markdown = @"# Test

[TOC cssclass=""custom-toc""]

## Heading One
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"custom-toc\"", result);
        Assert.Contains("<a href=\"#heading-one\">Heading One</a>", result);
    }


    [Fact]
    public void TocExtension_NestedHeadings_GeneratesNestedLists()
    {
        // Arrange
        var markdown = @"# Title

[TOC]

## Parent Heading
### Child Heading
#### Grandchild Heading

## Another Parent
";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        // Should have nested ul tags
        var ulCount = result.Split("<ul>").Length - 1;
        Assert.True(ulCount >= 2, "Should have multiple nested <ul> elements");

        Assert.Contains("<a href=\"#parent-heading\">Parent Heading</a>", result);
        Assert.Contains("<a href=\"#child-heading\">Child Heading</a>", result);
        Assert.Contains("<a href=\"#grandchild-heading\">Grandchild Heading</a>", result);
    }

    [Fact]
    public void TocExtension_NoHeadings_GeneratesNoToc()
    {
        // Arrange
        var markdown = @"Some text without headings.

[TOC]

More text.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.DoesNotContain("<nav class=\"ml_toc\"", result);
        Assert.DoesNotContain("<ul>", result);
    }

    [Fact]
    public void TocExtension_HeadingsGetIds()
    {
        // Arrange
        var markdown = @"# Test Article

[TOC]

## My Heading
Content here.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        // Verify heading has ID attribute
        Assert.Contains("id=\"my-heading\"", result);
        Assert.Contains("<a href=\"#my-heading\">My Heading</a>", result);
    }

    [Fact]
    public void TocExtension_SpecialCharactersInHeadings_GeneratesSafeIds()
    {
        // Arrange
        var markdown = @"# Test

[TOC]

## Getting Started: Installation & Setup
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        // Special characters should be removed/converted
        Assert.Contains("id=\"getting-started-installation-setup\"", result);
        Assert.Contains("<a href=\"#getting-started-installation-setup\"", result);
    }

    [Fact]
    public void TocExtension_WithTrailingSpaces_Matches()
    {
        // Arrange
        var markdown = @"# Test

[TOC]

## Heading One
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"ml_toc\"", result);
        Assert.Contains("<a href=\"#heading-one\">Heading One</a>", result);
    }

    [Fact]
    public void TocExtension_WithLeadingSpaces_Matches()
    {
        // Arrange
        var markdown = @"# Test

  [TOC]

## Heading One
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"ml_toc\"", result);
        Assert.Contains("<a href=\"#heading-one\">Heading One</a>", result);
    }

    [Fact]
    public void TocExtension_SingleQuotes_UsesCustomClass()
    {
        // Arrange
        var markdown = @"# Test

[TOC cssclass='custom-toc']

## Heading One
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"custom-toc\"", result);
        Assert.Contains("<a href=\"#heading-one\">Heading One</a>", result);
    }

    [Fact]
    public void TocExtension_NoQuotes_UsesCustomClass()
    {
        // Arrange
        var markdown = @"# Test

[TOC cssclass=custom-toc]

## Heading One
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"custom-toc\"", result);
        Assert.Contains("<a href=\"#heading-one\">Heading One</a>", result);
    }

    [Fact]
    public void TocExtension_ExtraSpacesAroundEquals_UsesCustomClass()
    {
        // Arrange
        var markdown = @"# Test

[TOC  cssclass = ""custom-toc"" ]

## Heading One
Content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"custom-toc\"", result);
        Assert.Contains("<a href=\"#heading-one\">Heading One</a>", result);
    }

    [Fact]
    public void TocExtension_DocumentStartsAtH2_AutoAdjustsMinLevel()
    {
        // Arrange - No H1, starts at H2 (typical blog post)
        var markdown = @"[TOC]

## Introduction
Some intro text.

## Getting Started
### Installation
### Configuration

## Conclusion
Final thoughts.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"ml_toc\"", result);
        Assert.Contains("<!-- [TOC] Auto-adjusted minLevel from 1 to 2", result);
        Assert.Contains("<a href=\"#introduction\">Introduction</a>", result);
        Assert.Contains("<a href=\"#getting-started\">Getting Started</a>", result);
        Assert.Contains("<a href=\"#installation\">Installation</a>", result);
        Assert.Contains("<a href=\"#conclusion\">Conclusion</a>", result);
    }

    [Fact]
    public void TocExtension_DocumentStartsAtH3_AutoAdjustsMinLevel()
    {
        // Arrange - No H1 or H2, starts at H3
        var markdown = @"[TOC]

### First Section
Content.

### Second Section
#### Subsection
More content.

### Third Section
Final content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"ml_toc\"", result);
        Assert.Contains("<!-- [TOC] Auto-adjusted minLevel from 1 to 3", result);
        Assert.Contains("<a href=\"#first-section\">First Section</a>", result);
        Assert.Contains("<a href=\"#second-section\">Second Section</a>", result);
        Assert.Contains("<a href=\"#subsection\">Subsection</a>", result);
    }

    [Fact]
    public void TocExtension_DocumentWithH1_NoAutoAdjustment()
    {
        // Arrange - Has H1, no auto-adjustment needed
        var markdown = @"# Main Title

[TOC]

## Section One
Content.

## Section Two
More content.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.Contains("<nav class=\"ml_toc\"", result);
        Assert.DoesNotContain("Auto-adjusted", result);
        Assert.Contains("<a href=\"#section-one\">Section One</a>", result);
        Assert.Contains("<a href=\"#section-two\">Section Two</a>", result);
    }

    [Fact]
    public void TocExtension_EmptyDocument_RendersNothing()
    {
        // Arrange
        var markdown = @"Some text without headings.

[TOC]

More text.";

        var pipeline = new MarkdownPipelineBuilder()
            .UseToc()
            .Build();

        // Act
        var result = Markdown.ToHtml(markdown, pipeline);

        // Assert
        Assert.DoesNotContain("<nav class=\"ml_toc\"", result);
        Assert.Contains("<!-- [TOC] No headings found", result);
    }

}
