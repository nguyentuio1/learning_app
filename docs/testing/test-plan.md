# LA-246: Test Plan — SmartDroneDelivery

## 1. Objectives
Ensure every pull request targeting `develop` or `main` satisfies functional acceptance criteria, RBAC/security boundaries, relational concurrency invariants, and UI/UX regression checks across Backend, Web, and Mobile.

---

## 2. Roles and Responsibilities

| Role | Responsibility |
| --- | --- |
| **QA / Test Engineer (Phan Văn Khải)** | Design test scope/strategy (`LA-245`), test plan (`LA-246`), test cases (`LA-247`), implement automated UI/integration tests, and publish execution reports (`LA-248`). |
| **Backend Developers** | Maintain API contracts, EF Core migrations, service logic, and `[PostgresFact]` concurrency tests. |
| **Frontend Web & Mobile Developers** | Maintain React/Flutter components, form validations, state management, and resolve CI/review findings. |
| **Tech Lead / Reviewer (`nguyentuio1`)** | Review PRs, verify edge cases and concurrency safety, and enforce merge gates. |

---

## 3. Test Environments

| Environment | Configuration |
| --- | --- |
| **Local Developer / QA Machine** | Windows, .NET 8 SDK (`dotnet test`), Node.js + npm (`npx vitest run`), EF Core InMemory provider (PostgreSQL tests skipped unless `TEST_POSTGRES_CONNECTION` is set). |
| **CI Pipeline (GitHub Actions)** | `ubuntu-latest` runners defined in `.github/workflows/ci.yml`:<br>1. **Backend tests**: .NET 8 + `postgres:16-alpine` service (`TEST_POSTGRES_CONNECTION` enabled so `[PostgresFact]` tests execute).<br>2. **Web lint, test and build**: ESLint, Vitest (`jsdom`), TypeScript/Vite production build.<br>3. **Mobile analyze and test**: `flutter analyze` + `flutter test`. |

---

## 4. Test Schedule & Milestones

| Milestone | Target Sprint / Date | Target Scope | Entry Criteria | Exit Criteria |
| --- | --- | --- | --- | --- |
| **M1 — Foundation & Auth/RBAC** | Sprint 1–2 (Code/Tests Merged; verify Jira status) | Auth, Profile, RBAC, User Management (`FR-01`–`FR-05`) | API & UI routes implemented | 100% unit/integration tests pass in CI |
| **M2 — Core Customer & Station APIs** | Sprint 3–4 (Code/Tests Merged; verify Jira status) | Addresses, Packages, Orders, Stations, Slots, Drones, Pickup (`FR-06`–`FR-11`, `FR-19`–`FR-23`) | Feature PRs merged to `develop` | All InMemory + `[PostgresFact]` tests pass |
| **M3 — Sprint 5 Workflow & Notifications** | Sprint 5 / 2026-11-15 (Code/Tests Merged; verify Jira status) | Approvals (`LA-31`), State Machine (`LA-32`), Scheduling (`LA-33`), Failures (`LA-34`), Notifications (`LA-326`) | Story branches & API contracts ready | PR review comments resolved; 3/3 CI checks green |
| **M4 — Tracking, Map, Confirmation, Dashboard, External API** | Sprint 6 / TBD | `LA-146` (`LA-35`), `LA-149` (`LA-36`), `LA-152` (`LA-37`), `LA-155` (`LA-38`), `LA-157` (`LA-39`) | Feature implementations merged to `develop` | Acceptance criteria verified and `LA-248` updated |

---

## 5. Entry & Exit Criteria (Quality Gate)

### Entry Criteria
- Feature code and API contracts (`docs/api/*.md`) are committed to the target branch.
- Application builds cleanly (`dotnet build`, `vite build`, `flutter analyze`).

### Exit Criteria
- **0 failed tests** across `Backend tests`, `Web lint, test and build`, and `Mobile analyze and test`.
- All `[PostgresFact]` concurrency tests pass against PostgreSQL 16 in CI.
- No unresolved `CHANGES_REQUESTED` review comments on the PR.
