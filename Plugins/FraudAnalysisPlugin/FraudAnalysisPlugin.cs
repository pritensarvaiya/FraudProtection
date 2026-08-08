using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FraudProtection.Plugins.FraudAnalysisPlugin;

public class FraudAnalysisPlugin
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<FraudAnalysisPlugin> _logger;

    public FraudAnalysisPlugin(IChatCompletionService chatCompletionService, ILogger<FraudAnalysisPlugin> logger)
    {
        _chatCompletionService = chatCompletionService;
        _logger = logger;
    }

    [KernelFunction("analyze_content")]
    [Description("Analyses a suspicious message, email, URL, or screenshot text for fraud/scam indicators and returns a structured JSON risk assessment.")]
    public async Task<string> AnalyzeContentAsync(
        [Description("The type of input being analysed: message, email, url, or screenshot")] string inputType,
        [Description("The primary text content to analyse")] string content,
        [Description("Optional secondary context, e.g. sender address or accompanying message")] string? secondaryContent = null)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(FraudAnalysisSystemPrompt.Instructions);

        var userPrompt = $"Input type: {inputType}\n\nPrimary content:\n{content}";
        if (!string.IsNullOrWhiteSpace(secondaryContent))
        {
            userPrompt += $"\n\nSecondary content:\n{secondaryContent}";
        }

        chatHistory.AddUserMessage(userPrompt);

        var response = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory);
        var rawText = response.FirstOrDefault()?.Content ?? "{}";

        var cleaned = StripJsonFences(rawText);

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            return cleaned;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini response was not valid JSON, returning fallback assessment. Raw: {Raw}", rawText);
            return BuildFallbackAssessment();
        }
    }

    private static string StripJsonFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            trimmed = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed;
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
            {
                trimmed = trimmed[..lastFence];
            }
        }
        return trimmed.Trim();
    }

    private static string BuildFallbackAssessment() => """
        {
          "riskLevel": "Suspicious",
          "riskScore": 50,
          "summary": "We could not fully analyse this content automatically. Treat it with caution.",
          "evidence": [],
          "recommendedActions": ["Avoid clicking any links or sharing personal information until you can verify the source directly."],
          "guidanceEnglish": "We were unable to complete a full automated analysis. As a precaution, do not click any links, do not share OTPs or personal details, and verify the sender through an official channel before responding.",
          "guidanceHindi": "हम इस सामग्री का पूरी तरह से स्वचालित विश्लेषण नहीं कर सके। सावधानी के तौर पर किसी भी लिंक पर क्लिक न करें, OTP या व्यक्तिगत जानकारी साझा न करें, और जवाब देने से पहले किसी आधिकारिक माध्यम से भेजने वाले की पुष्टि करें।"
        }
        """;
}
