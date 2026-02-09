using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.CLI.Commands;
using Mostlylucid.OcrNer.Extensions;
using Serilog;

namespace Mostlylucid.OcrNer.CLI.Services;

/// <summary>
/// Creates a DI container configured from CLI settings.
/// Applies CLI flag overrides on top of defaults.
/// </summary>
internal static class ServiceBootstrap
{
    public static ServiceProvider CreateServices(CommonSettings settings)
    {
        // In --json mode, suppress all logging so stdout is pure JSON.
        // In --quiet mode, only show warnings+. Otherwise, show info+.
        var logLevel = settings.Json
            ? Serilog.Events.LogEventLevel.Fatal
            : settings.Quiet
                ? Serilog.Events.LogEventLevel.Warning
                : Serilog.Events.LogEventLevel.Information;

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel);

        // In JSON mode, don't even attach the console sink
        if (!settings.Json)
        {
            logConfig = logConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = logConfig.CreateLogger();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSerilog(dispose: true));

        services.AddOcrNer(config =>
        {
            if (settings.ModelDirectory != null)
                config.ModelDirectory = settings.ModelDirectory;
            if (settings.MinConfidence.HasValue)
                config.MinConfidence = settings.MinConfidence.Value;
            if (settings.Language != null)
                config.TesseractLanguage = settings.Language;
            if (settings.MaxTokens.HasValue)
                config.MaxSequenceLength = settings.MaxTokens.Value;
            if (settings.Preprocess != null)
                config.Preprocessing = settings.Preprocess.ToLowerInvariant() switch
                {
                    "none" => PreprocessingLevel.None,
                    "minimal" => PreprocessingLevel.Minimal,
                    "aggressive" => PreprocessingLevel.Aggressive,
                    _ => PreprocessingLevel.Default
                };
            if (settings.AdvancedPreprocess)
                config.EnableAdvancedPreprocessing = true;
            if (settings.Recognizers)
                config.EnableRecognizers = true;
            if (settings.Culture != null)
                config.RecognizerCulture = settings.Culture;
        });

        return services.BuildServiceProvider();
    }
}
