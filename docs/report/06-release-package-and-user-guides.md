# VI. Release Package & User Guides

This chapter describes the reviewed software at integration baseline [`07f38d1`](https://github.com/nguyentuio1/learning_app/commit/07f38d1b2630e7414ad114887029c94b3b48acc6), including the completed Sprint 7 Flutter integration in [PR #235](https://github.com/nguyentuio1/learning_app/pull/235). The package is a source and documentation handoff. Build instructions below describe how to prepare distributable files; they do not assert that a signed mobile package, production deployment or new release tag has been published. The existing GitHub release `v0.3.0` is an earlier Sprint 3 pre-release.

## 1. Deliverable package

| Component | Location and handoff |
|---|---|
| Backend | [`backend/SmartDroneDelivery.Api`](../../backend/SmartDroneDelivery.Api), an ASP.NET Core 8 API; [`backend/SmartDroneDelivery.Tests`](../../backend/SmartDroneDelivery.Tests) contains automated verification. |
| Database | EF Core [`Migrations`](../../backend/SmartDroneDelivery.Api/Migrations) and [`AppDbContext.cs`](../../backend/SmartDroneDelivery.Api/Data/AppDbContext.cs). PostgreSQL 16 is the CI/local Compose reference. |
| Web portal | [`frontend/web`](../../frontend/web), React/TypeScript with its committed npm lockfile; `npm run build` produces `frontend/web/dist`. |
| Mobile apps | [`frontend/mobile`](../../frontend/mobile), one Flutter codebase with Customer and Station Operator entrypoints. Android has `customer` and `operator` product flavors. |
| Local infrastructure | [`docker-compose.yml`](../../docker-compose.yml) defines PostgreSQL and MinIO with persistent named volumes. |
| Documentation | Chapters I–VI, API contracts, design diagrams, business rules and test evidence. [Report contents](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) identifies the existing Confluence publication pages. |

To package a checkout, record the exact commit and use the committed lockfiles. Create an output directory, archive the chosen source commit, publish the API, build the web client and produce any required mobile packages using an appropriately configured platform toolchain. These are preparation commands from the repository root:

```powershell
New-Item -ItemType Directory -Force artifacts/release
git archive --format=zip --output=artifacts/release/source.zip 07f38d1b2630e7414ad114887029c94b3b48acc6
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

#### 2.2.1 Infrastructure and back end

1. Check out the recorded commit and start `docker compose up -d` from the repository root. PostgreSQL is exposed on host port `5433`; MinIO's S3 API and console use `9000` and `9001`. Compose supplies the local development settings. Use the configuration appropriate to the intended environment rather than treating the bundled development credentials as deployment credentials.
2. From `backend/SmartDroneDelivery.Api`, run `dotnet run --launch-profile http`. The launch profile selects Development and port `5055`. Database initialization applies EF Core migrations on the relational provider and ensures baseline roles/admin data. Development demo data is controlled separately by `Seed:DevelopmentData`.
3. Open `http://localhost:5055/swagger` in Development to inspect the API. If startup reports a database/storage failure, inspect the service logs and connection configuration; a running HTTP process alone does not prove initialization succeeded.
4. Staff access requires persisted roles and, for station operations, the assigned station. Use administrator-managed staff accounts and the synthetic Development data described in the repository README. Do not publish a real user's password or pickup code in report evidence.

#### 2.2.2 Front end

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

Review pending requests and approve or reject according to the current workflow. For an approved order, choose a valid departure and an eligible drone. Scheduling/assignment respects mission conflicts, payload/range and destination-slot availability. Monitor active deliveries and the timeline. If an incident occurs, record the failure and use an allowed recovery action such as rescheduling after the required package verification; the failure history remains visible. Use the delivery summary only when its local provider has been configured and enabled; generated information cannot operate a mission or change an order.

### 3.4 Station Operator workflow

Open the assigned station workspace. Inspect current availability and keep manually maintained slot states within the allowed `EMPTY`/`ERROR` transitions. For origin intake, look up the tracking number, confirm an approved order with its scheduled `PLANNED` mission, select a currently empty slot and enter the required physical/visual checks. Successful intake records `READY_FOR_TAKEOFF`; rejection records `FAILED` with the reason. Availability polling reconciles the displayed slot with the submitted selection.

At the destination, follow the authorized pickup/handover flow with the supplied code, including lockout/retry handling. Handle expired parcels through the dedicated clearing workflow. Operator notifications stay within operator-authorized destinations. A station operator does not gain customer-only detailed-order access through a notification or assistant link.

### 3.5 Logistics Manager and System Administrator workflows

Managers use date-filtered dashboard metrics and paginated reports, interpreting the defined order cohort and timezone consistently. Administrators maintain users/roles, station configuration and scoped external integration keys. Administrative access does not implicitly grant Customer assistant access. Use recorded source-backed summaries and logs within each endpoint's role rules; do not treat generated output as operational authority.

## 4. Handoff and publication record

The source/report package identifies the commit, build commands, existing chapter pages and verification links. [The report index](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) is the contents list for the six chapters. [The formatting checklist](proofreading-and-formatting-checklist.md) records chapter consistency and the distinct final human-review step. LA-204 owns final assembly and approval. Actual named proofreading confirmations, any signed/exported submission artifact and any deployment decision must be recorded as their own evidence; none is inferred from publishing these instructions.
