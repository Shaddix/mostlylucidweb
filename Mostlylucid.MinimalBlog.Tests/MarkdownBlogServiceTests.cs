using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.MinimalBlog;
using Xunit;

namespace Mostlylucid.MinimalBlog.Tests;

public class MarkdownBlogServiceTests : IDisposable
{
    private readonly string _testMarkdownPath;
    private readonly MarkdownBlogService _service;
    private readonly IMemoryCache _cache;

    public MarkdownBlogServiceTests()
    {
        _testMarkdownPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testMarkdownPath);

        var options = new MinimalBlogOptions
        {
            MarkdownPath = _testMarkdownPath
        };

        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new MarkdownBlogService(options, _cache);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testMarkdownPath))
        {
            Directory.Delete(_testMarkdownPath, true);
        }
        _cache.Dispose();
    }

    [Fact]
    public void GetAllPosts_EmptyDirectory_ReturnsEmptyList()
    {
        // Act
        var posts = _service.GetAllPosts();

        // Assert
        Assert.Empty(posts);
    }

    [Fact]
    public void GetAllPosts_WithSinglePost_ReturnsOnePost()
    {
        // Arrange
        var markdown = @"# Test Post

<!-- category -- Testing -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

This is test content.";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "test-post.md"), markdown);

        // Act
        var posts = _service.GetAllPosts();

        // Assert
        Assert.Single(posts);
        var post = posts.First();
        Assert.Equal("Test Post", post.Title);
        Assert.Equal("test-post", post.Slug);
        Assert.Contains("Testing", post.Categories);
    }

    [Fact]
    public void GetAllPosts_WithMultiplePosts_ReturnsOrderedByDate()
    {
        // Arrange
        var post1 = @"# Older Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-28T12:00</datetime>

Content";
        var post2 = @"# Newer Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "older.md"), post1);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "newer.md"), post2);

        // Act
        var posts = _service.GetAllPosts();

        // Assert
        Assert.Equal(2, posts.Count);
        Assert.Equal("Newer Post", posts[0].Title);
        Assert.Equal("Older Post", posts[1].Title);
    }

    [Fact]
    public void GetPost_ExistingPost_ReturnsPost()
    {
        // Arrange
        var markdown = @"# Found Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content here";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "found.md"), markdown);

        // Act
        var post = _service.GetPost("found");

        // Assert
        Assert.NotNull(post);
        Assert.Equal("Found Post", post.Title);
        Assert.Equal("found", post.Slug);
    }

    [Fact]
    public void GetPost_NonExistentPost_ReturnsNull()
    {
        // Act
        var post = _service.GetPost("nonexistent");

        // Assert
        Assert.Null(post);
    }

    [Fact]
    public void ParseFile_WithMultipleCategories_ParsesAllCategories()
    {
        // Arrange
        var markdown = @"# Multi Category Post

<!-- category -- Cat1, Cat2, Cat3 -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "multi-cat.md"), markdown);

        // Act
        var post = _service.GetPost("multi-cat");

        // Assert
        Assert.NotNull(post);
        Assert.Equal(3, post.Categories.Length);
        Assert.Contains("Cat1", post.Categories);
        Assert.Contains("Cat2", post.Categories);
        Assert.Contains("Cat3", post.Categories);
    }

    [Fact]
    public void ParseFile_WithoutCategories_ReturnsEmptyCategories()
    {
        // Arrange
        var markdown = @"# No Categories

<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "no-cat.md"), markdown);

        // Act
        var post = _service.GetPost("no-cat");

        // Assert
        Assert.NotNull(post);
        Assert.Empty(post.Categories);
    }

    [Fact]
    public void ParseFile_WithoutDate_UsesFileCreationDate()
    {
        // Arrange
        var markdown = @"# No Date Post

<!-- category -- Test -->

Content";
        var filePath = Path.Combine(_testMarkdownPath, "no-date.md");
        File.WriteAllText(filePath, markdown);
        var fileDate = File.GetCreationTimeUtc(filePath);

        // Act
        var post = _service.GetPost("no-date");

        // Assert
        Assert.NotNull(post);
        Assert.Equal(fileDate.Year, post.PublishedDate.Year);
        Assert.Equal(fileDate.Month, post.PublishedDate.Month);
        Assert.Equal(fileDate.Day, post.PublishedDate.Day);
    }

    [Fact]
    public void ParseFile_HiddenPost_NotInAllPosts()
    {
        // Arrange
        var markdown = @"# Hidden Post

<hidden>This is hidden</hidden>
<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "hidden.md"), markdown);

        // Act
        var allPosts = _service.GetAllPosts();
        var directPost = _service.GetPost("hidden");

        // Assert
        Assert.Empty(allPosts);
        Assert.NotNull(directPost);
        Assert.True(directPost.IsHidden);
    }

    [Fact]
    public void GetPostsByCategory_ExistingCategory_ReturnsMatchingPosts()
    {
        // Arrange
        var post1 = @"# Post 1

<!-- category -- Tutorial, Testing -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        var post2 = @"# Post 2

<!-- category -- Tutorial -->
<datetime class=""hidden"">2024-11-29T12:00</datetime>

Content";
        var post3 = @"# Post 3

<!-- category -- Other -->
<datetime class=""hidden"">2024-11-28T12:00</datetime>

Content";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "post1.md"), post1);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "post2.md"), post2);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "post3.md"), post3);

        // Act
        var posts = _service.GetPostsByCategory("Tutorial");

        // Assert
        Assert.Equal(2, posts.Count);
        Assert.All(posts, p => Assert.Contains("Tutorial", p.Categories));
    }

    [Fact]
    public void GetPostsByCategory_CaseInsensitive_ReturnsMatchingPosts()
    {
        // Arrange
        var markdown = @"# Post

<!-- category -- Tutorial -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "test.md"), markdown);

        // Act
        var postsLower = _service.GetPostsByCategory("tutorial");
        var postsUpper = _service.GetPostsByCategory("TUTORIAL");
        var postsMixed = _service.GetPostsByCategory("TuToRiAl");

        // Assert
        Assert.Single(postsLower);
        Assert.Single(postsUpper);
        Assert.Single(postsMixed);
    }

    [Fact]
    public void GetAllCategories_ReturnsDistinctCategories()
    {
        // Arrange
        var post1 = @"# Post 1

<!-- category -- Cat1, Cat2 -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        var post2 = @"# Post 2

<!-- category -- Cat2, Cat3 -->
<datetime class=""hidden"">2024-11-29T12:00</datetime>

Content";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "p1.md"), post1);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "p2.md"), post2);

        // Act
        var categories = _service.GetAllCategories();

        // Assert
        Assert.Equal(3, categories.Count);
        Assert.Contains("Cat1", categories);
        Assert.Contains("Cat2", categories);
        Assert.Contains("Cat3", categories);
    }

    [Fact]
    public void ParseFile_MarkdownToHtml_ConvertsCorrectly()
    {
        // Arrange
        var markdown = @"# Test Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

This is **bold** and this is *italic*.

## Subheading

- Item 1
- Item 2

```csharp
var x = 10;
```";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "html-test.md"), markdown);

        // Act
        var post = _service.GetPost("html-test");

        // Assert
        Assert.NotNull(post);
        Assert.Contains("<strong>bold</strong>", post.HtmlContent);
        Assert.Contains("<em>italic</em>", post.HtmlContent);
        Assert.Contains("<h2", post.HtmlContent);
        Assert.Contains("<ul>", post.HtmlContent);
        Assert.Contains("<code", post.HtmlContent);
    }

    [Fact]
    public void LoadAllPosts_FiltersTranslatedFiles()
    {
        // Arrange
        var basePost = @"# Base Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

English content";
        var translatedPost = @"# Translated Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Translated content";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "post.md"), basePost);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "post.es.md"), translatedPost);

        // Act
        var posts = _service.GetAllPosts();

        // Assert
        Assert.Single(posts);
        Assert.Equal("post", posts[0].Slug);
    }

    [Fact]
    public void GetAllPosts_UsesCaching()
    {
        // Arrange
        var markdown = @"# Cached Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        var filePath = Path.Combine(_testMarkdownPath, "cached.md");
        File.WriteAllText(filePath, markdown);

        // Act
        var posts1 = _service.GetAllPosts();

        // Delete the file
        File.Delete(filePath);

        // Should still return from cache
        var posts2 = _service.GetAllPosts();

        // Assert
        Assert.Single(posts1);
        Assert.Single(posts2);
        Assert.Equal(posts1[0].Slug, posts2[0].Slug);
    }

    [Fact]
    public void ParseFile_WithoutTitle_UsesSlugAsTitle()
    {
        // Arrange
        var markdown = @"<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content without title";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "no-title.md"), markdown);

        // Act
        var post = _service.GetPost("no-title");

        // Assert
        Assert.NotNull(post);
        Assert.Equal("no-title", post.Title);
    }

    [Fact]
    public void GetAllCategories_OrderedAlphabetically()
    {
        // Arrange
        var post1 = @"# Post 1

<!-- category -- Zebra, Apple, Middle -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "test.md"), post1);

        // Act
        var categories = _service.GetAllCategories();

        // Assert
        Assert.Equal(3, categories.Count);
        Assert.Equal("Apple", categories[0]);
        Assert.Equal("Middle", categories[1]);
        Assert.Equal("Zebra", categories[2]);
    }
}
