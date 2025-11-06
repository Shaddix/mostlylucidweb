using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mostlylucid.Markdig.FetchExtension.Services;

namespace Mostlylucid.Markdig.FetchExtension.Sqlite;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers a SQLite-based implementation of IMarkdownFetchService.
    ///     Cached markdown is persisted to SQLite database and survives restarts.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQLite connection string. Defaults to "Data Source=markdown-cache.db"</param>
    public static IServiceCollection AddSqliteMarkdownFetch(
        this IServiceCollection services,
        string? connectionString = null)
    {
        connectionString ??= "Data Source=markdown-cache.db";

        services.AddHttpClient();
        services.AddDbContext<MarkdownCacheDbContext>(options =>
            options.UseSqlite(connectionString));

        services.TryAddScoped<IMarkdownFetchService, SqliteMarkdownFetchService>();

        return services;
    }

    /// <summary>
    ///     Ensures the database schema is created. Call this during application startup.
    /// </summary>
    public static void EnsureMarkdownCacheDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MarkdownCacheDbContext>();
        dbContext.Database.EnsureCreated();
    }
}
