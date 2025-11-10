# Local Embeddings with Ollama

Ollama makes it easy to run AI models locally without sending data to external APIs. This is perfect for privacy-conscious applications and cost-effective development.

## What are Embeddings?

Embeddings are numerical representations of text that capture semantic meaning. The embedding model converts text into a fixed-size vector (in our case, 768 dimensions with nomic-embed-text).

## Why Local Embeddings?

Running embeddings locally gives you:
- **Zero API costs**: No per-token charges like OpenAI
- **Privacy**: Your content never leaves your machine
- **Speed**: No network latency
- **Offline capability**: Works without internet
- **Unlimited usage**: No rate limits

## The nomic-embed-text Model

We use Nomic AI's embedding model which:
- Produces 768-dimensional vectors
- Supports context length up to 8192 tokens
- Is optimized for text retrieval tasks
- Has competitive performance with commercial models
- Is completely free and open source

## Performance Considerations

Local embeddings are slower than GPU-accelerated cloud services, but for most applications the tradeoff is worth it:
- Embedding generation: ~100-200ms per document on CPU
- Still faster than network round-trips to external APIs
- Can be sped up with GPU support if needed
