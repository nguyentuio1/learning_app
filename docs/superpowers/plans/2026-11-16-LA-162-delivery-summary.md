# LA-162 delivery summary implementation plan

> Execute inline using superpowers:executing-plans and test-driven-development.

**Goal:** Implement the transient delivery-summary POST specified by LA-342.
**Architecture:** Separate persisted authorization/frozen timeline reader, bounded generator, controlled factual renderer, and HTTP boundary. Local Ollama selects event indexes; the server validates them and renders trusted status phrases and UTC times. Raw model prose never becomes the response. No generation transaction or summary storage. Independent of unfinished ETA story.
**Tech stack:** ASP.NET Core 8, EF Core 8, PostgreSQL, xUnit, HttpClient.
**Spec:** docs/api/history/sprint-7-api-contract.md section 4 and docs/design/sequence-ai-delivery-summarization.md.

## Constraints and review focus

- Owning active CUSTOMER or active DISPATCHER/MANAGER; check persisted role before generation and before returning. JWT only.
- Frozen source includes customer ownership, current status, order UpdatedAt and every ordered log ID/status/time. Snapshot reads use PostgreSQL repeatable-read; release transaction before model call.
- No remarks, names, contact, pickup codes, actor IDs or order IDs enter the model. Reasons remain unknown (empty allowlist).
- Preserve first/latest event, every failure/cancellation, all recovery transitions from FAILED and arrival/delivery events. Reject unknown statuses, excessive history (>200), invalid selections, or overlong rendered text; never silently truncate failure history.
- 10-second deadline, cancellation propagation, per-account concurrency rejection with Retry-After 5. Limiter is process-local; multi-instance rollout requires shared limiting.
- Model disabled by default, loopback only, redirects disabled, bounded output. No model installation or activation; model accuracy is separate acceptance.

## Tasks

- [x] Write HTTP behavior tests for authorization, no-history, chronology/privacy, stale-source/access rechecks and no writes. Observed 8 RED against missing endpoint, then 9 GREEN.
- [x] Implement snapshot reader, bounded service, factual renderer, controller, options and local adapter. Observed generation and adapter RED, then GREEN.
- [x] Add hostile/invalid local-output, cancellation/deadline/concurrency and PostgreSQL snapshot tests. Observed missing READY_FOR_TAKEOFF and provider-error RED; final focused run: 39/39 passed, no skips, including PostgreSQL. Independent review finding fixed and delta review clean.
- [ ] Require full PostgreSQL backend suite and web/mobile CI on current PR head. Commit under LA-162, PR into story/LA-41; merge own PR with authorized admin only if needed. Whole story into develop only after required consumers/QA acceptance. CI is the complete-suite gate; local validation is focused to avoid redundant full runs.
