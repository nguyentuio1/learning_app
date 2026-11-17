# LA-251: Deliverable Package List — SmartDroneDelivery

**Baseline Reference:** Branch `develop` at commit `2148e50` (including Sprint 1–7 releases `v0.3.0` / `release/sprint-5`, `release/sprint-6`, `release/sprint-7`)  
**Prepared By:** Phan Văn Khải

---

## 1. Source Code & Infrastructure Deliverables

| Deliverable | Repository Path | Version / Reference | Description |
| --- | --- | --- | --- |
| **Backend API Source** | [`backend/SmartDroneDelivery.Api/`](../../backend/SmartDroneDelivery.Api) | `develop` (`2148e50`) | ASP.NET Core 8 REST API + SignalR `/hubs/tracking` + background workers (Simulator, Pickup Timeout, ETA, Retention) |
| **Backend Automated Test Suite** | [`backend/SmartDroneDelivery.Tests/`](../../backend/SmartDroneDelivery.Tests) | `develop` (`2148e50`) + PR #247 | xUnit integration, PostgreSQL 16 concurrency, and performance benchmark tests |
| **EF Core Database Migrations** | [`backend/SmartDroneDelivery.Api/Migrations/`](../../backend/SmartDroneDelivery.Api/Migrations) | `develop` (`2148e50`) | Relational PostgreSQL 16 schema and migration history |
| **Web Portal Source** | [`frontend/web/`](../../frontend/web) | `develop` (`2148e50`) | React 19 + TypeScript + Ant Design + Vite SPA and Vitest suite (`frontend/web/tests/`) |
| **Mobile Apps Source (Flutter)** | [`frontend/mobile/`](../../frontend/mobile) | `develop` (`2148e50`) | Flutter/Riverpod codebase with `customer` and `operator` Android flavors and widget/integration tests (`frontend/mobile/test/`) |
| **Docker & Compose Configuration** | [`docker-compose.yml`](../../docker-compose.yml), [`backend/Dockerfile`](../../backend/Dockerfile), [`frontend/web/Dockerfile`](../../frontend/web/Dockerfile), [`.env.example`](../../.env.example) | `develop` (`2148e50`) | Full-stack container orchestration (PostgreSQL 16, MinIO, API, Nginx Web Portal) |

---

## 2. Operational, Backup & Verification Scripts

| Deliverable | Repository Path | Purpose |
| --- | --- | --- |
| **Docker Smoke Test** | [`scripts/docker-smoke-test.sh`](../../scripts/docker-smoke-test.sh) | Verifies running Docker Compose stack health and endpoints |
| **Database Backup Scripts** | [`scripts/backup-db.sh`](../../scripts/backup-db.sh), [`scripts/backup-db.ps1`](../../scripts/backup-db.ps1) | Custom-format (`pg_dump -Fc`) PostgreSQL backup automation |
| **Database Restore Scripts** | [`scripts/restore-db.sh`](../../scripts/restore-db.sh), [`scripts/restore-db.ps1`](../../scripts/restore-db.ps1) | `pg_restore` verification and recovery automation |
| **Performance Load Script** | [`scripts/k6-load-test.js`](../../scripts/k6-load-test.js) | k6 10 / 50 / 100 VU load & concurrency script (PR #247) |
| **CI/CD Workflows** | [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), [`.github/workflows/docker.yml`](../../.github/workflows/docker.yml) | Automated Backend (PostgreSQL 16), Web, Mobile, and Docker image build pipelines |

---

## 3. Requirements, Design & API Documentation

| Deliverable | Repository Path | Coverage |
| --- | --- | --- |
| **Software Requirements Specification (SRS)** | [`docs/requirements/SRS.md`](../requirements/SRS.md) | Consolidated SRS, actors, `FR-01`–`FR-34`, `NFR-01`–`NFR-09`, traceability appendices |
| **Use Cases, Functional Specs & Rules** | [`docs/requirements/use-cases.md`](../requirements/use-cases.md), [`docs/requirements/business-rules.md`](../requirements/business-rules.md), [`docs/requirements/application-messages.md`](../requirements/application-messages.md) | `UC-01`–`UC-22`, domain business rules (`BR-*`), UI message catalog (`MSG-*`) |
| **Design & Architecture Docs** | [`docs/design/`](../design) | Architecture, ERD, class specifications, and sequence diagrams |
| **Consolidated API Documentation** | [`docs/api/API_Documentation.md`](../api/API_Documentation.md) | Complete REST/SignalR endpoint catalog, JWT/`X-API-Key` auth, error codes, Swagger usage |
| **Module API Contracts & OpenAPI** | [`docs/api/auth-api.md`](../api/auth-api.md), [`docs/api/customer-order-api.md`](../api/customer-order-api.md), [`docs/api/station-api.md`](../api/station-api.md), [`docs/api/drone-api.md`](../api/drone-api.md), [`docs/api/delivery-workflow-api.md`](../api/delivery-workflow-api.md), [`docs/api/notification-api.md`](../api/notification-api.md), [`docs/api/sprint-6-api-contract.md`](../api/sprint-6-api-contract.md), [`docs/api/sprint-6-openapi.json`](../api/sprint-6-openapi.json), [`docs/api/sprint-7-api-contract.md`](../api/sprint-7-api-contract.md), [`docs/api/sprint-7-openapi.json`](../api/sprint-7-openapi.json) | Detailed module HTTP contracts and OpenAPI 3.0 schemas |

---

## 4. Testing, Operations, Guides & Final Report Deliverables

| Deliverable | Repository Path | Coverage |
| --- | --- | --- |
| **Test Scope, Plan & Test Cases** | [`docs/testing/test-scope-and-strategy.md`](../testing/test-scope-and-strategy.md), [`docs/testing/test-plan.md`](../testing/test-plan.md), [`docs/testing/test-cases-per-module.md`](../testing/test-cases-per-module.md) | QA strategy, test schedule, and `FR-01`–`FR-34` test cases |
| **Test Execution & Performance Reports** | [`docs/testing/test-report.md`](../testing/test-report.md), [`docs/testing/PerformanceTestReport.md`](../testing/PerformanceTestReport.md), [`docs/testing/evidence/`](../testing/evidence) | Execution logs, defect resolution log, and 10/50/100 concurrency metrics |
| **Deployment & Backup Guides** | [`docs/operations/DeploymentGuide.md`](../operations/DeploymentGuide.md), [`docs/operations/DatabaseBackupGuide.md`](../operations/DatabaseBackupGuide.md) | Environment setup, Docker deployment, health probes, and PostgreSQL backup/restore |
| **Installation & Role User Manuals** | [`docs/guides/README.md`](../guides/README.md), [`docs/guides/InstallationGuide.md`](../guides/InstallationGuide.md), [`docs/guides/UserManual.md`](../guides/UserManual.md) | Docker/Web/Android installation and 5-role operational manual |
| **Final Project Report Chapters (I–VI)** | [`docs/report/`](../report) | Consolidated chapters `01`–`06` (`LA-204`, `LA-252`–`LA-255`) |
