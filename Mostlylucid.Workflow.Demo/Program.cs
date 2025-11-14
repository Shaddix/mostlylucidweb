using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Mostlylucid.Workflow.Demo.Services;
using Mostlylucid.Workflow.Engine.Execution;
using Mostlylucid.Workflow.Engine.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// Add Hangfire with in-memory storage
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());

builder.Services.AddHangfireServer();

// Register workflow services
builder.Services.AddSingleton<WorkflowCacheService>();
builder.Services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

// Add Hangfire dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = Array.Empty<IDashboardAuthorizationFilter>() // No auth for demo
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
