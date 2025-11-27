using Mostlylucid.AudienceSegmentation.Demo.Services;
using Mostlylucid.SemanticSearch.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllersWithViews();

// Add semantic search services (reusing from existing project)
builder.Services.AddSemanticSearch(builder.Configuration);

// Add audience segmentation services
builder.Services.AddSingleton<OllamaProductGenerator>();
builder.Services.AddSingleton<SemanticSegmentationService>();
builder.Services.AddSingleton<CustomerProfileService>();

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Segmentation}/{action=Index}/{id?}");

app.Run();
