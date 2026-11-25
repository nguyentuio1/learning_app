# Sprint 8 Final Demo Rehearsal Plan & Runbook (LA-188)

Owner: [LA-188](https://hungndemail.atlassian.net/browse/LA-188), Hung Nguyen. Parent story: [LA-49](https://hungndemail.atlassian.net/browse/LA-49) (`story/LA-49-prepare-final-demo`).
Companion demo script: [Final demo script (LA-186)](demo-script.md).

> [!IMPORTANT]
> This runbook defines the pre-flight startup configuration, rehearsal guardrails, and live-rehearsal log for the whole-team dry-run of [demo-script.md](demo-script.md) on integrated `develop` (`cc1fc256cb7fdb4ee3b89234cb90b7d868235de0` or newer). Automated build and CI checks recorded below do **not** establish that the live demo works end-to-end; live rehearsal attendance, step-by-step `PASS`/`FAIL`/`BLOCKED` results, and Web + Flutter screenshots/videos must be recorded after the team runs through the script together.

---

## 1. Pre-flight environment checklist (post-LA-47 / LA-328)

Choose **one** startup path (do not mix them, as both bind port `5055`):

1. **Path A — One-command Docker stack (recommended for the demo):**
   - Copy `.env.example` to `.env` and populate every interpolated variable (`POSTGRES_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `JWT_KEY`, `ADMIN_PASSWORD`), plus `ASPNETCORE_ENVIRONMENT=Development`, `DroneSimulator__Enabled=true`, and `DeliveryFlight__CruiseSpeedMps=60`.
   - `docker-compose.yml` forwards `DroneSimulator__Enabled` and `DeliveryFlight__CruiseSpeedMps` into `services.api.environment` (defaulting to `false` and `12` when unset). Verify the effective API container environment before starting: `docker compose config` (confirm `DroneSimulator__Enabled: "true"` and `DeliveryFlight__CruiseSpeedMps: "60"` under `services.api.environment`).
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

On `develop` (`cc1fc256cb7fdb4ee3b89234cb90b7d868235de0`, including **LA-187** demo data), the Development seed (`SeedDevelopmentDataAsync`) already seeds stations `HCM-01` and `HCM-02` (`OPERATIONAL`), `HCM-03` (`MAINTENANCE`), `Customer`, `Dispatcher`, `LogisticsManager` (`MANAGER`), origin/destination station operators (`HCM-01` and `HCM-02`), and `IDLE` drone(s) *(older pre-LA-187 checkouts lacked the drone, `MANAGER`, and `HCM-02` operator seeds)*. Before rehearsing [demo-script.md](demo-script.md) (demo order **`HCM-01` -> `HCM-02`**), verify these prerequisites in the live environment:

| Scene(s) in `demo-script.md` | Prerequisite / Guardrail | Why / Failure avoided |
|---|---|---|
| **1, 3** (Setup & Dispatch) | Verify at least one `IDLE` drone exists in **Admin -> Drone fleet** matching the demo package payload/range (create one if missing). | Scene 3 scheduling fails without an eligible `IDLE` drone. |
| **1, 8** (Setup & Reports) | Verify the seeded `LogisticsManager` (`MANAGER`) account can sign in and view **Dashboard & reports**. | Required for Scene 8 metrics/reports verification. |
| **3, 4, 5** (Schedule -> Intake -> Flight) | In Scene 3, save `ScheduledDepartureTime` **at least 16 minutes ahead**, then assign the drone and complete intake before takeoff; ensure `DroneSimulator:Enabled=true`. Plan **25–30 minutes** for the full mission and pickup rehearsal. | Real scheduling requires at least 15 minutes lead time. `DroneSimulator:Enabled` defaults to `false` (`appsettings.json`), and `DroneSimulatorService` only takes off a `READY_FOR_TAKEOFF` order once `ScheduledDepartureTime <= now`. |
| **4, 6a/6b** (Origin Intake & Destination Pickup) | Verify `HCM-01` and `HCM-02` each have their assigned `StationOperator` before Scene 4/6a (or use Customer self-confirmation in the Customer app for `6b`). | `StationOperationService` enforces `station.OperatorId == caller` (`403` otherwise). |
| **6a/6b** (Secure Code Lockout) | Allow **at most 2 wrong codes**; the **3rd** wrong code locks the order for 10 minutes (`HTTP 423`, `OtpLockoutManager`: `failedLogs.Count >= 3`, `AddMinutes(10)`). | Prevents locking the demo order mid-rehearsal. |
| **7** (AI features) | Verify ETA and Delivery Summary either produce results or report their actual unavailable state honestly. Keep **Customer chat** disabled (`BLOCKED`) unless the cloud retention policy is explicitly approved. | Do not self-enable Customer chat or mark it `PASS` while disabled by policy. |
| **9** (Admin Audit Logs) | Verify **LA-177** (`AuditLogsPage` / `AuditLogsController`, merged via `#241`) is present and filters read-only audit records. | Required for Scene 9 audit verification. |

---

## 3. Rehearsal verification and issues log

### 3.1 Pre-rehearsal technical checks (observed)

| Check | Command / Source | Result | Scope / Notes |
|---|---|---|---|
| Integrated `develop` CI (`22be768` -> `cc1fc256cb7fdb4ee3b89234cb90b7d868235de0`) | [CI run 44263268429](https://github.com/nguyentuio1/learning_app/actions/runs/44263268429) · [Docker smoke 44263268425](https://github.com/nguyentuio1/learning_app/actions/runs/44263268425) | `Backend`, `Web`, `Mobile`, and `Docker compose smoke` passed | Automated CI handoff prior to live team rehearsal; does not replace the live dry-run. |

### 3.2 Live team rehearsal session metadata

> [!NOTE]
> Complete Sections 3.2 and 3.3 during the whole-team dry-run (`Hưng`, `Khải`, `Nhân`, ...) before closing **LA-188** / **LA-49**. Keep rows `Pending` until actually executed.

| Field | Value |
|---|---|
| **Date & Start / End time (`Asia/Ho_Chi_Minh`)** | *Pending live rehearsal* |
| **Participants** | *Pending (e.g., Hung Nguyen, Phan Van Khai, Nhan Luu Thien)* |
| **Environment** | *Pending (Docker Path A / Dev Path B + Android/iOS target)* |
| **Remote `develop` SHA** | `cc1fc256cb7fdb4ee3b89234cb90b7d868235de0` *(update if newer)* |
| **CI run URLs (Backend / Web / Mobile)** | *Link current run URLs for the rehearsed SHA* |
| **Web + Flutter Customer/Operator evidence** | *Attach screenshot/recording links for Web, Customer app, and Operator app* |
| **Open bugs / Linked tickets / Re-run result** | *Record any `FAIL` tickets, fix PRs, and re-test outcomes* |

### 3.3 Step-by-step live rehearsal checklist (`PASS` / `FAIL` / `BLOCKED`)

| Step | Actor & Client | Verification criteria | Result (`PASS`/`FAIL`/`BLOCKED`) | Notes / Ticket / Evidence link |
|---|---|---|---|---|
| **1** | `Customer` (Web / Mobile) | Create order + upload package photo; verify photo reloads from MinIO; save 6-digit pickup code. | *Pending* | — |
| **2** | `Dispatcher` (Web) | Approve order, schedule departure **>= 16 min ahead**, and assign eligible `IDLE` drone. | *Pending* | — |
| **3** | `StationOperator` (`HCM-01`, Flutter Operator app) | Origin package intake (`APPROVED -> READY_FOR_TAKEOFF`) and slot occupancy update. | *Pending* | — |
| **4** | Simulator + Tracking (`Dispatcher` / `Customer`) | Active simulator mission moves on basemap/live map (`IN_TRANSIT -> ARRIVED`). | *Pending* | — |
| **5** | `StationOperator` (`HCM-02`) / `Customer` (Flutter) | Confirm pickup / receipt (`ARRIVED -> DELIVERED`) and release destination slot. | *Pending* | — |
| **6** | `LogisticsManager` (Web) | Dashboard metrics & reports reflect completed order. | *Pending* | — |
| **7** | `SystemAdministrator` (Web) | Admin management & read-only Audit Logs filter the demo order actions. | *Pending* | — |
| **8** | Media & AI checks | Avatar/MinIO photo reload succeeds; AI ETA & Delivery Summary return output or honest unavailable status. | *Pending* | — |
| **9** | Customer AI Chat | Remains disabled pending cloud chat retention policy approval. | `BLOCKED` *(expected while disabled by policy)* | Do not enable or mark `PASS` without policy approval. |
