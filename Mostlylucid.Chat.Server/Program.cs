using Mostlylucid.Chat.Server.Hubs;
using Mostlylucid.Chat.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConversationService, InMemoryConversationService>();
builder.Services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();

// Add CORS for widget embedding
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// Map SignalR hub
app.MapHub<ChatHub>("/chathub");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Widget script endpoint
app.MapGet("/widget.js", async (HttpContext context) =>
{
    var jsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "widget.js");
    if (File.Exists(jsPath))
    {
        context.Response.ContentType = "application/javascript";
        await context.Response.SendFileAsync(jsPath);
    }
    else
    {
        context.Response.StatusCode = 404;
    }
});

app.Run();
