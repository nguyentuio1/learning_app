# LA-165 customer assistant implementation plan

> Execute inline using superpowers:executing-plans and test-driven-development.

**Goal:** Implement the LA-342 assistant HTTP contract, owned history, grounded answers and atomic exchanges, with persistence/generation disabled until retention approval.
**Architecture:** Persisted active CUSTOMER authorization; independent ownership checks for every explicit session/order; bounded validated inputs; model selects an allowlisted intent and server renders current facts. Existing AiChatSession/AiChatMessage store an atomic successful pair only. No raw model prose, operational tools, external provider, ETA-story or summary-story dependency.
**Stack:** ASP.NET Core 8, EF Core 8/PostgreSQL, xUnit; local structured Ollama adapter.
**Spec:** docs/api/sprint-7-api-contract.md section 5; docs/design/sequence-ai-customer-assistant.md.

## Constraints and review focus

- JWT only; recheck persisted ACTIVE CUSTOMER, session and order ownership before input/context access and after generation. Missing/foreign resources have identical 404.
- Require all three request fields; reject extra members. Trim message; 1–2,000 Unicode scalars; request body <=32 KiB. Reject secret-bearing content without echoing/logging/persisting it.
- Read at most latest20 owned history messages, chronological, <=12,000 scalars; discard oldest whole messages. Prior BOT text is never factual authority. Exclude secrets/unsafe legacy history from model context.
- Current order facts are redacted and frozen; model returns only STATUS/ETA/HISTORY/HELP/CLARIFY/REFUSE. Server-created replies <=4,000 scalars; ARRIVED differs from DELIVERED. Unknown ETA/reasons remain unknown.
- Model deadline10 seconds, request cancellation propagation; same-account concurrency429 + Retry-After5. Single-instance limiter; shared limiter is a rollout gate for replicas.
- No transaction spans inference. Final short PostgreSQL transaction locks current user/session/order, rechecks frozen source/history, and atomically commits new session if needed plus USER/BOT pair. Provider/access/source/database failure commits no successful exchange. No auto-retry/idempotency claim.
- Persisted IDs are decimal strings; owned session list newest-first and message history globally oldest-first before pagination. Reads create nothing. Titles remain null.
- Retention proposal: session lifetime30 days from creation, user deletion removes active DB session/messages immediately, hourly cleanup, backups <=30 days with restore cleanup before serving. Local provider payloads must not be retained/trained/logged. Default approved-policy marker empty; no storage/generation or cleanup before approval. Prepare concrete proposal and implementation before requesting approval to enable.

## Tasks

- [x] HTTP TDD: missing API21 RED, then22 GREEN; required fields/input bounds, persisted roles, owned list/history bigint ordering/pagination, missing/foreign parity and static nonpersisted responses.
- [x] TDD: generation/model/retention guards observed RED then GREEN. Independent review address/privacy finding fixed with RED/GREEN regression; receipt-status questions remain supported. All76 focused cases passed with no skips.
- [x] PostgreSQL: real migrations, no inference transaction, source/authorization/history races, post-BOT-insert rollback, owned deletion and retention cleanup. Genuine failure injector attachment verified. No unrelated operational writes.
- [ ] Independent review, focused verification and full current-head backend/web/mobile CI; own PR into LA-42 with authorized admin only when all checks pass. Verify remote ancestry and tested/merged tree equality. No whole-story Done/develop merge until required consumer/QA acceptance.
- [ ] Reconcile Jira and document policy/rollout gates; continue LA-173 integration verification and LA-252 owned documentation.
