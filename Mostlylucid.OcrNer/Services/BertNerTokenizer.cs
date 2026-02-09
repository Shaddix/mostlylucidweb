using Microsoft.Extensions.Logging;

namespace Mostlylucid.OcrNer.Services;

/// <summary>
/// WordPiece tokenizer for BERT NER models.
/// Tracks character offsets so NER predictions can be mapped back to the original text.
///
/// How it works:
/// 1. Text is split into words by whitespace
/// 2. Each word is broken into WordPiece sub-tokens using the vocab
/// 3. Special tokens [CLS] and [SEP] are added at the start and end
/// 4. Each token records which characters in the original text it came from
/// </summary>
public class BertNerTokenizer
{
    private readonly ILogger<BertNerTokenizer>? _logger;
    private readonly Dictionary<string, int> _vocab;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _unkId;
    private readonly int _padId;
    private readonly int _maxLength;

    /// <summary>
    /// Create a new tokenizer from a vocab.txt file.
    /// Each line in vocab.txt is one token, line number = token ID.
    /// </summary>
    /// <param name="vocabPath">Path to vocab.txt from the BERT model</param>
    /// <param name="maxLength">Maximum sequence length (default 512)</param>
    /// <param name="logger">Optional logger</param>
    public BertNerTokenizer(string vocabPath, int maxLength = 512, ILogger<BertNerTokenizer>? logger = null)
    {
        _logger = logger;
        _maxLength = maxLength;

        // Load vocab: each line is a token, line number = token ID
        var lines = File.ReadAllLines(vocabPath);
        _vocab = new Dictionary<string, int>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
            _vocab[lines[i]] = i;

        _clsId = _vocab.GetValueOrDefault("[CLS]", 101);
        _sepId = _vocab.GetValueOrDefault("[SEP]", 102);
        _unkId = _vocab.GetValueOrDefault("[UNK]", 100);
        _padId = _vocab.GetValueOrDefault("[PAD]", 0);

        _logger?.LogDebug("Loaded vocab with {Count} tokens", _vocab.Count);
    }

    /// <summary>
    /// Tokenize text into BERT input format with character offset tracking.
    ///
    /// Returns arrays ready for ONNX inference:
    /// - InputIds: token IDs including [CLS] and [SEP]
    /// - AttentionMask: 1 for real tokens, 0 for padding
    /// - TokenTypeIds: all zeros (single-sentence NER)
    /// - Offsets: (startChar, endChar) for each token in the original text
    /// </summary>
    public TokenizedInput Tokenize(string text)
    {
        var tokens = new List<int> { _clsId };
        var offsets = new List<(int Start, int End)> { (-1, -1) }; // [CLS] has no source

        // Split text into words by whitespace, track positions
        var words = SplitWithOffsets(text);

        foreach (var (word, wordStart, wordEnd) in words)
        {
            var subTokens = WordPieceTokenize(word);

            foreach (var (subToken, isFirst) in subTokens)
            {
                if (tokens.Count >= _maxLength - 1) // Leave room for [SEP]
                    break;

                var tokenId = _vocab.GetValueOrDefault(subToken, _unkId);
                tokens.Add(tokenId);

                // All sub-tokens of a word map to the word's char range
                offsets.Add((wordStart, wordEnd));
            }

            if (tokens.Count >= _maxLength - 1)
                break;
        }

        tokens.Add(_sepId);
        offsets.Add((-1, -1)); // [SEP] has no source

        // Pad to maxLength
        var seqLen = tokens.Count;
        var inputIds = new long[_maxLength];
        var attentionMask = new long[_maxLength];
        var tokenTypeIds = new long[_maxLength];
        var paddedOffsets = new (int Start, int End)[_maxLength];

        for (var i = 0; i < _maxLength; i++)
        {
            if (i < seqLen)
            {
                inputIds[i] = tokens[i];
                attentionMask[i] = 1;
                tokenTypeIds[i] = 0;
                paddedOffsets[i] = offsets[i];
            }
            else
            {
                inputIds[i] = _padId;
                attentionMask[i] = 0;
                tokenTypeIds[i] = 0;
                paddedOffsets[i] = (-1, -1);
            }
        }

        return new TokenizedInput
        {
            InputIds = inputIds,
            AttentionMask = attentionMask,
            TokenTypeIds = tokenTypeIds,
            Offsets = paddedOffsets,
            TokenCount = seqLen
        };
    }

    /// <summary>
    /// Split text into (word, startOffset, endOffset) tuples.
    /// Splits on whitespace while tracking character positions.
    /// </summary>
    private static List<(string Word, int Start, int End)> SplitWithOffsets(string text)
    {
        var result = new List<(string, int, int)>();
        var i = 0;

        while (i < text.Length)
        {
            // Skip whitespace
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            if (i >= text.Length)
                break;

            // BERT BasicTokenizer splits punctuation into separate tokens.
            // "Seattle," → "Seattle", ","
            if (char.IsPunctuation(text[i]))
            {
                result.Add((text[i].ToString(), i, i + 1));
                i++;
            }
            else
            {
                var start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && !char.IsPunctuation(text[i]))
                    i++;
                result.Add((text[start..i], start, i));
            }
        }

        return result;
    }

    /// <summary>
    /// Break a single word into WordPiece sub-tokens.
    ///
    /// Example: "playing" might become ["play", "##ing"]
    /// The "##" prefix indicates a continuation of the previous token.
    /// </summary>
    private List<(string Token, bool IsFirst)> WordPieceTokenize(string word)
    {
        var result = new List<(string, bool)>();

        // BERT NER uses a CASED vocab — do NOT lowercase.
        // "John" and "john" are different tokens; case is critical for NER.
        var start = 0;

        while (start < word.Length)
        {
            var found = false;
            var end = word.Length;

            while (start < end)
            {
                var substr = start == 0
                    ? word[start..end]
                    : "##" + word[start..end];

                if (_vocab.ContainsKey(substr))
                {
                    result.Add((substr, start == 0));
                    start = end;
                    found = true;
                    break;
                }

                end--;
            }

            if (!found)
            {
                // Single character not in vocab - use [UNK]
                result.Add(("[UNK]", start == 0));
                start++;
            }
        }

        return result;
    }
}

/// <summary>
/// Tokenized input ready for BERT ONNX inference.
/// All arrays are padded to maxLength.
/// </summary>
public class TokenizedInput
{
    /// <summary>
    /// Token IDs for the model (shape: [maxLength])
    /// </summary>
    public long[] InputIds { get; init; } = [];

    /// <summary>
    /// 1 for real tokens, 0 for padding (shape: [maxLength])
    /// </summary>
    public long[] AttentionMask { get; init; } = [];

    /// <summary>
    /// Segment IDs - all zeros for single-sentence NER (shape: [maxLength])
    /// </summary>
    public long[] TokenTypeIds { get; init; } = [];

    /// <summary>
    /// Character offsets (start, end) mapping each token back to source text.
    /// Special tokens and padding have (-1, -1).
    /// </summary>
    public (int Start, int End)[] Offsets { get; init; } = [];

    /// <summary>
    /// Number of real tokens (including [CLS] and [SEP], excluding padding)
    /// </summary>
    public int TokenCount { get; init; }
}
