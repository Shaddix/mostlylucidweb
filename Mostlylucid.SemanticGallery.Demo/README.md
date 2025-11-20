# Semantic Gallery Demo with Face Recognition

A proof-of-concept application demonstrating AI-powered image gallery with semantic search, automatic captioning, OCR text extraction, and face recognition capabilities.

## Features

- 🔍 **Semantic Search** - Find images by meaning, not just keywords
- 📝 **Automatic Captioning** - AI-generated descriptions using Florence-2
- 📄 **OCR Text Extraction** - Extract and search text within images
- 👤 **Face Detection** - Identify people in photos (extensible)
- 🎨 **Modern UI** - Beautiful, responsive interface with Tailwind CSS & Alpine.js
- 🚀 **Privacy-First** - Everything runs locally, no external APIs

## Architecture

This demo combines multiple AI capabilities:

- **Florence-2** - Multi-task vision model for captioning and OCR
- **all-MiniLM-L6-v2** - Text embeddings for semantic search
- **ONNX Runtime** - Efficient model inference
- **In-Memory Storage** - Simple storage (can be upgraded to Qdrant)

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- 4GB+ RAM recommended
- Optional: GPU with CUDA for faster inference

### Run the Demo

```bash
cd Mostlylucid.SemanticGallery.Demo
dotnet run
```

Then open your browser to `https://localhost:5001`

### First Run

On first run, the application will:
1. Download the Florence-2 model (~271MB) if `SemanticSearch` is enabled
2. Download the embedding model (~90MB) if configured
3. Create an `uploads` directory for images

**Note:** If AI models are not available, the demo will gracefully degrade and still allow image uploads and basic storage/retrieval.

## Configuration

### Enable Semantic Search (Optional)

To enable full semantic search capabilities, ensure you have the semantic search models:

```bash
# From the repository root
cd Mostlylucid.SemanticSearch
chmod +x download-models.sh
./download-models.sh
```

Then configure in `appsettings.json`:

```json
{
  "SemanticSearch": {
    "Enabled": true,
    "EmbeddingModelPath": "../Mostlylucid.SemanticSearch/models/all-MiniLM-L6-v2.onnx",
    "VocabPath": "../Mostlylucid.SemanticSearch/models/vocab.txt",
    "VectorSize": 384
  }
}
```

### Face Recognition (Future Enhancement)

The architecture is designed to support face recognition. To enable:

1. Download FaceNet ONNX model
2. Implement face detection (MTCNN, RetinaFace)
3. Uncomment face recognition code in `SimplifiedImageAnalysisService`

## Usage

### Upload Images

1. Drag & drop images or click the upload area
2. Images are automatically analyzed for:
   - Visual content (caption generation)
   - Text content (OCR)
   - Faces (if enabled)
3. Images are indexed for semantic search

### Search

**Semantic Queries:**
- "sunset over ocean" - finds beach/sunset images
- "nature landscape" - finds outdoor scenery
- "documents" - finds images with text

**Person Search (when face recognition is enabled):**
- "photos of John" - finds all images containing John

### View Details

Click any image to see:
- Full-resolution image
- AI-generated caption
- Extracted text (if any)
- Relevance score
- Upload metadata

## API Endpoints

### Upload Image
```http
POST /api/Gallery/upload
Content-Type: multipart/form-data

Form Data:
  image: <file>
```

### Search
```http
GET /api/Gallery/search?query=sunset&limit=20
```

### Search by Person
```http
GET /api/Gallery/search/person/John?limit=50
```

### Get All Images
```http
GET /api/Gallery/images
```

## Project Structure

```
Mostlylucid.SemanticGallery.Demo/
├── Controllers/
│   └── GalleryController.cs       # API endpoints
├── Services/
│   ├── SimplifiedImageAnalysisService.cs  # Image analysis
│   └── InMemoryGalleryService.cs          # Storage & search
├── Models/
│   └── GalleryModels.cs           # Data models
├── wwwroot/
│   ├── index.html                 # Gallery UI
│   └── uploads/                   # Uploaded images (created at runtime)
├── Program.cs                     # Application startup
└── README.md                      # This file
```

## Upgrading to Production

For production use, consider:

### 1. Replace In-Memory Storage with Qdrant

```csharp
// In Program.cs
services.AddSingleton<QdrantGalleryService>();
```

Qdrant provides:
- Persistent storage
- Scalable vector search
- Metadata filtering
- Multiple collection support

### 2. Add Database for Metadata

Store image metadata in PostgreSQL/SQLite:
- File paths
- Upload dates
- User tags
- Face labels

### 3. Implement Real Face Recognition

- Add FaceNet ONNX model
- Implement MTCNN face detector
- Create person management UI
- Add face labeling workflow

### 4. Background Processing

Use Hangfire or Azure Functions for:
- Asynchronous image processing
- Batch indexing
- Thumbnail generation

### 5. Add Authentication

Protect upload endpoints:
- ASP.NET Identity
- JWT tokens
- Role-based access

## Performance Notes

**Current Performance (CPU-only):**
- Image upload + analysis: ~5-10 seconds
- Semantic search: ~50-200ms
- OCR extraction: ~2-5 seconds

**With GPU:**
- Image analysis: ~0.5-2 seconds
- Significantly faster batch processing

## Troubleshooting

### "Semantic search not available"

Models are missing. Either:
1. Download models using the script in `Mostlylucid.SemanticSearch`
2. Run without semantic search (basic storage still works)

### "Failed to process image"

Check logs for:
- Invalid image format
- File size too large (>50MB)
- Insufficient memory

### No images showing

Check:
- `wwwroot/uploads` directory exists and is writable
- Images uploaded successfully (check API response)
- Browser console for JavaScript errors

## Related Blog Article

This demo accompanies the blog post: **"Building a Semantically Searchable Gallery App with AI-Powered Face Recognition"**

See: `/Mostlylucid/Markdown/building-semantic-gallery-with-face-recognition.md`

## License

Part of the Mostlylucid project.

## Contributing

This is a proof-of-concept demo. For production use, see the main Mostlylucid application.

## Future Enhancements

- [ ] Real face detection and recognition
- [ ] Qdrant vector database integration
- [ ] PostgreSQL metadata storage
- [ ] User authentication
- [ ] Batch upload processing
- [ ] Image editing and filters
- [ ] Album organization
- [ ] Sharing and permissions
- [ ] Mobile app
