# LA-158 ETA read slice verification

This is a partial implementation of LA-158, not completion of the ETA estimator or LA-40. It builds on the proposed LA-342 contract. No prediction producer, provider activation, model-input generation persistence or reservation update is introduced.

## Scope

- Read-only customer-owned / project-wide DISPATCHER or MANAGER ETA view, using the persisted active account and role. ADMIN and STATION_OPERATOR are denied.
- Shared typed nullable prediction in the dedicated route and existing customer-only order detail. Customer detail also rechecks the persisted CUSTOMER role.
- No-store on both successful read actions. Invalid legacy predictions are omitted; stored order ETA is not synthesized from flight duration.

## Evidence on 2026-11-16

- Before implementation: 16 failed / 1 passed focused HTTP cases. Missing route and stale persisted-role behavior reproduced.
- After implementation: 17 / 17 focused cases passed. Initial complete local suite: 857 passed / 89 PostgreSQL cases skipped.
- Independent review: no important/critical issue; missing no-store on order detail reproduced with a failing header assertion, then corrected.
- Real PostgreSQL projection test added to cover EF SQL translation, null absolute ETA, stored prediction parity and read-only tracking. It uses the existing isolated-database fixture and migrations.
- Local Windows/PostgreSQL 18 full run: 13 failed results, including fixture-cleanup failures, 941 passed, no skipped cases. Failures included SignalR handshake and broadcaster timing, transition/notification/simulator concurrency, integration/analytics and PostgreSQL fixture cleanup. The new ETA relational test reached cleanup and failed in DROP DATABASE, not in a projection assertion. Observed server waits include CheckpointDone and ProcSignalBarrier during fixture DROP DATABASE. Do not report this run as green. Raw evidence: artifacts/test-results/LA-158-postgres-regression.trx in the primary workspace.
- Second full run with DOTNET_PROCESSOR_COUNT=4: 946 passed, two failed results (ETA and analytics fixture cleanup at DROP DATABASE), none skipped. Raw evidence: artifacts/test-results/LA-158-final-regression.trx. This is still not a green full local run.
- Final isolated ETA run: **18 passed, zero failed/skipped**, including real PostgreSQL projection and the corrected no-store assertion. Local connection Command Timeout=120 allows fixture cleanup; no production timeout was changed. Raw evidence: artifacts/test-results/LA-158-focused-final.trx. CI uses PostgreSQL 16 on Linux, so local PostgreSQL 18 results must remain distinct. Check current-head CI before any integration claim.

## Remaining acceptance

The prediction model/provider, anchor and triggers, weight/weather inputs, complete input-version invalidation, concurrency/atomicity and reservation-safe full-route updates remain open. Completing reads does not satisfy LA-158 estimation/persistence or LA-40 status-driven estimate acceptance. Mobile PR #207 also has outstanding review findings. Keep LA-158 In Progress and the feature PR draft; do not merge the unfinished story into develop.
