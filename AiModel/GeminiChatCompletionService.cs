using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace FraudProtection.AiModel;

/// <summary>
/// Thin Semantic Kernel connector for Google Gemini, calling the generateContent REST API directly
/// (mirrors the hand-rolled adapter pattern used for other providers rather than relying on an
/// unstable/alpha-only official connector package).
/// </summary>
public class GeminiChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public GeminiChatCompletionService(string apiKey, string model, HttpClient httpClient)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Gemini API key must not be empty.", nameof(apiKey));
        }

        _apiKey = apiKey;
        _model = model;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var (systemInstruction, contents) = BuildGeminiPayloadParts(chatHistory);

        var requestBody = new GeminiRequest
        {
            SystemInstruction = systemInstruction is null ? null : new GeminiContent { Parts = [new GeminiPart { Text = systemInstruction }] },
            Contents = contents,
            GenerationConfig = new GeminiGenerationConfig { Temperature = 0.3, MaxOutputTokens = 2048 }
        };

        var requestUri = $"v1beta/models/{_model}:generateContent?key={_apiKey}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(requestUri, requestBody, GeminiJsonContext.Default.GeminiRequest, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Gemini API did not respond within {_httpClient.Timeout.TotalSeconds:N0} seconds.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Gemini API request failed ({response.StatusCode}): {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync(GeminiJsonContext.Default.GeminiResponse, cancellationToken);

            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;

            return [new ChatMessageContent(AuthorRole.Assistant, text)];
        }
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var result in results)
        {
            yield return new StreamingChatMessageContent(result.Role, result.Content);
        }
    }

    private static (string? systemInstruction, List<GeminiContent> contents) BuildGeminiPayloadParts(ChatHistory chatHistory)
    {
        string? systemInstruction = null;
        var contents = new List<GeminiContent>();

        foreach (var message in chatHistory)
        {
            if (message.Role == AuthorRole.System)
            {
                systemInstruction = string.IsNullOrEmpty(systemInstruction)
                    ? message.Content
                    : $"{systemInstruction}\n{message.Content}";
                continue;
            }

            var role = message.Role == AuthorRole.Assistant ? "model" : "user";
            contents.Add(new GeminiContent
            {
                Role = role,
                Parts = [new GeminiPart { Text = message.Content ?? string.Empty }]
            });
        }

        return (systemInstruction, contents);
    }
}

public class GeminiRequest
{
    [JsonPropertyName("system_instruction")]
    public GeminiContent? SystemInstruction { get; set; }

    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

public class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

[JsonSerializable(typeof(GeminiRequest))]
[JsonSerializable(typeof(GeminiResponse))]
internal partial class GeminiJsonContext : JsonSerializerContext
{
}
