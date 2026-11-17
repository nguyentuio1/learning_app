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
| **In-Process Load Benchmark** | [PerformanceBenchmarkTests.cs](../../backend/SmartDroneDelivery.Tests/PerformanceBenchmarkTests.cs) (`dotnet test --filter FullyQualifiedName~PerformanceBenchmarkTests`) |
| **k6 Load Script (Docker/Staging)** | [scripts/k6-load-test.js](../../scripts/k6-load-test.js) (`10 / 50 / 100 VUs` constant-VU scenarios, `p(95)<3000`, `rate<0.01`) |
| **Measured Environment** | Local Windows, .NET 8 ASP.NET Core `TestServer` (`AuthApiFactory`, EF Core InMemory provider). No Docker/PostgreSQL daemon was running locally during this measurement; database-level row locking (`SELECT ... FOR UPDATE`) is verified separately in CI against PostgreSQL 16. |
| **Raw Output Evidence** | [perf-benchmark-run.txt](evidence/perf-benchmark-run.txt) |

---

## 3. Measured Results (10, 50, 100 Concurrent Requests)

### 3.1 Concurrency Tier: 10 Simultaneous Requests

| Endpoint | Requests | Errors | Min (ms) | Avg (ms) | p95 (ms) | Max (ms) | Throughput (req/s) | <= 3,000 ms Target |
| --- | :---: | :---: | ---: | ---: | ---: | ---: | ---: | :---: |
| `GET /health/live` | 10 | 0 | 6.4 | 6.7 | 9.2 | 9.2 | 1,057.9 | **PASS** |
| `POST /api/auth/login` | 10 | 0 | 242.7 | 254.9 | 265.1 | 265.1 | 37.7 | **PASS** |
| `GET /api/delivery-orders` | 10 | 0 | 166.6 | 166.6 | 166.6 | 166.6 | 60.0 | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 10 | 0 | 105.3 | 105.3 | 105.4 | 105.4 | 94.8 | **PASS** |
| `POST /api/delivery-orders` | 10 | 0 | 164.4 | 164.5 | 164.5 | 164.5 | 60.7 | **PASS** |

### 3.2 Concurrency Tier: 50 Simultaneous Requests

| Endpoint | Requests | Errors | Min (ms) | Avg (ms) | p95 (ms) | Max (ms) | Throughput (req/s) | <= 3,000 ms Target |
| --- | :---: | :---: | ---: | ---: | ---: | ---: | ---: | :---: |
| `GET /health/live` | 50 | 0 | 1.5 | 1.7 | 1.9 | 2.0 | 19,480.3 | **PASS** |
| `POST /api/auth/login` | 50 | 0 | 863.4 | 882.1 | 982.9 | 1,034.9 | 48.3 | **PASS** |
| `GET /api/delivery-orders` | 50 | 0 | 37.8 | 38.2 | 38.6 | 50.9 | 971.5 | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 50 | 0 | 28.6 | 29.3 | 31.4 | 31.9 | 1,532.3 | **PASS** |
| `POST /api/delivery-orders` | 50 | 0 | 28.6 | 29.2 | 31.9 | 32.4 | 1,505.9 | **PASS** |

### 3.3 Concurrency Tier: 100 Simultaneous Requests

| Endpoint | Requests | Errors | Min (ms) | Avg (ms) | p95 (ms) | Max (ms) | Throughput (req/s) | <= 3,000 ms Target |
| --- | :---: | :---: | ---: | ---: | ---: | ---: | ---: | :---: |
| `GET /health/live` | 100 | 0 | 2.4 | 4.2 | 6.8 | 7.2 | 13,374.7 | **PASS** |
| `POST /api/auth/login` | 100 | 0 | 1,736.5 | 1,748.1 | 1,843.9 | 1,920.1 | 52.1 | **PASS** |
| `GET /api/delivery-orders` | 100 | 0 | 59.2 | 59.6 | 59.8 | 61.9 | 1,583.5 | **PASS** |
| `GET /api/delivery-orders/{id}/tracking` | 100 | 0 | 37.8 | 40.2 | 41.7 | 42.1 | 2,276.9 | **PASS** |
| `POST /api/delivery-orders` | 100 | 0 | 71.3 | 71.8 | 72.5 | 76.1 | 1,311.8 | **PASS** |

---

## 4. Relational Concurrency Correctness (PostgreSQL 16 CI)

In addition to throughput/latency measurement, data consistency under concurrent writes is verified in CI against `postgres:16-alpine` across:
- `DeliveryOrderConcurrencyTests`: concurrent order updates/cancellations serialize cleanly (`SELECT ... FOR UPDATE`).
- `LandingStationManagementTests` / `StationSlotManagementTests`: concurrent station/slot mutations preserve slot counts and roll back atomically.
- `DroneManagementTests`: concurrent duplicate serial creation enforces unique constraint `IX_drones_SerialNumber` (`409 Conflict`).
- `StationOperationsConcurrencyTests` & `PackagePickupConfirmationTests`: concurrent intake/reject, pickup/timeout, and OTP lockouts enforce single-winner semantics.
- `NotificationConcurrencyTests`: concurrent `read` and `read-all` preserve first `ReadAt` and exact `updatedCount`.

---

## 5. Observations & Recommendations

1. **BCrypt CPU Cost on `/api/auth/login`:** At 100 concurrent login requests, `p95` reaches **1,843.9 ms** (`max` 1,920.1 ms, throughput ~52.1 req/s) due to intentional BCrypt password-hashing CPU cost, which remains within the 3,000 ms target. For sustained high-rate login traffic (>100 concurrent logins), horizontal API scaling or rate-limiting at the reverse proxy is recommended.
2. **Non-Hashing Endpoints:** `GET /health/live`, `GET /api/delivery-orders`, `GET /api/delivery-orders/{id}/tracking`, and `POST /api/delivery-orders` all maintain `p95 < 170 ms` across 10, 50, and 100 concurrent requests with **0 errors**.
3. **Production / Docker Verification:** Execute `scripts/k6-load-test.js` against a live Docker Compose (`postgres:16-alpine` + `minio` + `api`) environment when deploying to staging to validate real network + PostgreSQL I/O latency.
