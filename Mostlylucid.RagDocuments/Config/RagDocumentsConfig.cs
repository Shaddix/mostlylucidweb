namespace Mostlylucid.RagDocuments.Config;

public class RagDocumentsConfig
{
    public const string SectionName = "RagDocuments";

    public bool RequireApiKey { get; set; } = false;
    public string ApiKey { get; set; } = "";
    public string UploadPath { get; set; } = "./uploads";
    public int MaxFileSizeMB { get; set; } = 100;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".md", ".txt", ".html"];
}

public class PromptsConfig
{
    public const string SectionName = "Prompts";

    public Dictionary<string, string> SystemPrompts { get; set; } = new()
    {
        ["Default"] = "You are a helpful assistant that answers questions based on the provided documents. Always cite your sources.",
        ["Technical"] = "You are a technical documentation expert. Provide precise, code-focused answers with examples.",
        ["Research"] = "You are a research assistant. Provide comprehensive analysis with multiple perspectives.",
        ["Concise"] = "You are a concise assistant. Provide brief, direct answers in 1-2 sentences."
    };

    public QueryClarificationConfig QueryClarification { get; set; } = new();
    public QueryDecompositionConfig QueryDecomposition { get; set; } = new();
    public SelfCorrectionConfig SelfCorrection { get; set; } = new();
    public ResponseSynthesisConfig ResponseSynthesis { get; set; } = new();
}

public class QueryClarificationConfig
{
    public bool Enabled { get; set; } = true;
    public string Prompt { get; set; } = """
        Analyze this query and determine if clarification is needed:

        Query: {query}
        Context: {context}

        Respond with JSON:
        {"needsClarification": bool, "clarificationQuestion": string | null, "rewrittenQuery": string}
        """;
    public double AmbiguityThreshold { get; set; } = 0.7;
}

public class QueryDecompositionConfig
{
    public bool Enabled { get; set; } = true;
    public string Prompt { get; set; } = """
        Break down this complex query into simpler sub-queries:

        Query: {query}

        Respond with JSON array of sub-queries.
        """;
    public int MaxSubQueries { get; set; } = 5;
}

public class SelfCorrectionConfig
{
    public bool Enabled { get; set; } = true;
    public string Prompt { get; set; } = """
        Evaluate if these search results adequately answer the query:

        Query: {query}
        Results: {results}

        Respond with JSON:
        {"adequate": bool, "missingInfo": string | null, "refinedQuery": string | null}
        """;
    public int MaxRetries { get; set; } = 2;
}

public class ResponseSynthesisConfig
{
    public string Prompt { get; set; } = """
        Based on the following sources, answer the user's question.

        {systemPrompt}

        Sources:
        {sources}

        Question: {query}

        Provide a comprehensive answer with citations [1], [2], etc.
        """;
    public bool IncludeSources { get; set; } = true;
    public int MaxSourcesPerResponse { get; set; } = 10;
}
