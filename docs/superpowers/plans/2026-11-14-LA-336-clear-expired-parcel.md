# LA-336 expired parcel clearing implementation plan

**Goal:** Implement the assigned Jira LA-336 backend operation, allowing the destination operator or admin to confirm physical removal of a timed-out parcel.

**Architecture:** Extend the station operations controller and a focused partial service. Use the existing operation gate for InMemory and one PostgreSQL transaction, locking order, station and slot in that order. Revalidate current parcel ownership after locking; historical mission references never grant ownership over a reused slot.

**Spec:** Jira LA-336, parent LA-34. POST `/api/landing-stations/{stationId}/operations/clear-expired-parcel`, body `slotId`, `reason` (trimmed 5–500 characters). Return 200 with order/slot identity and confirmation timestamp; 400 validation, 401 missing token, 403 unauthorized station/account, 404 missing station/slot, 409 invalid physical/order state or repeated clearing.

**Tech stack:** Existing ASP.NET Core 8, EF Core 8, PostgreSQL, xUnit. Base: `ee8d529` on the LA-34 story.

## Decisions and review focus

- Scope is expired destination parcels: require FAILED with latest failure from ARRIVED and COLLECTION_TIMEOUT metadata, or the existing legacy timeout log. Do not allow this endpoint to clear airborne/intake failures.
- Preserve order FAILED, actual delivery time, original structured failure reason, mission history and drone state. Append the physical-removal remark to the existing failure log, rather than inserting another FAILED episode.
- Audit uses action EXPIRED_PARCEL_CLEARED, slot identity, actor and a new nullable Reason (max 500) column; old audit rows remain valid.
- Protect reused destination/source slots, including a current READY source claim whose assignment predates the expired mission.
- Concurrent requests must yield one 200 and one 409; injected database audit insertion failure must roll back slot, order timestamp and log remark.

### Task 1: Backend operation and regression verification

Files: StationOperationsController.cs, StationOperationService.cs, new StationOperationService.ExpiredParcels.cs, StationOperationDtos.cs, AuditLog.cs, AppDbContext.cs, generated audit-reason migration/snapshot, new ExpiredParcelClearingTests.cs and ExpiredParcelClearingPostgresTests.cs; station-api.md and sequence-package-pickup-confirmation.md.

- [x] Add HTTP tests for valid clearing, authorization, validation, missing resources, invalid states, repeat requests and reused-slot protection. Run before implementation and confirm missing-route failures.
- [x] Add PostgreSQL tests for concurrent clear and audit-failure rollback. Run against a task-local server before implementation and confirm missing-route failures.
- [x] Implement the request/response, controller action, service and additive audit migration.
- [x] Run focused tests, then full backend suite with the PostgreSQL connection configured. Disable Windows EventLog output in the test process only (baseline needs this in the sandbox).
- [x] Document endpoint and sequence note; inspect migration, diff and security/resource-lock invariants before committing.

## Evidence

Baseline without EventLog suppression: 480 failures from sandbox EventLog access. With `Logging__EventLog__LogLevel__Default=None`: 537 passed, 52 PostgreSQL tests skipped, 0 failed. A task-local PostgreSQL 18 server is running on 127.0.0.1:55436 for transactional verification. NuGet vulnerability lookup is unavailable via the environment proxy; packages are cached.

Before implementation, functional requests and both PostgreSQL test methods failed with the missing route (404), as expected. After implementation: 27 focused functional tests passed; the full suite without a PostgreSQL connection passed 564 tests and skipped 54. Both PostgreSQL test methods also passed, including database-trigger audit failure and a successful retry, but default fixture disposal hit an independently reproduced local server `ProcSignalBarrier` hang during DROP DATABASE.

For local diagnosis only, `TEST_POSTGRES_KEEP_DATABASES=1` retains the factory's uniquely named databases after disposing the app and clearing connection pools. This only bypasses DROP; setup, migration and test assertions still execute and fail normally. Default/CI behavior is unchanged. Use it with a disposable PostgreSQL cluster, or manually remove retained databases when finished. This task uses an isolated cluster under the main repository's ignored `artifacts/la336-postgres/` directory.

Independent static review: no critical or important findings. Nonblocking compatibility note: legacy failures without episode metadata display the newly appended removal remark in their fallback failure-reason text; structured episodes preserve OriginalReason separately. This is retained because the task explicitly requires a status-log remark, without creating a false new failure episode.

EF model check: `dotnet ef migrations has-pending-model-changes --project backend/SmartDroneDelivery.Api --no-build` reported no model changes since the migration. The generated migration adds only nullable `audit_logs.Reason`, leaving existing rows valid.

Final full backend verification: **618 passed, 0 failed, 0 skipped** in 1 minute 21 seconds using the task-local PostgreSQL server, `Logging__EventLog__LogLevel__Default=None` and the explicit `TEST_POSTGRES_KEEP_DATABASES=1` teardown option. Command: `dotnet test backend/SmartDroneDelivery.Tests --no-restore --nologo --verbosity quiet`. `git diff --check` passed. Local test databases are intentionally retained inside the disposable cluster; the server is stopped after verification.
