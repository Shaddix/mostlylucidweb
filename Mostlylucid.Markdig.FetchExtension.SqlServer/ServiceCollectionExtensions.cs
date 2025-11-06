using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mostlylucid.Markdig.FetchExtension.SqlServer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers a SQL Server-based implementation of IMarkdownFetchService.
    ///     Cached markdown is persisted to SQL Server database and survives restarts.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">SQL Server connection string (required)</param>
    public static IServiceCollection AddSqlServerMarkdownFetch(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        services.AddHttpClient();
        services.AddDbContext<MarkdownCacheDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.TryAddScoped<IMarkdownFetchService, SqlServerMarkdownFetchService>();

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
