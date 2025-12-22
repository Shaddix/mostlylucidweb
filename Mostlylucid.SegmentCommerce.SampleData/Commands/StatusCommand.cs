using Mostlylucid.SegmentCommerce.SampleData.Models;
using Mostlylucid.SegmentCommerce.SampleData.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.SegmentCommerce.SampleData.Commands;

public class StatusSettings : CommandSettings
{
}

public class StatusCommand : AsyncCommand<StatusSettings>
{
    private readonly GenerationConfig _config;

    public StatusCommand(GenerationConfig config)
    {
        _config = config;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings)
    {
        AnsiConsole.Write(new Rule("[bold cyan]Service Status[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Service")
            .AddColumn("URL")
            .AddColumn("Status");

        // Check Ollama
        var ollamaStatus = await CheckOllamaAsync();
        table.AddRow(
            "Ollama",
            _config.OllamaBaseUrl,
            ollamaStatus ? "[green]Available[/]" : "[red]Unavailable[/]");

        // Check ComfyUI
        var comfyStatus = await CheckComfyUIAsync();
        table.AddRow(
            "ComfyUI",
            _config.ComfyUIBaseUrl,
            comfyStatus ? "[green]Available[/]" : "[red]Unavailable[/]");

        // Check Database
        var dbStatus = await CheckDatabaseAsync();
        table.AddRow(
            "PostgreSQL",
            MaskConnectionString(_config.ConnectionString),
            dbStatus ? "[green]Available[/]" : "[red]Unavailable[/]");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Start services:[/]");
        AnsiConsole.MarkupLine("  Ollama: [cyan]ollama serve[/]");
        AnsiConsole.MarkupLine("  ComfyUI: [cyan]docker compose -f docker-compose.comfyui.yml up -d[/]");
        AnsiConsole.MarkupLine("  PostgreSQL: [cyan]docker compose -f devdeps-docker-compose.yml up -d[/]");

        return 0;
    }

    private async Task<bool> CheckOllamaAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{_config.OllamaBaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckComfyUIAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{_config.ComfyUIBaseUrl}/system_stats");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckDatabaseAsync()
    {
        try
        {
            using var client = new Npgsql.NpgsqlConnection(_config.ConnectionString);
            await client.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "[dim](not configured)[/]";

        // Extract host and database, mask password
        var parts = connectionString.Split(';')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        var host = parts.FirstOrDefault(p => p.StartsWith("Host=", StringComparison.OrdinalIgnoreCase))?.Split('=').Last() ?? "?";
        var db = parts.FirstOrDefault(p => p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))?.Split('=').Last() ?? "?";

        return $"{host}/{db}";
    }
}
