using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostlylucid.OcrNer.CLI.Services;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Models;
using Mostlylucid.OcrNer.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.OcrNer.CLI.Commands;

/// <summary>
/// Extract named entities (people, orgs, locations) from text.
///
/// Usage:
///   ocrner ner "John Smith works at Microsoft in Seattle"
///   ocrner ner "Marie Curie won the Nobel Prize" -o entities.json
///   ocrner ner ./test.png -o test.txt
/// </summary>
public sealed class NerCommand : AsyncCommand<NerCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // If the argument resolves to image file(s), run OCR+NER pipeline.
        var files = OutputWriter.ResolveImageFiles(settings.Text);
        if (files.Count > 0)
        {
            if (!settings.Quiet)
            {
                AnsiConsole.Write(new FigletText("OcrNer").Color(Color.Cyan1));
                AnsiConsole.MarkupLine($"[dim]NER command detected image input — running OCR + NER on {files.Count} file(s)[/]");
                AnsiConsole.WriteLine();
            }

            await using var imageServices = ServiceBootstrap.CreateServices(settings);
            var pipeline = imageServices.GetRequiredService<IOcrNerPipeline>();
            var ocrResults = new List<(string File, OcrNerResult Result)>();

            if (settings.Quiet)
            {
                foreach (var file in files)
                {
                    var pipelineResult = await pipeline.ProcessImageAsync(file);
                    ocrResults.Add((file, pipelineResult));
                }
            }
            else
            {
                await ProgressHelper.RunAsync(async ctx =>
                {
                    var task = ctx.AddTask("[cyan]Processing image input...[/]", maxValue: files.Count);
                    foreach (var file in files)
                    {
                        task.Description = $"[cyan]{Markup.Escape(Path.GetFileName(file))}[/]";
                        var pipelineResult = await pipeline.ProcessImageAsync(file);
                        ocrResults.Add((file, pipelineResult));
                        task.Increment(1);
                    }

                    task.Description = $"[green]Processed {files.Count} image(s)[/]";
                });
            }

            await OutputWriter.WriteOcrResultsAsync(ocrResults, settings.Output, settings.Quiet);
            return 0;
        }

        if (!settings.Quiet)
        {
            AnsiConsole.Write(new FigletText("OcrNer").Color(Color.Cyan1));
            AnsiConsole.MarkupLine("[dim]Named Entity Recognition[/]");
            AnsiConsole.WriteLine();
        }

        await using var services = ServiceBootstrap.CreateServices(settings);
        var nerService = services.GetRequiredService<INerService>();
        var config = services.GetRequiredService<IOptions<OcrNerConfig>>().Value;

        NerResult? result = null;
        RecognizedSignals? signals = null;

        if (settings.Quiet)
        {
            result = await nerService.ExtractEntitiesAsync(settings.Text);
            if (config.EnableRecognizers)
            {
                var recognizer = services.GetRequiredService<ITextRecognizerService>();
                signals = recognizer.ExtractAll(settings.Text);
            }
        }
        else
        {
            await ProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Extracting entities...[/]", maxValue: 100);
                result = await nerService.ExtractEntitiesAsync(settings.Text);
                if (config.EnableRecognizers)
                {
                    var recognizer = services.GetRequiredService<ITextRecognizerService>();
                    signals = recognizer.ExtractAll(settings.Text);
                }
                task.Value = 100;
                task.Description = $"[green]Found {result.Entities.Count} entities[/]";
            });
        }

        if (result != null)
            await OutputWriter.WriteNerResultAsync(result, settings.Output, settings.Quiet, signals);

        return 0;
    }

    public sealed class Settings : CommonSettings
    {
        [CommandArgument(0, "<text>")]
        [Description("Text to analyze, or an image path/glob/directory for OCR+NER")]
        public string Text { get; init; } = "";
    }
}
