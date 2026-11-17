# Sprint 8 Final Demo Rehearsal Plan & Runbook (LA-188)

Owner: [LA-188](https://hungndemail.atlassian.net/browse/LA-188), Hung Nguyen. Parent story: [LA-49](https://hungndemail.atlassian.net/browse/LA-49) (`story/LA-49-prepare-final-demo`).
Companion demo script: [Final demo script (LA-186)](../demo/demo-script.md).

> [!IMPORTANT]
> This runbook defines the pre-flight startup configuration, rehearsal guardrails, and issue-tracking log for the whole-team dry-run of [demo-script.md](../demo/demo-script.md). Automated build and CI checks recorded below do **not** establish that the live demo works end-to-end; live rehearsal attendance and issue fixes must be recorded after the team runs through the script together.

---

## 1. Pre-flight environment checklist (post-LA-47 / LA-328)

Choose **one** startup path (do not mix them, as both bind port `5055`):

1. **Path A — One-command Docker stack (recommended for the demo):**
   - Copy `.env.example` to `.env` and populate every interpolated variable (`POSTGRES_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `JWT_KEY`, `ADMIN_PASSWORD`), plus `ASPNETCORE_ENVIRONMENT=Development`, `DroneSimulator__Enabled=true`, and `DeliveryFlight__CruiseSpeedMps=60`.
   - Run `docker compose up -d --build --wait`.
   - Open the web portal at `http://localhost:8080` and verify `curl http://localhost:5055/health/ready` reports `"status":"Healthy"`.
2. **Path B — Developer servers (local hot-reload):**
   - Copy `.env.example` to `.env` and populate all required variables (including `JWT_KEY` and `ADMIN_PASSWORD`, because Docker Compose interpolates the whole file even when starting infrastructure only).
   - Start infrastructure only (avoids port `5055` conflict): `docker compose up -d postgres minio`.
   - In `backend/SmartDroneDelivery.Api`, configure `dotnet user-secrets` (`ConnectionStrings:DefaultConnection`, `Minio:AccessKey`, `Minio:SecretKey`, `Jwt:Key`, `Seed:AdminPassword`, and `"DroneSimulator:Enabled" "true"` plus `"DeliveryFlight:CruiseSpeedMps" "60"`), then run `dotnet run --launch-profile http` (`http://localhost:5055/swagger`).
   - In `frontend/web`, run `npm ci` and `npm run dev` (`http://localhost:5173`).
3. **Flutter Mobile Apps (`frontend/mobile`):**
   - Run `flutter pub get`.
   - Customer app: `flutter run -t lib/main_customer.dart --flavor customer --dart-define=API_BASE_URL=http://<host>:5055/api`
   - Station Operator app: `flutter run -t lib/main_operator.dart --flavor operator --dart-define=API_BASE_URL=http://<host>:5055/api`

---

## 2. Rehearsal data prerequisites & scene guardrails

The Development seed (`SeedDevelopmentDataAsync`) creates stations `HCM-01` and `HCM-02` (`OPERATIONAL`), `HCM-03` (`MAINTENANCE`), plus `customer@...`, `dispatcher@...`, and one operator (`operator@...` assigned to `HCM-01`). Before rehearsing [demo-script.md](../demo/demo-script.md) (demo order **`HCM-01` -> `HCM-02`**), verify these prerequisites—either via **LA-187** demo data (Nhân Lưu Thiện) or manual Admin setup in Scene 1:

| Scene(s) in `demo-script.md` | Prerequisite / Guardrail | Why / Failure avoided |
|---|---|---|
| **1, 3** (Setup & Dispatch) | Create at least one `IDLE` drone in **Admin -> Drone fleet** matching the demo package payload/range. | `SeedDevelopmentDataAsync` seeds no drones; Scene 3 scheduling fails without an `IDLE` drone. |
| **1, 8** (Setup & Reports) | Create a `LogisticsManager` (`MANAGER`) account in **Admin -> Users**. | `SeedDevelopmentDataAsync` seeds no `MANAGER` account for Scene 8 dashboard/reports. |
| **3, 4, 5** (Schedule -> Intake -> Flight) | In Scene 3, set `ScheduledDepartureTime` **3–4 minutes ahead** so Scene 4 intake finishes before takeoff; ensure `DroneSimulator:Enabled=true`. | `DroneSimulator:Enabled` defaults to `false` (`appsettings.json`), and `DroneSimulatorService` only takes off a `READY_FOR_TAKEOFF` order once `ScheduledDepartureTime <= now`. |
| **4, 6a/6b** (Origin Intake & Destination Pickup) | Assign a second `StationOperator` to destination `HCM-02` before Scene 6a, **or** use Customer self-confirmation in the Customer app (`6b`). | `StationOperationService` enforces `station.OperatorId == caller` (`403` otherwise); `operator@...` is assigned only to `HCM-01` and cannot confirm pickup at `HCM-02`. |
| **6a/6b** (Secure Code Lockout) | Allow **at most 2 wrong codes**; the **3rd** wrong code locks the order for 10 minutes (`HTTP 423`, `OtpLockoutManager`: `failedLogs.Count >= 3`, `AddMinutes(10)`). | Prevents locking the demo order mid-rehearsal. |
| **9** (Admin Audit Logs) | Verify **LA-177** (`AuditLogsPage` / `AuditLogsController`, merged via `#241`) is present on the rehearsal checkout. | Required for Scene 9 read-only audit filtering. |

---

## 3. Rehearsal verification and issues log

### 3.1 Pre-rehearsal technical checks (observed)

| Check | Command / Source | Result | Scope / Notes |
|---|---|---|---|
| Backend build (`feature/LA-188-rehearse-demo`) | `dotnet build backend/SmartDroneDelivery.sln --no-restore` | `Build succeeded` (`0 Warning(s)`, `0 Error(s)`) | Build verification only (not a test run), executed locally on `2026-11-17`. |
| Repository CI (`32c33b3f25af5dee5a9930e7053ed360ea895ce2`) | [GitHub Actions run 44070729998](https://github.com/nguyentuio1/learning_app/actions/runs/44070729998) | `Backend tests`, `Web lint, test and build`, `Mobile analyze and test` all `success` | Automated CI on PR #240 prior to story merge; does not replace the live team dry-run. |

### 3.2 Live team rehearsal log

> [!NOTE]
> Complete this table during the whole-team dry-run before closing **LA-188** / **LA-49**.

| Rehearsal session | Date & Participants | Problems found | Fixes / PRs applied | Sign-off |
|---|---|---|---|---|
| Dry-run #1 (Full team) | *Pending live rehearsal* | *Record any startup, seed, UI, or timing issues observed* | *Link fix commits/PRs* | *Pending* |
