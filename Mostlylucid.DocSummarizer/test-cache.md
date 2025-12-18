# Test Document for Caching

This is a test document to verify the Learning Summarizer caching functionality.

## Introduction

The caching system works by storing segment embeddings in a vector store. When the same document is processed again, it can skip the embedding phase if the content hasn't changed.

## How Caching Works

The cache uses content hashing to identify documents. If the content hash matches, segments are loaded from the store instead of being re-embedded. This saves significant time on subsequent runs.

### Key Features

1. **Content-based identification**: Documents are identified by their content hash
2. **Segment-level caching**: Individual segments can be reused
3. **Evidence-based synthesis caching**: If the same segments are retrieved, the synthesis is cached

## Technical Details

The implementation uses xxHash64 for fast cache key generation. Segments are stored in Qdrant with their embeddings and salience scores.

### Architecture

The pipeline consists of three phases:
- Extract: Parse document into segments with embeddings
- Retrieve: Find relevant segments using vector search
- Synthesize: Generate summary from retrieved segments

## Conclusion

This caching system significantly improves performance for repeated summarization tasks. The first run embeds all segments, while subsequent runs can reuse existing embeddings.
