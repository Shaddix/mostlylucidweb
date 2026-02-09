using Mostlylucid.OcrNer.CLI.Commands;
using Spectre.Console.Cli;

// ──────────────────────────────────────────────────────────────
// Mostlylucid.OcrNer CLI
//
// A tool for OCR, Named Entity Recognition, and Image Captioning.
// All models auto-download on first use. Zero setup required.
//
// Usage:
//   ocrner ner "John Smith works at Microsoft in Seattle"
//   ocrner ocr invoice.png
//   ocrner caption photo.jpg
//   ocrner ocr ./scans/*.png -o results.json
// ──────────────────────────────────────────────────────────────

var args2 = args;

// ─── Stdin support ──────────────────────────────────────────
// When stdin is piped (e.g. echo "text" | ocrner ner --json),
// read it and inject as the text argument for the ner command.
string? stdinText = null;
if (Console.IsInputRedirected)
{
    stdinText = Console.In.ReadToEnd().TrimEnd('\r', '\n');
}

// Smart routing: detect what the user likely wants
if (args2.Length == 0 && stdinText == null)
{
    args2 = ["--help"];
}
else if (args2.Length == 0 && stdinText != null)
{
    // Bare stdin with no command → ner
    args2 = ["ner", stdinText];
}
else if (!args2[0].StartsWith('-') && !IsKnownCommand(args2[0]))
{
    if (IsImageFile(args2[0]) || IsGlobPattern(args2[0]) || Directory.Exists(args2[0]))
    {
        // Image file, glob, or directory → ocr command
        args2 = ["ocr", .. args2];
    }
    else
    {
        // Assume it's text → ner command
        args2 = ["ner", .. args2];
    }
}
else if (stdinText != null && args2.Length >= 1 && args2[0] == "ner")
{
    // ner command with stdin: inject stdin text if no text argument provided
    // Check if there's a positional argument (non-flag after "ner")
    var hasTextArg = args2.Skip(1).Any(a => !a.StartsWith('-') && a != "-");
    var hasDashArg = args2.Skip(1).Any(a => a == "-");
    if (!hasTextArg || hasDashArg)
    {
        // Remove the "-" placeholder if present, inject stdin text
        var filtered = args2.Where(a => a != "-").ToList();
        // Insert stdin text after "ner" command
        filtered.Insert(1, stdinText);
        args2 = filtered.ToArray();
    }
}

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("ocrner");

    config.AddCommand<NerCommand>("ner")
        .WithDescription("Extract named entities (people, orgs, locations) from text")
        .WithExample("ner", "\"John Smith works at Microsoft in Seattle\"")
        .WithExample("ner", "\"Marie Curie won the Nobel Prize\"", "-o", "entities.json")
        .WithExample("ner", "\"Apple announced a product launch\"", "-c", "0.8");

    config.AddCommand<OcrCommand>("ocr")
        .WithDescription("Extract text and entities from images (Tesseract OCR + BERT NER)")
        .WithExample("ocr", "invoice.png")
        .WithExample("ocr", "scans/*.png")
        .WithExample("ocr", "./documents/", "-o", "results.json")
        .WithExample("ocr", "scan.png", "-o", "output.md");

    config.AddCommand<CaptionCommand>("caption")
        .WithDescription("Generate image captions using Florence-2 vision model")
        .WithExample("caption", "photo.jpg")
        .WithExample("caption", "photos/*.jpg", "--ocr", "-o", "captions.json")
        .WithExample("caption", "photo.jpg", "--brief")
        .WithExample("caption", "photo.jpg", "--ner", "-o", "analysis.json");
});

return await app.RunAsync(args2);

// ─── Smart routing helpers ───────────────────────────────────

static bool IsKnownCommand(string arg) =>
    arg is "ner" or "ocr" or "caption";

static bool IsImageFile(string arg) =>
    !arg.StartsWith('-') && (
        arg.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        arg.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
        arg.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
        arg.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
        arg.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
        arg.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
        arg.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));

static bool IsGlobPattern(string arg) =>
    !arg.StartsWith('-') && arg.Contains('*');
