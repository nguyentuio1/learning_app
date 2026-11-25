# ETA prediction API and producer (LA-158)

Availability: source is integrated in the released application baseline, including the LA-40 consumers. The original backend implementation was PR #210. Read stored predictions through `GET /api/delivery-orders/{id}/eta`; CUSTOMER access is owner-only, DISPATCHER/MANAGER have the allowed staff scope, and ADMIN/STATION_OPERATOR are denied. The [historical read-slice verification](history/eta-read-verification.md) preserves the earlier partial implementation evidence. Integration does not establish deployment or prediction accuracy; remaining enabled-AI acceptance is tracked in LA-348.

## Implemented behavior

The opt-in worker visits bounded pages of PENDING/APPROVED orders that have neither a departure schedule nor mission history. A model receives only full-route distance, package weight/fragility, configured cruise speed and explicit unknown weather. No order/customer ID, station name, recipient/contact, address, pickup code or free-form message is sent.

The default adapter uses the local Ollama structured generate protocol, requires a loopback endpoint, disables redirects and bounds both output size and inference time. The explicit Gemini transport can instead convert the same internal request to Google's fixed HTTPS endpoint; see [provider configuration](../deployment/gemini-ai.md). Do not change the loopback BaseUrl validators to select Gemini. Model name/revision must be configured; no model is installed/downloaded automatically. Duration must be an integer from 1 to 1440 minutes and cannot undercut full-route distance divided by configured cruise speed. Weather and confidence remain null: model-generated claims are not verified observations or calibrated confidence.

Calculation runs outside a transaction. Before persistence, the producer locks the order, package and stations, rereads the complete input generation and checks the existing prediction version. Changed inputs, a concurrent schedule or an already committed competing prediction discard the result. The write is one transaction and never changes the order ETA, status, mission, slot or notification. A failed model or write does not freshen an old prediction.

Additive migration `20261116100155_AddEtaPredictionInputs` introduces nullable InputFingerprint and ModelVersion. New stamped predictions are valid scheduling inputs only while current route/package/speed features match. Legacy unstamped rows retain their existing interpretation. The scheduler still anchors absolute arrival at scheduled departure; the producer does not fabricate an anchor before a schedule exists.

## Configuration

Default behavior is disabled. Example explicitly showing that default:

```json
{
  "EtaPrediction": {
    "Enabled": false,
    "BaseUrl": "http://127.0.0.1:11434",
    "ModelName": "",
    "ModelRevision": "",
    "IntervalSeconds": 30,
    "BatchSize": 25
  }
}
```

For Ollama, enable only after installing/selecting a local model, pinning its revision and reviewing the model/input/accuracy policy. For Gemini, follow the explicit server-side provider/key/model configuration in the provider guide. Configuration accepts intervals 5–3600 seconds and batches 1–100. An enabled configuration with missing model metadata or a non-loopback internal endpoint fails startup. This document does not activate or deploy a provider.

`ModelRevision` is configured metadata stored with a prediction. The adapter does not inspect a local model's digest or prove that a mutable model name resolves to that revision. For local deployment, verify/pin the actual model artifact and record evidence. For Gemini, the revision records the configured provider identifier, not an immutable weights checksum; preserve returned modelVersion in live verification. Neither label proves accuracy. The [synthetic Gemini acceptance](../testing/acceptance/gemini-synthetic-acceptance.md) records successful ETA calls separately from model-quality/load acceptance.

## Acceptance still separate

Verification on 2026-11-16: **975 passed, zero failed/skipped** in the complete backend suite against local PostgreSQL. Local xUnit collections were run sequentially using RunSettings to avoid CPU-driven wall-clock timeouts; production's 10-second inference deadline is unchanged. Focused tests had independently demonstrated missing behavior, stale scheduling inputs, invalid legacy coordinates and an impossibly short duration before their fixes. All inference tests use a fake model/HTTP handler; no actual model was activated.

- Actual model selection and prediction-quality evaluation against representative deliveries.
- Weather observations and remaining-flight informational predictions; neither is invented or written into the shared full-route row.
- Rescheduled orders with mission history are conservatively excluded from production; stamped stale inputs fall back to the existing route estimate.
- LA-40 consumers/QA are integrated; future changes still require reviewed integration and current CI. The original 16 November test counts above are historical, not a new run.

Independent review found no important/critical issue after the legacy-coordinate guard. Deferred minor: an unexpected exception in one worker order aborts the rest of that batch; a persistent failure in a stable page could delay later orders. Expected model failures are handled as unavailable, not exceptions.
