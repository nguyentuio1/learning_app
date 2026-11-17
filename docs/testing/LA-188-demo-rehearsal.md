# Sprint 8 Final Demo Rehearsal Plan & Runbook (LA-188)

Owner: [LA-188](https://hungndemail.atlassian.net/browse/LA-188), Hung Nguyen. Parent story: [LA-49](https://hungndemail.atlassian.net/browse/LA-49) (`story/LA-49-prepare-final-demo`).

> [!IMPORTANT]
> This document defines the rehearsal script, pre-flight environment checks, and issue-tracking table for the whole-team demo rehearsal. Automated build/CI checks recorded here do **not** substitute for the live team dry-run; live rehearsal attendance and issue fixes must be recorded after the team executes the runbook.

---

## 1. Pre-flight environment checklist (post-LA-328)

Before starting the rehearsal, verify every service starts cleanly without committed secrets:

1. **Infrastructure (`docker compose up -d`):**
   - Copy `.env.example` to `.env` and populate `POSTGRES_PASSWORD`, `MINIO_ROOT_USER`, and `MINIO_ROOT_PASSWORD`.
   - Confirm PostgreSQL 16 (`localhost:5433`, database `drone_delivery_db`) and MinIO (`localhost:9000` / console `localhost:9001`) are healthy.
2. **Backend API (`backend/SmartDroneDelivery.Api`):**
   - Configure `dotnet user-secrets` (`ConnectionStrings:DefaultConnection`, `Minio:AccessKey`, `Minio:SecretKey`, `Jwt:Key`, `Seed:AdminPassword`).
   - Run `dotnet run --launch-profile http` -> verify `http://localhost:5055/swagger` loads and EF Core migrations complete without errors.
   - If using synthetic demo data, verify `Seed:DevelopmentData` is enabled only for local rehearsal and never commit real credentials.
3. **Web Portal (`frontend/web`):**
   - Run `npm ci` and `npm run dev` -> open `http://localhost:5173` (proxies `/api` and `/hubs/tracking` to `http://localhost:5055`).
4. **Flutter Mobile Apps (`frontend/mobile`):**
   - Run `flutter pub get`.
   - Customer app: `flutter run -t lib/main_customer.dart --flavor customer` (use `--dart-define=API_BASE_URL=http://<host>:5055/api` on physical devices).
   - Station Operator app: `flutter run -t lib/main_operator.dart --flavor operator`.

---

## 2. End-to-end live demo script

| Scene | Role & Client | Actions to demonstrate | Expected observable result | Fallback / guard if something fails |
|---|---|---|---|---|
| **1. System setup** | `SystemAdministrator` (Web) | Sign in -> verify 5 roles, operational stations, drone fleet, and `StationOperator` station assignment. | Stations show `OPERATIONAL` with `EMPTY` slots; operator is assigned to origin/destination station. | Re-check user role and station assignment in Admin -> Users. |
| **2. Order creation** | `Customer` (Mobile or Web) | Manage address & package -> create delivery order -> **record the one-time 6-digit pickup code**. | Order created in `PENDING`; 6-digit code shown once at creation. | If validation fails, check package weight/dimensions against drone limits. |
| **3. Dispatch & schedule** | `Dispatcher` (Web) | Open pending order -> Approve (`PENDING -> APPROVED`) -> select departure & assign eligible drone/slots. | Order moves to `APPROVED` with a `PLANNED` mission. | If `409 Conflict` occurs, refresh list and pick another free drone/slot. |
| **4. Origin intake** | `StationOperator` (Mobile) | Tap **Origin Intake** -> look up tracking number -> select `EMPTY` slot -> enter measured weight/dimensions & visual check -> **Confirm Intake** (also show slot `EMPTY <-> ERROR` toggle in detail sheet). | Order transitions `APPROVED -> READY_FOR_TAKEOFF`; slot becomes `OCCUPIED`. | If slot was taken during polling, dropdown reconciles to remaining `EMPTY` slot; confirm `PLANNED` mission exists. |
| **5. Live tracking & AI** | `Customer` & `Dispatcher` | Start/run simulator flight -> observe SignalR `IN_TRANSIT` & `ARRIVED` updates, live map, ETA countdown, and Delivery Summary (plus AI Assistant if enabled). | Map & status timeline update live; ETA shows countdown/overdue state; summary renders delivery record. | Note if AI provider/retention gates are off (local/default-off adapter behavior); never retry uncertain assistant POSTs without history recovery. |
| **6. Destination pickup** | `StationOperator` (Mobile) / `Customer` | Operator taps **Confirm Pickup** -> looks up `ARRIVED` order -> enters 6-digit pickup code (or Customer confirms delivery). Show `PickupTimeoutAlertsCard` for overdue parcels. | Order transitions `ARRIVED -> DELIVERED`; destination slot returns to `EMPTY`. | Avoid >3 wrong codes to prevent `423` temporary lockout during live demo. |
| **7. Reports & audit** | `LogisticsManager` / `SystemAdministrator` (Web) | Filter reporting period on Dashboard/Reports; open Admin Audit Logs. | Delivery metrics, completed count, and chronological audit entries reflect the demo order. | Refresh reporting range if cached metrics lag. |

---

## 3. Rehearsal verification and issues log

### 3.1 Pre-rehearsal technical checks (observed)

| Check | Command / Source | Result | Notes |
|---|---|---|---|
| Backend build (`feature/LA-188-rehearse-demo`) | `dotnet build backend/SmartDroneDelivery.sln --no-restore` | Passed (`0 Warning(s)`, `0 Error(s)`) | Executed locally on `2026-11-17` against `story/LA-49-prepare-final-demo` (`6954667`). |
| Full repository CI (`Backend`, `Web`, `Mobile`) | GitHub Actions PR workflow | Verified on PR | Covers `dotnet test`, `npm run lint/test/build`, and `flutter analyze/test`. |

### 3.2 Live team rehearsal log

> [!NOTE]
> Complete this table during the whole-team dry-run before closing **LA-188** / **LA-49**.

| Rehearsal session | Date & Participants | Problems found | Fixes / PRs applied | Sign-off |
|---|---|---|---|---|
| Dry-run #1 (Full team) | *Pending live rehearsal* | *Record any startup, seed, UI, or timing issues observed* | *Link fix commits/PRs* | *Pending* |
