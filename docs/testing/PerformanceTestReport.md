# LA-176: Performance & Concurrency Test Report — SmartDroneDelivery

**Report Date:** 2026-11-17  
**Baseline Commit:** `develop` (`2148e50`)  
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

## 5. Relational Concurrency Correctness (PostgreSQL 16 CI)

In addition to throughput/latency measurement, data consistency under concurrent writes is verified in CI against `postgres:16-alpine` across:
- `DeliveryOrderConcurrencyTests`: concurrent order updates/cancellations serialize cleanly (`SELECT ... FOR UPDATE`).
- `LandingStationManagementTests` / `StationSlotManagementTests`: concurrent station/slot mutations preserve slot counts and roll back atomically.
- `DroneManagementTests`: concurrent duplicate serial creation enforces unique constraint `IX_drones_SerialNumber` (`409 Conflict`).
- `StationOperationsConcurrencyTests` & `PackagePickupConfirmationTests`: concurrent intake/reject, pickup/timeout, and OTP lockouts enforce single-winner semantics.
- `NotificationConcurrencyTests`: concurrent `read` and `read-all` preserve first `ReadAt` and exact `updatedCount`.

---

## 6. Observations & Recommendations

1. **BCrypt CPU Cost on `/api/auth/login`:** In the barrier benchmark (Section 3), 10 and 50 concurrent login requests achieve `p95` of **510.5 ms** and **2,067.2 ms** (PASS `< 3,000 ms`), while 100 simultaneous logins at a barrier reach **3,608.0 ms** (`max` 3,695.3 ms, **FAIL**). Under the live `k6` mixed-endpoint `constant-vus` workload (Section 4), `POST /api/auth/login` **PASSED** all three tiers (`319.04 ms`, `1.49 s`, `2.18 s` `p95`).
2. **Suspected Backend Concurrency Bottleneck on `POST /api/delivery-orders`:** Live `k6` testing against PostgreSQL 18 recorded **245/1,118** failed `create_order` responses (`3.64%` overall `http_req_failed`) and `p95 = 4.32 s` at 100 VUs, alongside **13,069** un-deduplicated `IX_delivery_orders_TrackingNumber` (`23505`) log occurrences ([k6-live-api-errors-excerpt.txt](evidence/k6-live-api-errors-excerpt.txt)). Because final HTTP status counts were not logged, `MaxSaveAttempts = 5` retry exhaustion (`409 Conflict`) is the **suspected** failure cause rather than proven for all 245 responses. Even so, `NextTrackingNumberAsync` scanning for `max + 1` without a database sequence or lock can cause concurrent writers at 50–100 VUs to compute the same colliding number, trigger repeated `SaveChangesAsync()` retries (adding latency toward the `4.32 s` `p95`), and potentially exhaust the 5-attempt limit. Recommend investigating and replacing `max + 1` scanning with a **PostgreSQL sequence** (or atomic unique suffix) in a separate backend task/PR rather than increasing retry counts, then re-running the agreed workload.
3. **Production / Docker Verification:** Re-run `scripts/k6-load-test.js` on the agreed staging/Docker environment after `NextTrackingNumberAsync` is fixed.
