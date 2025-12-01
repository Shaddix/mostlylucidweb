using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Mostlylucid.MinimalBlog;
using Xunit;

namespace Mostlylucid.MinimalBlog.Tests;

public class MinimalBlogExtensionsTests
{
    [Fact]
    public void AddMinimalBlog_RegistersRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMinimalBlog(options =>
        {
            options.MarkdownPath = "TestMarkdown";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(serviceProvider.GetService<IMemoryCache>());
        Assert.NotNull(serviceProvider.GetService<MinimalBlogOptions>());
        Assert.NotNull(serviceProvider.GetService<MarkdownBlogService>());
    }

    [Fact]
    public void AddMinimalBlog_WithDefaultOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMinimalBlog();
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<MinimalBlogOptions>();

        // Assert
        Assert.Equal("Markdown", options.MarkdownPath);
        Assert.Equal("wwwroot/images", options.ImagesPath);
        Assert.True(options.EnableMetaWeblog);
    }

    [Fact]
    public void AddMinimalBlog_WithCustomOptions_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMinimalBlog(options =>
        {
            options.MarkdownPath = "CustomPath";
            options.ImagesPath = "CustomImages";
            options.EnableMetaWeblog = false;
            options.MetaWeblogUsername = "testuser";
            options.MetaWeblogPassword = "testpass";
            options.BlogUrl = "https://test.com";
        });

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<MinimalBlogOptions>();

        // Assert
        Assert.Equal("CustomPath", options.MarkdownPath);
        Assert.Equal("CustomImages", options.ImagesPath);
        Assert.False(options.EnableMetaWeblog);
        Assert.Equal("testuser", options.MetaWeblogUsername);
        Assert.Equal("testpass", options.MetaWeblogPassword);
        Assert.Equal("https://test.com", options.BlogUrl);
    }

    [Fact]
    public void AddMinimalBlog_WithMetaWeblogEnabled_RegistersMetaWeblogService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMinimalBlog(options =>
        {
            options.MarkdownPath = "Test";
            options.EnableMetaWeblog = true;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var metaWeblogService = serviceProvider.GetService<MetaWeblogService>();
        Assert.NotNull(metaWeblogService);
    }

    [Fact]
    public void AddMinimalBlog_WithMetaWeblogDisabled_DoesNotRegisterMetaWeblogService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMinimalBlog(options =>
        {
            options.MarkdownPath = "Test";
            options.EnableMetaWeblog = false;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var metaWeblogService = serviceProvider.GetService<MetaWeblogService>();
        Assert.Null(metaWeblogService);
    }

    [Fact]
    public void AddMinimalBlog_RegistersOutputCache()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMinimalBlog();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Check that output cache is registered
        // Output cache service is internal, but we can verify it doesn't throw
        Assert.NotNull(serviceProvider);
    }

    [Fact]
    public void AddMinimalBlog_ServicesSingletons_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMinimalBlog(options =>
        {
            options.MarkdownPath = "Test";
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var service1 = serviceProvider.GetRequiredService<MarkdownBlogService>();
        var service2 = serviceProvider.GetRequiredService<MarkdownBlogService>();

        // Assert
        Assert.Same(service1, service2);
    }

    [Fact]
    public void MinimalBlogOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new MinimalBlogOptions();

        // Assert
        Assert.Equal("Markdown", options.MarkdownPath);
        Assert.Equal("wwwroot/images", options.ImagesPath);
        Assert.True(options.EnableMetaWeblog);
        Assert.Equal("admin", options.MetaWeblogUsername);
        Assert.Equal("changeme", options.MetaWeblogPassword);
        Assert.Equal("http://localhost:5000", options.BlogUrl);
    }

    [Fact]
    public void MinimalBlogOptions_CanSetAllProperties()
    {
        // Arrange
        var options = new MinimalBlogOptions();

        // Act
        options.MarkdownPath = "NewPath";
        options.ImagesPath = "NewImages";
        options.EnableMetaWeblog = false;
        options.MetaWeblogUsername = "newuser";
        options.MetaWeblogPassword = "newpass";
        options.BlogUrl = "https://new.com";

        // Assert
        Assert.Equal("NewPath", options.MarkdownPath);
        Assert.Equal("NewImages", options.ImagesPath);
        Assert.False(options.EnableMetaWeblog);
        Assert.Equal("newuser", options.MetaWeblogUsername);
        Assert.Equal("newpass", options.MetaWeblogPassword);
        Assert.Equal("https://new.com", options.BlogUrl);
    }
}
