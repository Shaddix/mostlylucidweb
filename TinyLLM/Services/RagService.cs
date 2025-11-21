using System.IO;
using System.Text.Json;
using TinyLLM.Models;

namespace TinyLLM.Services;

public class RagService
{
    private readonly string _databasePath;
    private RagDatabase _database;
    private readonly object _lock = new();

    public RagService()
    {
        var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        _databasePath = Path.Combine(dataDirectory, "rag_database.json");
        _database = LoadDatabase();
    }

    private RagDatabase LoadDatabase()
    {
        if (!File.Exists(_databasePath))
            return new RagDatabase();

        try
        {
            var json = File.ReadAllText(_databasePath);
            return JsonSerializer.Deserialize<RagDatabase>(json) ?? new RagDatabase();
        }
        catch
        {
            return new RagDatabase();
        }
    }

    private void SaveDatabase()
    {
        var json = JsonSerializer.Serialize(_database, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_databasePath, json);
    }

    // Simple embedding using character n-grams and term frequency
    private float[] CreateSimpleEmbedding(string text, int dimensions = 128)
    {
        var embedding = new float[dimensions];
        var normalized = text.ToLowerInvariant();

        // Character-level features
        for (int i = 0; i < normalized.Length - 1 && i < dimensions / 2; i++)
        {
            var charCode = (normalized[i] + normalized[i + 1]) % dimensions;
            embedding[charCode] += 1.0f;
        }

        // Word-level features
        var words = normalized.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var hash = Math.Abs(word.GetHashCode()) % (dimensions / 2);
            embedding[dimensions / 2 + hash] += 1.0f;
        }

        // Normalize the embedding
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < dimensions; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return embedding;
    }

    private float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dotProduct = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
        }

        return dotProduct; // Already normalized in CreateSimpleEmbedding
    }

    public void AddEntry(string content, Dictionary<string, string>? metadata = null)
    {
        lock (_lock)
        {
            var entry = new RagEntry
            {
                Content = content,
                Embedding = CreateSimpleEmbedding(content),
                Metadata = metadata ?? new Dictionary<string, string>()
            };

            _database.Entries.Add(entry);
            _database.LastUpdated = DateTime.Now;
            SaveDatabase();
        }
    }

    public List<RagEntry> Search(string query, int topK = 3)
    {
        lock (_lock)
        {
            if (_database.Entries.Count == 0)
                return new List<RagEntry>();

            var queryEmbedding = CreateSimpleEmbedding(query);

            var results = _database.Entries
                .Select(entry => new
                {
                    Entry = entry,
                    Similarity = CosineSimilarity(queryEmbedding, entry.Embedding)
                })
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => x.Entry)
                .ToList();

            return results;
        }
    }

    public void AddConversationToRag(string userMessage, string assistantResponse)
    {
        var content = $"User: {userMessage}\nAssistant: {assistantResponse}";
        AddEntry(content, new Dictionary<string, string>
        {
            { "type", "conversation" },
            { "timestamp", DateTime.Now.ToString("O") }
        });
    }

    public string GetContextForQuery(string query, int topK = 3)
    {
        var results = Search(query, topK);
        if (results.Count == 0)
            return string.Empty;

        var context = "Relevant context from previous conversations:\n\n";
        for (int i = 0; i < results.Count; i++)
        {
            context += $"[{i + 1}] {results[i].Content}\n\n";
        }

        return context;
    }

    public int GetEntryCount()
    {
        lock (_lock)
        {
            return _database.Entries.Count;
        }
    }

    public void ClearDatabase()
    {
        lock (_lock)
        {
            _database = new RagDatabase();
            SaveDatabase();
        }
    }
}
