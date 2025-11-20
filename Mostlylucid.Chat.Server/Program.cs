using Microsoft.EntityFrameworkCore;
using Mostlylucid.Chat.Server.Data;
using Mostlylucid.Chat.Server.Hubs;
using Mostlylucid.Chat.Server.Middleware;
using Mostlylucid.Chat.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add SQLite database
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ChatDatabase") ?? "Data Source=chat.db"));

// Add services - using database-backed implementations
builder.Services.AddScoped<IConversationService, SqliteConversationService>();
builder.Services.AddScoped<IPresenceService, SqlitePresenceService>();
builder.Services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();

// Add SignalR with hub filter for API key validation
builder.Services.AddSignalR(options =>
{
    options.AddFilter<ApiKeyAuthorizationFilter>();
});
builder.Services.AddSingleton<ApiKeyAuthorizationFilter>();

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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.EnsureCreated();
    app.Logger.LogInformation("Database initialized");
}

// Use middleware
app.UseCors();
app.UseApiKeyAuth();

// Map SignalR hub
app.MapHub<ChatHub>("/chathub");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    database = "sqlite"
}));

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
        await context.Response.WriteAsync("Widget not found. Run 'npm run build' in Mostlylucid.Chat.Widget");
    }
});

// Admin endpoint example (protected by API key)
app.MapGet("/admin/stats", async (ChatDbContext db) =>
{
    var stats = new
    {
        totalConversations = await db.Conversations.CountAsync(),
        activeConversations = await db.Conversations.CountAsync(c => c.IsActive),
        totalMessages = await db.Messages.CountAsync(),
        onlineUsers = await db.Presence.CountAsync(p => p.IsOnline && p.UserType == "user"),
        onlineAdmins = await db.Presence.CountAsync(p => p.IsOnline && p.UserType == "admin")
    };
    return Results.Ok(stats);
});

app.Run();
