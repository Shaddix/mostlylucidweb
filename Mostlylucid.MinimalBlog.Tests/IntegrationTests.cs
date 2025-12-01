using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mostlylucid.MinimalBlog;
using System.Net;
using Xunit;

namespace Mostlylucid.MinimalBlog.Tests;

public class IntegrationTests : IDisposable
{
    private readonly string _testMarkdownPath;
    private readonly WebApplicationFactory<TestProgram> _factory;

    public IntegrationTests()
    {
        _testMarkdownPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testMarkdownPath);

        _factory = new WebApplicationFactory<TestProgram>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddRazorPages();
                    services.AddMinimalBlog(options =>
                    {
                        options.MarkdownPath = _testMarkdownPath;
                        options.ImagesPath = Path.Combine(_testMarkdownPath, "images");
                        options.EnableMetaWeblog = false;
                    });
                });

                builder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseStaticFiles();
                    app.UseMinimalBlog();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapRazorPages();
                    });
                });
            });
    }

    public void Dispose()
    {
        _factory?.Dispose();
        if (Directory.Exists(_testMarkdownPath))
        {
            Directory.Delete(_testMarkdownPath, true);
        }
    }

    [Fact]
    public async Task MinimalBlog_CanResolveServices()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Act & Assert
        var blogService = services.GetService<MarkdownBlogService>();
        var options = services.GetService<MinimalBlogOptions>();

        Assert.NotNull(blogService);
        Assert.NotNull(options);
    }

    [Fact]
    public async Task MinimalBlog_WithValidMarkdown_ParsesCorrectly()
    {
        // Arrange
        var markdown = @"# Integration Test Post

<!-- category -- Testing, Integration -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

This is an integration test post with **bold** text.";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "integration-test.md"), markdown);

        using var scope = _factory.Services.CreateScope();
        var blogService = scope.ServiceProvider.GetRequiredService<MarkdownBlogService>();

        // Act
        var posts = blogService.GetAllPosts();
        var post = blogService.GetPost("integration-test");

        // Assert
        Assert.Single(posts);
        Assert.NotNull(post);
        Assert.Equal("Integration Test Post", post.Title);
        Assert.Contains("Testing", post.Categories);
        Assert.Contains("Integration", post.Categories);
        Assert.Contains("<strong>bold</strong>", post.HtmlContent);
    }

    [Fact]
    public async Task MinimalBlog_CategoryFiltering_WorksCorrectly()
    {
        // Arrange
        var post1 = @"# Post 1

<!-- category -- Cat1 -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content 1";
        var post2 = @"# Post 2

<!-- category -- Cat2 -->
<datetime class=""hidden"">2024-11-29T12:00</datetime>

Content 2";
        var post3 = @"# Post 3

<!-- category -- Cat1, Cat2 -->
<datetime class=""hidden"">2024-11-28T12:00</datetime>

Content 3";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "post1.md"), post1);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "post2.md"), post2);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "post3.md"), post3);

        using var scope = _factory.Services.CreateScope();
        var blogService = scope.ServiceProvider.GetRequiredService<MarkdownBlogService>();

        // Act
        var cat1Posts = blogService.GetPostsByCategory("Cat1");
        var cat2Posts = blogService.GetPostsByCategory("Cat2");
        var allCategories = blogService.GetAllCategories();

        // Assert
        Assert.Equal(2, cat1Posts.Count);
        Assert.Equal(2, cat2Posts.Count);
        Assert.Equal(2, allCategories.Count);
        Assert.Contains("Cat1", allCategories);
        Assert.Contains("Cat2", allCategories);
    }

    [Fact]
    public async Task MinimalBlog_PostOrdering_MostRecentFirst()
    {
        // Arrange
        var oldPost = @"# Old Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-01-01T12:00</datetime>

Old content";
        var midPost = @"# Mid Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-06-15T12:00</datetime>

Mid content";
        var newPost = @"# New Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-12-01T12:00</datetime>

New content";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "old.md"), oldPost);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "mid.md"), midPost);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "new.md"), newPost);

        using var scope = _factory.Services.CreateScope();
        var blogService = scope.ServiceProvider.GetRequiredService<MarkdownBlogService>();

        // Act
        var posts = blogService.GetAllPosts();

        // Assert
        Assert.Equal(3, posts.Count);
        Assert.Equal("New Post", posts[0].Title);
        Assert.Equal("Mid Post", posts[1].Title);
        Assert.Equal("Old Post", posts[2].Title);
    }

    [Fact]
    public async Task MinimalBlog_HiddenPosts_ExcludedFromListing()
    {
        // Arrange
        var visiblePost = @"# Visible Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Visible content";
        var hiddenPost = @"# Hidden Post

<hidden>Secret</hidden>
<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Hidden content";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "visible.md"), visiblePost);
        File.WriteAllText(Path.Combine(_testMarkdownPath, "hidden.md"), hiddenPost);

        using var scope = _factory.Services.CreateScope();
        var blogService = scope.ServiceProvider.GetRequiredService<MarkdownBlogService>();

        // Act
        var allPosts = blogService.GetAllPosts();
        var directAccess = blogService.GetPost("hidden");

        // Assert
        Assert.Single(allPosts);
        Assert.Equal("Visible Post", allPosts[0].Title);
        Assert.NotNull(directAccess);
        Assert.True(directAccess.IsHidden);
    }

    [Fact]
    public async Task MinimalBlog_CachingBehavior_ReturnsSameInstances()
    {
        // Arrange
        var markdown = @"# Cached Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

Content";
        File.WriteAllText(Path.Combine(_testMarkdownPath, "cached.md"), markdown);

        using var scope = _factory.Services.CreateScope();
        var blogService = scope.ServiceProvider.GetRequiredService<MarkdownBlogService>();

        // Act
        var posts1 = blogService.GetAllPosts();
        var posts2 = blogService.GetAllPosts();

        // Assert
        Assert.Same(posts1, posts2); // Should be same cached instance
    }

    [Fact]
    public async Task MinimalBlog_ComplexMarkdown_RendersCorrectly()
    {
        // Arrange
        var markdown = @"# Complex Post

<!-- category -- Test -->
<datetime class=""hidden"">2024-11-30T12:00</datetime>

## Subheading

This has **bold**, *italic*, and `code`.

### List

- Item 1
- Item 2
  - Nested item

### Code Block

```csharp
public class Test {
    public int Value { get; set; }
}
```

### Blockquote

> This is a quote

### Link

[Example](https://example.com)";

        File.WriteAllText(Path.Combine(_testMarkdownPath, "complex.md"), markdown);

        using var scope = _factory.Services.CreateScope();
        var blogService = scope.ServiceProvider.GetRequiredService<MarkdownBlogService>();

        // Act
        var post = blogService.GetPost("complex");

        // Assert
        Assert.NotNull(post);
        Assert.Contains("<h2", post.HtmlContent);
        Assert.Contains("<h3", post.HtmlContent);
        Assert.Contains("<strong>bold</strong>", post.HtmlContent);
        Assert.Contains("<em>italic</em>", post.HtmlContent);
        Assert.Contains("<code>code</code>", post.HtmlContent);
        Assert.Contains("<ul>", post.HtmlContent);
        Assert.Contains("<li>", post.HtmlContent);
        Assert.Contains("<blockquote>", post.HtmlContent);
        Assert.Contains("href=\"https://example.com\"", post.HtmlContent);
    }
}

// Minimal test program class
public class TestProgram
{
    public static void Main(string[] args) { }
}
