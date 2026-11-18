# LA-176: Performance & Concurrency Test Report — SmartDroneDelivery

**Initial Report Date:** 2026-11-17
**Initial Baseline Commit:** `develop` (`2148e50`)
**Latest reference validation:** source `f7502b7`, including application `10700d2`; see Section 5.3 for new scoped PASS results. Earlier run results are preserved.
**Prepared By:** Phan Văn Khải (QA / Test)

---

## 1. Objectives & Target Threshold

- Measure API response time (`min`, `avg`, `p95`, `max`), throughput (`req/s`), and error rate under concurrent loads of **10, 50, and 100 simultaneous requests** across critical endpoints (`GET /health/live`, `POST /api/auth/login`, `GET /api/delivery-orders`, `GET /api/delivery-orders/{id}/tracking`, `POST /api/delivery-orders`).
- Evaluate against the project target of **3 seconds (3,000 ms)** response time.
- **Target Interpretation Note:** In this report, the **3,000 ms target is evaluated against `p95` latency** (and verified for `max` latency in the measured runs). Production PostgreSQL 16 + network round-trip latency under sustained k6 load should be benchmarked separately against a running Docker Compose stack.

---

## 2. Test Scripts & Environment

| Item | Details |
| --- | --- |
| **In-Process Load Benchmark** | [PerformanceBenchmarkTests.cs](../../backend/SmartDroneDelivery.Tests/PerformanceBenchmarkTests.cs) (`RUN_PERFORMANCE_BENCHMARKS=true dotnet test --filter FullyQualifiedName~PerformanceBenchmarkTests -c Release`; skipped in default CI without the env var) |
| **k6 Load Script (Docker/Staging)** | [scripts/k6-load-test.js](../../scripts/k6-load-test.js) (`10 / 50 / 100 VUs` constant-VU scenarios, `p(95)<3000`, `rate<0.01`) |
| **Measured Environment** | Local Windows, .NET 8 ASP.NET Core `TestServer` (`AuthApiFactory`, EF Core InMemory provider, `-c Release`). No Docker/PostgreSQL daemon was running locally during this measurement; database-level row locking (`SELECT ... FOR UPDATE`) is verified separately in CI against PostgreSQL 16. |
| **Raw Output Evidence** | [perf-benchmark-run.txt](evidence/perf-benchmark-run.txt) (complete 15/15 tier run; overall test assertion **FAILED** on `POST /api/auth/login` at 100 VUs) |

---

## 3. Measured Results (10, 50, 100 Concurrent Requests)

### 3.1 Concurrency Tier: 10 Simultaneous Requests

| Endpoint | Requests | Errors | Min (ms) | Avg (ms) | p95 (ms) | Max (ms) | Throughput (req/s) | <= 3,000 ms Target |
| --- | :---: | :---: | ---: | ---: | ---: | ---: | ---: | :---: |
| `GET /health/live` | 10 | 0 | 6.3 | 6.4 | 6.5 | 6.5 | 1,442.4 | **PASS** |
| `POST /api/auth/login` | 10 | 0 | 465.7 | 499.0 | 510.5 | 510.5 | 19.6 | **PASS** |
| `GET /api/delivery-orders` | 10 | 0 | 223.5 | 223.7 | 223.8 | 223.8 | 44.7 | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 10 | 0 | 195.2 | 195.4 | 195.6 | 195.6 | 51.1 | **PASS** |
| `POST /api/delivery-orders` | 10 | 0 | 277.6 | 277.8 | 277.9 | 277.9 | 36.0 | **PASS** |

### 3.2 Concurrency Tier: 50 Simultaneous Requests

| Endpoint | Requests | Errors | Min (ms) | Avg (ms) | p95 (ms) | Max (ms) | Throughput (req/s) | <= 3,000 ms Target |
| --- | :---: | :---: | ---: | ---: | ---: | ---: | ---: | :---: |
| `GET /health/live` | 50 | 0 | 1.7 | 1.8 | 1.9 | 2.0 | 20,156.4 | **PASS** |
| `POST /api/auth/login` | 50 | 0 | 1,853.2 | 1,895.1 | 2,067.2 | 2,195.0 | 22.8 | **PASS** |
| `GET /api/delivery-orders` | 50 | 0 | 28.0 | 28.4 | 28.8 | 44.2 | 1,123.2 | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 50 | 0 | 18.0 | 18.2 | 19.2 | 19.5 | 2,502.7 | **PASS** |
| `POST /api/delivery-orders` | 50 | 0 | 25.7 | 26.4 | 29.2 | 29.9 | 1,668.2 | **PASS** |

### 3.3 Concurrency Tier: 100 Simultaneous Requests

| Endpoint | Requests | Errors | Min (ms) | Avg (ms) | p95 (ms) | Max (ms) | Throughput (req/s) | <= 3,000 ms Target |
| --- | :---: | :---: | ---: | ---: | ---: | ---: | ---: | :---: |
| `GET /health/live` | 100 | 0 | 7.2 | 7.3 | 7.5 | 7.6 | 12,916.4 | **PASS** |
| `POST /api/auth/login` | 100 | 0 | 3,560.6 | 3,567.7 | 3,608.0 | 3,695.3 | 27.1 | **FAIL** |
| `GET /api/delivery-orders` | 100 | 0 | 47.3 | 47.6 | 48.0 | 49.0 | 2,001.3 | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 100 | 0 | 41.2 | 47.0 | 49.5 | 50.7 | 1,935.2 | **PASS** |
| `POST /api/delivery-orders` | 100 | 0 | 89.0 | 89.3 | 90.1 | 92.9 | 1,064.3 | **PASS** |

**Summary:** 14/15 endpoint-tier measurements meet `p95 < 3,000 ms` with 0 HTTP errors; 1/15 (`POST /api/auth/login` at 100 simultaneous requests) **FAILED** at `p95 = 3,608.0 ms` (`max = 3,695.3 ms`).

---

## 4. Live `k6` Mixed-Endpoint Constant-VU Run (PostgreSQL 18)

> **Workload Distinction Note:** Unlike Section 3's barrier benchmark (which fires 10/50/100 simultaneous requests to a single endpoint at a barrier), `scripts/k6-load-test.js` runs a **mixed-endpoint `constant-vus` workload** where each VU executes `health -> login -> list_orders -> tracking -> create_package -> create_order` sequentially with short sleeps. Results from the two workloads are kept strictly separate.

| Item | Details |
| --- | --- |
| **Tool & Version** | `k6.exe v2.4.0` (`commit/dd9395906d`, `go1.26.11`, `windows/amd64`) |
| **Application / Script SHA** | `f67aa52` (`SmartDroneDelivery.Api`, `-c Release`, `ASPNETCORE_ENVIRONMENT=Development`, `Seed__DevelopmentData=true`) |
| **Database** | Local PostgreSQL 18 (`localhost:5433`, `drone_delivery_db`, default `max_connections=100`, `shared_buffers=128MB`) |
| **Dataset** | Seeded demo customer (`customer@smartdronedelivery.com`), operational stations `HCM-01` and `HCM-02`, plus packages/orders created during iterations |
| **Workload & Volume** | `10 VUs` (15s), `50 VUs` (20s), `100 VUs` (20s); 1,118 total iterations, 6,713 HTTP requests (`92.84 req/s`) |
| **Raw Output Evidence** | [k6-live-run.txt](evidence/k6-live-run.txt), [k6-live-api-errors-excerpt.txt](evidence/k6-live-api-errors-excerpt.txt) (**FAILED** on `create_order` 100 VUs latency and `http_req_failed`) |

| Endpoint | 10 VUs `p95` (max) | 50 VUs `p95` (max) | 100 VUs `p95` (max) | `< 3,000 ms` Target |
| --- | ---: | ---: | ---: | :---: |
| `GET /health/live` | 3.93 ms (13.9 ms) | 328.47 ms (533.83 ms) | 1.79 s (2.04 s) | **PASS** |
| `POST /api/auth/login` | 319.04 ms (443.24 ms) | 1.49 s (2.12 s) | 2.18 s (3.05 s) | **PASS** |
| `GET /api/delivery-orders` | 20 ms (61.22 ms) | 609.88 ms (1.09 s) | 2.16 s (3.17 s) | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 16.57 ms (106.66 ms) | 518.99 ms (1.08 s) | 1.79 s (2.69 s) | **PASS** |
| `POST /api/delivery-orders` | 49.5 ms (299.49 ms) | 1.25 s (2.11 s) | **4.32 s** (5.35 s) | **FAIL (100 VUs)** |

- **Error Rate (`http_req_failed`):** **3.64%** (`245 / 6,713` failed, threshold `< 1%` — **FAIL**). All 245 failures occurred on `POST /api/delivery-orders` (`create order 201`: 873 passed / 245 failed).
- **Suspected Collision / Retry-Exhaustion Diagnosis:** The API log for this run recorded **13,069** matching log occurrences for `IX_delivery_orders_TrackingNumber` (`SqlState 23505`; see [k6-live-api-errors-excerpt.txt](evidence/k6-live-api-errors-excerpt.txt)) and **0** other `fail:` categories. That figure is an un-deduplicated log-occurrence count (a single handled exception can span multiple EF Core log categories/lines), and because `DeliveryOrderService.CreateAsync` (`f67aa52`) already wraps `SaveChangesAsync()` in a `MaxSaveAttempts = 5` retry loop that catches `DbUpdateException`, clears tracking, regenerates the tracking number, and returns `409 Conflict` after exhaustion, individual `23505` log entries include handled retries. Additionally, `appsettings.json` sets `Microsoft.AspNetCore` logging to `Warning` and `scripts/k6-load-test.js` only asserts `r.status === 201`, so a per-status-code breakdown (`409` vs `5xx`/timeouts) of the 245 failed responses was not captured; `MaxSaveAttempts = 5` retry exhaustion on `IX_delivery_orders_TrackingNumber` is therefore recorded as the **suspected** cause rather than proven for every one of the 245 responses.

---

## 5. Post-Fix Rerun After `LA-366` Merge (`1927fe9` / `develop` `10700d2`)

After merging `origin/develop` (`10700d2`, PR #281 / `LA-366` atomic `DeliveryTrackingCounters`), all previous API/DB processes were drained, the database was recreated with migration `20261118175032_AddDeliveryTrackingCounters` (verified `delivery_tracking_counters` table present), and both the in-process barrier benchmark and the live `k6` mixed-endpoint profile were re-run. All pre-fix **FAIL** outputs in Sections 3–4 are preserved unchanged.

### 5.1 In-Process Barrier Benchmark Rerun (`PerformanceBenchmarkTests`)

| Item | Details |
| --- | --- |
| **Application / Test SHA** | `1927fe9` (merge of `37f6523` + `origin/develop` `10700d2`) |
| **Environment & Runtime** | Local Windows, .NET `8.0.31` (SDK `10.0.402`), `-c Release`, `AuthApiFactory` (EF Core InMemory) |
| **Raw Output Evidence** | [perf-benchmark-rerun.txt](evidence/perf-benchmark-rerun.txt) (**FAILED** on `POST /api/auth/login` at 100 VUs) |

- **Results:** **14/15 PASS, 1/15 FAIL** (`0` HTTP errors):
  - `POST /api/auth/login`: 10 VUs `p95 = 539.1 ms` (**PASS**), 50 VUs `p95 = 2,280.8 ms` (**PASS**), 100 VUs `p95 = 4,285.0 ms` (`max = 4,306.5 ms`, **FAIL** — unchanged BCrypt CPU cost at 100 simultaneous logins).
  - Non-hashing endpoints (`GET /health/live`, `GET /api/delivery-orders`, `GET /api/delivery-orders/{id}/tracking`, `POST /api/delivery-orders`): all passed across `10 / 50 / 100` VUs (`p95 <= 244.4 ms`, `0` errors).

### 5.2 Live `k6` Mixed-Endpoint Constant-VU Rerun (PostgreSQL 18.4 + `LA-366`)

| Item | Details |
| --- | --- |
| **Tool & Version** | `k6.exe v2.4.0` (`commit/dd9395906d`, `go1.26.11`, `windows/amd64`) |
| **Application / Script SHA** | `1927fe9` (`SmartDroneDelivery.Api`, `-c Release`, `ASPNETCORE_ENVIRONMENT=Development`, `Seed__DevelopmentData=true`, `LA-366` migration `20261118175032_AddDeliveryTrackingCounters`) |
| **Database** | Fresh PostgreSQL `18.4` (`localhost:5433`, `drone_delivery_db`, `max_connections=100`, `shared_buffers=128MB`) |
| **Dataset & Volume** | Seeded demo customer (`customer@smartdronedelivery.com`), stations `HCM-01` & `HCM-02`, plus 2,305 packages and 2,305 orders created across 2,305 iterations (`13,833` HTTP requests, `205.12 req/s`) |
| **Raw Output Evidence** | [k6-live-rerun.txt](evidence/k6-live-rerun.txt) (**15/15 PASS**, `0.00%` error rate) |

| Endpoint | 10 VUs `p95` (max) | 50 VUs `p95` (max) | 100 VUs `p95` (max) | `< 3,000 ms` Target |
| --- | ---: | ---: | ---: | :---: |
| `GET /health/live` | 2.62 ms (38.33 ms) | 284 ms (512.57 ms) | 582.95 ms (880.2 ms) | **PASS** |
| `POST /api/auth/login` | 222.58 ms (349.27 ms) | 692.22 ms (879.55 ms) | 1.09 s (1.34 s) | **PASS** |
| `GET /api/delivery-orders` | 12.72 ms (31.02 ms) | 359.57 ms (532.14 ms) | 727.17 ms (1.09 s) | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 13.4 ms (61.24 ms) | 156.96 ms (406.68 ms) | 482.3 ms (911.23 ms) | **PASS** |
| `POST /api/delivery-orders` | 26.11 ms (109.08 ms) | 175.42 ms (468.46 ms) | 738.48 ms (1.2 s) | **PASS** |

- **Error Rate & `create_order` Status Counts:** `http_req_failed = 0.00%` (**0 out of 13,833**); `checks_succeeded = 100.00%` (**13,831 out of 13,831**); `POST /api/delivery-orders`: **2,305 `201 Created`**, **0 `409`**, **0 `5xx`/other failures** (`0` `fail:` or `23505` entries in API logs).


### 5.3 Recorded Reference Workstation and PostgreSQL16 Docker Validation

The **unchanged** `PerformanceBenchmarkTests` at `f7502b7` was run three times, each in a fresh test process with `RUN_PERFORMANCE_BENCHMARKS=true`. All 15 endpoint/tier cases passed in each run (45 measurements, zero HTTP errors). BCrypt remains cost11; no processor/thread-pool overrides, threshold changes, auth-result caching, or benchmark edits were applied.

| Reference environment | Recorded configuration |
| --- | --- |
| Host | Intel Core i5-13420H, 8 physical cores /12 logical processors |
| Barrier runtime | Windows, .NET8.0.31 /SDK10.0.402, Release TestServer + EFInMemory |
| Docker runtime | Docker29.8.1,12 exposed CPUs,16629112832 bytes VM memory; no explicit container CPU/memory caps |
| Live database/API | PostgreSQL16.15 (`postgres:16-alpine`), API .NET8.0.34 container, MinIO; isolated fresh synthetic database |
| Live load generator | k6v2.4.0 /go1.27.3 /linux-amd64, unchanged mixed-endpoint script at `f7502b7` |

| Barrier repeat | Login10 p95 | Login50 p95 | Login100 p95 (max) | Complete result /raw output |
| --- | ---: | ---: | ---: | --- |
| 1 | 290.0ms | 562.9ms | 1177.3ms (1211.1ms) | 15/15PASS; [raw1](evidence/perf-reference-barrier-1.txt) |
| 2 | 199.5ms | 813.8ms | 1234.4ms (1307.6ms) | 15/15PASS; [raw2](evidence/perf-reference-barrier-2.txt) |
| 3 | 254.1ms | 590.2ms | 1202.2ms (1213.3ms) | 15/15PASS; [raw3](evidence/perf-reference-barrier-3.txt) |

The separate Docker/PostgreSQL16 mixed profile also passed all15 per-endpoint/per-tier p95 thresholds: **0/17111 HTTP failures**, all17107 checks passed,2851 complete iterations and no interrupted iterations. At100VUs, login p95=**1.09s** (max1.62s) and create-order p95=**280.74ms** (max982.53ms). See [full raw k6 output](evidence/k6-reference-docker-pg16.txt) for every endpoint/tier. API log scan found zero23505 and zero `fail:` entries; one tracking-constraint text occurrence was the migration's `CREATE UNIQUE INDEX`, not an error count.

[Machine/runtime metadata, commands, timestamps and file hashes](evidence/performance-reference-environment.json) record the scope. The database/API/MinIO ran in a separate Compose project with synthetic data and were stopped after measurement. Existing application services were left running. Private generated secrets/configuration are excluded from evidence.

These results demonstrate the existing p95<3000ms and zero-error criteria on **this recorded reference workstation/profile**. They do not establish the cause of earlier failures or guarantee the same timings on every machine. Shared background load was not controlled. Earlier Windows/PG18 and barrier FAIL outputs remain unchanged historical results. No revised login workload or weaker BCrypt criterion is being substituted.

---

## 6. Relational Concurrency Correctness (PostgreSQL 16 CI)

In addition to throughput/latency measurement, data consistency under concurrent writes is verified in CI against `postgres:16-alpine` across:
- `DeliveryOrderConcurrencyTests`: concurrent order updates/cancellations serialize cleanly (`SELECT ... FOR UPDATE`).
- `LandingStationManagementTests` / `StationSlotManagementTests`: concurrent station/slot mutations preserve slot counts and roll back atomically.
- `DroneManagementTests`: concurrent duplicate serial creation enforces unique constraint `IX_drones_SerialNumber` (`409 Conflict`).
- `StationOperationsConcurrencyTests` & `PackagePickupConfirmationTests`: concurrent intake/reject, pickup/timeout, and OTP lockouts enforce single-winner semantics.
- `NotificationConcurrencyTests`: concurrent `read` and `read-all` preserve first `ReadAt` and exact `updatedCount`.

---

## 7. Observations & Recommendations

1. **Environment-specific login measurements:** Earlier barrier runs failed at3608.0ms and4285.0ms; three unchanged reference-workstation repeats pass at1177.3/1234.4/1202.2ms. The difference has not been traced to a specific CPU/load/scheduling cause. Do not claim the old failures were fixed by an authentication code change; none was made.
2. **Tracking-number repair:** LA-366 is integrated at `10700d2`. Both the PG18 mixed rerun (Section5.2) and separate PostgreSQL16 Docker run (Section5.3) show zero HTTP failures for their stated workloads. The atomic allocator's correctness and migration regressions are separate from performance measurements.
3. **Acceptance scope:** The original barrier and mixed criteria pass on the recorded reference hardware in Section5.3, with all earlier failures retained. Source integration/review must reference this configuration and successful current CI. Native/device, live AI policy/quality, production deployment and other-machine performance remain separate evidence scopes.
