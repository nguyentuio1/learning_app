# Tracking-number allocation (LA-366)

PostgreSQL allocates the numeric suffix for `DD-YYYYMM-NNNN` using one `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` on `delivery_tracking_counters`. The UTC month is the primary key; the database serializes increments across API processes. Normal allocation does not scan orders. Four digits are a minimum width, so the sequence continues numerically through 9999 to 10000.

The reservation commits before order creation. Failed requests may leave gaps. The unique tracking-number constraint remains, and the order, initial status log and customer notification are still saved together in one transaction. Concurrent attempts to reuse one package retain single-winner semantics. The InMemory provider used by non-relational tests has a numeric fallback; it is not the production allocator.

## Upgrade and backup

Drain/stop **all old API writers** before applying `20261118175032_AddDeliveryTrackingCounters` and starting the new version. The migration locks order writes while backfilling numeric monthly high-water marks, leaving existing tracking numbers unchanged. The lock protects that transaction only; mixed old/new API writers are unsupported because old binaries do not update the counter. With the single API Compose service, stop that service, update/build it and start it with the usual migration-on-start path. Do not run a second old standalone API against the same database during cutover.

The normal PostgreSQL database backup must include the counter table. Do not reset counters or restore only orders into a newer running database. Restore the consistent database backup using the existing [backup/restore guide](DatabaseBackupGuide.md). Generation is not a gapless invoice-number facility.

## Correctness evidence and QA handoff

`TrackingNumberAllocationTests` runs against real PostgreSQL via `TEST_POSTGRES_CONNECTION`, using disposable test databases. It checks 100 simultaneous distinct-package creations with unique codes and complete artifacts, one winner for competing same-package requests, and migration/backfill past 9999 with separate monthly counters. HTTP concurrency stays at 100; the test factory bounds this fixture's connection pool to 24 to avoid exhausting a shared PostgreSQL host.

Before the fix, the same bounded-pool test returned 89 conflicts out of 100 requests. With the atomic allocator, all three focused tests pass. A full local PostgreSQL-backed backend run reports 1189 passed / 5 skipped / 0 failed. These are correctness checks, not the LA-176 performance benchmark.

After this fix is integrated, Khai should run the agreed login barrier and mixed k6 profiles on the recorded environment, preserving the 3000 ms target and BCrypt settings. Record exact application/script SHAs, database/runtime, hardware limits, invocation, endpoint/tier samples, latency, and final HTTP status/error counts. Keep prior FAIL outputs and label each run separately. LA-176/PR252 remain open until their acceptance gates are met; this fix does not declare login or load performance passed.
