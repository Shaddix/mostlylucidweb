using System.Text;
using System.Text.RegularExpressions;

namespace Mostlylucid.Services.Blog;

/// <summary>
/// Parses search queries with Google-style operators:
/// - Quoted phrases: "exact match"
/// - Exclude terms: -unwanted
/// - Wildcards: ASP*
/// - Special handling for technical terms: ASP.NET, C#, .NET, etc.
/// </summary>
public partial class SearchQueryParser
{
    [GeneratedRegex(@"""([^""]+)""|(-)?(\S+)", RegexOptions.Compiled)]
    private static partial Regex QueryTokenRegex();

    public record ParsedQuery
    {
        public List<string> IncludeTerms { get; init; } = new();
        public List<string> ExcludeTerms { get; init; } = new();
        public List<string> Phrases { get; init; } = new();
        public List<string> WildcardTerms { get; init; } = new();
        public string OriginalQuery { get; init; } = string.Empty;
        public bool HasSpecialCharacters { get; init; }
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "if", "in", "into", "is", "it",
        "no", "not", "of", "on", "or", "such", "that", "the", "their", "then", "there", "these",
        "they", "this", "to", "was", "will", "with"
    };

    /// <summary>
    /// Technical terms that should be treated as single tokens despite special characters
    /// </summary>
    private static readonly Dictionary<string, string> TechnicalTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["asp.net"] = "aspnet",
        ["c#"] = "csharp",
        [".net"] = "dotnet",
        ["f#"] = "fsharp",
        ["node.js"] = "nodejs",
        ["vue.js"] = "vuejs",
        ["react.js"] = "reactjs",
        ["next.js"] = "nextjs",
        ["asp.net core"] = "aspnetcore"
    };

    public ParsedQuery Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new ParsedQuery { OriginalQuery = query };

        var result = new ParsedQuery { OriginalQuery = query };
        var hasSpecialChars = query.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != '"' && c != '-');

        // First, replace known technical terms with their searchable versions
        var processedQuery = ReplaceTechnicalTerms(query);

        var matches = QueryTokenRegex().Matches(processedQuery);

        foreach (Match match in matches)
        {
            // Quoted phrase
            if (match.Groups[1].Success)
            {
                var phrase = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(phrase))
                {
                    result.Phrases.Add(phrase);
                }
                continue;
            }

            // Excluded term (starts with -)
            if (match.Groups[2].Success)
            {
                var term = match.Groups[3].Value.Trim();
                if (!string.IsNullOrWhiteSpace(term) && !StopWords.Contains(term))
                {
                    result.ExcludeTerms.Add(term.ToLowerInvariant());
                }
                continue;
            }

            // Regular term or wildcard
            var token = match.Groups[3].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Check if it's a wildcard term (contains *)
                if (token.Contains('*'))
                {
                    // Remove the asterisk for the actual search term, PostgreSQL handles :* suffix
                    var wildcardTerm = token.Replace("*", "").ToLowerInvariant();
                    if (!string.IsNullOrWhiteSpace(wildcardTerm) && !StopWords.Contains(wildcardTerm))
                    {
                        result.WildcardTerms.Add(wildcardTerm);
                    }
                }
                else if (!StopWords.Contains(token))
                {
                    result.IncludeTerms.Add(token.ToLowerInvariant());
                }
            }
        }

        return result with { HasSpecialCharacters = hasSpecialChars };
    }

    /// <summary>
    /// Replace technical terms with searchable versions while preserving original for phrase matching
    /// </summary>
    private string ReplaceTechnicalTerms(string query)
    {
        var result = query;

        // Sort by length descending to match longer terms first (e.g., "asp.net core" before "asp.net")
        foreach (var (term, replacement) in TechnicalTerms.OrderByDescending(kv => kv.Key.Length))
        {
            // Use regex for case-insensitive whole-word matching
            var pattern = $@"\b{Regex.Escape(term)}\b";
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Build PostgreSQL tsquery string from parsed query
    /// </summary>
    public string BuildTsQuery(ParsedQuery parsed)
    {
        var queryParts = new List<string>();

        // Add include terms with AND
        foreach (var term in parsed.IncludeTerms)
        {
            queryParts.Add(term);
        }

        // Add wildcard terms with :* suffix
        foreach (var term in parsed.WildcardTerms)
        {
            queryParts.Add($"{term}:*");
        }

        // Phrases are handled separately in the query builder
        // (using ILIKE for exact substring matching)

        return queryParts.Count > 0 ? string.Join(" & ", queryParts) : string.Empty;
    }

    /// <summary>
    /// Check if a term should be treated as an acronym
    /// </summary>
    public bool IsAcronymLike(string term)
    {
        return term.Length <= 6 && term.Any(char.IsUpper);
    }
}
