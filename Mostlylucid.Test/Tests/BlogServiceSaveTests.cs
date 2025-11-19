using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Mostlylucid.Blog;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Services.Umami;
using Mostlylucid.Test.Extensions;

namespace Mostlylucid.Test.Tests;

public class BlogServiceSaveTests
{
    private readonly Mock<IMostlylucidDBContext> _dbContextMock;
    private readonly IServiceProvider _serviceProvider;

    public BlogServiceSaveTests()
    {
        // 1. Setup ServiceCollection for DI
        var services = new ServiceCollection();

        // 2. Create a mock of IMostlylucidDbContext
        _dbContextMock = new Mock<IMostlylucidDBContext>();

        // 3. Register the mock of IMostlylucidDbContext into the ServiceCollection
        services.AddSingleton(_dbContextMock.Object);
        services.AddScoped<IUmamiDataSortService, UmamiDataSortFake>();
        // Optionally register other services
        services.AddScoped<IBlogService, BlogService>();
        services.AddScoped<IBlogViewService, BlogPostViewService>(); // Example service that depends on IMostlylucidDbContext
        services.AddLogging(configure => configure.AddConsole());
        // Add BlogPostProcessingContext - required by BlogService and MarkdownRenderingService
        services.AddScoped<BlogPostProcessingContext>();
        services.AddScoped<MarkdownRenderingService>();

        // Mock IWebHostEnvironment
        var mockWebHostEnvironment = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        mockWebHostEnvironment.Setup(m => m.WebRootPath).Returns(System.IO.Path.GetTempPath());
        mockWebHostEnvironment.Setup(m => m.ContentRootPath).Returns(System.IO.Path.GetTempPath());
        services.AddSingleton(mockWebHostEnvironment.Object);

        // Add ImageConfig
        services.AddSingleton(new Mostlylucid.Shared.Config.Markdown.ImageConfig
        {
            DefaultFormat = "webp",
            DefaultQuality = 50,
            PrimaryImageFolder = "articleimages"
        });

        // 4. Build the service provider
        _serviceProvider = services.BuildServiceProvider();
    }

    private IBlogViewService SetupBlogService()
    {
        var blogPosts = BlogEntityExtensions.GetBlogPostEntities(1);
        _dbContextMock.SetupDbSet(blogPosts, x => x.BlogPosts);

        var languages = LanguageExtensions.GetLanguageEntities(1);
        _dbContextMock.SetupDbSet(languages, x => x.Languages);

        // Setup Categories DbSet - required for SavePost
        var categories = new List<Mostlylucid.Shared.Entities.CategoryEntity>();
        _dbContextMock.SetupDbSet(categories, x => x.Categories);

        // Setup SaveChangesAsync to return success
        _dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Resolve the IBlogService from the service provider
        return _serviceProvider.GetRequiredService<IBlogViewService>();
    }

    [Fact]
    public async Task SaveBlogPost()
    {
        var blogService = SetupBlogService();


        // Act
        await blogService.SavePost("Test Title", "en", "#Test Category");

        // Assert - Use It.IsAny<CancellationToken>() to match any token instance
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}