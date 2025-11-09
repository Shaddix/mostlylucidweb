using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Mostlylucid.BlogLLM.Models;

namespace Mostlylucid.BlogLLM.Services;

public class EmbeddingService : IDisposable
{
    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;
    private readonly int _embeddingDimension;
    private readonly bool _useGpu;

    public EmbeddingService(string modelPath, string tokenizerPath, int dimensions = 384, bool useGpu = false)
    {
        _embeddingDimension = dimensions;
        _useGpu = useGpu;

        var options = new SessionOptions();
        if (useGpu)
        {
            options.AppendExecutionProvider_CUDA(0);
        }

        _session = new InferenceSession(modelPath, options);
        _tokenizer = Tokenizer.CreateTokenizer(tokenizerPath);
    }

    public float[] GenerateEmbedding(string text)
    {
        // Tokenize
        var encoding = _tokenizer.Encode(text);
        var inputIds = encoding.Ids.Select(id => (long)id).ToArray();
        var attentionMask = Enumerable.Repeat(1L, inputIds.Length).ToArray();

        // Pad/truncate to 512
        const int maxLength = 512;
        var paddedInputIds = new long[maxLength];
        var paddedAttentionMask = new long[maxLength];

        int length = Math.Min(inputIds.Length, maxLength);
        Array.Copy(inputIds, paddedInputIds, length);
        Array.Fill(paddedAttentionMask, 1L, 0, length);

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(paddedInputIds, new[] { 1, maxLength });
        var attentionMaskTensor = new DenseTensor<long>(paddedAttentionMask, new[] { 1, maxLength });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        // Run inference
        using var results = _session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();

        // Mean pooling
        return MeanPooling(outputTensor, paddedAttentionMask);
    }

    public async Task<List<ContentChunk>> GenerateEmbeddingsAsync(
        List<ContentChunk> chunks,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            chunks[i].Embedding = await Task.Run(() => GenerateEmbedding(chunks[i].Text), cancellationToken);
            progress?.Report((i + 1, chunks.Count));
        }

        return chunks;
    }

    private float[] MeanPooling(Tensor<float> outputTensor, long[] attentionMask)
    {
        int seqLength = outputTensor.Dimensions[1];
        int embeddingDim = outputTensor.Dimensions[2];

        var embedding = new float[embeddingDim];
        int tokenCount = 0;

        for (int seq = 0; seq < seqLength; seq++)
        {
            if (attentionMask[seq] == 0) continue;

            tokenCount++;
            for (int dim = 0; dim < embeddingDim; dim++)
            {
                embedding[dim] += outputTensor[0, seq, dim];
            }
        }

        // Average and normalize
        for (int dim = 0; dim < embeddingDim; dim++)
        {
            embedding[dim] /= tokenCount;
        }

        return Normalize(embedding);
    }

    private float[] Normalize(float[] vector)
    {
        float magnitude = 0;
        foreach (var val in vector)
        {
            magnitude += val * val;
        }
        magnitude = MathF.Sqrt(magnitude);

        var normalized = new float[vector.Length];
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        return normalized;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
