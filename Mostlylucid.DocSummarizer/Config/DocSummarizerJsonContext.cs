using System.Text.Json;
using System.Text.Json.Serialization;
using Mostlylucid.DocSummarizer.Models;
using OllamaSharp.Models;

namespace Mostlylucid.DocSummarizer.Config;

/// <summary>
/// JSON serialization context for AOT compilation
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DocSummarizerConfig))]
[JsonSerializable(typeof(OllamaConfig))]
[JsonSerializable(typeof(DoclingConfig))]
[JsonSerializable(typeof(QdrantConfig))]
[JsonSerializable(typeof(ProcessingConfig))]
[JsonSerializable(typeof(OutputConfig))]
[JsonSerializable(typeof(BatchConfig))]
[JsonSerializable(typeof(OutputFormat))]
[JsonSerializable(typeof(DocumentSummary))]
[JsonSerializable(typeof(DocumentChunk))]
[JsonSerializable(typeof(ChunkSummary))]
[JsonSerializable(typeof(TopicSummary))]
[JsonSerializable(typeof(SummarizationTrace))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(SummarizationMode))]
[JsonSerializable(typeof(BatchResult))]
[JsonSerializable(typeof(BatchSummary))]
[JsonSerializable(typeof(Services.DoclingResponse))]
[JsonSerializable(typeof(Services.DoclingDocument))]
[JsonSerializable(typeof(Services.DoclingTaskResponse))]
[JsonSerializable(typeof(Services.DoclingStatusResponse))]
[JsonSerializable(typeof(Services.DoclingResultResponse))]
[JsonSerializable(typeof(Services.ModelInfo), TypeInfoPropertyName = "DocSummarizerModelInfo")]
// OllamaSharp types for AOT support
[JsonSerializable(typeof(GenerateRequest))]
[JsonSerializable(typeof(GenerateResponseStream))]
[JsonSerializable(typeof(GenerateDoneResponseStream))]
[JsonSerializable(typeof(EmbedRequest))]
[JsonSerializable(typeof(EmbedResponse))]
[JsonSerializable(typeof(ShowModelResponse))]
[JsonSerializable(typeof(ListModelsResponse))]
[JsonSerializable(typeof(Model))]
[JsonSerializable(typeof(RequestOptions))]
[JsonSerializable(typeof(List<Model>))]
[JsonSerializable(typeof(List<double>))]
[JsonSerializable(typeof(List<List<double>>))]
public partial class DocSummarizerJsonContext : JsonSerializerContext
{
}
