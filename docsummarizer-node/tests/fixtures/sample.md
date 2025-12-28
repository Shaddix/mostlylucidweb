# Sample Document for Testing

This is a test document used for integration testing of the docsummarizer npm package.

## Introduction

The DocSummarizer tool provides local-first document summarization using BERT embeddings.
It supports multiple modes including pure extractive (Bert) and RAG-enhanced (BertRag).

## Key Features

- **Local ONNX embeddings** - No cloud API required
- **Citation-grounded answers** - Every claim is backed by source text
- **Semantic search** - Find relevant sections using vector similarity
- **Multiple output formats** - JSON, Markdown, Console

## How It Works

1. **Segment** - Splits documents into semantic units
2. **Embed** - Generates 384-dim vectors using BERT
3. **Score** - Computes salience scores
4. **Retrieve** - Finds relevant segments
5. **Synthesize** - Generates coherent answers

## Conclusion

This document demonstrates the basic structure expected by the summarizer.
It includes headings, paragraphs, lists, and formatted text.
