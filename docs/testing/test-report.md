# LA-248: Test Execution Report — SmartDroneDelivery

**Report Date:** 2026-11-16
**Prepared By:** Phan Văn Khải (QA / Test)

---

## Current LA-45 performance evidence

The later [performance reference validation](PerformanceTestReport.md#53-recorded-reference-workstation-and-postgresql16-docker-validation) at source `f7502b7` records three unchanged 10/50/100 barrier runs (45/45 measurements pass) and a separate PostgreSQL16 Docker mixed k6 run (15/15 thresholds, zero HTTP failures). Machine/runtime/command details and raw logs are linked there; previous failed runs are preserved. This establishes performance for that recorded reference instance, not native-device, enabled-AI or universal deployment acceptance. The older execution snapshots below keep their original dates and source revisions.

## 1. Execution Summary by Baseline & PR

Execution results are separated strictly by **Baseline / PR**, **Environment**, **Commit SHA**, **Test Result**, and **Evidence URL** so local `develop` runs are never mixed with feature PR CI runs. Passing automated/fake-provider tests establish API/UI contract and boundary behavior; they do not claim live AI model quality, chat retention approval, physical drone hardware control, or production deployment acceptance.

### 1.1 `origin/develop` Baseline (Local Verification)

| Baseline / PR | Layer | Environment | Commit SHA | Test Result | Evidence |
| --- | --- | --- | --- | --- | --- |
| `origin/develop` (Sprint 5 snapshot) | Backend (.NET 8, `dotnet test`) | Local Windows, EF Core InMemory (`TEST_POSTGRES_CONNECTION` unset) | `636f9a4` | **372 Passed, 0 Failed, 21 Skipped** (393 total; 21 `[PostgresFact]` skipped locally) | [backend-develop-test-run.txt](evidence/backend-develop-test-run.txt) |
| `origin/develop` (Sprint 5 snapshot) | Web (Vitest, 20 files / 183 tests) | Local Windows, `jsdom` | `636f9a4` | **183 Passed, 0 Failed** (15 files / 117 tests passed in parallel; 5 AntD-heavy files / 66 tests passed 66/66 with `--no-file-parallelism`) | Local CLI run on `636f9a4`; CI Web job on PR #176 (`c9ce9bc`): https://github.com/nguyentuio1/learning_app/actions/runs/43678283005/job/132703130618 |
| `origin/develop` (Sprint 5 snapshot) | Mobile (Flutter, 26 test files) | Local Windows (Flutter SDK not installed locally) | `636f9a4` | Not executed locally; verified via GitHub Actions CI | CI Mobile job on PR #176 (`c9ce9bc`): https://github.com/nguyentuio1/learning_app/actions/runs/43678283005/job/132703130839 |

### 1.2 Sprint 5 Feature PR Verification

| Baseline / PR | Environment | Commit SHA | Test Result | Evidence URL |
| --- | --- | --- | --- | --- |
| **PR #160 (`LA-133`)** — Merged | Local Vitest (`deliveryApprovalUi.test.tsx`) + GitHub PR | `4a8a306` | **7/7 Passed** (Web Approval UI) | https://github.com/nguyentuio1/learning_app/pull/160 |
| **PR #164 (`LA-138`)** — Merged | Local Vitest (`deliverySchedulingUi.test.tsx`) + GitHub PR | `f108962` | **7/7 Passed** (Web Scheduling UI) | https://github.com/nguyentuio1/learning_app/pull/164 |
| **PR #168 (`LA-135`)** — Merged | Local `dotnet test --filter OrderStateMachineTests` + GitHub PR | `b3ea247` | **26/26 Passed** (Backend State Machine) | https://github.com/nguyentuio1/learning_app/pull/168 |
| **PR #163 (`LA-142`)** — Merged | Local Vitest (`deliveryFailureHandlingUi.test.tsx`) + GitHub PR | `bda9ddb` | **6/6 Passed** (Web Failure Handling UI) | https://github.com/nguyentuio1/learning_app/pull/163 |
| **PR #162 (`LA-326`)** — **Approved & Merged** | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | `c4bd627` | **3/3 CI Checks Passed** (`Backend tests`, `Web lint, test and build`, `Mobile analyze and test`) | https://github.com/nguyentuio1/learning_app/pull/162 |

### 1.3 Sprint 6 & Sprint 7 Feature and Story PR Verification

| Baseline / PR | Environment | Commit SHA (Head / Merge) | Test Result | Evidence URL |
| --- | --- | --- | --- | --- |
| **PR #198 (`LA-157`)** & **PR #203 (`LA-39`)** — Merged | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | PR #198: `f5b8924` / `dbf50cc`<br>PR #203: `c6f0b9d` / `89579f8` | **3/3 CI Checks Passed** (`ExternalIntegrationAcceptanceTests`) | https://github.com/nguyentuio1/learning_app/pull/198<br>https://github.com/nguyentuio1/learning_app/pull/203 |
| **PR #199 (`LA-152`)** & **PR #208 (`LA-37`)** — Merged | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | PR #199: `7c81aca` / `4c22afa`<br>PR #208: `b411d14` / `321733b` | **3/3 CI Checks Passed** (`DeliveryConfirmationAcceptanceTests`) | https://github.com/nguyentuio1/learning_app/pull/199<br>https://github.com/nguyentuio1/learning_app/pull/208 |
| **PR #200 (`LA-146`)** & **PR #205 (`LA-35`)** — Merged | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | PR #200: `9d1c9cb` / `f330956`<br>PR #205: `10ebbf6` / `10fa726` | **3/3 CI Checks Passed** (`TrackingAcceptanceTests`) | https://github.com/nguyentuio1/learning_app/pull/200<br>https://github.com/nguyentuio1/learning_app/pull/205 |
| **PR #201 (`LA-155`)** & **PR #202 (`LA-38`)** — Merged | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | PR #201: `ab2a12f` / `e534022`<br>PR #202: `18cb307` / `a965a4e` | **3/3 CI Checks Passed** (`AnalyticsAcceptanceTests`) | https://github.com/nguyentuio1/learning_app/pull/201<br>https://github.com/nguyentuio1/learning_app/pull/202 |
| **PR #204 (`LA-149`)** & **PR #206 (`LA-36`)** — Merged | Local Vitest (`deliveryMapUi.test.tsx`) + GitHub Actions CI | PR #204: `e6d68ff` / `2535349`<br>PR #206: `c761577` / `462d297` | **3/3 CI Checks Passed** (Web & Mobile Delivery Map) | https://github.com/nguyentuio1/learning_app/pull/204<br>https://github.com/nguyentuio1/learning_app/pull/206 |
| **PR #216 (`LA-173`)** — Merged | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | `2b5b069` / `8413d84` | **3/3 CI Checks Passed** (Story PR CI trigger & SignalR protocol collection isolation) | https://github.com/nguyentuio1/learning_app/pull/216 |
| **PR #220 (`LA-161`)** & **PR #226 (`LA-40`)** — Merged | Local Vitest (`deliveryEtaUi.test.tsx`) + GitHub Actions CI | PR #220: `176413b` / `27698e8`<br>PR #226: `31aa0ca` / `99b8a5c` | **3/3 CI Checks Passed** (Guarded ETA API, Web & Mobile) | https://github.com/nguyentuio1/learning_app/pull/220<br>https://github.com/nguyentuio1/learning_app/pull/226 |
| **PR #228 (`LA-164`)** — Open on `story/LA-41-delivery-summarization` | Local Vitest (`deliverySummaryUi.test.tsx`) + GitHub Actions CI | `135f87e` (Head) | **8/8 Passed** locally (`deliverySummaryUi` + `deliverySummaryFreshness`); **3/3 CI Checks Passed** | https://github.com/nguyentuio1/learning_app/pull/228 |
| **PR #229 (`LA-168`)** — Open on `story/LA-42-customer-ai-assistant` | Local `dotnet test` (`CustomerAssistantAcceptanceTests`) + GitHub Actions CI | `58ba04a` (Head; updated in follow-up commit) | **3/3 Passed** locally (`CustomerAssistantAcceptanceTests`) | https://github.com/nguyentuio1/learning_app/pull/229 |
| **PR #230 (`LA-172`)** — Open on `develop` | Local `dotnet test` (`FullSystemIntegrationTests`) + GitHub Actions CI | Current PR head | **3/3 Passed** locally (`FullSystemIntegrationTests`: end-to-end delivery lifecycle, UTC midnight boundary, failure recovery + v1 API) | https://github.com/nguyentuio1/learning_app/pull/230 |

---

## 2. Module Coverage

### 2.1 Merged `origin/develop` Baseline (`99b8a5c`)

- **Backend (`SmartDroneDelivery.Tests`):** Auth, Authentication Boundaries, RBAC & Boundaries, Profile & Boundaries, User Management, Development Seed Data, Delivery Order Creation/Queries/Mutations/Concurrency, Order Review, Order State Machine, Scheduling, Failure Handling, Flight Telemetry, Drone Simulator, Real-Time Tracking & SignalR Hub, Delivery Confirmation, Analytics & Reports, External v1 Integration API, and Guarded ETA Read/Producer/Local Model Adapter.
- **Frontend Web (`frontend/web/tests/`):** Auth Session, Auth Review UI, Customer Registration, Account Flow, Account Motion, Route Loading, Customer Address/Package/Order Flows, User Management UI, Landing Station/Slot/Drone Management UI, Delivery Approval/Scheduling/Failure Handling UI, Customer Notifications UI, Real-Time Tracking & Delivery Map UI, and Delivery ETA UI (`deliveryEtaUi.test.tsx`, `deliveryEtaStatusRefresh.test.tsx`).
- **Frontend Mobile (`frontend/mobile/test/`):** App Smoke, DI Wiring, API Exception, Dio Client, Auth/Logging Interceptors, Auth/Profile Repositories, Customer Addresses/Packages/Orders/Notifications, Real-Time Tracking, Delivery Map, Delivery Confirmation, ETA Estimation (`eta_estimation_screens_test.dart`), and Operator Station Availability & Package Pickup.

### 2.2 Additional Coverage in Open Feature PRs (Not Yet Merged into `develop`)

- **PR #228 (`LA-164`, on `story/LA-41-delivery-summarization`):** `deliverySummaryUi.test.tsx` (Web UI & integration tests for AI delivery summarization).
- **PR #229 (`LA-168`, on `story/LA-42-customer-ai-assistant`):** `CustomerAssistantAcceptanceTests.cs` (acceptance & regression tests for Customer AI Assistant).
- **PR #230 (`LA-172`, targeting `develop`):** `FullSystemIntegrationTests.cs` (end-to-end multi-actor delivery lifecycle, UTC midnight analytics boundary, and external v1 API recovery flow).

---

## 3. Defect Tracking & Resolution Log

| Defect ID | PR / Module | Severity | Summary | Status | Verification Evidence |
| --- | --- | --- | --- | --- | --- |
| `DEF-01` | PR #160 (`LA-133` Web) | P2 | Date filter test passed without asserting new request count or UTC `fromDate`/`toDate`; unused `undici` override in `package.json`. | **Verified** | Commit `4a8a306`; `deliveryApprovalUi.test.tsx` **7/7 passed** |
| `DEF-02` | PR #164 (`LA-138` Web) | P2 | Schedule mock overwrote unscheduled state before `PUT`; 15-minute buffer test only checked empty input. | **Verified** | Commit `f108962`; `deliverySchedulingUi.test.tsx` **7/7 passed** |
| `DEF-03` | PR #168 (`LA-135` Backend) | Minor | Duplicate terminal transition test; missing timeout guard on second `EnterOperationAsync()`. | **Verified** | Commit `b3ea247`; `OrderStateMachineTests` **26/26 passed** |
| `DEF-04` | PR #163 (`LA-142` Web) | Minor | Non-UUID order ID prevented navigation verification after incident report; missing refreshed resolution assertion. | **Verified** | Commit `bda9ddb`; `deliveryFailureHandlingUi.test.tsx` **6/6 passed** |
| `DEF-05` | PR #162 (`LA-326` Mobile) | P2 | Notification list only fetched page 1 (50 items); items beyond 50 could not be opened or marked read. | **Verified** | Commit `c4bd627`; CI `Mobile analyze and test` **success** |
| `DEF-06` | PR #162 (`LA-326` Mobile & Web) | P2 | Missing foreground/resume & periodic refresh for unread notification badge. | **Verified** | Commit `c4bd627`; CI `Web` & `Mobile` **success** |
| `DEF-07` | PR #162 (`LA-326` Web) | P2 | Marking the sole unread item on page 2 left UI on empty page 2 with pagination hidden. | **Verified** | Commit `c4bd627`; `customerNotificationsUi.test.tsx` **7/7 passed** |
| `DEF-08` | PR #162 (`LA-326` Backend) | P2 | Non-atomic read/read-all could overwrite first `ReadAt` or double-count `updatedCount` under concurrency. | **Verified** | Commit `c4bd627`; CI `Backend tests` (`NotificationConcurrencyTests` on Postgres 16) **success** |
| `DEF-09` | PR #162 (`LA-326` Mobile) | P2 | `loadMore()` race condition: delayed `All` page 2 response could overwrite `Unread` list after filter switch. | **Verified** | Commit `c4bd627` (`_generation` guard + regression test); CI `Mobile analyze and test` **success** |
| `DEF-10` | PR #216 (`LA-173` Backend CI) | P2 | Parallel API/BCrypt test fixtures contended with real SignalR WebSocket handshake in `TrackingHubTests`. | **Verified** | Commit `2b5b069` (`SignalRProtocolCollection` nonparallel isolation); [LA-173-ci-stability.md](LA-173-ci-stability.md) |

---

## 4. Open Items & Next Steps
1. **Merged Stories (`develop` at `99b8a5c`):** Sprint 6 (`LA-35`–`LA-39`) and Sprint 7 Story `LA-40` (`LA-158`–`LA-161` Guarded ETA) are integrated into `develop` with 3/3 CI checks green.
2. **Active Sprint 7 Reviews:**
   - **PR #222 (`LA-253`):** SRS consolidation updated per review feedback and re-opened against `develop`.
   - **PR #228 (`LA-164`):** Delivery summarization Web UI & integration tests open against `story/LA-41-delivery-summarization`.
   - **PR #229 (`LA-168`):** Customer AI assistant acceptance & regression tests open against `story/LA-42-customer-ai-assistant`.
   - **`LA-172` (`LA-44`):** End-to-end full system integration tests (`FullSystemIntegrationTests.cs`) and updated Sprint 6–7 test evidence.
