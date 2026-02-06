using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Mostlylucid.OcrNer.Config;
using Mostlylucid.OcrNer.Models;
using System.Text.Json;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// BERT-based Named Entity Recognition service using ONNX Runtime.
///
/// How it works:
/// 1. Text is tokenized into WordPiece tokens with character offsets
/// 2. Tokens are fed through the BERT NER ONNX model
/// 3. The model outputs logits for 9 BIO labels per token
/// 4. Softmax + argmax gives the predicted label per token
/// 5. BIO tags are merged: B-PER starts a new person, I-PER continues it
/// 6. WordPiece sub-tokens are merged back into full words
/// 7. Entities below the confidence threshold are filtered out
///
/// The model and vocab are auto-downloaded on first use.
/// </summary>
public class NerService : INerService, IDisposable
{
    private readonly ILogger<NerService> _logger;
    private readonly OcrNerConfig _config;
    private readonly ModelDownloader _downloader;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private InferenceSession? _session;
    private BertNerTokenizer? _tokenizer;
    private string[]? _id2Label;
    private bool _initialized;

    /// <summary>
    /// The 9 BIO labels used by the bert-base-NER model.
    /// O = Outside (not an entity), B- = Beginning of entity, I- = Inside/continuation.
    /// </summary>
    private static readonly string[] DefaultLabels =
        ["O", "B-MISC", "I-MISC", "B-PER", "I-PER", "B-ORG", "I-ORG", "B-LOC", "I-LOC"];

    public NerService(
        ILogger<NerService> logger,
        IOptions<OcrNerConfig> config,
        ModelDownloader downloader)
    {
        _logger = logger;
        _config = config.Value;
        _downloader = downloader;
    }

    /// <inheritdoc />
    public async Task<NerResult> ExtractEntitiesAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new NerResult { SourceText = text };

        await EnsureInitializedAsync(ct);

        var tokenized = _tokenizer!.Tokenize(text);
        var logits = RunInference(tokenized);
        var entities = DecodeEntities(text, tokenized, logits);

        return new NerResult
        {
            SourceText = text,
            Entities = entities
        };
    }

    /// <summary>
    /// Lazy initialization: download model + create ONNX session on first use
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var paths = await _downloader.EnsureNerModelAsync(ct);

            // Load label mapping from config.json
            _id2Label = LoadLabels(paths.ConfigPath);

            // Create tokenizer
            _tokenizer = new BertNerTokenizer(paths.VocabPath, _config.MaxSequenceLength, _logger as ILogger<BertNerTokenizer>);

            // Create ONNX inference session
            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            _session = new InferenceSession(paths.ModelPath, sessionOptions);

            _initialized = true;
            _logger.LogInformation("NER model loaded successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Load the id2label mapping from the model's config.json.
    /// Falls back to default BERT NER labels if parsing fails.
    /// </summary>
    private string[] LoadLabels(string configPath)
    {
        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("id2label", out var id2LabelProp))
            {
                var labels = new string[id2LabelProp.EnumerateObject().Count()];
                foreach (var prop in id2LabelProp.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out var idx) && idx < labels.Length)
                        labels[idx] = prop.Value.GetString() ?? "O";
                }
                _logger.LogDebug("Loaded {Count} labels from config.json", labels.Length);
                return labels;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse config.json, using default labels");
        }

        return DefaultLabels;
    }

    /// <summary>
    /// Run ONNX inference on tokenized input.
    /// Returns raw logits: float[seqLen, numLabels]
    /// </summary>
    private float[,] RunInference(TokenizedInput tokenized)
    {
        var seqLen = _config.MaxSequenceLength;
        var numLabels = _id2Label!.Length;

        // Create input tensors with shape [1, seqLen]
        var inputIdsTensor = new DenseTensor<long>(tokenized.InputIds, [1, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>(tokenized.AttentionMask, [1, seqLen]);
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenized.TokenTypeIds, [1, seqLen]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var results = _session!.Run(inputs);

        // Output is "logits" with shape [1, seqLen, numLabels]
        var outputTensor = results.First().AsTensor<float>();
        var logits = new float[seqLen, numLabels];

        for (var i = 0; i < seqLen; i++)
            for (var j = 0; j < numLabels; j++)
                logits[i, j] = outputTensor[0, i, j];

        return logits;
    }

    /// <summary>
    /// Decode BIO tags from logits into merged NerEntity objects.
    ///
    /// BIO decoding rules:
    /// - B-XXX: Start of a new entity of type XXX
    /// - I-XXX: Continuation of the current entity (must match type)
    /// - O: Not an entity
    ///
    /// WordPiece merging: sub-tokens like ["John", "##son"] → "Johnson"
    /// Confidence: minimum softmax probability across all tokens in the entity
    /// </summary>
    private List<NerEntity> DecodeEntities(string text, TokenizedInput tokenized, float[,] logits)
    {
        var entities = new List<NerEntity>();
        var numLabels = _id2Label!.Length;

        // Compute softmax and argmax per token
        var predictions = new (int LabelIdx, float Confidence)[tokenized.TokenCount];
        for (var i = 0; i < tokenized.TokenCount; i++)
        {
            // Softmax
            var maxLogit = float.MinValue;
            for (var j = 0; j < numLabels; j++)
                maxLogit = Math.Max(maxLogit, logits[i, j]);

            var sumExp = 0f;
            var probs = new float[numLabels];
            for (var j = 0; j < numLabels; j++)
            {
                probs[j] = MathF.Exp(logits[i, j] - maxLogit);
                sumExp += probs[j];
            }
            for (var j = 0; j < numLabels; j++)
                probs[j] /= sumExp;

            // Argmax
            var bestIdx = 0;
            for (var j = 1; j < numLabels; j++)
                if (probs[j] > probs[bestIdx])
                    bestIdx = j;

            predictions[i] = (bestIdx, probs[bestIdx]);
        }

        // Merge BIO spans
        string? currentType = null;
        int currentStart = -1, currentEnd = -1;
        var currentConfidence = 1f;

        // Skip [CLS] (index 0), process up to [SEP]
        var prevOffset = (-1, -1);
        for (var i = 1; i < tokenized.TokenCount - 1; i++)
        {
            var (labelIdx, confidence) = predictions[i];
            var label = _id2Label[labelIdx];
            var (charStart, charEnd) = tokenized.Offsets[i];

            if (charStart < 0) continue; // Skip special tokens

            // WordPiece sub-tokens (same offset as previous) should continue
            // the current entity, not start a new one. Only the first sub-token
            // of a word determines the entity label.
            var currentOffset = (charStart, charEnd);
            if (currentOffset == prevOffset)
            {
                // Sub-token: just extend the current entity span if active
                if (currentType != null)
                    currentEnd = charEnd;
                continue;
            }
            prevOffset = currentOffset;

            if (label.StartsWith("B-"))
            {
                // Flush previous entity
                if (currentType != null)
                    FlushEntity(entities, text, currentType, currentStart, currentEnd, currentConfidence);

                currentType = label[2..]; // "B-PER" → "PER"
                currentStart = charStart;
                currentEnd = charEnd;
                currentConfidence = confidence;
            }
            else if (label.StartsWith("I-") && currentType != null && label[2..] == currentType)
            {
                // Continue current entity
                currentEnd = charEnd;
                currentConfidence = Math.Min(currentConfidence, confidence);
            }
            else if (label.StartsWith("I-") && (currentType == null || label[2..] != currentType))
            {
                // Orphaned I-tag (no matching B-tag) — treat as B-tag.
                // This is a standard NER post-processing heuristic.
                if (currentType != null)
                    FlushEntity(entities, text, currentType, currentStart, currentEnd, currentConfidence);

                currentType = label[2..];
                currentStart = charStart;
                currentEnd = charEnd;
                currentConfidence = confidence;
            }
            else
            {
                // O tag - flush
                if (currentType != null)
                    FlushEntity(entities, text, currentType, currentStart, currentEnd, currentConfidence);
                currentType = null;
            }
        }

        // Flush final entity
        if (currentType != null)
            FlushEntity(entities, text, currentType, currentStart, currentEnd, currentConfidence);

        return entities;
    }

    /// <summary>
    /// Create a NerEntity from a merged span, applying confidence filter
    /// </summary>
    private void FlushEntity(
        List<NerEntity> entities, string text,
        string type, int start, int end, float confidence)
    {
        if (confidence < _config.MinConfidence) return;

        var entityText = text[start..end].Trim();
        if (string.IsNullOrWhiteSpace(entityText)) return;

        entities.Add(new NerEntity
        {
            Text = entityText,
            Label = type,
            Confidence = confidence,
            StartOffset = start,
            EndOffset = end
        });
    }

    public void Dispose()
    {
        _session?.Dispose();
        _initLock.Dispose();
    }
}
