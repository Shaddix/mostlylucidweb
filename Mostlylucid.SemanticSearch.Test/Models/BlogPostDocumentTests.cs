using FluentAssertions;
using Mostlylucid.SemanticSearch.Models;
using Xunit;

namespace Mostlylucid.SemanticSearch.Test.Models;

public class BlogPostDocumentTests
{
    [Fact]
    public void BlogPostDocument_CanBeCreated()
    {
        // Arrange & Act
        var document = new BlogPostDocument
        {
            Id = "test-post_en",
            Slug = "test-post",
            Title = "Test Post",
            Content = "This is test content.",
            Language = "en",
            Categories = new List<string> { "Test", "Sample" },
            PublishedDate = DateTime.UtcNow,
            ContentHash = "hash123"
        };

        // Assert
        document.Id.Should().Be("test-post_en");
        document.Slug.Should().Be("test-post");
        document.Title.Should().Be("Test Post");
        document.Content.Should().Be("This is test content.");
        document.Language.Should().Be("en");
        document.Categories.Should().HaveCount(2);
        document.ContentHash.Should().Be("hash123");
    }

    [Fact]
    public void BlogPostDocument_CategoriesInitializesToEmptyList()
    {
        // Arrange & Act
        var document = new BlogPostDocument
        {
            Id = "test_en",
            Slug = "test",
            Title = "Test",
            Content = "Content",
            Language = "en",
            PublishedDate = DateTime.UtcNow
        };

        // Assert
        document.Categories.Should().NotBeNull();
        document.Categories.Should().BeEmpty();
    }

    [Fact]
    public void BlogPostDocument_WithMultipleLanguages_HasDifferentIds()
    {
        // Arrange & Act
        var docEn = new BlogPostDocument
        {
            Id = "test-post_en",
            Slug = "test-post",
            Title = "Test Post",
            Content = "English content",
            Language = "en",
            PublishedDate = DateTime.UtcNow
        };

        var docEs = new BlogPostDocument
        {
            Id = "test-post_es",
            Slug = "test-post",
            Title = "Publicación de prueba",
            Content = "Contenido en español",
            Language = "es",
            PublishedDate = DateTime.UtcNow
        };

        // Assert
        docEn.Id.Should().NotBe(docEs.Id);
        docEn.Slug.Should().Be(docEs.Slug);
        docEn.Language.Should().NotBe(docEs.Language);
    }
}

public class SearchResultTests
{
    [Fact]
    public void SearchResult_CanBeCreated()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Slug = "test-post",
            Title = "Test Post",
            Language = "en",
            Score = 0.95f,
            Categories = new List<string> { "Test" },
            PublishedDate = DateTime.UtcNow
        };

        // Assert
        result.Slug.Should().Be("test-post");
        result.Title.Should().Be("Test Post");
        result.Language.Should().Be("en");
        result.Score.Should().Be(0.95f);
        result.Categories.Should().HaveCount(1);
    }

    [Fact]
    public void SearchResult_ScoreIsWithinValidRange()
    {
        // Arrange
        var result = new SearchResult
        {
            Slug = "test",
            Title = "Test",
            Language = "en",
            Score = 0.85f,
            PublishedDate = DateTime.UtcNow
        };

        // Assert
        result.Score.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public void SearchResult_CategoriesCanBeEmpty()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Slug = "test",
            Title = "Test",
            Language = "en",
            Score = 0.5f,
            Categories = new List<string>(),
            PublishedDate = DateTime.UtcNow
        };

        // Assert
        result.Categories.Should().BeEmpty();
    }
}
