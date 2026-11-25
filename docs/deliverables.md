# Deliverable inventory — SmartDroneDelivery

**Application source baseline:** `develop` at [`9651736`](https://github.com/nguyentuio1/learning_app/commit/9651736ad900113fdd76488a44a3f8011894d6eb), checked 25 November 2026. This inventory maps each handoff component to its actual source/document location. Source availability is distinct from runtime/deployment acceptance.

Original inventory: Phan Văn Khải (LA-251). Post-release organization/status correction: LA-371, building on LA-368. Start with the [documentation index](README.md) for a reading order. The [v1.0.0 release](https://github.com/nguyentuio1/learning_app/releases/tag/v1.0.0) and later `develop` documentation/cleanup changes are distinct; this file does not claim those later commits are released to `main`.

---

## 1. Source Code & Infrastructure Deliverables

| Deliverable | Repository Path | Version / Reference | Description |
| --- | --- | --- | --- |
| **Backend API Source** | [`backend/SmartDroneDelivery.Api/`](../backend/SmartDroneDelivery.Api) | Baseline above | ASP.NET Core 8 REST API + SignalR `/hubs/tracking` + background workers (Simulator, Pickup Timeout, ETA, Retention) |
| **Backend Automated Test Suite** | [`backend/SmartDroneDelivery.Tests/`](../backend/SmartDroneDelivery.Tests) | Baseline above | xUnit integration, opt-in performance and PostgreSQL concurrency tests; [PR #252](https://github.com/nguyentuio1/learning_app/pull/252) is integrated. Execution environments/results remain scoped in the test reports |
| **EF Core Database Migrations** | [`backend/SmartDroneDelivery.Api/Migrations/`](../backend/SmartDroneDelivery.Api/Migrations) | Baseline above | Relational PostgreSQL schema and migration history; Compose/CI reference uses PostgreSQL 16 |
| **Web Portal Source** | [`frontend/web/`](../frontend/web) | Baseline above | React + TypeScript + Ant Design + Vite SPA and Vitest suite (`frontend/web/tests/`) |
| **Mobile Apps Source (Flutter)** | [`frontend/mobile/`](../frontend/mobile) | Baseline above | Customer/Operator flavors and tests. LA-369 removed the unused simulation via [PR #292](https://github.com/nguyentuio1/learning_app/pull/292); production assistant remains gated |
| **Docker & Compose Configuration** | [`docker-compose.yml`](../docker-compose.yml), [`backend/Dockerfile`](../backend/Dockerfile), [`frontend/web/Dockerfile`](../frontend/web/Dockerfile), [`.env.example`](../.env.example) | Baseline above | PostgreSQL, MinIO, API and Nginx web orchestration; instructions do not prove production hosting |

---

## 2. Operational, Backup & Verification Scripts

| Deliverable | Repository Path | Purpose |
| --- | --- | --- |
| **Docker Smoke Test** | [`scripts/docker-smoke-test.sh`](../scripts/docker-smoke-test.sh) | Verifies running Docker Compose stack health and endpoints |
| **Database Backup Scripts** | [`scripts/backup-db.sh`](../scripts/backup-db.sh), [`scripts/backup-db.ps1`](../scripts/backup-db.ps1) | Custom-format (`pg_dump -Fc`) PostgreSQL backup automation (PR #253) |
| **Database Restore Scripts** | [`scripts/restore-db.sh`](../scripts/restore-db.sh), [`scripts/restore-db.ps1`](../scripts/restore-db.ps1) | `pg_restore` verification and recovery automation (PR #253) |
| **Performance Load Script** | [`scripts/k6-load-test.js`](../scripts/k6-load-test.js) | Integrated mixed-endpoint workload; [performance report](testing/reports/performance-report.md) retains historical failures and later measured reference passes |
| **Demo Workflow Smoke Script** | [`scripts/demo-smoke.py`](../scripts/demo-smoke.py) | Explicit loopback Development workflow; optional simulator completion follows the scheduling rule and is not a team rehearsal |
| **CI/CD Workflows** | [`.github/workflows/ci.yml`](../.github/workflows/ci.yml), [`.github/workflows/docker.yml`](../.github/workflows/docker.yml) | Automated Backend (PostgreSQL 16), Web, Mobile, and Docker image build pipelines |

---

## 3. Requirements, Design & API Documentation

| Deliverable | Repository Path | Coverage |
| --- | --- | --- |
| **Software Requirements Specification (SRS)** | [`docs/requirements/SRS.md`](requirements/SRS.md) | Consolidated SRS, actors, `FR-01`–`FR-34`, `NFR-01`–`NFR-09`, traceability appendices |
| **Use Cases, Functional Specs & Rules** | [`docs/requirements/use-cases.md`](requirements/use-cases.md), [`docs/requirements/business-rules.md`](requirements/business-rules.md), [`docs/requirements/application-messages.md`](requirements/application-messages.md) | `UC-01`–`UC-22`, domain business rules (`BR-*`), UI message catalog (`MSG-*`) |
| **Design & Architecture Docs** | [`docs/design/`](design) | Architecture, ERD, class specifications, and sequence diagrams |
| **API Documentation** | [Service index](api/README.md), [implemented HTTP catalog](api/api-documentation.md), [historical handoffs](api/history/README.md) | Authentication, roles, request/response boundaries, errors and Swagger usage. SignalR protocol and partial sprint OpenAPI snapshots retain their dated scope; they are not a full current schema export |
| **AI Policy Proposals** | [Decision index](decisions/README.md) | Local retention/cloud processing proposals and missing approval evidence, kept separate from deployment instructions and test outcomes |

---

## 4. Testing, Operations, Guides & Final Report Deliverables

| Deliverable | Repository Path | Coverage |
| --- | --- | --- |
| **Test Scope, Plan & Test Cases** | [Testing index](testing/README.md), [strategy](testing/test-scope-and-strategy.md), [plan](testing/test-plan.md), [module cases](testing/test-cases-per-module.md) | QA strategy and functional test cases, with responsibilities/results distinguished from proposed schedules |
| **Test Execution & Performance Reports** | [`docs/testing/reports/test-report.md`](testing/reports/test-report.md), [performance report](testing/reports/performance-report.md), [`docs/testing/evidence/`](testing/evidence) | Execution logs, defect resolution log, historical failures and later scoped reference passes (PR #251, PR #252) |
| **Deployment & Backup Guides** | [`docs/deployment/deployment-guide.md`](deployment/deployment-guide.md), [`docs/deployment/database-backup-guide.md`](deployment/database-backup-guide.md) | Environment setup, Docker deployment, health probes, and PostgreSQL backup/restore (PR #253, PR #254) |
| **Installation & Role User Manuals** | [`docs/guides/README.md`](guides/README.md), [`docs/guides/installation-guide.md`](guides/installation-guide.md), [`docs/guides/user-manual.md`](guides/user-manual.md) | Docker/Web/Android installation and 5-role operational manual (PR #256, PR #257, PR #258) |
| **Final Project Report Chapters (I–VI)** | [`docs/report/`](report) | Consolidated chapters `01`–`06` (`LA-204`, `LA-252`–`LA-255`) |
| **Runtime Acceptance Records** | [Acceptance index](testing/acceptance/README.md), [raw evidence](testing/evidence/README.md) | Recorded client/service environments, tested revisions and limitations; original log/image/JSON/SQL files retained |
| **Demo & Rehearsal** | [Demo script](demo/demo-script.md), [rehearsal record](demo/demo-rehearsal.md) | Prepared runbook and a separate actual execution log; component checks do not prove a performed team rehearsal |

## 5. Acceptance and release boundaries

The v1.0.0 source/report release exists; it does not establish production hosting or a signed/native mobile acceptance run. LA-204 consolidation/export is Done following the owner's explicit acceptance; instructor evaluation is separate. Flutter MinIO upload/display has [actual runtime evidence](testing/acceptance/flutter-photo-upload.md). LA-368 API organization and LA-369 obsolete simulation cleanup are integrated in the baseline above. Enabled-AI policy/quality/load acceptance (LA-348), native/WebSocket verification (LA-370, PR #291 still under review) and whole-team rehearsal (LA-49/LA-188) remain open. Historical failures and later performance passes remain scoped to their recorded workload/environment. Reorganizing these documents creates no new test, deployment or Confluence publication result.
