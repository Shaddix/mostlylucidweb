using Mostlylucid.MinimalBlog;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages
builder.Services.AddRazorPages();

// Add Minimal Blog with custom configuration
builder.Services.AddMinimalBlog(options =>
{
    options.MarkdownPath = "Markdown";
    options.ImagesPath = "wwwroot/images";
    options.EnableMetaWeblog = false; // Disabled for demo
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseMinimalBlog();
app.MapRazorPages();

app.Run();
