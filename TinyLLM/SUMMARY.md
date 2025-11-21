# TinyLLM - Project Summary

## What Is This?

TinyLLM is a Windows WPF application demonstrating how RAG (Retrieval-Augmented Generation) can dramatically enhance small language models. It supports both Ollama integration and direct GGUF model loading, with instant model switching to compare performance.

## Key Features

- **Dual Model Support**: Works with Ollama (recommended) or standalone GGUF files via LlamaSharp
- **Model Selector**: Dropdown to switch between any installed model on-the-fly
- **RAG Memory**: File-based vector store using simple but effective embeddings
- **GPU Acceleration**: Optional CUDA support for local models
- **Streaming Responses**: Real-time token-by-token generation
- **Model Comparison**: Instantly switch between tiny (2B) and larger (9B+) models
- **RAG Toggle**: Enable/disable RAG to see the difference
- **Modern UI**: Dark-themed WPF interface with conversation history

## The Point

The application demonstrates that tiny LLMs (2B parameters) become surprisingly useful when augmented with RAG. Without RAG, they're forgetful and limited. With RAG, they gain memory and context-awareness.

## Architecture

```
User Input → Chat Service → Model (Ollama/LlamaSharp)
                ↓
           RAG Service → Vector Store (JSON)
                ↓
        Retrieved Context → Injected into Prompt
```

## Tech Stack

- .NET 9.0 WPF
- LlamaSharp 0.17.0 (local GGUF loading)
- Ollama API integration (optional)
- Simple vector embeddings (character/word n-grams)
- JSON file storage for RAG database

## Project Structure

```
TinyLLM/
├── Models/
│   ├── ChatMessage.cs      # Conversation data
│   ├── RagEntry.cs          # Vector store entries
│   ├── ModelInfo.cs         # Model metadata
│   └── AppSettings.cs       # Configuration
├── Services/
│   ├── ModelDownloader.cs   # Downloads GGUF models
│   ├── RagService.cs        # Vector store and retrieval
│   ├── ChatService.cs       # LlamaSharp integration
│   ├── OllamaService.cs     # Ollama API client
│   └── OllamaChatService.cs # Ollama chat handling
├── MainWindow.xaml          # WPF UI
├── MainWindow.xaml.cs       # UI logic
├── App.xaml / App.xaml.cs   # Application entry
├── README.md                # Full documentation
├── QUICKSTART.md            # Getting started guide
├── ARTICLE.md               # Technical blog post
└── build.ps1                # Build script

Generated at runtime:
├── models/                  # Downloaded GGUF files
└── data/
    └── rag_database.json    # Vector store
```

## Quick Start

### Option 1: With Ollama (Recommended)

```bash
# Install Ollama first, then pull a model
ollama pull gemma2:2b

# Build and run
cd TinyLLM
dotnet run
```

The app auto-detects Ollama models and lists them in the dropdown.

### Option 2: Standalone

```bash
# Just run - it will download Gemma 2 2B automatically
cd TinyLLM
dotnet run
```

First launch downloads ~1.7GB model with progress bar.

## Usage

1. **Select Model**: Choose from dropdown (Ollama or local)
2. **Enable/Disable RAG**: Toggle to see the difference
3. **Chat**: Type messages, watch streaming responses
4. **Switch Models**: Compare tiny vs large models instantly
5. **View RAG Stats**: Counter shows stored conversations

## RAG Demonstration

**Without RAG (short-term memory only):**
```
User: My name is Scott
Bot: Nice to meet you!

[Later]
User: What's my name?
Bot: I don't know your name.
```

**With RAG (persistent memory):**
```
User: My name is Scott
Bot: Nice to meet you!

[Later]
User: What's my name?
Bot: Your name is Scott.
```

## Model Comparison

The app makes it trivial to compare models:

- **gemma2:2b**: Fast (~0.5s), limited depth
- **gemma2:9b**: Slower (~2s), much better quality
- **qwen2.5:7b**: Good balance of speed and quality

Switch models mid-conversation. RAG context persists across switches.

## Performance

**Gemma 2 2B on RTX 3070:**
- CPU: ~12 tokens/sec, 1.5s first token
- GPU: ~45 tokens/sec, 0.4s first token

**Via Ollama (optimised):**
- GPU: ~50 tokens/sec, 0.3s first token

## Files Generated

- `models/gemma-2-2b-it-Q4_K_M.gguf` - ~1.7GB (if using local mode)
- `data/rag_database.json` - Vector store, grows with conversations

## Building

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run

# Or use PowerShell script
.\build.ps1
```

## Key Code Files

- `Services/RagService.cs:73` - Embedding generation
- `Services/RagService.cs:92` - Cosine similarity search
- `Services/OllamaChatService.cs:69` - Streaming chat with RAG
- `Services/ChatService.cs:76` - LlamaSharp integration
- `MainWindow.xaml.cs:118` - Model loading logic
- `MainWindow.xaml.cs:283` - Message sending with RAG

## Requirements

- Windows 10/11
- .NET 9.0 SDK
- ~2GB disk space (for model)
- Optional: NVIDIA GPU + CUDA 12
- Optional: Ollama runtime

## Licence

MIT - Do what you want with it

## What's Next?

Potential enhancements:
1. Better embeddings (sentence-transformers)
2. Document ingestion (PDFs, etc.)
3. Proper vector database (Qdrant, Milvus)
4. Conversation export/import
5. Side-by-side model comparison
6. Voice input/output
7. Multi-model ensemble

## Documentation

- **README.md**: Full technical documentation
- **QUICKSTART.md**: Getting started guide
- **ARTICLE.md**: Technical blog post with code walkthrough
- **This file**: Quick summary for developers

## The Bottom Line

This is a demonstration of RAG + tiny LLMs. It shows that a 2B parameter model with RAG can be more useful than a 7B model without it. The code is simple, the UI is clean, and it runs entirely locally.

Perfect for:
- Learning about RAG systems
- Comparing LLM performance
- Local AI experimentation
- Privacy-focused AI applications
- Understanding the RAG architecture

Not suitable for:
- Production deployments (embeddings too simple)
- Large-scale applications (JSON file store)
- Mission-critical systems (error handling is basic)

It's an educational project that happens to be quite useful. Enjoy!
