using System.Text.Json;
using FraudProtection.Models;
using FraudProtection.Services.HistoryService;
using Microsoft.SemanticKernel;

namespace FraudProtection.Services.FraudAnalysisService;

public class FraudAnalysisService : IFraudAnalysisService
{
    private readonly Kernel _kernel;
    private readonly IHistoryService _historyService;
    private readonly ILogger<FraudAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FraudAnalysisService(Kernel kernel, IHistoryService historyService, ILogger<FraudAnalysisService> logger)
    {
        _kernel = kernel;
        _historyService = historyService;
        _logger = logger;
    }

    public async Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Content must not be empty.", nameof(request));
        }

        var arguments = new KernelArguments
        {
            ["inputType"] = request.InputType.ToString(),
            ["content"] = request.Content,
            ["secondaryContent"] = request.SecondaryContent
        };

        var result = await _kernel.InvokeAsync("FraudAnalysis", "analyze_content", arguments);
        var json = result.GetValue<string>() ?? "{}";

        var parsed = JsonSerializer.Deserialize<GeminiAssessment>(json, JsonOptions) ?? new GeminiAssessment();

        var caseId = Guid.NewGuid().ToString("N")[..12];
        var analyzedAt = DateTimeOffset.UtcNow;
        var riskLevel = ParseRiskLevel(parsed.RiskLevel);

        var response = new AnalyzeResponse
        {
            CaseId = caseId,
            RiskLevel = riskLevel,
            RiskScore = Math.Clamp(parsed.RiskScore, 0, 100),
            Summary = parsed.Summary ?? string.Empty,
            Evidence = parsed.Evidence?.Select(e => new EvidenceItem
            {
                Snippet = e.Snippet ?? string.Empty,
                Reason = e.Reason ?? string.Empty,
                Pattern = e.Pattern ?? string.Empty
            }).ToList() ?? [],
            RecommendedActions = parsed.RecommendedActions ?? [],
            GuidanceEnglish = parsed.GuidanceEnglish ?? string.Empty,
            GuidanceHindi = parsed.GuidanceHindi ?? string.Empty,
            AnalyzedAtUtc = analyzedAt
        };

        _historyService.AddCase(new AnalyzedCase
        {
            CaseId = caseId,
            InputType = request.InputType,
            ContentPreview = request.Content.Length > 120 ? request.Content[..120] + "…" : request.Content,
            RiskLevel = riskLevel,
            RiskScore = response.RiskScore,
            AnalyzedAtUtc = analyzedAt
        });

        return response;
    }

    private RiskLevel ParseRiskLevel(string? raw)
    {
        if (Enum.TryParse<RiskLevel>(raw, ignoreCase: true, out var level))
        {
            return level;
        }

        _logger.LogWarning("Unrecognised risk level '{Raw}' from model output, defaulting to Suspicious.", raw);
        return RiskLevel.Suspicious;
    }

    private class GeminiAssessment
    {
        public string? RiskLevel { get; set; }
        public int RiskScore { get; set; }
        public string? Summary { get; set; }
        public List<GeminiEvidence>? Evidence { get; set; }
        public List<string>? RecommendedActions { get; set; }
        public string? GuidanceEnglish { get; set; }
        public string? GuidanceHindi { get; set; }
    }

    private class GeminiEvidence
    {
        public string? Snippet { get; set; }
        public string? Reason { get; set; }
        public string? Pattern { get; set; }
    }
}
