using System.Text.Json.Serialization;

namespace Mostlylucid.DocSummarizer.Config;

/// <summary>
///     Summary template configuration - controls LLM prompts and output format
/// </summary>
public class SummaryTemplate
{
    /// <summary>
    ///     Template name for identification
    /// </summary>
    public string Name { get; set; } = "default";

    /// <summary>
    ///     Description of what this template produces
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    ///     Target word count for executive summary (0 = no limit)
    /// </summary>
    public int TargetWords { get; set; }

    /// <summary>
    ///     Maximum bullet points for bullet-style output (0 = no limit)
    /// </summary>
    public int MaxBullets { get; set; }

    /// <summary>
    ///     Number of paragraphs for executive summary (0 = auto)
    /// </summary>
    public int Paragraphs { get; set; }

    /// <summary>
    ///     Output style: Prose, Bullets, Mixed, CitationsOnly
    /// </summary>
    public OutputStyle OutputStyle { get; set; } = OutputStyle.Prose;

    /// <summary>
    ///     Include topic breakdowns
    /// </summary>
    public bool IncludeTopics { get; set; } = true;

    /// <summary>
    ///     Include source citations [chunk-X]
    /// </summary>
    public bool IncludeCitations { get; set; } = true;

    /// <summary>
    ///     Include open questions / areas for follow-up
    /// </summary>
    public bool IncludeQuestions { get; set; }

    /// <summary>
    ///     Include processing trace/metadata
    /// </summary>
    public bool IncludeTrace { get; set; }

    /// <summary>
    ///     Custom prompt prefix for executive summary generation
    /// </summary>
    public string? ExecutivePrompt { get; set; }

    /// <summary>
    ///     Custom prompt for topic synthesis
    /// </summary>
    public string? TopicPrompt { get; set; }

    /// <summary>
    ///     Custom prompt for chunk summarization
    /// </summary>
    public string? ChunkPrompt { get; set; }

    /// <summary>
    ///     Tone: Professional, Casual, Academic, Technical
    /// </summary>
    public SummaryTone Tone { get; set; } = SummaryTone.Professional;

    /// <summary>
    ///     Audience level: Executive, Technical, General
    /// </summary>
    public AudienceLevel Audience { get; set; } = AudienceLevel.General;

    /// <summary>
    ///     Get the LLM prompt for executive summary based on template settings
    /// </summary>
    public string GetExecutivePrompt(string topicSummaries, string? focus)
    {
        if (!string.IsNullOrEmpty(ExecutivePrompt))
            return ExecutivePrompt
                .Replace("{topics}", topicSummaries)
                .Replace("{focus}", focus ?? "");

        var wordGuide = TargetWords > 0 ? $"in approximately {TargetWords} words" : "";
        var paragraphGuide = Paragraphs > 0 ? $"using {Paragraphs} paragraph(s)" : "";
        var styleGuide = OutputStyle switch
        {
            OutputStyle.Bullets => "as bullet points",
            OutputStyle.Mixed => "with a brief intro followed by bullet points",
            OutputStyle.CitationsOnly => "listing only the key citations and their relevance",
            _ => "in prose form"
        };
        var toneGuide = Tone switch
        {
            SummaryTone.Casual => "Use a conversational, accessible tone.",
            SummaryTone.Academic => "Use formal academic language with precise terminology.",
            SummaryTone.Technical => "Use technical language appropriate for domain experts.",
            _ => "Use clear, professional language."
        };
        var audienceGuide = Audience switch
        {
            AudienceLevel.Executive => "Write for busy executives who need key takeaways quickly.",
            AudienceLevel.Technical => "Include technical details relevant to practitioners.",
            _ => "Write for a general professional audience."
        };

        var citationGuide = IncludeCitations
            ? "IMPORTANT: Include [chunk-N] citations after each key fact to show sources."
            : "";

        return $"""
                {(focus != null ? $"Focus: {focus}\n" : "")}Topics covered:
                {topicSummaries}

                Write an executive summary {wordGuide} {paragraphGuide} {styleGuide}.
                {toneGuide}
                {audienceGuide}
                {citationGuide}
                """;
    }

    /// <summary>
    ///     Get the LLM prompt for topic synthesis
    /// </summary>
    public string GetTopicPrompt(string topic, string context, string? focus)
    {
        if (!string.IsNullOrEmpty(TopicPrompt))
            return TopicPrompt
                .Replace("{topic}", topic)
                .Replace("{context}", context)
                .Replace("{focus}", focus ?? "");

        var bulletGuide = MaxBullets > 0 ? $"Write {MaxBullets} bullet points" : "Write 2-3 bullet points";
        var citationGuide = IncludeCitations
            ? "End each bullet with the source citation in format [chunk-N]."
            : "";

        return $"""
                Topic: {topic}
                {(focus != null ? $"Focus: {focus}\n" : "")}
                Sources:
                {context}

                {bulletGuide} summarizing this topic.
                {citationGuide}
                """;
    }

    /// <summary>
    ///     Get the LLM prompt for chunk summarization
    /// </summary>
    public string GetChunkPrompt(string heading, string content)
    {
        if (!string.IsNullOrEmpty(ChunkPrompt))
            return ChunkPrompt
                .Replace("{heading}", heading)
                .Replace("{content}", content);

        var bulletGuide = MaxBullets > 0 ? $"{MaxBullets} bullet points" : "2-4 bullet points";

        return $"""
                Section: {heading}

                Content:
                {content}

                Summarize this section in {bulletGuide}. Be specific and preserve key facts.
                """;
    }

    /// <summary>
    ///     Built-in templates
    /// </summary>
    public static class Presets
    {
        /// <summary>
        ///     Default template - balanced prose with topics
        /// </summary>
        public static SummaryTemplate Default => new()
        {
            Name = "default",
            Description = "Balanced summary with executive overview and topic breakdowns",
            TargetWords = 500,
            Paragraphs = 3,
            OutputStyle = OutputStyle.Prose,
            IncludeTopics = true,
            IncludeCitations = true,
            IncludeQuestions = true,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.General
        };

        /// <summary>
        ///     Brief - quick 2-3 sentence summary
        /// </summary>
        public static SummaryTemplate Brief => new()
        {
            Name = "brief",
            Description = "Quick 2-3 sentence summary for fast scanning",
            TargetWords = 50,
            Paragraphs = 1,
            OutputStyle = OutputStyle.Prose,
            IncludeTopics = false,
            IncludeCitations = false,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.Executive
        };

        /// <summary>
        ///     One-liner - single sentence
        /// </summary>
        public static SummaryTemplate OneLiner => new()
        {
            Name = "oneliner",
            Description = "Single sentence summary",
            TargetWords = 25,
            Paragraphs = 1,
            OutputStyle = OutputStyle.Prose,
            IncludeTopics = false,
            IncludeCitations = false,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.Executive,
            ExecutivePrompt = """
                              {topics}

                              Write a single sentence (maximum 25 words) that captures the main point of this document.
                              """
        };

        /// <summary>
        ///     Bullets - key points only
        /// </summary>
        public static SummaryTemplate Bullets => new()
        {
            Name = "bullets",
            Description = "Bullet point list of key takeaways",
            MaxBullets = 7,
            OutputStyle = OutputStyle.Bullets,
            IncludeTopics = false,
            IncludeCitations = true,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.General,
            ExecutivePrompt = """
                              {topics}

                              List the 5-7 most important points from this document as bullet points.
                              Start each bullet with a verb. Include source citations.
                              """
        };

        /// <summary>
        ///     Executive - for leadership briefings
        /// </summary>
        public static SummaryTemplate Executive => new()
        {
            Name = "executive",
            Description = "Executive briefing format with recommendations",
            TargetWords = 150,
            Paragraphs = 1,
            MaxBullets = 3,
            OutputStyle = OutputStyle.Mixed,
            IncludeTopics = false,
            IncludeCitations = false,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.Executive,
            ExecutivePrompt = """
                              {topics}

                              Write an executive briefing with:
                              1. One paragraph overview (50 words max)
                              2. Three key takeaways as bullets
                              3. One recommended action

                              Be direct and actionable. No jargon.
                              """
        };

        /// <summary>
        ///     Detailed - comprehensive with all sections
        /// </summary>
        public static SummaryTemplate Detailed => new()
        {
            Name = "detailed",
            Description = "Comprehensive summary with full topic breakdowns",
            TargetWords = 1000,
            Paragraphs = 5,
            OutputStyle = OutputStyle.Prose,
            IncludeTopics = true,
            IncludeCitations = true,
            IncludeQuestions = true,
            IncludeTrace = true,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.General
        };

        /// <summary>
        ///     Technical - for technical documentation
        /// </summary>
        public static SummaryTemplate Technical => new()
        {
            Name = "technical",
            Description = "Technical summary preserving implementation details",
            TargetWords = 300,
            OutputStyle = OutputStyle.Mixed,
            IncludeTopics = true,
            IncludeCitations = true,
            IncludeQuestions = true,
            IncludeTrace = false,
            Tone = SummaryTone.Technical,
            Audience = AudienceLevel.Technical,
            ExecutivePrompt = """
                              {topics}

                              Write a technical summary that:
                              1. Explains the purpose and scope
                              2. Lists key technical components/features
                              3. Notes any requirements, dependencies, or limitations
                              4. Highlights implementation considerations

                              Use technical terminology where appropriate. Include citations.
                              """
        };

        /// <summary>
        ///     Academic - formal scholarly style
        /// </summary>
        public static SummaryTemplate Academic => new()
        {
            Name = "academic",
            Description = "Academic abstract format",
            TargetWords = 250,
            Paragraphs = 1,
            OutputStyle = OutputStyle.Prose,
            IncludeTopics = false,
            IncludeCitations = true,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Academic,
            Audience = AudienceLevel.Technical,
            ExecutivePrompt = """
                              {topics}

                              Write an academic abstract following this structure:
                              - Background/Context (1-2 sentences)
                              - Purpose/Objective (1 sentence)
                              - Methods/Approach (1-2 sentences)
                              - Key Findings (2-3 sentences)
                              - Conclusions/Implications (1-2 sentences)

                              Use formal academic language. Cite sources.
                              """
        };

        /// <summary>
        ///     Citations - just the references
        /// </summary>
        public static SummaryTemplate Citations => new()
        {
            Name = "citations",
            Description = "List of key passages with citations only",
            OutputStyle = OutputStyle.CitationsOnly,
            IncludeTopics = false,
            IncludeCitations = true,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.General,
            ExecutivePrompt = """
                              {topics}

                              List the 10 most important quotes or facts from this document.
                              Format each as: "Quote or fact" [chunk-X]
                              Order by importance. Include the source citation for each.
                              """
        };

        /// <summary>
        ///     Book Report - classic book report style summary
        /// </summary>
        public static SummaryTemplate BookReport => new()
        {
            Name = "bookreport",
            Description = "Classic book report style with setting, characters, plot, and themes",
            TargetWords = 800,
            Paragraphs = 6,
            OutputStyle = OutputStyle.Prose,
            IncludeTopics = true,
            IncludeCitations = true,
            IncludeQuestions = false,
            IncludeTrace = false,
            Tone = SummaryTone.Casual,
            Audience = AudienceLevel.General,
            ExecutivePrompt = """
                              {topics}

                              Write a comprehensive book report style summary with these sections:

                              **Overview**: What is this document about? Who wrote it and why? Set the scene. (3-4 sentences)

                              **Setting**: Where and when does this take place? Describe the world/context.

                              **Main Characters**: Who are the important people? Describe each major character's personality, motivations, and role in the story. Include at least 4-5 characters.

                              **Plot Summary**: What happens? Summarize the key events in chronological order. Include the main conflict, key turning points, and resolution. This should be detailed - at least 3-4 paragraphs.

                              **Themes & Motifs**: What are the main themes explored? What messages or lessons does the author convey? Discuss at least 3 major themes.

                              **Analysis & Opinion**: Is this work effective? What makes it memorable? What are its strengths and weaknesses?

                              Write in an engaging, accessible style. Include citations where you reference specific events or quotes.
                              """
        };

        /// <summary>
        ///     Meeting Notes - formatted for meeting follow-up
        /// </summary>
        public static SummaryTemplate MeetingNotes => new()
        {
            Name = "meeting",
            Description = "Meeting notes format with decisions, actions, and follow-ups",
            TargetWords = 200,
            MaxBullets = 10,
            OutputStyle = OutputStyle.Mixed,
            IncludeTopics = false,
            IncludeCitations = true,
            IncludeQuestions = true,
            IncludeTrace = false,
            Tone = SummaryTone.Professional,
            Audience = AudienceLevel.General,
            ExecutivePrompt = """
                              {topics}

                              Format this as meeting notes:

                              **Summary**: One paragraph overview of what was discussed.

                              **Key Decisions**:
                              - List any decisions made or conclusions reached

                              **Action Items**:
                              - List any tasks, assignments, or next steps mentioned
                              - Include who is responsible if mentioned

                              **Open Questions**:
                              - List any unresolved issues or questions that need follow-up

                              Be concise and actionable. Include source citations.
                              """
        };

        /// <summary>
        ///     List all available template names
        /// </summary>
        public static IReadOnlyList<string> AvailableTemplates => new[]
        {
            "default", "brief", "oneliner", "bullets", "executive", "detailed", "technical", "academic", "citations", "bookreport", "meeting"
        };

        /// <summary>
        ///     Get template by name
        /// </summary>
        public static SummaryTemplate GetByName(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "brief" => Brief,
                "oneliner" or "one-liner" => OneLiner,
                "bullets" or "bullet" => Bullets,
                "executive" or "exec" => Executive,
                "detailed" or "full" => Detailed,
                "technical" or "tech" => Technical,
                "academic" => Academic,
                "citations" or "refs" => Citations,
                "bookreport" or "book-report" or "book" => BookReport,
                "meeting" or "notes" or "meetingnotes" => MeetingNotes,
                _ => Default
            };
        }
    }
}

/// <summary>
///     Output style for summaries
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OutputStyle>))]
public enum OutputStyle
{
    /// <summary>
    ///     Flowing prose paragraphs
    /// </summary>
    Prose,

    /// <summary>
    ///     Bullet point list
    /// </summary>
    Bullets,

    /// <summary>
    ///     Mix of prose intro with bullet points
    /// </summary>
    Mixed,

    /// <summary>
    ///     Just citations/references
    /// </summary>
    CitationsOnly
}

/// <summary>
///     Tone for summary writing
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SummaryTone>))]
public enum SummaryTone
{
    /// <summary>
    ///     Clear, professional business language
    /// </summary>
    Professional,

    /// <summary>
    ///     Conversational, accessible
    /// </summary>
    Casual,

    /// <summary>
    ///     Formal academic language
    /// </summary>
    Academic,

    /// <summary>
    ///     Technical/domain-specific language
    /// </summary>
    Technical
}

/// <summary>
///     Target audience for summary
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AudienceLevel>))]
public enum AudienceLevel
{
    /// <summary>
    ///     General professional audience
    /// </summary>
    General,

    /// <summary>
    ///     Senior leadership / executives
    /// </summary>
    Executive,

    /// <summary>
    ///     Technical practitioners
    /// </summary>
    Technical
}