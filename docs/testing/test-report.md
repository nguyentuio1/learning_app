# LA-248: Test Execution Report — SmartDroneDelivery

**Report Date:** 2026-11-15  
**Prepared By:** Phan Văn Khải (QA / Test)

---

## 1. Execution Summary by Baseline & PR

Execution results are separated strictly by **Baseline / PR**, **Environment**, **Commit SHA**, **Test Result**, and **Evidence URL** so local `develop` runs are never mixed with feature PR CI runs.

### 1.1 `origin/develop` Baseline (Local Verification)

| Baseline / PR | Layer | Environment | Commit SHA | Test Result | Evidence |
| --- | --- | --- | --- | --- | --- |
| `origin/develop` | Backend (.NET 8, `dotnet test`) | Local Windows, EF Core InMemory (`TEST_POSTGRES_CONNECTION` unset) | `636f9a4` | **372 Passed, 0 Failed, 21 Skipped** (393 total; 21 `[PostgresFact]` skipped locally) | [backend-develop-test-run.txt](evidence/backend-develop-test-run.txt) |
| `origin/develop` | Web (Vitest, 20 files / 183 tests) | Local Windows, `jsdom` | `636f9a4` | **183 Passed, 0 Failed** (15 files / 117 tests passed in parallel; 5 AntD-heavy files / 66 tests passed 66/66 with `--no-file-parallelism`) | Local CLI run on `636f9a4`; CI Web job on PR #176 (`c9ce9bc`): https://github.com/nguyentuio1/learning_app/actions/runs/43678283005/job/132703130618 |
| `origin/develop` | Mobile (Flutter, 26 test files) | Local Windows (Flutter SDK not installed locally) | `636f9a4` | Not executed locally; verified via GitHub Actions CI | CI Mobile job on PR #176 (`c9ce9bc`): https://github.com/nguyentuio1/learning_app/actions/runs/43678283005/job/132703130839 |

### 1.2 Sprint 5 Feature PR Verification

| Baseline / PR | Environment | Commit SHA | Test Result | Evidence URL |
| --- | --- | --- | --- | --- |
| **PR #160 (`LA-133`)** — Merged | Local Vitest (`deliveryApprovalUi.test.tsx`) + GitHub PR | `4a8a306` | **7/7 Passed** (Web Approval UI) | https://github.com/nguyentuio1/learning_app/pull/160 |
| **PR #164 (`LA-138`)** — Merged | Local Vitest (`deliverySchedulingUi.test.tsx`) + GitHub PR | `f108962` | **7/7 Passed** (Web Scheduling UI) | https://github.com/nguyentuio1/learning_app/pull/164 |
| **PR #168 (`LA-135`)** — Merged | Local `dotnet test --filter OrderStateMachineTests` + GitHub PR | `b3ea247` | **26/26 Passed** (Backend State Machine) | https://github.com/nguyentuio1/learning_app/pull/168 |
| **PR #163 (`LA-142`)** — Merged | Local Vitest (`deliveryFailureHandlingUi.test.tsx`) + GitHub PR | `bda9ddb` | **6/6 Passed** (Web Failure Handling UI) | https://github.com/nguyentuio1/learning_app/pull/163 |
| **PR #162 (`LA-326`)** — **Approved & Merged** | GitHub Actions CI (`ubuntu-latest` + `postgres:16-alpine`) | `c4bd627` | **3/3 CI Checks Passed** (`Backend tests`, `Web lint, test and build`, `Mobile analyze and test`) | https://github.com/nguyentuio1/learning_app/pull/162 |

---

## 2. Module Coverage on `origin/develop` (`636f9a4`)

- **Backend (`SmartDroneDelivery.Tests`, 24 files, 393 tests):** Auth, Authentication Boundaries, RBAC & Boundaries, Profile & Boundaries, User Management, Development Seed Data, Delivery Order Creation/Queries/Mutations/Concurrency (6 `[PostgresFact]`), Landing Station Access/Management (3 `[PostgresFact]`), Station Slot Access/Management/Availability (3 `[PostgresFact]`), Drone Fleet Access/Management (3 `[PostgresFact]`), Package Pickup Confirmation (3 `[PostgresFact]`), Pickup Timeout, and Station Operations Concurrency (3 `[PostgresFact]`).
- **Frontend Web (`frontend/web/tests/`, 20 files, 183 tests):** Auth Session, Auth Review UI, Customer Registration, Account Flow, Account Motion, Route Loading, Customer Address Contract, Customer Package Contract, Order Creation/Queries/Mutations, User Management & Auth/UI, Landing Station Contract/UI, Station Slot Contract/UI, Drone Management Contract/UI.
- **Frontend Mobile (`frontend/mobile/test/`, 26 files on `develop` + `notifications_page_test.dart` merged via PR #162):** App Smoke, DI Wiring, API Exception, Dio Client, Auth/Logging Interceptors, Auth/Profile Repositories, Customer Addresses/Packages/Orders/Notifications, Operator Station Availability & Package Pickup, Profile Pages.

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

---

## 4. Open Items & Next Steps
1. **PR #162 (`LA-326`):** **Approved by `nguyentuio1` and Merged** into `develop` (`c4bd627`).
2. **PR #176 (`LA-202`):** Test Scope & Strategy (`LA-245`), Test Plan (`LA-246`), Test Cases (`LA-247`), and Test Report (`LA-248`) open for review.
3. **Upcoming Test Tasks (Pending Feature Implementations on `develop`):**
   - `LA-146` (`LA-35`): `[Test] real-time delivery tracking` (`FR-24`)
   - `LA-149` (`LA-36`): `[Test] delivery map` (`FR-25`)
   - `LA-152` (`LA-37`): `[Test] delivery confirmation` (`FR-26`, `FR-27`)
   - `LA-155` (`LA-38`): `[Test] dashboard and reports` (`FR-28`)
   - `LA-157` (`LA-39`): `[Test] external integration API` (`FR-34`)
