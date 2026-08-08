# ScamShield AI — Architecture

## Overview

ScamShield AI is a multilingual fraud/scam detection assistant. A user submits a suspicious
message, email, URL, or screenshot's text; the backend runs it through Google Gemini via
Microsoft Semantic Kernel, and returns a structured, explainable risk assessment with safety
guidance in English and Hindi.

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 Web API (C#) |
| AI orchestration | Microsoft Semantic Kernel 1.30 |
| LLM | Google Gemini (`gemini-2.0-flash` by default), called via a hand-written `IChatCompletionService` over Gemini's REST API |
| Frontend | React 19 + Vite (plain JS/JSX) |
| History storage | In-memory (`ConcurrentDictionary`), per MVP scope — no database |
| API docs | Swagger / OpenAPI (Swashbuckle) |

## Why a hand-written Gemini connector

Semantic Kernel does not ship a stable, officially-supported Gemini connector — the only
Microsoft package for Google models (`Microsoft.SemanticKernel.Connectors.Google`) has stayed in
`1.x-alpha` for its entire lifetime. Rather than depend on an experimental package, the backend
implements `IChatCompletionService` directly against Gemini's `generateContent` REST endpoint
(`AiModel/GeminiChatCompletionService.cs`). This is a thin adapter: it converts a Semantic Kernel
`ChatHistory` into Gemini's `contents`/`system_instruction` request shape, and converts the
response back into `ChatMessageContent`. The rest of the app talks to `IChatCompletionService` and
the `Kernel`, and has no direct dependency on Gemini's wire format — swapping providers later means
writing one new adapter class, not touching controllers, services, or plugins.

## Request flow

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant C as FraudController
    participant S as FraudAnalysisService
    participant K as Semantic Kernel
    participant P as FraudAnalysisPlugin
    participant G as GeminiChatCompletionService
    participant Gemini as Gemini REST API
    participant H as HistoryService (in-memory)

    U->>C: POST /api/fraud/analyze { inputType, content }
    C->>S: AnalyzeAsync(request)
    S->>K: InvokeAsync("FraudAnalysis", "analyze_content", args)
    K->>P: analyze_content(inputType, content)
    P->>G: GetChatMessageContentsAsync(chatHistory)
    G->>Gemini: POST /v1beta/models/{model}:generateContent
    Gemini-->>G: candidate text (JSON risk assessment)
    G-->>P: ChatMessageContent
    P-->>K: raw JSON string
    K-->>S: raw JSON string
    S->>S: parse JSON, clamp score, map risk level
    S->>H: AddCase(analyzedCase)
    S-->>C: AnalyzeResponse
    C-->>U: 200 OK { riskLevel, riskScore, evidence, guidanceEnglish, guidanceHindi, ... }
```

## Component map

```mermaid
flowchart LR
    subgraph Frontend["Frontend (React + Vite)"]
        Form[AnalyzeForm]
        Result[RiskResult]
        History[HistoryList]
        Api["services/api.js"]
    end

    subgraph Backend["Backend (ASP.NET Core 8)"]
        FC[FraudController]
        HC[HealthController]
        FS[FraudAnalysisService]
        HS[HistoryService — in-memory]
        Kernel[Semantic Kernel]
        Plugin[FraudAnalysisPlugin]
        Gemini[GeminiChatCompletionService]
    end

    GeminiAPI[(Google Gemini REST API)]

    Form -->|"POST /api/fraud/analyze"| Api
    Api -->|fetch| FC
    History -->|"GET /api/fraud/history"| Api
    FC --> FS
    FS -->|"InvokeAsync"| Kernel
    Kernel --> Plugin
    Plugin -->|"GetChatMessageContentsAsync"| Gemini
    Gemini -->|HTTPS| GeminiAPI
    FS -->|"AddCase / GetCases"| HS
    FC -.->|reads| HS
    Result -.->|renders| Api
```

## Layering rules

- **Controllers** (`Controllers/`) — HTTP concerns only: model binding, status codes, no business logic.
- **Services** (`Services/`) — orchestration and business rules. `FraudAnalysisService` invokes the
  Kernel and maps the AI's JSON output into the API's response DTOs; `HistoryService` is a plain
  in-memory store behind an interface so it can be swapped for a real database later without
  touching controllers.
- **Plugins** (`Plugins/`) — Semantic Kernel functions. `FraudAnalysisPlugin.analyze_content` is the
  only Kernel function; it owns the system prompt and talks to `IChatCompletionService` directly for
  the actual LLM call, then hands back raw JSON for the service layer to parse.
- **AiModel** (`AiModel/`) — the LLM provider adapter (`GeminiChatCompletionService`). This is the
  only place that knows Gemini's REST shape.
- **Models** (`Models/`) — plain DTOs and enums shared across layers.

`HealthController` deliberately has **no dependency on `Kernel` or `IChatCompletionService`** — it
was split out from `FraudController` specifically so `/api/health` keeps working even if the Gemini
API key is missing or invalid. ASP.NET Core resolves all constructor dependencies for a controller
before any action runs, so a health check sharing a controller with AI-dependent actions would fail
if the AI provider was misconfigured.

## Credentials

| Secret | Where it lives | Notes |
|---|---|---|
| `Gemini:ApiKey` | `appsettings.Development.json` (gitignored) or `dotnet user-secrets` | Never committed. `appsettings.json` only holds an empty placeholder. |

## Known limits

- In-memory history — no persistence across restarts (acceptable for this MVP; see README).
- No authentication/authorization on any endpoint.
- Single-provider (Gemini only), no automatic fallback if Gemini is unavailable.
- The AI's JSON output is trusted but validated: if Gemini returns malformed JSON, the plugin falls
  back to a fixed "Suspicious" assessment rather than crashing the request.
