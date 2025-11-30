using Mostlylucid.MinimalBlog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMinimalBlog(options =>
{
    options.MarkdownPath = builder.Configuration["MarkdownPath"] ?? "../Mostlylucid/Markdown";
    options.ImagesPath = builder.Configuration["ImagesPath"] ?? "wwwroot/images";
    options.EnableMetaWeblog = builder.Configuration.GetValue("MetaWeblog:Enabled", true);
    options.MetaWeblogUsername = builder.Configuration["MetaWeblog:Username"] ?? "admin";
    options.MetaWeblogPassword = builder.Configuration["MetaWeblog:Password"] ?? "changeme";
    options.BlogUrl = builder.Configuration["MetaWeblog:BlogUrl"] ?? "http://localhost:5000";
});

var app = builder.Build();

app.UseStaticFiles();
app.UseMinimalBlog();
app.MapRazorPages();

app.Run();
