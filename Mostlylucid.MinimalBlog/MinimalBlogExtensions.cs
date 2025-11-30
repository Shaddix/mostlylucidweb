using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Mostlylucid.MinimalBlog;

public static class MinimalBlogExtensions
{
    /// <summary>
    /// Adds Minimal Blog services to the application.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMinimalBlog(
        this IServiceCollection services,
        Action<MinimalBlogOptions>? configure = null)
    {
        var options = new MinimalBlogOptions();
        configure?.Invoke(options);

        // Configure paths via configuration or defaults
        services.AddSingleton(options);

        services.AddMemoryCache();
        services.AddOutputCache(cacheOptions =>
        {
            cacheOptions.AddBasePolicy(b => b.Expire(TimeSpan.FromMinutes(10)));
            cacheOptions.AddPolicy("Blog", b => b.Expire(TimeSpan.FromHours(1)).Tag("blog"));
        });

        services.AddSingleton<MarkdownBlogService>();

        if (options.EnableMetaWeblog)
        {
            services.AddSingleton<MetaWeblogService>();
        }

        return services;
    }

    /// <summary>
    /// Configures Minimal Blog middleware and endpoints.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static WebApplication UseMinimalBlog(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<MinimalBlogOptions>();

        // Ensure images directory exists
        var imagesPath = options.ImagesPath;
        var imagesDir = Path.IsPathRooted(imagesPath)
            ? imagesPath
            : Path.Combine(app.Environment.ContentRootPath, imagesPath);
        Directory.CreateDirectory(imagesDir);

        // Serve images
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(imagesDir),
            RequestPath = "/images"
        });

        app.UseOutputCache();

        // MetaWeblog XML-RPC endpoint
        if (options.EnableMetaWeblog)
        {
            app.MapPost("/metaweblog", async (HttpContext ctx, MetaWeblogService svc) =>
            {
                ctx.Response.ContentType = "text/xml";
                var response = await svc.HandleRequestAsync(ctx.Request.Body);
                await ctx.Response.WriteAsync(response);
            });
        }

        return app;
    }
}

/// <summary>
/// Configuration options for Minimal Blog
/// </summary>
public class MinimalBlogOptions
{
    /// <summary>
    /// Path to markdown files directory
    /// </summary>
    public string MarkdownPath { get; set; } = "Markdown";

    /// <summary>
    /// Path to images directory
    /// </summary>
    public string ImagesPath { get; set; } = "wwwroot/images";

    /// <summary>
    /// Enable MetaWeblog API endpoint for external editors
    /// </summary>
    public bool EnableMetaWeblog { get; set; } = true;

    /// <summary>
    /// MetaWeblog API username
    /// </summary>
    public string MetaWeblogUsername { get; set; } = "admin";

    /// <summary>
    /// MetaWeblog API password
    /// </summary>
    public string MetaWeblogPassword { get; set; } = "changeme";

    /// <summary>
    /// Blog URL for MetaWeblog API
    /// </summary>
    public string BlogUrl { get; set; } = "http://localhost:5000";
}
