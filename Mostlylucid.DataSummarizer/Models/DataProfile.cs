namespace Mostlylucid.DataSummarizer.Models;

/// <summary>
/// Options for controlling profiling behavior
/// </summary>
public class ProfileOptions
{
    /// <summary>
    /// Specific columns to profile (null = all columns)
    /// </summary>
    public List<string>? Columns { get; set; }
    
    /// <summary>
    /// Columns to exclude from profiling
    /// </summary>
    public List<string>? ExcludeColumns { get; set; }
    
    /// <summary>
    /// Maximum number of columns to profile (0 = unlimited). 
    /// For wide tables, selects most interesting columns.
    /// </summary>
    public int MaxColumns { get; set; } = 50;
    
    /// <summary>
    /// Maximum number of numeric column pairs for correlation analysis
    /// </summary>
    public int MaxCorrelationPairs { get; set; } = 100;
    
    /// <summary>
    /// Skip expensive pattern detection (time series, trends)
    /// </summary>
    public bool FastMode { get; set; }
    
    /// <summary>
    /// Skip correlation analysis entirely
    /// </summary>
    public bool SkipCorrelations { get; set; }
    
    /// <summary>
    /// Sample size for MathNet robust stats (MAD, etc)
    /// </summary>
    public int SampleSize { get; set; } = 5000;
    
    /// <summary>
    /// Include LLM-friendly column descriptions in output
    /// </summary>
    public bool IncludeDescriptions { get; set; } = true;
    
    /// <summary>
    /// Ignore CSV parsing errors (malformed rows)
    /// </summary>
    public bool IgnoreErrors { get; set; }
    
    /// <summary>
    /// Target column for supervised analysis (e.g., "Exited" for churn)
    /// </summary>
    public string? TargetColumn { get; set; }
    
    /// <summary>
    /// Callback to update status during profiling (for UI feedback)
    /// </summary>
    public Action<string>? OnStatusUpdate { get; set; }
}

/// <summary>
/// Complete profile of a tabular data source
/// </summary>
public class DataProfile
{
    public string SourcePath { get; set; } = "";
    public DataSourceType SourceType { get; set; }
    public string? SheetName { get; set; } // For Excel
    public long RowCount { get; set; }
    public int ColumnCount => Columns.Count;
    public List<ColumnProfile> Columns { get; set; } = [];
    public List<DataAlert> Alerts { get; set; } = [];
    public List<ColumnCorrelation> Correlations { get; set; } = [];
    public List<DataInsight> Insights { get; set; } = [];
    public List<DetectedPattern> Patterns { get; set; } = [];
    public TargetProfile? Target { get; set; }
    public TimeSpan ProfileTime { get; set; }
}

/// <summary>
/// Statistical profile for a single column
/// </summary>
public class ColumnProfile
{
    public string Name { get; set; } = "";
    public string DuckDbType { get; set; } = "";
    public ColumnType InferredType { get; set; }
    
    /// <summary>
    /// Semantic role (Identifier, Measure, Category, BinaryFlag, Target, Text)
    /// </summary>
    public SemanticRole SemanticRole { get; set; } = SemanticRole.Unknown;
    
    /// <summary>
    /// Human-readable description of the column (auto-generated or LLM-enhanced)
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Why this column is interesting (for wide table prioritization)
    /// </summary>
    public double InterestScore { get; set; }
    
    // Counts
    public long Count { get; set; }
    public long NullCount { get; set; }
    public long UniqueCount { get; set; }
    public double NullPercent => Count > 0 ? (NullCount * 100.0 / Count) : 0;
    public double UniquePercent
    {
        get
        {
            if (Count <= 0) return 0;
            var pct = UniqueCount * 100.0 / Count;
            return Math.Min(100.0, Math.Max(0, pct));
        }
    }
    
    // Numeric stats (null if not numeric)
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Mean { get; set; }
    public double? Median { get; set; }
    public double? StdDev { get; set; }
    public double? Q25 { get; set; }
    public double? Q75 { get; set; }
    public double? Skewness { get; set; }
    public double? Kurtosis { get; set; } // Tail heaviness (3 = normal)
    public int OutlierCount { get; set; }
    public int ZeroCount { get; set; } // Count of zero values (useful for sparse data)
    
    /// <summary>
    /// Interquartile range (Q75 - Q25)
    /// </summary>
    public double? Iqr => Q25.HasValue && Q75.HasValue ? Q75 - Q25 : null;
    
    /// <summary>
    /// Total range (Max - Min)
    /// </summary>
    public double? Range => Min.HasValue && Max.HasValue ? Max - Min : null;
    
    /// <summary>
    /// Coefficient of variation (StdDev / Mean) - relative dispersion
    /// </summary>
    public double? CoefficientOfVariation => Mean.HasValue && Mean != 0 && StdDev.HasValue 
        ? Math.Abs(StdDev.Value / Mean.Value) : null;
    
    // Categorical stats
    public List<ValueCount>? TopValues { get; set; }
    public double? ImbalanceRatio { get; set; }
    
    /// <summary>
    /// Shannon entropy (bits) - information content for categorical columns
    /// Higher = more uniform, Lower = more concentrated
    /// </summary>
    public double? Entropy { get; set; }
    
    /// <summary>
    /// Mode - most frequent value
    /// </summary>
    public string? Mode { get; set; }
    
    // Date stats
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public int? DateGapDays { get; set; }
    
    /// <summary>
    /// Date span in days
    /// </summary>
    public int? DateSpanDays => MinDate.HasValue && MaxDate.HasValue 
        ? (int)(MaxDate.Value - MinDate.Value).TotalDays : null;
    
    // Text stats
    public double? AvgLength { get; set; }
    public int? MaxLength { get; set; }
    public int? MinLength { get; set; }
    public int EmptyStringCount { get; set; } // Count of empty/whitespace strings
    
    // Additional robust stats
    public double? Mad { get; set; } // Median absolute deviation
    
    // Pattern detection results
    public List<TextPatternMatch> TextPatterns { get; set; } = [];
    public DistributionType? Distribution { get; set; }
    public TrendInfo? Trend { get; set; }
    public TimeSeriesInfo? TimeSeries { get; set; }
    
    /// <summary>
    /// Periodicity detection result (for numeric time series columns)
    /// </summary>
    public PeriodicityInfo? Periodicity { get; set; }
}

public enum ColumnType
{
    Unknown,
    Numeric,
    Categorical,
    DateTime,
    Text,
    Boolean,
    Id // Likely an identifier column
}

public enum SemanticRole
{
    Unknown,
    Identifier,
    Measure,
    Category,
    BinaryFlag,
    Target,
    FreeText
}

public class ValueCount
{
    public string Value { get; set; } = "";
    public long Count { get; set; }
    public double Percent { get; set; }
}

/// <summary>
/// Data quality alert
/// </summary>
public class DataAlert
{
    public AlertSeverity Severity { get; set; }
    public string Column { get; set; } = "";
    public AlertType Type { get; set; }
    public string Message { get; set; } = "";
}

public enum AlertSeverity { Info, Warning, Error }

public enum AlertType
{
    HighNulls,
    HighCardinality,
    LowCardinality,
    Constant,
    HighSkewness,
    Outliers,
    Imbalanced,
    PossibleId,
    DateGaps,
    // Decision-oriented flags
    TargetImbalance,
    PotentialLeakage,
    OrdinalAsCategory,
    ZeroInflated,
    ModelingHint,
    DataQuality,
    // PII/Sensitive data
    PiiDetected,
    // Constraints
    ConstraintViolation
}

/// <summary>
/// Correlation between two numeric columns
/// </summary>
public class ColumnCorrelation
{
    public string Column1 { get; set; } = "";
    public string Column2 { get; set; } = "";
    public double Correlation { get; set; }
    public string Metric { get; set; } = "pearson";
    public string Strength => Math.Abs(Correlation) switch
    {
        >= 0.8 => "Very Strong",
        >= 0.6 => "Strong",
        >= 0.4 => "Moderate",
        >= 0.2 => "Weak",
        _ => "Negligible"
    };
}

/// <summary>
/// An insight derived from the data
/// </summary>
public class DataInsight
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Sql { get; set; }
    public object? Result { get; set; }
    public InsightSource Source { get; set; }
    public double Score { get; set; }
    public Dictionary<string, double> ScoreBreakdown { get; set; } = new();
    public List<string> RelatedColumns { get; set; } = [];
}

public enum InsightSource
{
    Statistical,
    LlmGenerated
}

public class TargetProfile
{
    public string ColumnName { get; set; } = "";
    public bool IsBinary { get; set; }
    public Dictionary<string, double> ClassDistribution { get; set; } = new();
    public List<FeatureEffect> FeatureEffects { get; set; } = [];
}

public class FeatureEffect
{
    public string Feature { get; set; } = "";
    public double Magnitude { get; set; }
    public double Support { get; set; }
    public string Summary { get; set; } = "";
    public string Metric { get; set; } = "";
    public Dictionary<string, double> Details { get; set; } = new();
}

/// <summary>
/// Summary report in markdown format
/// </summary>
public class DataSummaryReport
{
    public DataProfile Profile { get; set; } = new();
    public string ExecutiveSummary { get; set; } = "";
    public string MarkdownReport { get; set; } = "";
    public Dictionary<string, string> FocusFindings { get; set; } = new();
}

#region Pattern Detection Models

/// <summary>
/// A detected data pattern
/// </summary>
public class DetectedPattern
{
    public PatternType Type { get; set; }
    public string Description { get; set; } = "";
    public List<string> RelatedColumns { get; set; } = [];
    public double Confidence { get; set; }
    public Dictionary<string, object> Details { get; set; } = [];
}

public enum PatternType
{
    TimeSeries,
    Monotonic,
    TextFormat,
    ForeignKey,
    Distribution,
    Trend,
    Seasonality,
    Clustering,
    Sequential
}

/// <summary>
/// Text pattern match for a column
/// </summary>
public class TextPatternMatch
{
    public TextPatternType PatternType { get; set; }
    public int MatchCount { get; set; }
    public double MatchPercent { get; set; }
    
    /// <summary>
    /// For novel patterns: the detected regex pattern
    /// </summary>
    public string? DetectedRegex { get; set; }
    
    /// <summary>
    /// For novel patterns: example values that match this pattern
    /// </summary>
    public List<string>? Examples { get; set; }
    
    /// <summary>
    /// LLM-generated description of what this pattern represents
    /// </summary>
    public string? Description { get; set; }
}

public enum TextPatternType
{
    Email,
    Url,
    Phone,
    Uuid,
    IpAddress,
    CreditCard,
    PostalCode,
    Date,
    Currency,
    Percentage,
    /// <summary>
    /// A novel pattern detected via character class analysis (not a known format)
    /// </summary>
    Novel
}

/// <summary>
/// Distribution classification result
/// </summary>
public enum DistributionType
{
    Unknown,
    Normal,
    Uniform,
    LeftSkewed,
    RightSkewed,
    Bimodal,
    PowerLaw,
    Exponential
}

/// <summary>
/// Trend information for a numeric column
/// </summary>
public class TrendInfo
{
    public TrendDirection Direction { get; set; }
    public double Slope { get; set; }
    public double RSquared { get; set; } // Fit quality
    public string? RelatedDateColumn { get; set; }
}

public enum TrendDirection
{
    None,
    Increasing,
    Decreasing,
    Fluctuating
}

/// <summary>
/// Time series characteristics
/// </summary>
public class TimeSeriesInfo
{
    public string DateColumn { get; set; } = "";
    public TimeGranularity Granularity { get; set; }
    public int GapCount { get; set; }
    public double GapPercent { get; set; }
    public bool HasSeasonality { get; set; }
    public int? SeasonalPeriod { get; set; }
    public bool IsContiguous { get; set; }
}

public enum TimeGranularity
{
    Unknown,
    Minute,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

/// <summary>
/// Periodicity detection result from autocorrelation analysis
/// </summary>
public class PeriodicityInfo
{
    /// <summary>Whether a significant periodic pattern was detected</summary>
    public bool HasPeriodicity { get; set; }
    
    /// <summary>The dominant period (number of time units between cycles)</summary>
    public int DominantPeriod { get; set; }
    
    /// <summary>Confidence in the detection (0-1, based on ACF peak strength)</summary>
    public double Confidence { get; set; }
    
    /// <summary>Human-readable interpretation (e.g., "Weekly cycle (7 periods)")</summary>
    public string SuggestedInterpretation { get; set; } = "";
}

#endregion

#region Tool Mode Output Models

/// <summary>
/// Structured output for LLM tool integration.
/// Designed to be machine-readable with evidence-grounded claims.
/// </summary>
public record ToolOutput
{
    /// <summary>Whether the operation succeeded</summary>
    public required bool Success { get; init; }
    
    /// <summary>Error message if failed</summary>
    public string? Error { get; init; }
    
    /// <summary>Source file path or pattern</summary>
    public required string Source { get; init; }
    
    /// <summary>The profile result (null if failed or in QA mode)</summary>
    public ToolProfile? Profile { get; init; }
    
    /// <summary>The QA answer result (null if failed or in profile mode)</summary>
    public ToolAnswer? Answer { get; init; }
    
    /// <summary>Processing metadata</summary>
    public ToolMetadata? Metadata { get; init; }
}

/// <summary>
/// QA answer for tool output
/// </summary>
public record ToolAnswer
{
    /// <summary>The question that was asked</summary>
    public required string Question { get; init; }
    
    /// <summary>The answer generated from the data</summary>
    public required string Response { get; init; }
    
    /// <summary>Mode used (Registry, SingleFile)</summary>
    public required string Mode { get; init; }
    
    /// <summary>Relevant columns that informed the answer</summary>
    public List<string>? SourceColumns { get; init; }
    
    /// <summary>SQL query if applicable</summary>
    public string? Sql { get; init; }
}

/// <summary>
/// Simplified profile for tool output (subset of DataProfile)
/// </summary>
public record ToolProfile
{
    /// <summary>Source file path</summary>
    public required string SourcePath { get; init; }
    
    /// <summary>Number of rows in the dataset</summary>
    public required long RowCount { get; init; }
    
    /// <summary>Number of columns analyzed</summary>
    public required int ColumnCount { get; init; }
    
    /// <summary>Executive summary (1-3 sentences)</summary>
    public required string ExecutiveSummary { get; init; }
    
    /// <summary>Column profiles</summary>
    public required List<ToolColumnProfile> Columns { get; init; }
    
    /// <summary>Data quality alerts</summary>
    public required List<ToolAlert> Alerts { get; init; }
    
    /// <summary>Key insights derived from data</summary>
    public required List<ToolInsight> Insights { get; init; }
    
    /// <summary>Top correlations between columns</summary>
    public List<ToolCorrelation>? Correlations { get; init; }
    
    /// <summary>Target analysis results (if target column specified)</summary>
    public ToolTargetAnalysis? TargetAnalysis { get; init; }
}

/// <summary>
/// Simplified column profile for tool output
/// </summary>
public record ToolColumnProfile
{
    /// <summary>Column name</summary>
    public required string Name { get; init; }
    
    /// <summary>Inferred type (Numeric, Categorical, DateTime, Text, Boolean, Id)</summary>
    public required string Type { get; init; }
    
    /// <summary>Semantic role (Identifier, Measure, Category, BinaryFlag, Target, FreeText)</summary>
    public string? Role { get; init; }
    
    /// <summary>Percentage of null values</summary>
    public required double NullPercent { get; init; }
    
    /// <summary>Number of unique values</summary>
    public required long UniqueCount { get; init; }
    
    /// <summary>Percentage of unique values</summary>
    public required double UniquePercent { get; init; }
    
    /// <summary>Statistics (type-dependent)</summary>
    public ToolColumnStats? Stats { get; init; }
    
    /// <summary>Distribution type if detected (Normal, Uniform, Skewed, etc.)</summary>
    public string? Distribution { get; init; }
    
    /// <summary>Trend if detected</summary>
    public string? Trend { get; init; }
    
    /// <summary>Periodicity info if detected (for numeric time series)</summary>
    public ToolPeriodicityInfo? Periodicity { get; init; }
}

/// <summary>
/// Periodicity info for tool output
/// </summary>
public record ToolPeriodicityInfo
{
    /// <summary>Dominant period in time units</summary>
    public required int Period { get; init; }
    
    /// <summary>Confidence (0-1)</summary>
    public required double Confidence { get; init; }
    
    /// <summary>Human-readable interpretation</summary>
    public required string Interpretation { get; init; }
}

/// <summary>
/// Column statistics (varies by type)
/// </summary>
public record ToolColumnStats
{
    // Numeric
    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Mean { get; init; }
    public double? Median { get; init; }
    public double? StdDev { get; init; }
    public double? Skewness { get; init; }
    public double? Kurtosis { get; init; }
    public int? OutlierCount { get; init; }
    public int? ZeroCount { get; init; }
    public double? CoefficientOfVariation { get; init; }
    public double? Iqr { get; init; }
    
    // Categorical
    public string? TopValue { get; init; }
    public double? TopValuePercent { get; init; }
    public double? ImbalanceRatio { get; init; }
    public double? Entropy { get; init; }
    
    // DateTime
    public string? MinDate { get; init; }
    public string? MaxDate { get; init; }
    public int? DateGapDays { get; init; }
    public int? DateSpanDays { get; init; }
    
    // Text
    public double? AvgLength { get; init; }
    public int? MaxLength { get; init; }
    public int? MinLength { get; init; }
    public int? EmptyStringCount { get; init; }
}

/// <summary>
/// Alert for tool output
/// </summary>
public record ToolAlert
{
    /// <summary>Severity: Info, Warning, Error</summary>
    public required string Severity { get; init; }
    
    /// <summary>Column name (if applicable)</summary>
    public string? Column { get; init; }
    
    /// <summary>Alert type</summary>
    public required string Type { get; init; }
    
    /// <summary>Human-readable message</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Insight for tool output
/// </summary>
public record ToolInsight
{
    /// <summary>Insight title</summary>
    public required string Title { get; init; }
    
    /// <summary>Insight description</summary>
    public required string Description { get; init; }
    
    /// <summary>Relevance score (0-1)</summary>
    public double Score { get; init; }
    
    /// <summary>Source: Statistical, LlmGenerated</summary>
    public required string Source { get; init; }
    
    /// <summary>Related columns</summary>
    public List<string>? RelatedColumns { get; init; }
}

/// <summary>
/// Correlation for tool output
/// </summary>
public record ToolCorrelation
{
    /// <summary>First column</summary>
    public required string Column1 { get; init; }
    
    /// <summary>Second column</summary>
    public required string Column2 { get; init; }
    
    /// <summary>Correlation coefficient (-1 to 1)</summary>
    public required double Coefficient { get; init; }
    
    /// <summary>Strength: Very Strong, Strong, Moderate, Weak, Negligible</summary>
    public required string Strength { get; init; }
}

/// <summary>
/// Target analysis for tool output (when --target is specified)
/// </summary>
public record ToolTargetAnalysis
{
    /// <summary>Target column name</summary>
    public required string TargetColumn { get; init; }
    
    /// <summary>Whether target is binary</summary>
    public required bool IsBinary { get; init; }
    
    /// <summary>Class distribution (value -> percentage)</summary>
    public required Dictionary<string, double> ClassDistribution { get; init; }
    
    /// <summary>Top feature drivers</summary>
    public required List<ToolFeatureDriver> TopDrivers { get; init; }
}

/// <summary>
/// Feature driver for target analysis
/// </summary>
public record ToolFeatureDriver
{
    /// <summary>Feature column name</summary>
    public required string Feature { get; init; }
    
    /// <summary>Effect magnitude (Cohen's d for numeric, rate delta for categorical)</summary>
    public required double Magnitude { get; init; }
    
    /// <summary>Support (what % of data this applies to)</summary>
    public required double Support { get; init; }
    
    /// <summary>Human-readable summary</summary>
    public required string Summary { get; init; }
    
    /// <summary>Metric used (CohenD, RateDelta)</summary>
    public required string Metric { get; init; }
}

/// <summary>
/// Processing metadata for tool output
/// </summary>
public record ToolMetadata
{
    /// <summary>Time taken to process in seconds</summary>
    public required double ProcessingSeconds { get; init; }
    
    /// <summary>Number of columns analyzed</summary>
    public required int ColumnsAnalyzed { get; init; }
    
    /// <summary>Number of rows analyzed</summary>
    public required long RowsAnalyzed { get; init; }
    
    /// <summary>Ollama model used (null if no LLM)</summary>
    public string? Model { get; init; }
    
    /// <summary>Whether LLM was used</summary>
    public required bool UsedLlm { get; init; }
    
    /// <summary>Target column if specified</summary>
    public string? TargetColumn { get; init; }
    
    /// <summary>Profile timestamp (ISO 8601)</summary>
    public required string ProfiledAt { get; init; }
    
    /// <summary>Stored profile ID (for drift comparison). Use with --compare-to flag.</summary>
    public string? ProfileId { get; init; }
    
    /// <summary>Schema hash (profiles with same hash have identical schema)</summary>
    public string? SchemaHash { get; init; }
    
    /// <summary>Content hash (same hash = exact same file content, skip re-profiling)</summary>
    public string? ContentHash { get; init; }
    
    /// <summary>Drift detection result if a baseline was found</summary>
    public ToolDriftSummary? Drift { get; init; }
}

/// <summary>
/// Summary of drift detection for tool output
/// </summary>
public record ToolDriftSummary
{
    /// <summary>ID of the baseline profile used for comparison</summary>
    public required string BaselineProfileId { get; init; }
    
    /// <summary>When the baseline was profiled</summary>
    public required string BaselineDate { get; init; }
    
    /// <summary>Overall drift score (0-1, higher = more drift)</summary>
    public required double DriftScore { get; init; }
    
    /// <summary>Whether drift exceeds significance threshold</summary>
    public required bool HasSignificantDrift { get; init; }
    
    /// <summary>Row count change percentage</summary>
    public double RowCountChangePercent { get; init; }
    
    /// <summary>Number of columns with significant drift</summary>
    public int DriftedColumnCount { get; init; }
    
    /// <summary>Columns removed since baseline</summary>
    public List<string>? RemovedColumns { get; init; }
    
    /// <summary>Columns added since baseline</summary>
    public List<string>? AddedColumns { get; init; }
    
    /// <summary>Summary message</summary>
    public required string Summary { get; init; }
    
    /// <summary>Recommendations based on drift</summary>
    public List<string>? Recommendations { get; init; }
}

#endregion
