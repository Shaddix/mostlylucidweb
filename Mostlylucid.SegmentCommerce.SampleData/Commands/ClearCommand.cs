using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class ClearSettings : CommandSettings
{
    [CommandOption("--connection <STRING>")]
    [Description("Database connection string (overrides appsettings)")]
    public string? ConnectionString { get; set; }

    [CommandOption("--confirm")]
    [Description("Skip confirmation prompt")]
    public bool Confirm { get; set; }

    [CommandOption("--keep-categories")]
    [Description("Keep category data")]
    public bool KeepCategories { get; set; }
}

public class ClearCommand : AsyncCommand<ClearSettings>
{
    private readonly GenerationConfig _config;

    public ClearCommand(GenerationConfig config)
    {
        _config = config;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ClearSettings settings)
    {
        AnsiConsole.Write(new FigletText("Clear DB").Color(Color.Red));
        AnsiConsole.MarkupLine("[bold red]Database Clear Utility[/]");
        AnsiConsole.WriteLine();

        var connectionString = settings.ConnectionString ?? _config.ConnectionString;

        if (string.IsNullOrEmpty(connectionString))
        {
            AnsiConsole.MarkupLine("[red]No database connection string provided[/]");
            AnsiConsole.MarkupLine("[dim]Use --connection or set ConnectionStrings:DefaultConnection in appsettings.json[/]");
            return 1;
        }

        // Show what will be cleared
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Table");
        table.AddColumn("Action");

        table.AddRow("product_embeddings", "[red]DELETE ALL[/]");
        table.AddRow("product_variations", "[red]DELETE ALL[/]");
        table.AddRow("product_taxonomy", "[red]DELETE ALL[/]");
        table.AddRow("store_products", "[red]DELETE ALL[/]");
        table.AddRow("products", "[red]DELETE ALL[/]");
        table.AddRow("sellers", "[red]DELETE ALL[/]");
        table.AddRow("interest_scores", "[red]DELETE ALL[/]");
        table.AddRow("interest_embeddings", "[red]DELETE ALL[/]");
        table.AddRow("signals", "[red]DELETE ALL[/]");
        table.AddRow("session_profiles", "[red]DELETE ALL[/]");
        table.AddRow("anonymous_profiles", "[red]DELETE ALL[/]");
        table.AddRow("profile_keys", "[red]DELETE ALL[/]");
        table.AddRow("interaction_events", "[red]DELETE ALL[/]");
        table.AddRow("visitor_profiles", "[red]DELETE ALL[/]");
        table.AddRow("outbox_messages", "[red]DELETE ALL[/]");
        table.AddRow("job_queue", "[red]DELETE ALL[/]");

        if (!settings.KeepCategories)
        {
            table.AddRow("categories", "[red]DELETE ALL[/]");
            table.AddRow("taxonomy_nodes", "[red]DELETE ALL[/]");
            table.AddRow("stores", "[red]DELETE ALL[/]");
            table.AddRow("store_users", "[red]DELETE ALL[/]");
        }
        else
        {
            table.AddRow("categories", "[green]KEEP[/]");
            table.AddRow("taxonomy_nodes", "[green]KEEP[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!settings.Confirm)
        {
            var confirm = AnsiConsole.Confirm("[red]This will permanently delete all data. Continue?[/]", false);
            if (!confirm)
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                return 0;
            }
        }

        await AnsiConsole.Status()
            .StartAsync("Clearing database...", async ctx =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<SegmentCommerceDbContext>();
                optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

                await using var dbContext = new SegmentCommerceDbContext(optionsBuilder.Options);

                ctx.Status("Clearing product embeddings...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM product_embeddings");

                ctx.Status("Clearing product variations...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM product_variations");

                ctx.Status("Clearing product taxonomy...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM product_taxonomy");

                ctx.Status("Clearing store products...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM store_products");

                ctx.Status("Clearing products...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM products");

                ctx.Status("Clearing sellers...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sellers");

                ctx.Status("Clearing interest scores...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM interest_scores");

                ctx.Status("Clearing interest embeddings...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM interest_embeddings");

                ctx.Status("Clearing signals...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM signals");

                ctx.Status("Clearing session profiles...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM session_profiles");

                ctx.Status("Clearing anonymous profiles...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM anonymous_profiles");

                ctx.Status("Clearing profile keys...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM profile_keys");

                ctx.Status("Clearing interaction events...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM interaction_events");

                ctx.Status("Clearing visitor profiles...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM visitor_profiles");

                ctx.Status("Clearing outbox messages...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM outbox_messages");

                ctx.Status("Clearing job queue...");
                await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM job_queue");

                if (!settings.KeepCategories)
                {
                    ctx.Status("Clearing categories...");
                    await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM categories");

                    ctx.Status("Clearing taxonomy nodes...");
                    await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM taxonomy_nodes");

                    ctx.Status("Clearing stores...");
                    await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM store_users");
                    await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM stores");
                }

                ctx.Status("Resetting sequences...");
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    DO $$
                    DECLARE
                        r RECORD;
                    BEGIN
                        FOR r IN (SELECT relname FROM pg_class WHERE relkind = 'S') LOOP
                            EXECUTE 'ALTER SEQUENCE ' || quote_ident(r.relname) || ' RESTART WITH 1';
                        END LOOP;
                    END $$;
                ");
            });

        AnsiConsole.MarkupLine("[green]Database cleared successfully![/]");
        return 0;
    }
}
