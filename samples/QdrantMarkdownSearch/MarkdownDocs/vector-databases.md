# Understanding Vector Databases

A vector database stores high-dimensional vectors (arrays of numbers) and enables fast similarity search. Unlike traditional databases that match exact values, vector databases find items that are *similar* based on mathematical distance.

## How Vectors Work

Each piece of text is converted into a vector using an AI model. Similar texts produce similar vectors. For example:

- "cat" might be: [0.2, 0.8, 0.1, ...]
- "kitten" might be: [0.21, 0.79, 0.11, ...]
- "car" might be: [0.9, 0.1, 0.3, ...]

The distance between "cat" and "kitten" is small (similar), while "cat" and "car" are far apart (different).

## Qdrant's Architecture

Qdrant uses several optimizations:
- **HNSW Index**: Hierarchical Navigable Small World graphs for fast approximate nearest neighbor search
- **Quantization**: Reduces memory usage without significant accuracy loss
- **Filtering**: Combine vector similarity with traditional filters
- **Payloads**: Store metadata alongside vectors

## Use Cases

Vector databases excel at:
- Semantic search engines
- Recommendation systems
- Duplicate detection
- Anomaly detection
- Question answering systems
- Image similarity search
