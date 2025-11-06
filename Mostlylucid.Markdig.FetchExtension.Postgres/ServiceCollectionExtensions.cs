using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mostlylucid.Markdig.FetchExtension.Postgres;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers a PostgreSQL-based implementation of IMarkdownFetchService.
    ///     Cached markdown is persisted to PostgreSQL database and survives restarts.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">PostgreSQL connection string (required)</param>
    public static IServiceCollection AddPostgresMarkdownFetch(
        this IServiceCollection services,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        services.AddHttpClient();
        services.AddDbContext<MarkdownCacheDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.TryAddScoped<IMarkdownFetchService, PostgresMarkdownFetchService>();

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
