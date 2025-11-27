# TinyLLM - Local AI Chat with RAG Memory

A Windows WPF application that runs tiny LLMs locally with RAG-based conversation memory. Supports both Ollama integration and direct GGUF model loading. Perfect for privacy-focused, offline AI assistance with contextual memory.

## Features

- **🎯 Dual Model Support**:
  - **Ollama Integration**: Automatically detects and uses Ollama models (recommended)
  - **Direct GGUF Loading**: Fallback to LlamaSharp for standalone operation
- **🤖 Smart Model Selection**: Dropdown to switch between any installed model
- **📦 Default Model**: Uses Gemma 2 2B (~1.7GB) - more capable than TinyLlama
- **💾 RAG Memory**: File-based vector store remembers past conversations and provides relevant context
- **⚡ GPU Acceleration**: Optional CUDA GPU support for local models (toggle CPU/GPU)
- **📥 Auto-Download**: Downloads Gemma 2 2B if no models available
- **💬 Streaming Responses**: Real-time token-by-token response generation
- **🔄 Model Refresh**: Query available Ollama models with one click
- **🎨 Modern Dark UI**: Clean, modern WPF interface with chat bubbles
- **📦 Flexible Deployment**: Works with or without Ollama

## How It Works

### The "Tiny" Philosophy

Despite using a 1.1B parameter model (tiny by modern standards), TinyLLM achieves useful performance through:

1. **RAG-Enhanced Context**: The RAG system stores all conversations as embeddings in a local JSON file
2. **Smart Retrieval**: When you ask a question, it searches past conversations for relevant context
3. **Context Injection**: Retrieved memories are injected into the prompt, giving the tiny model more information
4. **Conversation History**: Recent messages are always included for continuity

This means the model "remembers" and can reference past conversations, making it much more useful than a plain tiny LLM.

## Requirements

- Windows 10/11
- .NET 9.0 SDK
- ~2GB free disk space (for model)
- **Optional**: NVIDIA GPU with CUDA 12 support for GPU acceleration

## Building from Source

```bash
cd TinyLLM
dotnet restore
dotnet build
dotnet run
```

## Usage

### First Launch

1. Launch the application
2. Wait for the model to download (progress bar will show status)
3. Model will automatically load into memory
4. Start chatting!

### Chat Interface

- **Type your message** in the text box at the bottom
- **Press Enter** to send (Shift+Enter for new line)
- **Responses stream** in real-time, token by token
- **RAG context** is automatically used for relevant past conversations

### Settings

- **Use GPU**: Toggle between CPU and GPU inference
  - Requires NVIDIA GPU with CUDA 12
  - Will reload model when changed
  - GPU is significantly faster if available

- **Use RAG**: Enable/disable RAG context injection
  - When enabled, searches past conversations for relevant context
  - Recommended to keep enabled for better responses

- **Clear RAG**: Delete all stored conversation history
  - Useful for starting fresh
  - Cannot be undone

### RAG Counter

The "RAG Entries" counter shows how many conversation exchanges are stored in the vector database.

## Technical Details

### Model

- **Model**: TinyLlama 1.1B Chat v1.0
- **Quantization**: Q4_K_M (4-bit quantization)
- **Size**: ~700 MB
- **Context**: 2048 tokens
- **Source**: [TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF](https://huggingface.co/TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF)

### RAG Implementation

- **Embeddings**: Simple character and word n-gram based embeddings (128 dimensions)
- **Storage**: JSON file (`data/rag_database.json`)
- **Similarity**: Cosine similarity for retrieval
- **Top-K**: Retrieves top 3 most relevant past conversations

### Architecture

```
TinyLLM/
├── Models/
│   ├── ChatMessage.cs      # Chat message data model
│   ├── RagEntry.cs          # RAG database entry model
│   └── AppSettings.cs       # Application settings
├── Services/
│   ├── ModelDownloader.cs   # Downloads LLM from HuggingFace
│   ├── RagService.cs        # Vector store and retrieval
│   └── ChatService.cs       # LlamaSharp integration
├── MainWindow.xaml          # WPF UI
├── MainWindow.xaml.cs       # UI logic
└── TinyLLM.csproj          # Project file
```

## Performance

### CPU Mode (Intel i7 / AMD Ryzen 5+)
- First token: ~1-2 seconds
- Generation speed: ~5-15 tokens/second
- Memory usage: ~1.5GB RAM

### GPU Mode (NVIDIA RTX 3060+)
- First token: ~0.5 seconds
- Generation speed: ~30-60 tokens/second
- Memory usage: ~1.5GB VRAM

## Customization

### Using Different Models

Edit `ModelDownloader.cs` to change the model URL:

```csharp
const string modelUrl = "YOUR_MODEL_URL";
const string modelName = "your-model.gguf";
```

Recommended tiny models:
- **TinyLlama 1.1B** - Default, best balance
- **Phi-2 2.7B** - Better quality, slower
- **Gemma 2B** - Google's tiny model

### Adjusting Settings

Edit `AppSettings.cs` defaults:

```csharp
public int ContextSize { get; set; } = 2048;      // Larger = more context
public float Temperature { get; set; } = 0.7f;     // Higher = more creative
public int MaxTokens { get; set; } = 512;          // Max response length
public int TopRagResults { get; set; } = 3;        // Number of RAG results
```

### Improving RAG Embeddings

The current implementation uses simple n-gram embeddings. For better quality, consider:

1. **Use a proper embedding model** (e.g., sentence-transformers)
2. **Integrate with LlamaSharp's built-in embeddings**
3. **Use a vector database** like Qdrant or Milvus

## Troubleshooting

### Model Won't Download
- Check internet connection
- Verify HuggingFace is accessible
- Try manually downloading the model to `models/` folder

### GPU Not Working
- Ensure NVIDIA drivers are up to date
- Verify CUDA 12 is installed
- Check GPU has sufficient VRAM (~2GB minimum)
- Application will automatically fall back to CPU if GPU fails

### Out of Memory
- Reduce `ContextSize` in AppSettings
- Close other memory-intensive applications
- Use CPU mode instead of GPU

### Slow Performance
- Enable GPU mode if available
- Reduce `MaxTokens` for faster responses
- Consider a more powerful model if hardware allows

## License

MIT License - Feel free to use, modify, and distribute.

## Credits

- **LLamaSharp**: .NET bindings for llama.cpp
- **TinyLlama**: Compact but capable language model
- **TheBloke**: Quantized GGUF models on HuggingFace

## Future Enhancements

- [ ] Better embedding models for RAG
- [ ] Document ingestion (PDF, TXT, etc.)
- [ ] Export/import chat history
- [ ] Multiple model support
- [ ] System prompts customization
- [ ] Voice input/output
- [ ] Multi-turn conversation branching
