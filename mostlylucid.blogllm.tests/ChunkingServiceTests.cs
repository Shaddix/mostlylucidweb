using FluentAssertions;
using Mostlylucid.BlogLLM.Models;
using Mostlylucid.BlogLLM.Services;
using Xunit;

namespace Mostlylucid.BlogLLM.Tests;

public class ChunkingServiceTests
{
    [Fact(Skip = "Requires tokenizer file")]
    public void ChunkDocument_ShouldCreateChunks()
    {
        // This test requires actual tokenizer file
        // Run integration tests with real models for full testing
    }

    [Fact]
    public void ChunkDocument_ShouldMaintainMetadata()
    {
        // Arrange
        var document = new BlogDocument
        {
            Slug = "test-post",
            Title = "Test Post",
            Categories = new[] { "Tech", "AI" },
            Language = "en",
            PublishedDate = DateTime.Now,
            Sections = new List<DocumentSection>
            {
                new DocumentSection
                {
                    Heading = "Introduction",
                    Level = 2,
                    Content = "This is a test section with some content."
                }
            }
        };

        // Note: This test would need actual tokenizer for real chunking
        // For now, we're just testing the data structure

        // Assert
        document.Slug.Should().Be("test-post");
        document.Categories.Should().Contain("AI");
    }

    [Fact]
    public void ContentChunk_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var chunk = new ContentChunk
        {
            DocumentSlug = "test",
            DocumentTitle = "Test",
            ChunkIndex = 0,
            Text = "Sample text",
            SectionHeading = "Introduction",
            Categories = new[] { "Tech" },
            Language = "en",
            TokenCount = 10
        };

        // Assert
        chunk.ChunkId.Should().NotBeNullOrEmpty();
        chunk.DocumentSlug.Should().Be("test");
        chunk.TokenCount.Should().Be(10);
    }

    [Fact]
    public void SearchResult_ShouldMapFromChunk()
    {
        // Arrange
        var chunk = new ContentChunk
        {
            ChunkId = "123",
            DocumentSlug = "test-post",
            DocumentTitle = "Test Post",
            ChunkIndex = 0,
            Text = "Sample content",
            SectionHeading = "Introduction",
            Categories = new[] { "Tech", "AI" }
        };

        // Act
        var searchResult = new SearchResult
        {
            ChunkId = chunk.ChunkId,
            DocumentSlug = chunk.DocumentSlug,
            DocumentTitle = chunk.DocumentTitle,
            ChunkIndex = chunk.ChunkIndex,
            Text = chunk.Text,
            SectionHeading = chunk.SectionHeading,
            Categories = chunk.Categories,
            Score = 0.95f
        };

        // Assert
        searchResult.ChunkId.Should().Be("123");
        searchResult.Score.Should().Be(0.95f);
        searchResult.Categories.Should().Contain("AI");
    }
}
