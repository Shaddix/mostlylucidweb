using Hangfire;
using Hangfire.Dashboard;
using Hangfire.InMemory;
using Mostlylucid.Workflow.Demo.Services;
using Mostlylucid.Workflow.Engine.Execution;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Engine.Nodes;

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
builder.Services.AddSingleton<INodeRegistry, NodeRegistry>();
builder.Services.AddScoped<IWorkflowExecutor, WorkflowExecutor>();

// Register workflow node types
builder.Services.AddTransient<SetVariableNode>();
builder.Services.AddTransient<LogNode>();
builder.Services.AddTransient<DelayNode>();
builder.Services.AddTransient<HttpRequestNode>();
builder.Services.AddTransient<ConditionNode>();
builder.Services.AddTransient<TransformNode>();

builder.Services.AddHttpClient();

var app = builder.Build();

// Register node types with the registry
var nodeRegistry = app.Services.GetRequiredService<INodeRegistry>();
nodeRegistry.RegisterNode<SetVariableNode>("SetVariable");
nodeRegistry.RegisterNode<LogNode>("Log");
nodeRegistry.RegisterNode<DelayNode>("Delay");
nodeRegistry.RegisterNode<HttpRequestNode>("HttpRequest");
nodeRegistry.RegisterNode<ConditionNode>("Condition");
nodeRegistry.RegisterNode<TransformNode>("Transform");

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
