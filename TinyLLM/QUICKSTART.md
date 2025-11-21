# TinyLLM Quick Start Guide

## Prerequisites

1. **Windows 10/11** (WPF application)
2. **.NET 9.0 SDK** - Download from: https://dotnet.microsoft.com/download/dotnet/9.0
3. **~2GB free disk space** for the model
4. **Optional**: NVIDIA GPU with CUDA 12 for GPU acceleration

## Installation & First Run

### Option 1: PowerShell Script (Recommended)

```powershell
cd TinyLLM
.\build.ps1
```

This will:
- Check for .NET SDK
- Detect GPU availability
- Restore packages
- Build the application
- Optionally launch it

### Option 2: Manual Build

```bash
cd TinyLLM
dotnet restore
dotnet build
dotnet run
```

### Option 3: Visual Studio

1. Open `TinyLLM.csproj` in Visual Studio 2022+
2. Press F5 to build and run

## First Launch

When you first launch TinyLLM:

1. **Model Download**: The app will automatically download TinyLlama 1.1B (~700MB)
   - Progress bar shows download status
   - This only happens once - model is cached locally

2. **Model Loading**: After download, the model loads into memory (~1-2GB RAM)
   - Status bar shows "Model loaded (CPU)" or "Model loaded (GPU)"

3. **Ready to Chat**: Once loaded, you can start chatting immediately!

## Using TinyLLM

### Switching Models

**Want to compare tiny vs larger models?** It's dead simple:

1. Click the **Model** dropdown in the header
2. Select any model (Ollama or local GGUF)
3. Wait a moment for it to load
4. Start chatting!

**Recommended comparisons:**
- **gemma2:2b** (tiny, fast) vs **gemma2:9b** (larger, smarter)
- **qwen2.5:1.5b** (smallest) vs **qwen2.5:7b** (much better)
- **phi3:3.8b** (Microsoft's tiny model) vs **llama3.1:8b** (Meta's baseline)

The model loads instantly if using Ollama, or downloads first if it's a GGUF file.

### Basic Chat

1. Type your message in the text box at the bottom
2. Press **Enter** to send (or click "Send" button)
3. Use **Shift+Enter** for multi-line messages
4. Responses stream in real-time

### Example Conversations

Try these to see the RAG memory in action:

**First message:**
```
My name is John and I love Python programming
```

**Later (even after restart):**
```
What's my name and what do I like?
```

The AI will remember because it's stored in the RAG database!

### Settings Explained

#### Use GPU
- **When enabled**: Uses NVIDIA GPU with CUDA for much faster inference
- **When disabled**: Uses CPU (slower but works on any PC)
- **Note**: Changing this setting reloads the model

**Recommended**: Enable if you have an NVIDIA GPU

#### Use RAG
- **When enabled**: Searches past conversations for relevant context
- **When disabled**: AI has no memory, treats each message independently

**Recommended**: Keep enabled for useful conversations

#### RAG Entries Counter
- Shows how many conversation pieces are stored
- Each user+assistant exchange = 1 entry
- More entries = better memory

#### Clear RAG
- Deletes all stored conversations
- Useful for starting fresh or privacy
- **Warning**: Cannot be undone!

## Performance Tips

### For Best Speed:
1. ✅ Enable GPU if available
2. ✅ Keep context size at 2048 or lower
3. ✅ Close memory-intensive apps

### For Best Quality:
1. ✅ Keep RAG enabled
2. ✅ Have meaningful conversations (builds better context)
3. ✅ Use specific questions (helps RAG find relevant context)

## Troubleshooting

### "Model failed to load"
- **Cause**: Download interrupted or corrupted
- **Fix**: Delete `models/` folder and restart app

### GPU doesn't work
- **Cause**: Missing CUDA drivers or incompatible GPU
- **Fix**: Update NVIDIA drivers or use CPU mode

### Slow on CPU
- **Normal**: CPU inference is ~5-15 tokens/sec
- **GPU is much faster**: ~30-60 tokens/sec
- **Consider**: Upgrade to GPU or use a lighter model

### Out of memory
- **Fix 1**: Close other applications
- **Fix 2**: Use CPU mode (uses RAM instead of VRAM)
- **Fix 3**: Reduce context size in code

## File Locations

After first run, these folders are created:

```
TinyLLM/
├── models/               # Downloaded LLM models (~700MB)
│   └── tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf
├── data/                 # RAG database
│   └── rag_database.json
└── bin/                  # Compiled application
    └── Release/
        └── net9.0-windows/
            └── TinyLLM.exe
```

## Privacy & Security

- ✅ **100% Local**: No data leaves your machine
- ✅ **No Internet**: Only downloads model on first run, then fully offline
- ✅ **Your Data**: RAG database is a simple JSON file you own
- ✅ **No Telemetry**: No tracking, analytics, or phone-home

## Next Steps

### Customize the Model

Want a different model? Edit `Services/ModelDownloader.cs`:

```csharp
// Change these lines:
const string modelUrl = "YOUR_MODEL_URL";
const string modelName = "your-model.gguf";
```

### Improve RAG Quality

Current RAG uses simple embeddings. For production:
- Consider proper embedding models (sentence-transformers)
- Use a vector database (Qdrant, Milvus)
- Implement chunking for longer conversations

### Add Features

Ideas for enhancement:
- Document ingestion (PDF, DOCX)
- Export chat history
- Multiple personalities/system prompts
- Voice input/output
- Web search integration

## Support & Issues

- **Documentation**: See full README.md
- **Source Code**: All code is commented and readable
- **Issues**: Check project repository

## Have Fun!

TinyLLM is designed to be:
- 🎓 **Educational**: Learn about LLMs and RAG
- 🔒 **Private**: Your data stays local
- ⚡ **Fast**: Optimized for quick responses
- 🎨 **Customizable**: Modify to your needs

Enjoy your local AI assistant! 🤖
