using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.OcrNer.CLI.Services;
using Mostlylucid.OcrNer.Models;
using Mostlylucid.OcrNer.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.OcrNer.CLI.Commands;

/// <summary>
/// Extract text and entities from images using Tesseract OCR + BERT NER.
/// Accepts a single image, glob pattern (*.png), or directory.
///
/// Usage:
///   ocrner ocr invoice.png
///   ocrner ocr "scans/*.png"
///   ocrner ocr ./documents/ -o results.json
///   ocrner ocr invoice.png --json
/// </summary>
public sealed class OcrCommand : AsyncCommand<OcrCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var files = OutputWriter.ResolveImageFiles(settings.Path);

        if (files.Count == 0)
        {
            if (settings.Json)
            {
                OutputWriter.WriteJsonError("ocr", $"No image files found matching: {settings.Path}");
                return 1;
            }

            AnsiConsole.MarkupLine($"[red]No image files found matching:[/] {Markup.Escape(settings.Path)}");
            AnsiConsole.MarkupLine("[dim]Supported formats: .png, .jpg, .jpeg, .bmp, .tiff, .gif, .webp[/]");
            return 1;
        }

        if (!settings.EffectiveQuiet)
        {
            AnsiConsole.Write(new FigletText("OcrNer").Color(Color.Cyan1));
            AnsiConsole.MarkupLine($"[dim]OCR + NER Pipeline — {files.Count} file(s)[/]");
            AnsiConsole.WriteLine();
        }

        await using var services = ServiceBootstrap.CreateServices(settings);
        var pipeline = services.GetRequiredService<IOcrNerPipeline>();

        var results = new List<(string File, OcrNerResult Result)>();

        if (settings.EffectiveQuiet)
        {
            foreach (var file in files)
            {
                var result = await pipeline.ProcessImageAsync(file);
                results.Add((file, result));
            }
        }
        else
        {
            await ProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Processing images...[/]", maxValue: files.Count);

                foreach (var file in files)
                {
                    task.Description = $"[cyan]{Markup.Escape(Path.GetFileName(file))}[/]";
                    var result = await pipeline.ProcessImageAsync(file);
                    results.Add((file, result));
                    task.Increment(1);
                }

                task.Description = $"[green]Processed {files.Count} image(s)[/]";
            });
        }

        if (settings.Json)
        {
            OutputWriter.WriteOcrJsonToStdout(results);
        }
        else
        {
            await OutputWriter.WriteOcrResultsAsync(results, settings.Output, settings.Quiet);
        }

        return 0;
    }

    public sealed class Settings : CommonSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Image file, glob pattern (*.png), or directory")]
        public string Path { get; init; } = "";
    }
}
