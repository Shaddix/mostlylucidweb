using Mostlylucid.MinimalBlog;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(b => b.Expire(TimeSpan.FromMinutes(10)));
    options.AddPolicy("Blog", b => b.Expire(TimeSpan.FromHours(1)).Tag("blog"));
});
builder.Services.AddSingleton<MarkdownBlogService>();
builder.Services.AddSingleton<MetaWeblogService>();

var app = builder.Build();

// Serve images from configured path
var imagesPath = builder.Configuration["ImagesPath"] ?? "wwwroot/images";
var imagesDir = Path.IsPathRooted(imagesPath) ? imagesPath : Path.Combine(app.Environment.ContentRootPath, imagesPath);
Directory.CreateDirectory(imagesDir);

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesDir),
    RequestPath = "/images"
});

app.UseOutputCache();

// MetaWeblog XML-RPC endpoint
app.MapPost("/metaweblog", async (HttpContext ctx, MetaWeblogService svc) =>
{
    ctx.Response.ContentType = "text/xml";
    var response = await svc.HandleRequestAsync(ctx.Request.Body);
    await ctx.Response.WriteAsync(response);
});

app.MapRazorPages();

app.Run();
