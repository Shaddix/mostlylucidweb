using Mostlylucid.MinimalBlog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(b => b.Expire(TimeSpan.FromMinutes(10)));
    options.AddPolicy("Blog", b => b.Expire(TimeSpan.FromHours(1)).Tag("blog"));
});
builder.Services.AddSingleton<MarkdownBlogService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseOutputCache();
app.MapRazorPages();

app.Run();
