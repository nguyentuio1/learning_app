# LA-348 synthetic Gemini acceptance — 2026-11-19

Base application commit: `a90668c1763dfa4d32ab04dc800510c9e4c22558` (`develop`).
Test harness: changes accompanying this document on `complete/LA348-acceptance-20261119`; no production code or activation configuration changed.
Successful live run: 2026-11-18 23:22:32–23:22:37 UTC (2026-11-19 06:22:32–06:22:37 Asia/Saigon).

## Executed evidence

The supplied server credential was recovered from the authorized current-session user message into the test child process only. Its value was not printed, committed or written to the report. All provider inputs were synthetic. No production customer conversation was sent. No billing change or Ollama download was started; no Ollama process was running when checked.

`LiveGeminiGatewayTests` plus `LiveGeminiApiAcceptanceTests`: **4 tests passed, 0 failed, 0 skipped** in the successful live run. They made six provider calls, each returning HTTP 200, `finishReason=STOP`, and `modelVersion=gemini-3.5-flash-lite` for requested model `gemini-3.5-flash-lite`.

| Check | Measured elapsed | Observed result |
| --- | ---: | --- |
| ETA gateway, 11.3 km / 500 g invented features | 1,049 ms | Validated integer duration 4 minutes |
| Summary gateway, three invented events | 913 ms | Indexes `[0,1,2]`; grounded chronological text and receipt confirmation |
| Assistant gateway, invented status question | 625 ms | Intent `Status`; no conversation persistence |
| Summary HTTP endpoint with synthetic owned order | 942 ms | `GENERATED`; awaiting pickup; no fixture pickup code in output |
| Assistant HTTP endpoint with synthetic owned order | 1,150 ms including deletion | `ANSWERED`; new synthetic session then deleted with 204; no fixture pickup code in output |
| ETA service with synthetic order and real provider | 883 ms | `Generated`; 16-minute validated prediction persisted in isolated in-memory store; confidence remains null |

The API fixture first sent foreign-order summary and assistant requests: both returned 404 and the provider call counter remained zero. Its final provider counter was three, one per successful service flow.

An earlier exploratory live run passed ETA and summary but the assistant response was rejected by the existing parser as incomplete/blocked. The finish reason was not captured in that first run, so the underlying provider reason is unknown. The parser did not fabricate a successful reply. The subsequent instrumented run passed without any production change. This observed intermittent failure remains an availability limitation; the successful samples do not establish reliable availability. The initial new API harness also returned 400 because its assistant request omitted the required explicit `sessionId`; the harness was corrected to send `sessionId: null` before the successful run.

Separately, **115 deterministic transport/API/pipeline tests passed with zero failures** on the same application base. Coverage includes unavailable-provider no-write behavior, Gemini quota preservation and no retry, unapproved cloud assistant zero-send gate, foreign-order/session isolation, grounded replies, sensitive-input refusal, deletion, default-off behavior, deadline cancellation, slot release and account isolation. These checks use synthetic in-memory API fixtures and controlled gateways; they are not actual provider failure or load tests.

## Reproduce safely

Supply `TEST_GEMINI_API_KEY` only in the test process and optionally `TEST_GEMINI_MODEL=gemini-3.5-flash-lite`, then run:

```text
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj --filter FullyQualifiedName~LiveGemini --logger "console;verbosity=detailed"
```

The tests deliberately skip if the key is absent. Test-only assistant policy configuration is confined to the synthetic fixture and does not authorize deployment. Provider provenance logging records status, finish reason, model identifier and elapsed time; it does not log prompts, credentials or complete responses.

Local ignored evidence in the acceptance worktree: `artifacts/ai-acceptance/live-gemini-real.trx`, `live-gemini-real.log`, and `ai-acceptance.trx`. The TRX timestamps and individual test results are the source for this report. The successful live log superseded the exploratory log; the exploratory failure is retained explicitly above and was reported during execution.

## Acceptance still requiring evidence or an owner decision

- Real customer cloud chat remains disabled. The [decision package](LA-348-cloud-policy-decision.md) contains the proposed data flow, notices, retention terms and exact approvals still missing.
- Six successful generation samples below three seconds do not prove any percentile, sustained three-second requirement, concurrency target or project quota capacity. No actual provider concurrent-load pass is claimed.
- ETA bounds/grounding checks do not prove prediction accuracy. There is no labeled route-duration evaluation here.
- The API fixture uses real routing/auth/service code with an in-memory database. PostgreSQL transaction/restore behavior, deployed UI, real backup expiry and customer end-to-end acceptance are outside this run.
- Deployment topology and a shared account limiter must be resolved before multi-instance assistant activation.

These results complete a concrete synthetic three-service technical exercise. They do not complete cloud-policy approval, production activation, performance acceptance or all LA-348 acceptance criteria.
