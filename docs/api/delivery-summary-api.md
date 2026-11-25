# Delivery summary API (LA-162)

Source availability: integrated with the LA-41 web/QA consumers in the released application. The original implementation and fake-provider/PostgreSQL checks below describe the API boundary; actual synthetic Gemini evidence is linked separately. Remaining activation/quality/load acceptance belongs to LA-348.

Implements `POST /api/delivery-orders/{id}/ai-summary` from [the Sprint 7 contract](history/sprint-7-api-contract.md#4-delivery-summary--la-162). No request body. Bearer authentication only. An active persisted CUSTOMER may summarize their own order; active DISPATCHER/MANAGER have the redacted project-wide scope established by that contract. ADMIN/STATION_OPERATOR are denied. This does not change customer-only order-detail authorization.

## Behavior

- Empty history returns `{ orderId, outcome: "NO_HISTORY", text: null, generatedAt: null }` without calling a model, even when generation is disabled.
- Generation returns `{ orderId, outcome: "GENERATED", text, generatedAt }`; text is plain English, at most 4,000 Unicode scalar values. All protected controller responses use `Cache-Control: no-store`.
- The model selects zero-based event indexes from chronological status facts. The server validates the selections and renders trusted phrases and UTC event times. Raw model prose is never published. First/latest event, failure/cancellation, recovery, arrival/delivery and repeated-status events cannot be omitted. ARRIVED explicitly means destination-station arrival awaiting pickup; DELIVERED means receipt confirmation.
- No reason category is currently allowlisted. The service omits all raw remarks, cancel reasons, actor identifiers, recipient/customer identity/contact, addresses, pickup codes, order/tracking IDs and telemetry from model input. It does not infer a reason, weather or promised arrival.
- The PostgreSQL reader uses a short repeatable-read snapshot and releases its transaction before inference. It rechecks persisted access, ownership, order status/UpdatedAt and every used log ID/status/time after inference. Source changes return `409`; lost access returns `401`/`403`/`404`, never candidate content. Remarks changes do not invalidate a summary because remarks are excluded from its facts.
- Generated summaries are transient: no summary/chat/status/audit/operational row is written. No migrations are required. This branch has no ETA-story dependency.

## Limits and failures

At most 200 chronological events enter inference. Excessive history or required narrative exceeding the text limit returns `503`; failure history is never silently truncated. Unknown status vocabulary and malformed/unsafe model selections return `503`. The authoritative order timeline remains available.

Inference and its source recheck have a 10-second deadline (`504`). Request cancellation propagates; it never fabricates a success. Missing configuration/provider failures return `503`, not successful empty results. A process-local account gate rejects simultaneous generation with `429` and `Retry-After: 5`; slots are released on every outcome. Multi-instance rollout requires a shared limiter before claiming an account-wide limit across replicas.

## Provider activation gate

`DeliverySummary:Enabled` defaults to false. The default local Ollama `/api/generate` adapter requires a loopback HTTP(S) `BaseUrl`, a configured `ModelName` and `ModelRevision`. URL credentials/query/fragment and redirects are disallowed. The explicit Gemini transport uses the same internal loopback request and sends to Google's fixed HTTPS endpoint with a server-side key. See [provider configuration](../deployment/gemini-ai.md); do not replace the loopback URL with a cloud URL. The structured index-selection boundary and existing grounding checks apply to both providers. No model is installed, downloaded or started automatically.

No model is enabled by this documentation. Before deployment, configure the chosen provider/model and validate representative lifecycle, failure/recovery, rescheduling and performance cases. For Gemini, revision records the configured provider model identifier rather than an immutable weights checksum. [Synthetic Gemini gateway/API checks](../deployment/LA-348-synthetic-acceptance-20261119.md) have passed in their recorded environment; they do not establish deployed UI, sustained availability or model quality. Chat retention policy is unrelated to this transient endpoint; no chat persistence is enabled.

## Verification

Behavior tests use the actual HTTP/auth/EF stack with a substituted structured selector. They cover persisted-role/ownership parity, no-history, privacy, failure/recovery/arrival, changed logs without order UpdatedAt changes, lost authorization/ownership, cancellation/deadline and account concurrency. The local HTTP adapter tests use a fake HTTP handler, not an actual model. PostgreSQL tests apply real migrations and verify numeric-ID chronology for tied timestamps, no transaction retained during model selection, no summary writes, and source-change rejection.

Run `dotnet test backend/SmartDroneDelivery.Tests --filter FullyQualifiedName~Summary`, setting `TEST_POSTGRES_CONNECTION` to a test PostgreSQL server to include the database test. Current backend, web and mobile CI remains the gate for future changes; the LA-41 source/consumer integration is already complete.
