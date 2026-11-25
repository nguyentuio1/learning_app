# LA-370: SignalR WebSocket Delivery & Native Mobile Runtime Acceptance

**Ticket:** `LA-370` (under `LA-6`)  
**Baseline Commit:** `develop` (`bd2860a`)  
**Date:** 2026-11-25  
**Coordinated With:** `LA-175` ([test-report.md](reports/test-report.md)), `LA-188` ([demo-rehearsal.md](../demo/demo-rehearsal.md))

---

## 1. Summary & Scope

- **SignalR WebSocket Delivery (`StatusChanged` + live `PositionUpdated`), Automatic Reconnect & Automatic HTTP Fallback:** **PASS** on integrated `develop` (`bd2860a`), verified via both `TrackingHubTests` (**6/6 passed**) and a live `@microsoft/signalr` WebSocket run against `SmartDroneDelivery.Api` (`-c Release`, `DroneSimulator__Enabled=true`) + PostgreSQL `18.4` with **0 SignalR callback warnings**.
- **Native Android Runtime Execution:** **BLOCKED / UNTESTED** on this workstation (Flutter SDK and Android SDK/`adb`/emulator are not installed).
- **Native iOS Runtime Execution:** **BLOCKED / UNTESTED** (Windows host; requires macOS + Xcode + iOS Simulator/device).
- **Reused Flutter Web Browser-Runtime Evidence (distinct from native Android/iOS):** Linked from `LA-346`, `LA-363`, and `LA-364` without relabeling browser runs as native mobile execution.

---

## 2. SignalR WebSocket Delivery, Automatic Reconnect & HTTP Fallback (Criterion 2)

### 2.1 Automated SignalR Protocol Suite (`TrackingHubTests`)

- **Command:** `dotnet test backend/SmartDroneDelivery.sln --filter FullyQualifiedName~TrackingHubTests -c Release --verbosity normal`
- **Result:** **6/6 Passed** (0 failed, 0 skipped, 0 warnings/errors).
- **Verified Behaviors:** Authenticated WebSocket handshake (`Anonymous_hub_negotiate_is_unauthorized_and_query_token_is_scoped_to_hub`), single-order `Subscribe` & disconnect cleanup, live `StatusChanged` and `PositionUpdated` frame delivery without client refresh, and foreign/unknown order rejection.

### 2.2 Live End-to-End SignalR WebSocket Run (`bd2860a`)

- **Environment:** Local Windows, `SmartDroneDelivery.Api` (`-c Release`, `ASPNETCORE_ENVIRONMENT=Development`, `DroneSimulator__Enabled=true`, `DeliveryFlight__CruiseSpeedMps=60`), `http://127.0.0.1:5055`, PostgreSQL `18.4` (`localhost:5433`, `drone_delivery_db`), Node.js `@microsoft/signalr` test client (`withAutomaticReconnect([0, 300, 800, 1500])`, `HttpTransportType.WebSockets` enforced) via local WS proxy `http://127.0.0.1:5056` to test real socket drop & outage transitions.
- **Reproducible Script:** [scripts/tests/verify-signalr-live.mjs](../../scripts/tests/verify-signalr-live.mjs)
- **Evidence:** [la370-signalr-websocket-live.json](evidence/la370-signalr-websocket-live.json) (valid JSON) · [la370-signalr-websocket-live-log.txt](evidence/la370-signalr-websocket-live-log.txt) (raw SignalR & lifecycle log, zero callback warnings).

| Check | Provenance / Action | Observed Outcome | Result |
| --- | --- | --- | :---: |
| **Negotiate & WebSocket Transport** | `POST /hubs/tracking/negotiate?negotiateVersion=1` + Node.js `HubConnection` (`HttpTransportType.WebSockets`) | `200 OK`, `availableTransports: ["WebSockets", "ServerSentEvents", "LongPolling"]`; connected over `ws://127.0.0.1:5056/hubs/tracking` | **PASS** |
| **Owned-Order Subscription** | Customer login (`200`), package (`201`), order `DD-202611-0001` (`633a9d55-1153-4243-981a-18a794fc0e8d`, `201`), `Subscribe(orderId)` | Subscription succeeded for owned order; zero callback warnings | **PASS** |
| **Live `StatusChanged` & `PositionUpdated` over WebSocket** | `approve` (`200`), `schedule` (`200`), `flight-mission` (`201`), `intake` (`200` -> `READY_FOR_TAKEOFF`), test DB `ScheduledDepartureTime` set to past so `DroneSimulatorService` takes off | Asserted `StatusChanged` events for `orderId` (`READY_FOR_TAKEOFF`, `IN_TRANSIT`) and live `PositionUpdated` (`latitude: 10.7769`, `longitude: 106.7009`, `altitudeMeters: 80`, `speedMps: 60`, `batteryPercentage: 100`, `timestamp: 2026-11-25T14:09:16.712913Z`) | **PASS** |
| **Automatic Reconnect (`withAutomaticReconnect`)** | Dropped active WebSocket TCP socket (`1006`) while `DroneSimulatorService` was active | `onreconnecting` fired -> `HubConnection` automatically reconnected on attempt 1 (`0 ms`) -> `onreconnected` automatically re-invoked `Subscribe(orderId)` -> received **fresh** `PositionUpdated` (`latitude: 10.777687`, `longitude: 106.701658`, `timestamp: 2026-11-25T14:09:18.721168Z`, strictly newer than initial) | **PASS** |
| **Automatic HTTP Fallback on Outage (Node.js test client)** | Blocked hub (`503`) and dropped WebSocket; plus negative checks (unauthenticated & wrong `orderId`) | `withAutomaticReconnect` exhausted 4 attempts -> `onclose` automatically triggered HTTP polling `GET /api/delivery-orders/{id}/tracking` in the **Node.js test client** (`200 OK`, `returnedOrderId === orderId`, `returnedStatus: "IN_TRANSIT"`, `lastPosition: { latitude: 10.778469, longitude: 106.702411 }`, `hasPickupSecureCode: false`; negative checks: unauthenticated `401`, wrong `orderId` `404`; **not** a Native Mobile test) | **PASS (Test Client)** |

### 2.3 How to Re-run `scripts/tests/verify-signalr-live.mjs`

1. **Install web dependencies** (provides `@microsoft/signalr` for the Node.js client):
   ```powershell
   cd frontend/web
   npm ci
   cd ../..
   ```
2. **PostgreSQL prerequisite & authentication:**
   - Ensure PostgreSQL is running (e.g., `C:\Program Files\PostgreSQL\18\bin\psql.exe` on `localhost:5433`, database `drone_delivery_db`) with `trust` authentication or `PGPASSWORD` set.
   - Override defaults if needed via `PSQL_PATH`, `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, and `PGPASSWORD`.
3. **Start the API with simulator & dev seed enabled** (against a clean/fresh database):
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT="Development"
   $env:ASPNETCORE_URLS="http://127.0.0.1:5055"
   $env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=drone_delivery_db;Username=postgres"
   $env:Jwt__Key="<at-least-32-byte-secret>"
   $env:Minio__AccessKey="localminioaccess"
   $env:Minio__SecretKey="localminiosecret123"
   $env:Seed__DevelopmentData="true"
   $env:DroneSimulator__Enabled="true"
   $env:DeliveryFlight__CruiseSpeedMps="60"
   dotnet run --project backend/SmartDroneDelivery.Api -c Release --no-launch-profile
   ```
4. **Run from repository root:**
   ```powershell
   node scripts/tests/verify-signalr-live.mjs > docs/testing/evidence/la370-signalr-websocket-live.json 2> docs/testing/evidence/la370-signalr-websocket-live-log.txt
   ```

---

## 3. Native Mobile Platform Coverage & Reused Web Evidence (Criteria 1 & 3)

| Platform / Target | Status | Build SHA / Reachable URLs | Actual Outcomes / Actionable Blockers |
| --- | --- | --- | --- |
| **Native Android (Device / Emulator)** | **UNTESTED / BLOCKED** | N/A | **Blocker:** `flutter` CLI and Android SDK (`%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe` / emulator) are not installed on this Windows workstation (`C:\Program Files\Android` exists without SDK/Flutter). **Action:** Install Flutter SDK + Android SDK/emulator (or connect a physical Android device), run `flutter run -t lib/main_customer.dart --flavor customer --dart-define=API_BASE_URL=http://10.0.2.2:5055/api` (or LAN host IP + reachable `Minio__PublicBaseUrl`), and record login, order creation, MinIO photo upload/reload, and SignalR WebSocket tracking outcomes. |
| **Native iOS (Device / Simulator)** | **UNTESTED / BLOCKED** | N/A | **Blocker:** Windows host cannot build or run iOS binaries. **Action:** Execute on macOS + Xcode + iOS Simulator/device against reachable LAN API/MinIO URLs and record outcomes. |
| **Flutter Web (Chrome, Reused QA Evidence)** | **PASS (Browser-Scoped)** | `c47a904` / `0c9b83d` (`http://127.0.0.1:15064` API, MinIO) | Reused from [flutter-photo-upload.md](acceptance/flutter-photo-upload.md), [customer-journey.md](acceptance/customer-journey.md), and [operator-journey.md](acceptance/operator-journey.md): package `201`, MinIO photo/avatar upload `200` with byte-exact reload, order `DD-202611-0001` `201`, HTTP tracking fallback `200`. Does **not** substitute for native Android/iOS. |

---

## 4. Coordination & Next Steps (Criterion 4)

- No backend or mobile code defects were observed in the verified SignalR WebSocket path (`bd2860a`); no source-code workaround was needed.
- **LA-175 / LA-188 Coordination:** SignalR WebSocket delivery (`TrackingHubTests` 6/6 + live `bd2860a` WebSocket `StatusChanged`/`PositionUpdated`/automatic reconnect/automatic HTTP fallback run) is now verified separately from `LA-363`'s HTTP fallback evidence and cross-referenced in [test-report.md](reports/test-report.md) and [demo-rehearsal.md](../demo/demo-rehearsal.md). Whole-team rehearsal (`LA-188`) and native Android/iOS runtime verification (`LA-370` Criterion 1) remain open until executed on an Android emulator/device and iOS Simulator/device.
