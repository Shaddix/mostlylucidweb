using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.OcrNer.Models;
using Mostlylucid.OcrNer.Services;
using Spectre.Console;

namespace Mostlylucid.OcrNer.CLI.Services;

/// <summary>
/// Formats and writes results to console or file.
/// Supports: console (Spectre panels/tables), .txt, .md, .json
/// </summary>
internal static class OutputWriter
{
    private static readonly string[] ImageExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".gif", ".webp"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ─── Public API ──────────────────────────────────────────

    /// <summary>
    /// Write NER results to console or file.
    /// </summary>
    public static async Task WriteNerResultAsync(
        NerResult result, string? outputPath, bool quiet,
        RecognizedSignals? signals = null)
    {
        if (outputPath != null)
        {
            var content = FormatByExtension(outputPath,
                () => FormatNerText(result, signals),
                () => FormatNerMarkdown(result, signals),
                () => FormatNerJson(result, signals));

            await WriteFileAsync(outputPath, content, quiet);
        }
        else
        {
            WriteNerToConsole(result, quiet);
            if (signals is { HasAnySignals: true })
                WriteSignalsToConsole(signals);
        }
    }

    /// <summary>
    /// Write OCR+NER results for one or more images to console or file.
    /// </summary>
    public static async Task WriteOcrResultsAsync(
        List<(string File, OcrNerResult Result)> results, string? outputPath, bool quiet)
    {
        if (outputPath != null)
        {
            var content = FormatByExtension(outputPath,
                () => FormatOcrText(results),
                () => FormatOcrMarkdown(results),
                () => FormatOcrJson(results));

            await WriteFileAsync(outputPath, content, quiet);
        }
        else
        {
            WriteOcrToConsole(results, quiet);
        }
    }

    /// <summary>
    /// Write caption results for one or more images to console or file.
    /// </summary>
    public static async Task WriteCaptionResultsAsync(
        List<(string File, VisionCaptionResult Caption, VisionOcrResult? Ocr, NerResult? Ner)> results,
        string? outputPath, bool quiet)
    {
        if (outputPath != null)
        {
            var content = FormatByExtension(outputPath,
                () => FormatCaptionText(results),
                () => FormatCaptionMarkdown(results),
                () => FormatCaptionJson(results));

            await WriteFileAsync(outputPath, content, quiet);
        }
        else
        {
            WriteCaptionToConsole(results, quiet);
        }
    }

    /// <summary>
    /// Resolve a file path, glob pattern, or directory to a list of image files.
    /// Handles Windows (no shell glob expansion) and Unix.
    /// </summary>
    public static List<string> ResolveImageFiles(string input)
    {
        // Glob pattern (e.g. "scans/*.png")
        if (input.Contains('*') || input.Contains('?'))
        {
            var dir = Path.GetDirectoryName(input);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            var pattern = Path.GetFileName(input);
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly)
                    .Where(IsImageExtension)
                    .OrderBy(f => f)
                    .ToList()
                : [];
        }

        // Directory → all images in it
        if (Directory.Exists(input))
        {
            return Directory.GetFiles(input, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsImageExtension)
                .OrderBy(f => f)
                .ToList();
        }

        // Single file
        if (File.Exists(input))
            return [input];

        return [];
    }

    // ─── Console formatters ──────────────────────────────────

    private static void WriteNerToConsole(NerResult result, bool quiet)
    {
        if (!quiet)
        {
            AnsiConsole.MarkupLine($"[dim]Input:[/] {Markup.Escape(Truncate(result.SourceText, 120))}");
            AnsiConsole.WriteLine();
        }

        if (result.Entities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No entities found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]Entity[/]")
            .AddColumn("[bold]Confidence[/]")
            .AddColumn("[bold]Position[/]");

        foreach (var e in result.Entities)
        {
            var color = EntityColor(e.Label);
            table.AddRow(
                $"[{color}]{Markup.Escape(e.Label)}[/]",
                Markup.Escape(e.Text),
                $"{e.Confidence:P0}",
                $"{e.StartOffset}-{e.EndOffset}");
        }

        AnsiConsole.Write(table);
    }

    private static void WriteOcrToConsole(
        List<(string File, OcrNerResult Result)> results, bool quiet)
    {
        foreach (var (file, result) in results)
        {
            if (!quiet)
            {
                AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(Path.GetFileName(file))}[/]")
                    .LeftJustified());
                AnsiConsole.MarkupLine($"[dim]OCR confidence: {result.OcrResult.Confidence:P0}[/]");
                AnsiConsole.WriteLine();
            }

            if (!string.IsNullOrWhiteSpace(result.OcrResult.Text))
            {
                AnsiConsole.Write(new Panel(Markup.Escape(Truncate(result.OcrResult.Text, 500)))
                    .Header("[bold]Extracted Text[/]")
                    .Border(BoxBorder.Rounded)
                    .Padding(1, 0));
                AnsiConsole.WriteLine();
            }

            if (result.NerResult.Entities.Count > 0)
            {
                var table = new Table()
                    .Border(TableBorder.Simple)
                    .AddColumn("[bold]Type[/]")
                    .AddColumn("[bold]Entity[/]")
                    .AddColumn("[bold]Confidence[/]");

                foreach (var e in result.NerResult.Entities)
                {
                    var color = EntityColor(e.Label);
                    table.AddRow(
                        $"[{color}]{Markup.Escape(e.Label)}[/]",
                        Markup.Escape(e.Text),
                        $"{e.Confidence:P0}");
                }

                AnsiConsole.Write(table);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No entities found.[/]");
            }

            if (result.Signals is { HasAnySignals: true })
                WriteSignalsToConsole(result.Signals);

            AnsiConsole.WriteLine();
        }
    }

    private static void WriteCaptionToConsole(
        List<(string File, VisionCaptionResult Caption, VisionOcrResult? Ocr, NerResult? Ner)> results,
        bool quiet)
    {
        foreach (var (file, caption, ocr, ner) in results)
        {
            if (!quiet)
                AnsiConsole.Write(new Rule($"[cyan]{Markup.Escape(Path.GetFileName(file))}[/]")
                    .LeftJustified());

            if (caption.Success)
            {
                AnsiConsole.Write(new Panel(Markup.Escape(caption.Caption ?? ""))
                    .Header("[bold]Caption[/]")
                    .Border(BoxBorder.Rounded)
                    .Padding(1, 0));

                if (!quiet)
                    AnsiConsole.MarkupLine($"[dim]({caption.DurationMs}ms)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine(
                    $"[red]Caption failed: {Markup.Escape(caption.Error ?? "Unknown")}[/]");
            }

            if (ocr is { Success: true })
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Panel(Markup.Escape(ocr.Text ?? ""))
                    .Header("[bold]Vision OCR[/]")
                    .Border(BoxBorder.Rounded)
                    .Padding(1, 0));

                if (!quiet)
                    AnsiConsole.MarkupLine($"[dim]({ocr.DurationMs}ms)[/]");
            }

            if (ner != null)
            {
                AnsiConsole.WriteLine();
                if (ner.Entities.Count > 0)
                {
                    var table = new Table()
                        .Border(TableBorder.Simple)
                        .AddColumn("[bold]NER Type[/]")
                        .AddColumn("[bold]Entity[/]")
                        .AddColumn("[bold]Confidence[/]");

                    foreach (var e in ner.Entities)
                    {
                        var color = EntityColor(e.Label);
                        table.AddRow(
                            $"[{color}]{Markup.Escape(e.Label)}[/]",
                            Markup.Escape(e.Text),
                            $"{e.Confidence:P0}");
                    }

                    AnsiConsole.Write(table);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No entities found in OCR text.[/]");
                }
            }

            AnsiConsole.WriteLine();
        }
    }

    // ─── Signals console writer ──────────────────────────────

    private static void WriteSignalsToConsole(RecognizedSignals signals)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[cyan]Recognized Signals[/]").LeftJustified());

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]Text[/]")
            .AddColumn("[bold]Details[/]");

        foreach (var dt in signals.DateTimes)
            table.AddRow("[cyan]DateTime[/]", Markup.Escape(dt.Text), Markup.Escape(dt.TypeName ?? ""));
        foreach (var n in signals.Numbers)
            table.AddRow("[cyan]Number[/]", Markup.Escape(n.Text), Markup.Escape($"value={n.Value}"));
        foreach (var u in signals.Urls)
            table.AddRow("[cyan]URL[/]", Markup.Escape(u.Text), "");
        foreach (var p in signals.PhoneNumbers)
            table.AddRow("[cyan]Phone[/]", Markup.Escape(p.Text), "");
        foreach (var e in signals.Emails)
            table.AddRow("[cyan]Email[/]", Markup.Escape(e.Text), "");
        foreach (var ip in signals.IpAddresses)
            table.AddRow("[cyan]IP[/]", Markup.Escape(ip.Text), "");

        AnsiConsole.Write(table);
    }

    // ─── Text formatters ─────────────────────────────────────

    private static string FormatNerText(NerResult result, RecognizedSignals? signals = null)
    {
        var lines = new List<string>
        {
            $"Input: {result.SourceText}",
            $"Entities: {result.Entities.Count}",
            ""
        };

        foreach (var e in result.Entities)
            lines.Add(
                $"  [{e.Label}] {e.Text} (confidence: {e.Confidence:P0}, chars {e.StartOffset}-{e.EndOffset})");

        if (signals is { HasAnySignals: true })
        {
            lines.Add("");
            lines.Add("Recognized Signals:");
            foreach (var dt in signals.DateTimes)
                lines.Add($"  [DateTime] {dt.Text} ({dt.TypeName})");
            foreach (var n in signals.Numbers)
                lines.Add($"  [Number] {n.Text} (value={n.Value})");
            foreach (var u in signals.Urls)
                lines.Add($"  [URL] {u.Text}");
            foreach (var p in signals.PhoneNumbers)
                lines.Add($"  [Phone] {p.Text}");
            foreach (var e in signals.Emails)
                lines.Add($"  [Email] {e.Text}");
            foreach (var ip in signals.IpAddresses)
                lines.Add($"  [IP] {ip.Text}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatOcrText(List<(string File, OcrNerResult Result)> results)
    {
        var lines = new List<string>();
        foreach (var (file, result) in results)
        {
            lines.Add($"File: {file}");
            lines.Add($"OCR Confidence: {result.OcrResult.Confidence:P0}");
            lines.Add($"Text: {result.OcrResult.Text}");
            lines.Add($"Entities: {result.NerResult.Entities.Count}");
            foreach (var e in result.NerResult.Entities)
                lines.Add($"  [{e.Label}] {e.Text} ({e.Confidence:P0})");

            if (result.Signals is { HasAnySignals: true })
            {
                lines.Add("Recognized Signals:");
                foreach (var dt in result.Signals.DateTimes)
                    lines.Add($"  [DateTime] {dt.Text} ({dt.TypeName})");
                foreach (var n in result.Signals.Numbers)
                    lines.Add($"  [Number] {n.Text} (value={n.Value})");
                foreach (var u in result.Signals.Urls)
                    lines.Add($"  [URL] {u.Text}");
                foreach (var p in result.Signals.PhoneNumbers)
                    lines.Add($"  [Phone] {p.Text}");
                foreach (var e in result.Signals.Emails)
                    lines.Add($"  [Email] {e.Text}");
                foreach (var ip in result.Signals.IpAddresses)
                    lines.Add($"  [IP] {ip.Text}");
            }

            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCaptionText(
        List<(string File, VisionCaptionResult Caption, VisionOcrResult? Ocr, NerResult? Ner)> results)
    {
        var lines = new List<string>();
        foreach (var (file, caption, ocr, ner) in results)
        {
            lines.Add($"File: {file}");
            lines.Add(caption.Success
                ? $"Caption: {caption.Caption}"
                : $"Caption failed: {caption.Error}");
            if (ocr is { Success: true })
                lines.Add($"Vision OCR: {ocr.Text}");
            if (ner != null)
            {
                lines.Add($"Entities: {ner.Entities.Count}");
                foreach (var e in ner.Entities)
                    lines.Add($"  [{e.Label}] {e.Text} ({e.Confidence:P0})");
            }
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    // ─── Markdown formatters ─────────────────────────────────

    private static string FormatNerMarkdown(NerResult result, RecognizedSignals? signals = null)
    {
        var lines = new List<string>
        {
            "# Named Entity Recognition Results",
            "",
            $"> **Input:** {result.SourceText}",
            "",
            "| Type | Entity | Confidence | Position |",
            "|------|--------|------------|----------|"
        };

        foreach (var e in result.Entities)
            lines.Add($"| {e.Label} | {e.Text} | {e.Confidence:P0} | {e.StartOffset}-{e.EndOffset} |");

        if (signals is { HasAnySignals: true })
        {
            lines.Add("");
            lines.Add("## Recognized Signals");
            lines.Add("");
            lines.Add("| Type | Text | Details |");
            lines.Add("|------|------|---------|");
            foreach (var dt in signals.DateTimes)
                lines.Add($"| DateTime | {dt.Text} | {dt.TypeName} |");
            foreach (var n in signals.Numbers)
                lines.Add($"| Number | {n.Text} | value={n.Value} |");
            foreach (var u in signals.Urls)
                lines.Add($"| URL | {u.Text} | |");
            foreach (var p in signals.PhoneNumbers)
                lines.Add($"| Phone | {p.Text} | |");
            foreach (var e in signals.Emails)
                lines.Add($"| Email | {e.Text} | |");
            foreach (var ip in signals.IpAddresses)
                lines.Add($"| IP | {ip.Text} | |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatOcrMarkdown(List<(string File, OcrNerResult Result)> results)
    {
        var lines = new List<string> { "# OCR + NER Results", "" };

        foreach (var (file, result) in results)
        {
            lines.Add($"## {Path.GetFileName(file)}");
            lines.Add("");
            lines.Add($"**OCR Confidence:** {result.OcrResult.Confidence:P0}");
            lines.Add("");

            if (!string.IsNullOrWhiteSpace(result.OcrResult.Text))
            {
                lines.Add("### Extracted Text");
                lines.Add("");
                lines.Add("```");
                lines.Add(result.OcrResult.Text);
                lines.Add("```");
                lines.Add("");
            }

            if (result.NerResult.Entities.Count > 0)
            {
                lines.Add("### Entities");
                lines.Add("");
                lines.Add("| Type | Entity | Confidence |");
                lines.Add("|------|--------|------------|");
                foreach (var e in result.NerResult.Entities)
                    lines.Add($"| {e.Label} | {e.Text} | {e.Confidence:P0} |");
                lines.Add("");
            }

            if (result.Signals is { HasAnySignals: true })
            {
                lines.Add("### Recognized Signals");
                lines.Add("");
                lines.Add("| Type | Text | Details |");
                lines.Add("|------|------|---------|");
                foreach (var dt in result.Signals.DateTimes)
                    lines.Add($"| DateTime | {dt.Text} | {dt.TypeName} |");
                foreach (var n in result.Signals.Numbers)
                    lines.Add($"| Number | {n.Text} | value={n.Value} |");
                foreach (var u in result.Signals.Urls)
                    lines.Add($"| URL | {u.Text} | |");
                foreach (var p in result.Signals.PhoneNumbers)
                    lines.Add($"| Phone | {p.Text} | |");
                foreach (var e in result.Signals.Emails)
                    lines.Add($"| Email | {e.Text} | |");
                foreach (var ip in result.Signals.IpAddresses)
                    lines.Add($"| IP | {ip.Text} | |");
                lines.Add("");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatCaptionMarkdown(
        List<(string File, VisionCaptionResult Caption, VisionOcrResult? Ocr, NerResult? Ner)> results)
    {
        var lines = new List<string> { "# Image Captions", "" };

        foreach (var (file, caption, ocr, ner) in results)
        {
            lines.Add($"## {Path.GetFileName(file)}");
            lines.Add("");
            lines.Add(caption.Success
                ? $"**Caption:** {caption.Caption}"
                : $"**Error:** {caption.Error}");
            lines.Add("");

            if (ocr is { Success: true })
            {
                lines.Add("### Visible Text");
                lines.Add("");
                lines.Add("```");
                lines.Add(ocr.Text ?? "");
                lines.Add("```");
                lines.Add("");
            }

            if (ner != null)
            {
                lines.Add("### Entities");
                lines.Add("");
                lines.Add($"Count: {ner.Entities.Count}");
                lines.Add("");
                if (ner.Entities.Count > 0)
                {
                    lines.Add("| Type | Entity | Confidence |");
                    lines.Add("|------|--------|------------|");
                    foreach (var e in ner.Entities)
                        lines.Add($"| {e.Label} | {e.Text} | {e.Confidence:P0} |");
                    lines.Add("");
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    // ─── JSON formatters ─────────────────────────────────────

    private static string FormatNerJson(NerResult result, RecognizedSignals? signals = null) =>
        JsonSerializer.Serialize(new
        {
            sourceText = result.SourceText,
            entityCount = result.Entities.Count,
            entities = result.Entities.Select(e => new
            {
                type = e.Label,
                text = e.Text,
                confidence = Math.Round(e.Confidence, 4),
                startOffset = e.StartOffset,
                endOffset = e.EndOffset
            }),
            signals = signals is { HasAnySignals: true } ? new
            {
                culture = signals.Culture,
                dateTimes = signals.DateTimes.Select(dt => new { dt.Text, dt.Start, dt.End, dt.TypeName }),
                numbers = signals.Numbers.Select(n => new { n.Text, n.Start, n.Value, n.TypeName }),
                urls = signals.Urls.Select(u => new { u.Text, u.Start }),
                phoneNumbers = signals.PhoneNumbers.Select(p => new { p.Text, p.Start }),
                emails = signals.Emails.Select(e => new { e.Text, e.Start }),
                ipAddresses = signals.IpAddresses.Select(ip => new { ip.Text, ip.Start })
            } : null
        }, JsonOptions);

    private static string FormatOcrJson(List<(string File, OcrNerResult Result)> results) =>
        JsonSerializer.Serialize(new
        {
            fileCount = results.Count,
            results = results.Select(r => new
            {
                file = r.File,
                ocr = new
                {
                    text = r.Result.OcrResult.Text,
                    confidence = Math.Round(r.Result.OcrResult.Confidence, 4)
                },
                entities = r.Result.NerResult.Entities.Select(e => new
                {
                    type = e.Label,
                    text = e.Text,
                    confidence = Math.Round(e.Confidence, 4)
                }),
                signals = r.Result.Signals is { HasAnySignals: true } ? new
                {
                    culture = r.Result.Signals.Culture,
                    dateTimes = r.Result.Signals.DateTimes.Select(dt => new { dt.Text, dt.Start, dt.End, dt.TypeName }),
                    numbers = r.Result.Signals.Numbers.Select(n => new { n.Text, n.Start, n.Value, n.TypeName }),
                    urls = r.Result.Signals.Urls.Select(u => new { u.Text, u.Start }),
                    phoneNumbers = r.Result.Signals.PhoneNumbers.Select(p => new { p.Text, p.Start }),
                    emails = r.Result.Signals.Emails.Select(e => new { e.Text, e.Start }),
                    ipAddresses = r.Result.Signals.IpAddresses.Select(ip => new { ip.Text, ip.Start })
                } : null
            })
        }, JsonOptions);

    private static string FormatCaptionJson(
        List<(string File, VisionCaptionResult Caption, VisionOcrResult? Ocr, NerResult? Ner)> results) =>
        JsonSerializer.Serialize(new
        {
            fileCount = results.Count,
            results = results.Select(r => new
            {
                file = r.File,
                caption = r.Caption.Success ? r.Caption.Caption : null,
                error = r.Caption.Success ? null : r.Caption.Error,
                durationMs = r.Caption.DurationMs,
                visionOcr = r.Ocr is { Success: true }
                    ? new
                    {
                        text = r.Ocr.Text,
                        durationMs = r.Ocr.DurationMs
                    }
                    : null,
                ner = r.Ner != null
                    ? new
                    {
                        sourceText = r.Ner.SourceText,
                        entityCount = r.Ner.Entities.Count,
                        entities = r.Ner.Entities.Select(e => new
                        {
                            type = e.Label,
                            text = e.Text,
                            confidence = Math.Round(e.Confidence, 4),
                            startOffset = e.StartOffset,
                            endOffset = e.EndOffset
                        })
                    }
                    : null
            })
        }, JsonOptions);

    // ─── Helpers ──────────────────────────────────────────────

    private static string FormatByExtension(
        string path, Func<string> textFn, Func<string> mdFn, Func<string> jsonFn)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json" => jsonFn(),
            ".md" => mdFn(),
            _ => textFn()
        };
    }

    private static async Task WriteFileAsync(string path, string content, bool quiet)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(path, content);
        if (!quiet)
            AnsiConsole.MarkupLine($"[green]Saved to:[/] {Markup.Escape(path)}");
    }

    private static bool IsImageExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ImageExtensions.Contains(ext);
    }

    private static string EntityColor(string label) => label switch
    {
        "PER" => "green",
        "ORG" => "blue",
        "LOC" => "yellow",
        "MISC" => "magenta",
        _ => "white"
    };

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
}
