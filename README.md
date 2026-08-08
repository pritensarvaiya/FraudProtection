Hackathon link: https://notepad.pw/hackthons

# ScamShield AI — Backend

Multilingual digital fraud protection API. Analyses suspicious messages, emails, URLs, and
screenshot text, and returns an explainable risk assessment with bilingual (English + Hindi)
safety guidance.

Built with **ASP.NET Core 8** and **Microsoft Semantic Kernel**, orchestrating **Google Gemini**
as the underlying LLM.

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full system design.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Google Gemini API key ([Google AI Studio](https://aistudio.google.com/app/apikey))

## Setup

1. Clone the repo and restore dependencies:

   ```bash
   dotnet restore
   ```

2. Configure your Gemini API key. **Never commit real keys to `appsettings.json`.** Use one of:

   **Option A — user-secrets (recommended):**
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-api-key>"
   ```

   **Option B — `appsettings.Development.json`** (already gitignored):
   ```json
   {
     "Gemini": {
       "ApiKey": "<your-gemini-api-key>",
       "Model": "gemini-3.1-flash-lite"
     }
   }
   ```

3. Run the API:

   ```bash
   dotnet run --urls "http://localhost:5199"
   ```

4. Open Swagger UI at `http://localhost:5199/swagger` to explore the API interactively.

## API Endpoints

| Method | Route | Description |
|---|---|---|
| `POST` | `/api/fraud/analyze` | Analyze a message/email/URL/screenshot text for fraud risk |
| `GET` | `/api/fraud/history` | List all analyzed cases (most recent first) |
| `GET` | `/api/fraud/history/{caseId}` | Get a single analyzed case |
| `GET` | `/api/health` | Health check (no AI dependency) |

### Example request

```bash
curl -X POST http://localhost:5199/api/fraud/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "inputType": "Message",
    "content": "URGENT: Your bank account will be suspended. Click here to verify: http://bit.ly/fake and enter your OTP."
  }'
```

### Example response

```json
{
  "caseId": "1d626d2609a0",
  "riskLevel": "High",
  "riskScore": 95,
  "summary": "This message is a classic phishing attempt...",
  "evidence": [
    { "snippet": "URGENT: Your bank account will be suspended.", "reason": "Creates artificial urgency.", "pattern": "Urgency" }
  ],
  "recommendedActions": ["Do not click the link.", "Never share your OTP."],
  "guidanceEnglish": "Banks will never ask you to verify your identity via a text link...",
  "guidanceHindi": "बैंक कभी भी आपसे टेक्स्ट मैसेज के लिंक के जरिए...",
  "analyzedAtUtc": "2026-08-08T04:06:09Z"
}
```

## Testing

`scripts/test-matrix.sh` runs an end-to-end verification matrix against a running instance of the
API: one **fraud (positive)** and one **safe (negative)** example for each of the four input types
(Message, Email, Url, Screenshot) — 8 cases total, covering the full
Semantic Kernel → Gemini → risk-mapping pipeline in one pass.

```bash
# with the API already running on http://localhost:5199
bash scripts/test-matrix.sh

# or against a different host/port
BASE_URL=http://localhost:5000 bash scripts/test-matrix.sh
```

Expected outcome — the fraud cases should classify `High` (score ~90+) and the safe cases should
classify `Low` (score under ~20), with `evidence` snippets that are real substrings of the input
you sent, not invented text:

| # | Input type | Scenario | Expected |
|---|---|---|---|
| 1 | Message | Fake SIM/KYC-block phishing | High |
| 2 | Message | Casual meeting confirmation | Low |
| 3 | Email | Fake delivery-fee scam impersonating a courier/retailer | High |
| 4 | Email | Legitimate invoice email | Low |
| 5 | Url | Typosquatted login-phishing domain | High |
| 6 | Url | Reputable public website | Low |
| 7 | Screenshot | Lottery/advance-fee scam text | High |
| 8 | Screenshot | Innocuous social DM | Low |

This is a manual smoke test, not an automated test suite — there is no CI wiring or assertions;
read the printed JSON per case and compare `riskLevel`/`riskScore` against the table above.

## Configuration reference

| Key | Description | Default |
|---|---|---|
| `Gemini:ApiKey` | Google Gemini API key (required) | — |
| `Gemini:Model` | Gemini model name | `gemini-3.1-flash-lite` |

## Known limits

- **History is in-memory only** — analyzed cases are lost on app restart (no database in this MVP).
- **No authentication** — CORS is wide open (`AllowAnyOrigin`) for demo purposes; add auth before any
  real deployment.
- **Single LLM provider** — Gemini only; no fallback provider is wired in.
- Risk assessments are **guidance, not certainty** — the AI is explicitly prompted to avoid absolute
  claims, but false positives/negatives are possible like any LLM-based classifier.

## Team contributions

| Name | Role | Contribution |
|---|---|---|
|  |  |  |
|  |  |  |
|  |  |  |
