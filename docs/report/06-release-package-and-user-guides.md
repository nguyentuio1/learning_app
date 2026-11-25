# VI. Release Package & User Guides

> **Current handoff alignment (LA-351 - 19 November 2026):** Integrated documentation baseline [`a90668c`](https://github.com/nguyentuio1/learning_app/commit/a90668c), combined application `10c77d0`. LA-48 technical documentation and LA-203 guides are integrated, including [PR #280](https://github.com/nguyentuio1/learning_app/pull/280); prior pending-story labels for PRs #255?#259 are superseded. Standalone [Installation guide](../guides/installation-guide.md), [User manual](../guides/user-manual.md), [Deployment guide](../deployment/deployment-guide.md), and [Database backup guide](../deployment/database-backup-guide.md) are available in this integrated source.
>
> **Status correction, 25 November 2026:** PR #288 integrated the final candidate and LA-351 corrections at develop `24c03ab`; PR #289 released v1.0.0. Supported API-backed browser [Customer](../testing/acceptance/customer-journey.md) and [Operator](../testing/acceptance/operator-journey.md) runs disclose their prepared ARRIVED fixtures. [Flutter MinIO upload/display](../testing/acceptance/flutter-photo-upload.md) is verified within its runtime scope. Native Android/iOS/WebSocket evidence (LA-370), enabled-AI policy/quality/latency (LA-348) and actual whole-team rehearsal (LA-49/LA-188) remain separate. LA-204 consolidation/export is owner-accepted and Done; instructor evaluation is separate. See the [report index](README.md) for current release assets and historical publication scope.

The package is a source and documentation handoff. Build instructions describe how to prepare distributable files; they do not assert a signed mobile package, production deployment or new release tag. The existing `v0.3.0` release is an earlier Sprint 3 pre-release. Dated historical sources retain their original scope.

## 1. Deliverable package

| Component | Location and handoff |
|---|---|
| Backend | [`backend/SmartDroneDelivery.Api`](../../backend/SmartDroneDelivery.Api), an ASP.NET Core 8 API; [`backend/SmartDroneDelivery.Tests`](../../backend/SmartDroneDelivery.Tests) contains automated verification. |
| Database | EF Core [`Migrations`](../../backend/SmartDroneDelivery.Api/Migrations) and [`AppDbContext.cs`](../../backend/SmartDroneDelivery.Api/Data/AppDbContext.cs). PostgreSQL 16 is the CI/local Compose reference. |
| Web portal | [`frontend/web`](../../frontend/web), React/TypeScript with its committed npm lockfile; `npm run build` produces `frontend/web/dist`. |
| Mobile apps | [`frontend/mobile`](../../frontend/mobile), one Flutter codebase with Customer and Station Operator entrypoints. Android has `customer` and `operator` product flavors. |
| Local infrastructure | [`docker-compose.yml`](../../docker-compose.yml) defines full-stack `postgres` (`5433`), `minio` (`9000`/`9001`), `api` (`5055`), and `web` (`8080`) with persistent named volumes; [`docker-compose.gemini.yml`](../../docker-compose.gemini.yml) adds optional Gemini AI overlay. |
| Documentation | Chapters I–VI, API contracts, design diagrams, business rules and test evidence. [Report contents](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) identifies the existing Confluence publication pages. |

To package a checkout, record the exact commit and use the committed lockfiles. Create an output directory, archive the chosen source commit, publish the API, build the web client and produce any required mobile packages using an appropriately configured platform toolchain. These are preparation commands from the repository root:

```powershell
New-Item -ItemType Directory -Force artifacts/release
git archive --format=zip --output=artifacts/release/source.zip a90668c
dotnet publish backend/SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj -c Release -o artifacts/release/api
npm --prefix frontend/web ci
npm --prefix frontend/web run build
```

With the .NET EF CLI tool matching the project's EF Core 8 dependencies installed, an idempotent database script can be generated using `dotnet ef migrations script --idempotent --project backend/SmartDroneDelivery.Api --output artifacts/release/database.sql`. Review it for the target database before applying it. Keep environment credentials, signing material and real customer data outside the archive.

## 2. Installation guides

### 2.1 System requirements

#### 2.1.1 Hardware requirements

A development workstation must support the chosen .NET, Node.js, Docker and Flutter toolchains. Allocate Docker storage for PostgreSQL/MinIO volumes and sufficient resources for any Android emulator. A physical device can use a reachable host API address instead. CPU, RAM and storage minima have not been measured for this project, so this guide does not present an untested minimum as a product guarantee. Native iOS builds require a macOS/Xcode environment; Android and iOS signing or store submission are separate packaging steps.

#### 2.1.2 Software requirements

Use .NET SDK 8, Node.js 24 with npm (the version selected by CI), Docker Compose, and Flutter stable with Dart compatible with `sdk: ^3.13.5` in [`pubspec.yaml`](../../frontend/mobile/pubspec.yaml). Android build settings use Java 17 compatibility. The repository's current iOS project contains the `Runner` scheme; distinct `customer`/`operator` iOS schemes must not be assumed to exist. Check native configuration before packaging two separate iOS applications.

The Android `release` build currently selects the debug signing configuration in `android/app/build.gradle.kts`. Replace it with the intended release signing configuration before distributing an Android release; a release-mode build alone does not establish store-ready signing.

### 2.2 Installation instructions

#### 2.2.1 Required Secrets, MinIO Public URL, and Startup Modes

Committed configuration files contain no live secrets (`LA-328`). Copy [`.env.example`](../../.env.example) to an uncommitted `.env` at the repository root and set `POSTGRES_PASSWORD`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD` (≥ 8 chars), `JWT_KEY` (≥ 32 random bytes), and optional `ADMIN_EMAIL` / `ADMIN_PASSWORD` (8–100 chars, letter + digit).

- **MinIO Public URL (`MINIO_PUBLIC_URL`):** Defaults to `http://localhost:9000`. Because browsers and mobile apps fetch package and avatar photos directly from MinIO, set `MINIO_PUBLIC_URL` in `.env` to a client-reachable host IP/URL (never the internal Docker hostname `minio:9000`) when testing from a physical phone or remote browser (`LA-346`).
- **Gemini AI Overlay (`LA-348`):** AI flags (`EtaPrediction:Enabled`, `DeliverySummary:Enabled`, `CustomerAssistant:Enabled`) are `false` by default. See [gemini-ai.md](../deployment/gemini-ai.md) for `ModelProvider:Provider=Gemini`, `ModelProvider:GeminiApiKey`, and `docker-compose.gemini.yml`. Cloud assistant retention policy `LA-348-gemini-retention-v1` remains a **DRAFT**; stored customer chat stays disabled unless both policy approval and `ModelProvider:GeminiAssistantApprovedPolicyVersion` (plus the web build flag) are configured.

Choose **one** of the two mutually exclusive API startup modes below (do **not** run `dotnet run` on port `5055` while the Docker `api` container is already bound to `5055`):

**Option A — Full-Stack Docker Compose (Recommended for Demo/Integration):**

```bash
cp .env.example .env
# Fill POSTGRES_PASSWORD, MINIO_ROOT_PASSWORD, JWT_KEY, ADMIN_PASSWORD, and MINIO_PUBLIC_URL
docker compose up -d --build --wait
curl -sf http://localhost:5055/health/live
curl -sf http://localhost:5055/health/ready
# Web portal: http://localhost:8080
```

With Gemini AI overlay (for ETA/Summary when `GEMINI_API_KEY` and explicit flags are set in `.env`):

```bash
docker compose -f docker-compose.yml -f docker-compose.gemini.yml up -d --build --wait
```

**Option B — Infrastructure-Only + Local Developer API & Web:**

1. Start only `postgres` (`5433`) and `minio` (`9000`/`9001`):
   ```bash
   docker compose up -d postgres minio
   ```
2. Configure `dotnet user-secrets` once (matching `.env`):
   ```bash
   cd backend/SmartDroneDelivery.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=drone_delivery_db;Username=postgres;Password=<POSTGRES_PASSWORD>"
   dotnet user-secrets set "Minio:AccessKey" "<MINIO_ROOT_USER>"
   dotnet user-secrets set "Minio:SecretKey" "<MINIO_ROOT_PASSWORD>"
   dotnet user-secrets set "Minio:PublicBaseUrl" "http://<reachable-host>:9000"
   dotnet user-secrets set "Jwt:Key" "<at-least-32-random-bytes>"
   dotnet user-secrets set "Seed:AdminPassword" "<your-admin-password>"
   dotnet user-secrets set "DroneSimulator:Enabled" "true"
   cd ../..
   ```
3. Run the API (`Development` profile on port `5055`, Swagger at `http://localhost:5055/swagger`):
   ```bash
   dotnet run --project backend/SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj --launch-profile http
   ```
4. Staff access requires persisted roles and, for station operations, the assigned station. Use administrator-managed staff accounts and the synthetic Development data described in [deployment-guide.md](../deployment/deployment-guide.md) and [demo-script.md](../demo/demo-script.md). Do not publish a real user's password or pickup code in report evidence.

#### 2.2.2 Five-Role Demo Data & `DroneSimulator` Settings (Development vs. Production)

- **Production Default:** `DroneSimulator:Enabled` is **`false`** in `appsettings.json`, and Development demo seeding is disabled when `ASPNETCORE_ENVIRONMENT=Production`. Keep `DroneSimulator:Enabled=false` in Production unless a controlled simulation environment is intended.
- **Development 5-Role Demo Seeding (`LA-347`):** Set `ASPNETCORE_ENVIRONMENT=Development` (in `.env` for Option A, or via `--launch-profile http` in Option B) and configure `ADMIN_PASSWORD` / `Seed:AdminPassword`. On startup, `SeedData` creates three stations, four drones, sample orders, and all five roles (`customer@smartdronedelivery.com`, `dispatcher@smartdronedelivery.com`, station operators, `manager@smartdronedelivery.com`, plus the configured admin).
- **Simulator Activation & Flight Timing:** Set `DroneSimulator__Enabled=true` in `.env` (Option A) or `dotnet user-secrets set "DroneSimulator:Enabled" "true"` (Option B); otherwise scheduled missions stay in `READY_FOR_TAKEOFF` and never take off. `DroneSimulator` ticks every `TickIntervalSeconds: 2`. For the `11.3 km` `HCM-01` → `HCM-02` route, default `DeliveryFlight:CruiseSpeedMps` (`12 m/s`) takes ~16 minutes; set `DeliveryFlight__CruiseSpeedMps=60` (~3 minutes) when running a live demo and disclose that flight speed was accelerated.

#### 2.2.3 Front end

For the web portal, run `npm ci` and `npm run dev` in `frontend/web`, then open `http://localhost:5173`. The Vite development server proxies `/api` and `/hubs/tracking` to `VITE_API_BASE_URL`, defaulting to `http://localhost:5055`. For a built web portal, serve `dist` with the application routing fallback and equivalent API/WebSocket routing; the development proxy is not a production server configuration.

For mobile, run the following from `frontend/mobile`:

```bash
flutter pub get
flutter run -t lib/main_customer.dart --flavor customer
flutter run -t lib/main_operator.dart --flavor operator
```

These flavor names are configured for Android. On iOS, select the appropriate Dart entrypoint with the existing native scheme until separate native schemes are configured. API defaults are `http://10.0.2.2:5055/api` for the Android emulator and `http://localhost:5055/api` for other local targets. Override with `--dart-define=API_BASE_URL=http://<reachable-host>:5055/api` when the host differs. A physical device cannot use the workstation's `localhost` address.

### 2.3 Verification and troubleshooting

Backend tests run with `dotnet test backend/SmartDroneDelivery.sln`. Real PostgreSQL transaction/concurrency tests require `TEST_POSTGRES_CONNECTION` pointing to a disposable test server with database-creation permissions. Web checks are `npm run lint`, `npm test` and `npm run build`; mobile checks are `flutter analyze --no-fatal-infos` and `flutter test`. CI executes these layers and uses PostgreSQL 16 for the relational tests. [Chapter V](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927583) records dated runs and their scope.

For a connection failure, check the API process, Compose service health, the client API base address and the host port. For denied access, check authentication, persisted role, station assignment and order ownership. For a `409` conflict, reload the current order or slot state rather than replaying a stale action. `423` pickup lockout and `429` throttling responses include retry guidance. Do not repeatedly resubmit an assistant request whose result is uncertain; recover owned history first.

## 3. User manual

### 3.1 Overview

The web portal provides customer and operational workspaces according to the authenticated role. The two Flutter entrypoints serve Customer and Station Operator workflows. The API is authoritative for authorization and state changes. A recorded position is not a flight command; the software simulator represents the flight workflow in this project. `ARRIVED` means the parcel reached the destination station, while `DELIVERED` means receipt was confirmed.

### 3.2 Customer workflow

1. Register a Customer account or sign in. Maintain addresses and packages, then create an order with an origin, a different destination station and recipient details. The initial state is `PENDING`. Save the one-time pickup code shown at creation; ordinary detail/history responses do not disclose it again.
2. Use My Orders, filters and history to inspect recorded progress. Edit only while `PENDING`; cancellation with a reason is allowed in `PENDING` or `APPROVED` subject to current server state.
3. Open tracking/map to view reported progress and any available ETA. Missing, stale or unavailable estimates remain distinguishable from current predictions. Arrival does not imply successful recipient handover.
4. When the order is `ARRIVED`, use the pickup-code confirmation flow. A successful confirmation changes the order to `DELIVERED`; an incorrect or temporarily locked code remains an error/retry state.
5. Open the assistant with an owned order or session when that feature has been enabled. The implemented service uses owned facts and refuses foreign resources. Generation and conversation writes remain default-off pending the approved retention/provider/backup configuration; the report does not enable them. Account-loss and uncertain-send recovery remain part of the client flow.

### 3.3 Dispatcher workflow

Review pending requests and approve or reject according to the current workflow. For an approved order, choose a valid departure and an eligible drone. Scheduling/assignment respects mission conflicts, payload/range and destination-slot availability. Monitor active deliveries and the timeline. If an incident occurs, record the failure and use an allowed recovery action such as rescheduling after the required package verification; the failure history remains visible. Use the delivery summary only when its chosen Ollama/Gemini provider has been configured and enabled; generated information cannot operate a mission or change an order.

### 3.4 Station Operator workflow

Open the assigned station workspace. Inspect current availability and keep manually maintained slot states within the allowed `EMPTY`/`ERROR` transitions. For origin intake, look up the tracking number, confirm an approved order with its scheduled `PLANNED` mission, select a currently empty slot and enter the required physical/visual checks. Successful intake records `READY_FOR_TAKEOFF`; rejection records `FAILED` with the reason. Availability polling reconciles the displayed slot with the submitted selection.

At the destination, follow the authorized pickup/handover flow with the supplied code, including lockout/retry handling. Handle expired parcels through the dedicated clearing workflow. Operator notifications stay within operator-authorized destinations. A station operator does not gain customer-only detailed-order access through a notification or assistant link.

### 3.5 Logistics Manager and System Administrator workflows

Managers use date-filtered dashboard metrics and paginated reports at `/manager/analytics`, interpreting the defined order cohort and timezone consistently, with read-only visibility into tracking, map, ETA, summary, and station availability (drone fleet and slot administration remain restricted). Administrators maintain users/roles (`/admin/users`), landing stations (`/stations`), drones (`/drones`), scoped external integration keys (`/api/admin/integration-keys`), and security audit logs (`/admin/audit-logs`). Administrative access does not implicitly grant Customer assistant access. Use recorded source-backed summaries and logs within each endpoint's role rules; do not treat generated output as operational authority.

## 4. Handoff and publication record

The source/report package identifies the commit, build commands, existing chapter pages and verification links. [The report index](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) is the contents list for the six chapters. [The formatting checklist](proofreading-and-formatting-checklist.md) records chapter consistency and the distinct final human-review step. LA-204 owns final assembly and approval. Actual named proofreading confirmations, any signed/exported submission artifact and any deployment decision must be recorded as their own evidence; none is inferred from publishing these instructions.
