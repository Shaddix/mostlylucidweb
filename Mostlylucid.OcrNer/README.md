# Mostlylucid.OcrNer

**Local-first OCR, Named Entity Recognition, and Vision captioning for .NET**

[![NuGet](https://img.shields.io/nuget/v/Mostlylucid.OcrNer.svg?style=flat-square)](https://www.nuget.org/packages/Mostlylucid.OcrNer/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0%20%7C%2010.0-purple?style=flat-square)](https://dotnet.microsoft.com/)

One line of setup, zero model downloads. Everything auto-downloads on first use.

---

## Features

- **Tesseract OCR** - extract text from images with ImageSharp preprocessing
- **OpenCV preprocessing** - deskew, denoise, and binarize degraded documents (opt-in)
- **BERT NER** - extract people, organizations, locations, and miscellaneous entities via ONNX
- **Florence-2 Vision** - local image captioning and scene-text OCR
- **Microsoft.Recognizers.Text** - rule-based extraction of dates, numbers, URLs, phones, emails, IPs (opt-in)
- **Auto-download** - models download from HuggingFace/GitHub on first use with atomic caching
- **Full DI integration** - `AddOcrNer()` registers everything as singletons

## Quick Start

```bash
dotnet add package Mostlylucid.OcrNer
```

```csharp
// Register services
builder.Services.AddOcrNer(builder.Configuration);

// Use the pipeline
var pipeline = serviceProvider.GetRequiredService<IOcrNerPipeline>();
var result = await pipeline.ProcessImageAsync("invoice.png");

foreach (var entity in result.NerResult.Entities)
{
    // entity.Label: "PER", "ORG", "LOC", "MISC"
    // entity.Text: "John Smith"
    // entity.Confidence: 0.9996
}
```

## Configuration

```json
{
  "OcrNer": {
    "EnableOcr": true,
    "TesseractLanguage": "eng",
    "MinConfidence": 0.5,
    "MaxSequenceLength": 512,
    "ModelDirectory": "models/ocrner",
    "Preprocessing": "Default",
    "EnableAdvancedPreprocessing": false,
    "EnableRecognizers": false,
    "RecognizerCulture": "en-us"
  }
}
```

All settings have sensible defaults. The entire section can be omitted.

## Services

| Service | What it does | Use when... |
|---------|-------------|-------------|
| `INerService` | BERT NER from text | You already have text |
| `IOcrService` | Tesseract OCR from images | You need text from scans/screenshots |
| `IOcrNerPipeline` | OCR then NER in one call | You have images and want entities |
| `ITextRecognizerService` | Dates, phones, URLs, etc. | You want structured data alongside NER |
| `IVisionService` | Florence-2 captioning + OCR | You need image understanding |

## CLI Tool

A companion CLI is available at [Mostlylucid.OcrNer.CLI](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.OcrNer.CLI):

```bash
ocrner "John Smith works at Microsoft in Seattle"
ocrner ocr invoice.png -o results.json
ocrner caption photo.jpg --ocr --ner
```

## Documentation

- [Part 1: Building OCR + NER from Scratch](https://www.mostlylucid.net/blog/simple-ocr-ner-extraction)
- [Part 2: The NuGet Package](https://www.mostlylucid.net/blog/simple-ocr-ner-nuget)
- [Source Code](https://github.com/scottgal/mostlylucidweb/tree/main/Mostlylucid.OcrNer)

## License

MIT
