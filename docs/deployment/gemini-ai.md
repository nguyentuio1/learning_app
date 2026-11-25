# Gemini-backed AI services (LA-348)

The application can use the Gemini Developer API instead of a local Ollama runtime.
`ModelProvider:Provider` defaults to `Ollama`, so existing loopback behavior stays
unchanged until explicitly configured. All per-service generation flags remain
default-off. Gemini requests go only to the fixed HTTPS Google endpoint; the
server sends the API key in a header, never a URL or a frontend bundle.

## Local configuration

Use environment variables or .NET user-secrets for the API process:

```text
ModelProvider__Provider=Gemini
ModelProvider__GeminiApiKey=<server-side secret>
EtaPrediction__Enabled=true
EtaPrediction__ModelName=gemini-3.5-flash-lite
EtaPrediction__ModelRevision=gemini-3.5-flash-lite
DeliverySummary__Enabled=true
DeliverySummary__ModelName=gemini-3.5-flash-lite
DeliverySummary__ModelRevision=gemini-3.5-flash-lite
```

The existing loopback BaseUrl is the canonical internal generation request; the
explicit Gemini transport converts it to Google's structured `generateContent`
API. Do not change the existing BaseUrl validators to accept arbitrary external
addresses. ModelRevision records the provider model identifier used for the
deployment; it is not a local weights checksum or a promise that Google will
keep the model unchanged. Record `modelVersion` from live validation as evidence.

For Docker, put the key in an uncommitted `.env` and explicitly set
`AI_ETA_ENABLED=true` / `AI_SUMMARY_ENABLED=true`, then run:

```text
docker compose -f docker-compose.yml -f docker-compose.gemini.yml up -d --build --wait
```

The overlay refuses to load without the key and forwards it only to `api`.
It does not start/download Ollama, enable billing or silently enable chat.
Changing a frontend build flag requires rebuilding the web image.

`dotnet run` does not automatically load the repository's `.env` file. That file
is consumed by Compose; its key reaches the API only when the Gemini overlay is
included. For a local .NET process, set the `ModelProvider__*` environment values
above or use .NET user-secrets. A variable named `GEMINI_API_KEY` alone is not
the API's `ModelProvider:GeminiApiKey` setting. The web composer independently
needs `VITE_AI_ASSISTANT_SEND_ENABLED=true` at build/start time; that flag does
not configure the server or grant cloud-processing permission. Never put the
provider key in a `VITE_*` variable.

## Inputs, failures and quotas

ETA sends numeric route/package features, not contact information. Summary sends
status transitions and timestamps, not names, addresses or pickup codes. Existing
ownership checks and grounded renderers remain authoritative: the model cannot
change an order or invent operational facts. Common operations remain independent
of inference; ETA runs in its bounded worker and summary/chat retain their existing
generation deadlines. A single fast provider response does not prove the 3-second
requirement or concurrency acceptance.

The adapter requests schema-constrained JSON and rejects incomplete/safety-blocked
generation or oversized responses. Quota/auth errors are not converted into fake
successful AI output; the existing service reports unavailable/timeout. Automatic
recovery is off by default. A deployment may explicitly enable
`ModelProvider__GeminiRetryTransientFailures=true` (Compose:
`AI_GEMINI_TRANSIENT_RETRY_ENABLED=true`). This permits at most two inference
attempts, each bounded to four seconds including response-body parsing, separated
by 200 ms. Only HTTP 502/503/504 or a first-attempt timeout permit a second call.
The existing ten-second application deadline and caller cancellation still apply.
Auth/quota errors, malformed or blocked output, and responses with `Retry-After`
are not retried. Each attempt uses the same model and bounded payload; a retry
may consume an additional provider request, even if the cancelled request was
already processed remotely. Application writes and conversation commits are
never replayed by this transport. There is no automatic model substitution.
The free tier has model/project-specific limits and is not an unlimited
availability guarantee. Check the actual project's limits in AI Studio.

## Customer-assistant activation

**Cloud assistant policy `LA-348-gemini-retention-v1` is a DRAFT, not approved.**
The existing `LA-165-retention-v1` proposal assumed a local provider and cannot by
itself authorize sending customer-written text/history to Google. Free-tier
content may be used to improve Google's products under the applicable terms.
Only synthetic, non-personal gateway tests have been authorized and performed.

Before enabling stored customer conversations, the deployment owner must approve
the cloud data-use arrangement and update the user notice, confirm actual backup
expiry/restore cleanup, approve retention (30-day active sessions and explicitly
configured backup retention of 1–30 days), and decide which messages may leave
the server. Then configure **both** the existing assistant policy controls and
`ModelProvider:GeminiAssistantApprovedPolicyVersion` with the approved cloud
policy version. Without the extra marker the transport sends no assistant text.
Merely possessing a working key or passing synthetic tests is not this approval.
The web send composer remains off until its separate build flag is enabled.

## Reproducible verification

Unit transport tests run without network or credentials:

```text
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj --filter FullyQualifiedName~GeminiModelHandlerTests
```

Set `TEST_GEMINI_API_KEY` in the test process, optionally `TEST_GEMINI_MODEL`, for:

```text
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj --filter FullyQualifiedName~LiveGeminiGatewayTests --logger "console;verbosity=detailed"
```

These opt-in tests exercise actual ETA, summary and assistant gateway parsers with
synthetic data, including test-only policy configuration. They create no stored
conversation and do not establish model quality, production approval or end-to-end
UI acceptance. Record success/failure, model, latency and tested SHA; never include
keys or tokens. Older model IDs can appear in model listings yet reject generation
for new users, so validate generation with the selected project rather than relying
only on list-models.

References: [Google pricing](https://ai.google.dev/gemini-api/docs/pricing),
[rate limits](https://ai.google.dev/gemini-api/docs/rate-limits),
[terms](https://ai.google.dev/gemini-api/terms), and the repository's
[original retention proposal](../decisions/assistant-retention-policy.md).
