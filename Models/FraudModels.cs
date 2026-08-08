namespace FraudProtection.Models;

public enum InputType
{
    Message,
    Email,
    Url,
    Screenshot
}

public enum RiskLevel
{
    Low,
    Suspicious,
    High
}

public class AnalyzeRequest
{
    public InputType InputType { get; set; } = InputType.Message;

    /// <summary>Raw text of the message/email/URL, or OCR'd/pasted text from a screenshot.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Optional second input for MVP's "accept two inputs" requirement (e.g. sender + message, or URL + message).</summary>
    public string? SecondaryContent { get; set; }

    /// <summary>Base64-encoded screenshot image data (no data: URI prefix), sent so the model can read the text directly.</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>MIME type of <see cref="ImageBase64"/>, e.g. "image/png".</summary>
    public string? ImageMimeType { get; set; }
}

public class EvidenceItem
{
    public string Snippet { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
}

public class AnalyzeResponse
{
    public string CaseId { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<EvidenceItem> Evidence { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
    public string GuidanceEnglish { get; set; } = string.Empty;
    public string GuidanceHindi { get; set; } = string.Empty;
    public DateTimeOffset AnalyzedAtUtc { get; set; }
}

public class AnalyzedCase
{
    public string CaseId { get; set; } = string.Empty;
    public InputType InputType { get; set; }
    public string ContentPreview { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public DateTimeOffset AnalyzedAtUtc { get; set; }
}
