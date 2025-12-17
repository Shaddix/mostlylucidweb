# Document Summary: CHANGELOG.md

*Generated: 2025-12-17 18:10:21*

## Executive Summary

The DocSummarizer has undergone significant updates with the release of version v2.1.0 and v2.0.0. The latest version, v2.1.0, introduces support for a wide range of input formats, including plain text, Markdown, PDF, DOCX, XLSX, PPTX, HTML, XHTML, CSV, AsciiDoc, PNG, JPEG, TIFF, BMP, and WebVTT (.vtt) [chunk-1]. This expansion in file format compatibility enables users to easily integrate the tool into their workflows.

The v2.0.0 release brought about a major feature update, introducing a JSON-based configuration system that allows for automatic discovery of configuration files from multiple locations [chunk-2]. Additionally, the tool now includes a command-line option for checking dependencies and model information, providing users with comprehensive details on available models and their documentation [chunk-3]. These updates enhance the usability and flexibility of the DocSummarizer, making it an even more valuable resource for professionals and researchers.

## Topic Summaries

### Changelog - DocSummarizer

*Sources: chunk-0*

Here are 3 bullet points summarizing the changelog:

* A new "tool" command has been added for AI agent integration, MCP servers, and automated pipelines, which includes features such as evidence-grounded claims, chunk IDs, structured topics, named entities, and processing metadata [chunk-0].
* The WebFetcher service has been introduced with comprehensive security controls, including SSRF protection, DNS rebinding protection, request limits, content-type gating, decompression bomb protection, HTML sanitization, image guardrails, and protocol downgrade prevention [chunk-0].
* A new QualityAnalyzer service has been added to assess summary quality, which includes metrics such as citation density, coherence flow, entity consistency, factuality detection, evidence density, and quality grades (A-F) based on a set of heuristics [chunk-0].

### v2.1.0 - Summary Templates & Expanded Format Support (2025-12-17)

*Sources: chunk-1*

Here are 3 bullet points summarizing the new features in Section v2.1.0:

• The updated version supports a wide range of input formats, including plain text (.txt), Markdown (.md), PDF, DOCX, XLSX, PPTX, HTML, XHTML, CSV, AsciiDoc, PNG, JPEG, TIFF, BMP, WEBP (with OCR), and WebVTT (.vtt) for captions [chunk-1].
• A new Smart Plain Text Chunking feature has been added, which automatically detects plain text vs markdown, splits plain text by paragraphs, extracts titles from short first paragraphs, and uses the first sentence as a chunk heading for better traceability [chunk-1].
• The Summary Templates System now includes 9 built-in templates with customizable properties, such as target word count, output style, tone, audience level, and section visibility, allowing users to create tailored summaries [chunk-1].

### v2.0.0 - Major Feature Release (2025-12-16)

*Sources: chunk-2*

Here are 4 bullet points summarizing the new features:

* The tool now includes a JSON-based configuration system, allowing for automatic discovery from multiple locations (custom path via `--config` option, `docsummarizer.json`, `.docsummarizer.json`, and `~/.docsummarizer.json`) and comprehensive configuration sections for all aspects of the tool [chunk-2].
* Batch processing has been improved with features such as file extension filtering, recursive directory scanning, glob pattern support, continue on error or fail-fast modes, and comprehensive batch summary reports [chunk-2].
* The tool now supports multiple output formats, including console, text, markdown, JSON, and configurable output directory, with automatic file naming based on source documents [chunk-2].
* AOT compilation support has been added, enabling native AOT compilation, full trimming, JSON source generation for AOT compatibility, faster startup times, smaller memory footprint, and platform-specific optimized builds [chunk-2].

### Check dependencies with model info

*Sources: chunk-3*

Here are 4 bullet points summarizing the section:

* The `dotnet run --check --verbose` command is used to check dependencies and model information, including available models such as "ministral-3:3b" and "nomic-embed-text", with links to their respective documentation [chunk-3].
* The default model info for "ministral-3:3b" includes parameters such as 3B, Q4_0 quantization, and a context window of 128,000 tokens [chunk-3].
* AOT (Ahead-of-Time) publishing is provided for Windows, Linux, and macOS platforms, with different architectures (x64, x64, and ARM64) [chunk-3].
* Performance improvements include faster startup times (<100ms), reduced binary size (~40MB from ~150MB), and lower memory usage (~50MB from ~150MB) compared to the previous version [chunk-3].

## Processing Trace

| Metric | Value |
|--------|-------|
| Document | CHANGELOG.md |
| Chunks | 4 total, 4 processed |
| Topics | 4 |
| Time | 50.9s |
| Coverage | 100 % |
| Citation rate | 0.00 |
