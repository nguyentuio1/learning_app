# Gemini web demo and bounded recovery

Executed 25–26 November 2026 (Asia/Ho_Chi_Minh). Application base:
`17417a07600387e6353cf98abf9a4d33030cf213`, plus the opt-in recovery changes
accompanying this record. Final source revision and required CI belong to the PR.

## Environment and activation

Real React/Vite web at loopback port 5173, API at 5055, PostgreSQL/MinIO, and
Gemini `gemini-3.5-flash-lite`. Only seeded synthetic accounts, orders and example
questions were used, in a separate `la348_web_demo_20261125` database. Existing
application databases were unchanged. The owner requested completing the web AI
task and supplied a credential; it was stored in .NET user-secrets, outside Git
and frontend configuration. Production customer cloud processing remains outside
this synthetic demonstration's authorization.

The initial baseline had generation and composer flags disabled: the owned-order
assistant POST returned 503 and the browser displayed the disabled notice with
no Send button. Explicit server provider/model/service configuration and the web
composer flag enabled the real flow. The local startup script was executed and
web/API readiness checked after restart. Configuration instructions are in
[the deployment guide](../../deployment/gemini-ai.md).

## Actual observations

| Check | Result and boundary |
|---|---|
| Web assistant, before recovery patch | HTTP 200 `ANSWERED`, 1,406 ms; stored USER/BOT pair and grounded pending-review reply displayed |
| Web summary, same run | HTTP 200 `GENERATED`, 1,969 ms; summary matches the recorded pending event |
| Web ETA | HTTP 200 displays persisted 11.28 km / 16-minute predictions for eligible PENDING/APPROVED fixtures; completed fixture has no active prediction |
| Earlier web samples | Chat 503 and summary 504 occurred; these are retained failures, not successful generations |
| Web with bounded recovery enabled | Chat still returned 503 after 3,766 ms; summary succeeded in 1,438 ms; ETA displayed the existing cached prediction, not a new inference |
| Live API recovery probe | One summary recovered from upstream 503 to 200; the following assistant exhausted both 503 attempts and remained unavailable |
| Provider diagnostic | A separately listed `gemini-3.1-flash-lite` generation returned HTTP 503 / `UNAVAILABLE`, reporting high demand; `gemini-3.5-flash` timed out after 15 seconds. Neither alternative was activated |
| Output-budget experiment | Four classification calls returned 200/STOP, including one taking 10,984 ms; increasing output tokens did not establish a latency fix |

Local ignored evidence under `artifacts/la348-web-runtime/` includes
`baseline.json`, `disabled-baseline.png`, `enabled-web-before-recovery.json`,
`enabled-web.json`, the three `enabled-*.png` screenshots, `live-tests.log`,
earlier retained failure logs, `assistant-token-probe.json`, and
`provider-model-probes.json`. Screenshots were visually inspected. Artifacts
are local execution evidence, not files guaranteed to exist in a fresh clone.

## Regression protection and reproduction

```text
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj --filter "FullyQualifiedName~GeminiModelHandlerTests|FullyQualifiedName~GeminiRecoveryApiTests"
```

The 29 deterministic cases pass. They cover fresh identical requests, successful
recovery, two-call exhaustion, original default-off behavior, auth/quota errors,
`Retry-After`, malformed/blocked output, caller cancellation, stalled headers,
stalled successful response body, body disposal, and a stalled final attempt.
The API regressions verify foreign-order rejection before any provider call,
exactly one owned session/two messages after recovery, no pickup-code/contact
leakage, and zero partial sessions/messages after exhaustion. The first observed
run against the missing recovery behavior failed all three transient-status cases
and the two-call expectation before implementation.

For opt-in real-provider tests, supply `TEST_GEMINI_API_KEY` to the test process
and `TEST_GEMINI_RETRY_ENABLED=true`, then run:

```text
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj --filter FullyQualifiedName~LiveGemini --logger "console;verbosity=detailed"
```

The live API fixture reports logical generations separately from actual provider
attempts (three to six with recovery); its foreign-order checks still require
zero provider calls. The measured recovery run was **2 passed / 2 failed**, not
an all-green live suite. Unit tests and required CI do not replace provider
availability or runtime acceptance.

## Remaining acceptance boundaries

The app is configured and has produced real web answers, summaries and ETA
predictions. Optional recovery improves individual transient failures; it cannot
guarantee provider capacity. There is no fabricated answer, automatic alternate
model, quota retry, billing change, or longer application deadline.

LA-348 remains open for reliable service/performance acceptance and any intended
real-customer cloud/retention rollout. The mixed measurements do not establish a
three-second percentile, sustained concurrency, prediction accuracy, native
runtime, real backup expiry or permission to send real customer text. Preserve
the [cloud decision boundary](../../decisions/ai-cloud-processing.md) and the
[earlier gateway/API record](gemini-synthetic-acceptance.md).
