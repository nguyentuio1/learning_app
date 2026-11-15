# LA-156 external integration API

Implement LA-341 section 6 for LA-39, using native execution and a fresh whole-branch security/code review. Candidate scope/key/rate policies remain separate from business acceptance.

1. RED HTTP contract tests for admin provisioning/revocation, one-time plaintext, bound-customer scopes/expiry/current eligibility, first-party credential isolation, ownership and unknown fields, create/read/tracking, rate-limit headers and Swagger.
2. Persist integration-key metadata with a unique SHA-256 hash of a 256-bit random secret, never plaintext. Add an EF migration and atomic metadata audits. Existing JWT auth remains the first-party default; explicit API-key auth is only attached to external controllers. Bound identity is derived on the server, with fresh persisted eligibility checks per request. Standard fixed-window partitioned rate limiters enforce 60 requests and 10 creates/minute/key with no queue.
3. Reuse first-party atomic order creation and LA-143's non-HTTP tracking reader/DTO, adding a strict-customer entry point that cannot inherit staff visibility after role changes. Do not import its controller/hub/UI or integrate its unfinished story. External DTO projections exclude credentials/recipient fields on reads. Document this shared-source relationship so later story integration retains one reader implementation.
4. Real PostgreSQL migration/lifecycle/create-isolation and rollback tests; HTTP focused tests; full backend regression; generated Swagger and static OpenAPI verification; independent review and fix findings. Commit/push with LA-156, require backend/web/mobile CI before merging into story/LA-39-external-integration. LA-157 remains teammate acceptance work. No incomplete story merge into develop.

Review focus: stale scope/revocation/account/role after issuing a key, foreign package/address/order, duplicate package concurrency, secrets in lists/audits/Swagger responses, mixed JWT/key credentials, rate isolation by key, database migration/FK/hash uniqueness, terminal/stale telemetry semantics, admin demotion and atomic audit rollback.

## Execution ledger

- RED: all 20 initial HTTP cases failed against missing routes/scheme. Initial GREEN: 20/20.
- PostgreSQL verification: migration/hash metadata, create/read/revoke, injected audit failures rolling back provisioning/revocation, concurrent package creation yielding one order/log/notification, current-mission/no-sample/stale/terminal tracking.
- Review found two blockers. RED regressions proved mixed GET/POST requests shared the wrong limiter partition and a concurrent CUSTOMER-to-ADMIN promotion could widen external tracking. Fixed partition identity to `(key, create)` and added `GetForCustomerAsync`, which always checks customer ownership. A URL-casing create-budget bypass was separately reproduced and fixed.
- Focused verification after fixes: 28/28 cases, zero skips. Added foreign-address and stale-admin coverage for full regression. Fresh follow-up review: no remaining blockers.
- Ruling: reuse the LA-143 non-HTTP service/DTO with the strict-customer extension, importing no controller/hub/worker/UI or story commits. LA-35 and LA-39 can finish independently; retain the strict entry point when reconciling shared source later.
- Ruling: candidate LA-341 key/rate policies are the technical implementation baseline requested by the user; business acceptance remains labeled pending. Limits are process-local fixed windows and do not claim distributed enforcement.
- Local verification uses a dedicated PostgreSQL 18 cluster on port 55443, sequential collections, retained unique test databases and disabled Windows EventLog sink. No package dependencies changed. Full-suite/remote-CI results are recorded in the PR and Jira.
