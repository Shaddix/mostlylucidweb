namespace Mostlylucid.OcrNer.Models;

/// <summary>
///     All signals extracted from text via Microsoft.Recognizers.Text.
/// </summary>
public record RecognizedSignals
{
    /// <summary>
    ///     Culture used for recognition (e.g., "en-us", "en-gb", "de-de").
    /// </summary>
    public string Culture { get; init; } = "en-us";

    public List<RecognizedDateTime> DateTimes { get; init; } = [];
    public List<RecognizedNumber> Numbers { get; init; } = [];
    public List<RecognizedSequence> Urls { get; init; } = [];
    public List<RecognizedSequence> PhoneNumbers { get; init; } = [];
    public List<RecognizedSequence> Emails { get; init; } = [];
    public List<RecognizedSequence> IpAddresses { get; init; } = [];

    public bool HasTemporalSignals => DateTimes.Count > 0;

    public bool HasAnySignals => DateTimes.Count + Numbers.Count + Urls.Count +
        PhoneNumbers.Count + Emails.Count + IpAddresses.Count > 0;
}

/// <summary>
///     Recognized date/time expression with resolution.
/// </summary>
public record RecognizedDateTime
{
    public required string Text { get; init; }
    public int Start { get; init; }
    public int End { get; init; }
    public string? TypeName { get; init; }
    public IDictionary<string, object> Resolution { get; init; } = new Dictionary<string, object>();
}

/// <summary>
///     Recognized number with resolved value.
/// </summary>
public record RecognizedNumber
{
    public required string Text { get; init; }
    public int Start { get; init; }
    public string? Value { get; init; }
    public string? TypeName { get; init; }
}

/// <summary>
///     Recognized sequence (URL, phone, email, IP).
/// </summary>
public record RecognizedSequence
{
    public required string Text { get; init; }
    public int Start { get; init; }
    public required string TypeName { get; init; }
}
