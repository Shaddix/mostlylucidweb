using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.CLI.Services;
using Mostlylucid.OcrNer.Models;
using Mostlylucid.OcrNer.Services;
using Mostlylucid.OcrNer.Services.Preprocessing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Mostlylucid.OcrNer.CLI.Commands;

/// <summary>
/// Generate image captions using Florence-2 vision model.
/// Optionally extract visible text via Florence-2's built-in OCR.
///
/// Usage:
///   ocrner caption photo.jpg
///   ocrner caption "photos/*.jpg" --ocr -o captions.json
///   ocrner caption photo.jpg --brief
///   ocrner caption photo.jpg --ner -o analysis.json
///   ocrner caption photo.jpg --json
/// </summary>
public sealed class CaptionCommand : AsyncCommand<CaptionCommand.Settings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var files = OutputWriter.ResolveImageFiles(settings.Path);

        if (files.Count == 0)
        {
            if (settings.Json)
            {
                OutputWriter.WriteJsonError("caption", $"No image files found matching: {settings.Path}");
                return 1;
            }

            AnsiConsole.MarkupLine($"[red]No image files found matching:[/] {Markup.Escape(settings.Path)}");
            AnsiConsole.MarkupLine("[dim]Supported formats: .png, .jpg, .jpeg, .bmp, .tiff, .gif, .webp[/]");
            return 1;
        }

        if (!settings.EffectiveQuiet)
        {
            AnsiConsole.Write(new FigletText("OcrNer").Color(Color.Cyan1));
            AnsiConsole.MarkupLine($"[dim]Florence-2 Vision — {files.Count} file(s)[/]");
            AnsiConsole.WriteLine();
        }

        await using var services = ServiceBootstrap.CreateServices(settings);
        var vision = services.GetRequiredService<IVisionService>();
        var preprocessor = services.GetRequiredService<ImagePreprocessor>();
        var includeOcr = settings.IncludeOcr || settings.IncludeNer;
        var nerService = settings.IncludeNer
            ? services.GetRequiredService<INerService>()
            : null;

        // Determine preprocessing level from settings
        var prepLevel = (settings.Preprocess?.ToLowerInvariant()) switch
        {
            "none" => PreprocessingLevel.None,
            "minimal" => PreprocessingLevel.Minimal,
            "aggressive" => PreprocessingLevel.Aggressive,
            _ => PreprocessingLevel.Default
        };

        var results = new List<(string File, VisionCaptionResult Caption, VisionOcrResult? Ocr, NerResult? Ner)>();

        // Helper: preprocess image to a temp file if needed, returns path to use
        async Task<string> MaybePreprocess(string file)
        {
            if (prepLevel == PreprocessingLevel.None)
                return file;

            var options = prepLevel switch
            {
                PreprocessingLevel.Minimal => PreprocessingOptions.Minimal,
                PreprocessingLevel.Aggressive => PreprocessingOptions.Aggressive,
                _ => PreprocessingOptions.Default
            };

            var bytes = preprocessor.PreprocessFile(file, options);
            var tempPath = Path.Combine(Path.GetTempPath(), $"ocrner_{Path.GetFileName(file)}");
            await File.WriteAllBytesAsync(tempPath, bytes);
            return tempPath;
        }

        if (settings.EffectiveQuiet)
        {
            foreach (var file in files)
            {
                var processedFile = await MaybePreprocess(file);
                try
                {
                    var caption = await vision.CaptionAsync(processedFile, detailed: !settings.Brief);
                    VisionOcrResult? ocr = includeOcr
                        ? await vision.ExtractTextAsync(processedFile)
                        : null;
                    NerResult? ner = null;
                    if (settings.IncludeNer && ocr is { Success: true } && !string.IsNullOrWhiteSpace(ocr.Text))
                        ner = await nerService!.ExtractEntitiesAsync(ocr.Text);

                    results.Add((file, caption, ocr, ner));
                }
                finally
                {
                    if (processedFile != file) File.Delete(processedFile);
                }
            }
        }
        else
        {
            await ProgressHelper.RunAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Captioning images...[/]", maxValue: files.Count);

                foreach (var file in files)
                {
                    task.Description = $"[cyan]{Markup.Escape(Path.GetFileName(file))}[/]";
                    var processedFile = await MaybePreprocess(file);
                    try
                    {
                        var caption = await vision.CaptionAsync(processedFile, detailed: !settings.Brief);
                        VisionOcrResult? ocr = includeOcr
                            ? await vision.ExtractTextAsync(processedFile)
                            : null;
                        NerResult? ner = null;
                        if (settings.IncludeNer && ocr is { Success: true } && !string.IsNullOrWhiteSpace(ocr.Text))
                            ner = await nerService!.ExtractEntitiesAsync(ocr.Text);

                        results.Add((file, caption, ocr, ner));
                    }
                    finally
                    {
                        if (processedFile != file) File.Delete(processedFile);
                    }
                    task.Increment(1);
                }

                task.Description = $"[green]Captioned {files.Count} image(s)[/]";
            });
        }

        if (settings.Json)
        {
            OutputWriter.WriteCaptionJsonToStdout(results);
        }
        else
        {
            await OutputWriter.WriteCaptionResultsAsync(results, settings.Output, settings.Quiet);
        }

        return 0;
    }

    public sealed class Settings : CommonSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Image file, glob pattern (*.png), or directory")]
        public string Path { get; init; } = "";

        [CommandOption("--brief")]
        [Description("Generate a shorter, less detailed caption")]
        public bool Brief { get; init; }

        [CommandOption("--ocr")]
        [Description("Also extract visible text using Florence-2 OCR")]
        public bool IncludeOcr { get; init; }

        [CommandOption("--ner")]
        [Description("Extract named entities from OCR text (implies --ocr)")]
        public bool IncludeNer { get; init; }
    }
}
