# LA-158 full-route prediction pipeline

Continue the verified read slice in PR #210. The user requested continued implementation and authorized admin merges for their own eligible PRs; this does not permit merging unfinished stories or teammates' PRs through bypass.

## Technical choices

Builder choices: full-route/full-flight duration only; generate for PENDING or APPROVED orders with no departure schedule and no mission history. Never change a reserved flight, station slot or an absolute arrival timestamp without an agreed anchor. Scheduling derives arrival from departure plus the accepted full-flight duration as before. Terminal/in-flight reads continue using the stored order timeline.

Use a local Ollama adapter with explicitly configured model and immutable model revision. It remains disabled by default; do not download a model, call a provider, transfer customer data or claim accepted model accuracy. Send only route distance, package weight/fragility, configured cruise speed and explicit unknown weather. Local endpoint must be loopback and redirects are disabled. Validate structured duration output; confidence stays null because an LLM's self-reported confidence is not calibrated.

Persist an additive input fingerprint and model revision on new predictions. Fingerprint covers route IDs/coordinates, package ID/weight/fragility and configured cruise speed. Compare source order version/status and the full input fingerprint after generation. Serialize final writes on the order, lock package/station inputs, reject a competing prediction version, and atomically save only the new full-route prediction. No database lock spans generation. The scheduling consumer rejects stamped predictions whose current inputs differ; preserve legacy null-stamp behavior for existing tests/data.

The worker is opt-in and processes bounded batches. Unconfigured/failed generation never refreshes calculation time or changes operational state. No recalculation HTTP route is introduced. Generation policy/model accuracy and later in-flight informational predictions remain acceptance gates, explicitly distinct from completing the backend pipeline.

## Steps

1. Add failing pipeline tests for successful persistence, missing/failed model, invalid output, changed package/route/order, scheduled/terminal/mission guard, competing calls and current scheduling inputs. Introduce interfaces only as necessary to compile the tests; observe behavior failures before implementation.
2. Implement minimized features/fingerprints, production transaction, additive EF migration, and scheduling validation. Test real PostgreSQL upserts/rollback and input locks.
3. Add local structured-output adapter and opt-in worker; test request minimization, disabled defaults, malformed responses and cancellation. Do not activate actual inference.
4. Independent review, relevant tests plus complete backend regression, update docs/Jira and extend PR #210 through merge commits. Require current-head backend/web/mobile CI before any integration. Keep incomplete LA-40 out of develop.

## Record

- Contract PR #209 merged into remote develop at 88ef069 after user explicitly authorized admin for own PRs; contract commit ancestry verified.
- Starting branch contains remote develop plus the read slice e2fe057 through a merge commit. Unrelated checkout changes remain untouched.
- Pipeline RED: 14 missing-behavior failures, then 14 GREEN. Scheduling input-stamp regression failed (18 instead of independently expected fallback 16), then passed after consumer validation. Adapter tests observed RED before implementation; corrected an overly exact exception-type assertion to allow JsonReaderException, a JsonException subtype.
- Focused backend/real PostgreSQL run: 44 passed, zero skipped. Includes migration, calculation outside transactions, rollback after insert, competing initial predictions and station edits without order timestamp changes. Invalid legacy coordinates separately reproduced RED (Generated instead of Ineligible) and guarded before inference.
- Independent review: no important/critical findings beyond the owned coordinate guard. Deferred minor: unexpected worker-order exceptions can abort remaining items in that batch.
- Final producer regression: 975 passed, zero failed/skipped with real PostgreSQL and serial local xUnit collections (official RunSettings support). Production inference deadline remains 10 seconds. A prior parallel local run had 972 pass/two timing failures (existing SignalR handshake and a concurrency test that received Unavailable on deadline rather than Superseded). The concurrency assertion now accepts either safe discard outcome while still requiring the committed winning prediction to remain unchanged.
- Additional safety regression: a model duration of one minute for an 11.12 km route at 12 m/s reproduced RED; generation and scheduling now reject stamped durations below the route's configured cruise-time floor. This is a conservative builder choice, not accepted model accuracy.
