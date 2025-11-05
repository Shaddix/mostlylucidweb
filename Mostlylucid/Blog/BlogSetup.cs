using Mostlylucid.Blog.Markdown;
using Mostlylucid.Blog.ViewServices;
using Mostlylucid.Blog.WatcherService;
using Mostlylucid.Blog.ValidationService;
using Mostlylucid.DbContext;
using Mostlylucid.DbContext.EntityFramework;
using Mostlylucid.Services.Blog;
using Mostlylucid.Services.Interfaces;
using Mostlylucid.Services.Markdown;
using Mostlylucid.Markdig.FetchExtension;
using Mostlylucid.Shared.Config;
using Mostlylucid.Shared.Config.Markdown;
using Npgsql;

namespace Mostlylucid.Blog;

public static class BlogSetup
{
    public static void SetupBlog(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        var config = services.ConfigurePOCO<BlogConfig>(configuration.GetSection(BlogConfig.Section));
       services.ConfigurePOCO<MarkdownConfig>(configuration.GetSection(MarkdownConfig.Section));

        // Register HttpClient factory for fetching remote markdown
        services.AddHttpClient();

        // Register markdown fetch service
        services.AddScoped<IMarkdownFetchService, MarkdownFetchService>();

        switch (config.Mode)
        {
            case BlogMode.File:
                Log.Information("Using file based blog");
                services.AddScoped<IBlogViewService, MarkdownBlogViewService>();
                services.AddScoped<IBlogPopulator, MarkdownBlogPopulator>();
                break;
            case BlogMode.Database:
                Log.Information("Using Database based blog");
           services.SetupDatabase(configuration, env);
                services.AddScoped<IBlogViewService, BlogPostViewService>();
                services.AddScoped<ICommentService, CommentService>();
                services.AddScoped<IBlogPopulator, BlogPopulator>();
                services.AddScoped<BlogSearchService>();
                services.AddScoped<CommentViewService>();
                services.AddSingleton<BlogUpdater>();
                services.AddScoped<IBlogService, BlogService>();
                services.AddScoped<BlogValidationService>();
                services.AddHostedService<MarkdownDirectoryWatcherService>();
                services.AddHostedService<BlogReconciliationService>();

                // Register markdown fetch polling service (only in database mode)
                services.AddHostedService<MarkdownFetchPollingService>();
                break;
        }
        services.AddScoped<IMarkdownBlogService, MarkdownBlogPopulator>();

        services.AddScoped<MarkdownRenderingService>();
    }
    
    public static async Task PopulateBlog(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
    
        var config = scope.ServiceProvider.GetRequiredService<BlogConfig>();
        var cancellationToken = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        if (config.Mode == BlogMode.File)
        {
            var context = scope.ServiceProvider.GetRequiredService<IBlogPopulator>();
            await context.Populate(cancellationToken);
        }
     
    }
    
}