# Final demo script (LA-186)

Owner: [LA-186](https://hungndemail.atlassian.net/browse/LA-186), TBA Review. Parent story: [LA-49](https://hungndemail.atlassian.net/browse/LA-49).
Related: demo data [LA-187](https://hungndemail.atlassian.net/browse/LA-187) (Nhân Lưu Thiện), rehearsal [LA-188](https://hungndemail.atlassian.net/browse/LA-188) (Hung Nguyen, `docs/testing/LA-188-demo-rehearsal.md`).

This script shows one parcel going through the whole delivery flow, so that every actor appears once: System Administrator, Customer, Dispatcher, Station Operator, Logistics Manager, and the system itself (drone simulator, real-time tracking, notifications). Each step lists what to click, what the audience must see, and what to do when something goes wrong.

What this demo is and is not:
- The drone is the **built-in simulator** (`DroneSimulator`); no flight hardware is controlled (out of scope, SRS).
- The AI functions (ETA model, delivery summary model, customer assistant) are **default-off** until model quality and the chat retention policy are approved. Show the default behaviour and say so; do not switch them on for the demo.
- A successful demo is evidence of the flow on one machine. It is not end-to-end acceptance of the release.

Allow about 25–30 minutes for one newly created parcel to complete the live flow. Scheduling requires departure at least 15 minutes in the future. For a shorter presentation, prepare a separate scheduled/intaken order before the audience arrives and explicitly distinguish it from the newly created order; do not imply the scheduling wait was skipped.

---

## 0. Before the demo (30 minutes earlier)

### 0.1 Start the system

Use **one** of the two ways; do not mix them (both use port 5055).

| | Docker (recommended for the demo) | Developer servers |
|---|---|---|
| Start | `cp .env.example .env`, fill every value, add the demo settings below, then `docker compose up -d --build --wait` | `docker compose up -d postgres minio`, then `dotnet run --launch-profile http` in `backend/SmartDroneDelivery.Api`, then `npm run dev` in `frontend/web` |
| Web portal | <http://localhost:8080> | <http://localhost:5173> |
| API / Swagger | <http://localhost:5055> (no Swagger in Production) | <http://localhost:5055/swagger> |
| Health | `curl http://localhost:5055/health/ready` → `"status":"Healthy"` | same |

Demo settings (environment variable form for `.env` / Docker; for developer servers use `dotnet user-secrets set "Section:Key" value`):

| Setting | Value | Why |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | creates the demo accounts and stations (README, "Accounts and test data") |
| `DroneSimulator__Enabled` | `true` | without it the drone never takes off (default `false`) |
| `DeliveryFlight__CruiseSpeedMps` | `60` | HCM-01 → HCM-02 is 11.3 km: about 16 min at the default 12 m/s, about 3 min at 60 m/s. **Tell the audience the flight is accelerated for the demo.** |
| `ADMIN_PASSWORD` / `Seed:AdminPassword` | your choice | first administrator (LA-328) |

Mobile apps (on the presenter laptop's emulator, or a phone on the same network):

```bash
cd frontend/mobile
flutter run -t lib/main_operator.dart --flavor operator --dart-define=API_BASE_URL=http://<laptop-ip>:5055/api
flutter run -t lib/main_customer.dart --flavor customer --dart-define=API_BASE_URL=http://<laptop-ip>:5055/api
```

### 0.2 Data the flow needs (check every row)

The Development seed creates three stations, a customer, a dispatcher and one station operator. The rest must exist before the demo, either from LA-187 demo data or created by hand as the administrator:

| Needed | Seeded? | If missing |
|---|---|---|
| Stations `HCM-01`, `HCM-02` `OPERATIONAL` with `EMPTY` slots; `HCM-03` in `MAINTENANCE` | yes | — |
| Customer `customer@smartdronedelivery.com` with an address and a package | account yes, address/package no | create them in scene 2 (part of the demo) |
| Dispatcher `dispatcher@smartdronedelivery.com` | yes | — |
| Operator of the **origin** `HCM-01`: `operator@smartdronedelivery.com` | yes | — |
| Operator of the **destination** `HCM-02` | **no** | Admin → Users → create a Station Operator, then Landing stations → `HCM-02` → assign that operator. Without it, scene 6 is done by the customer (6b). |
| At least one `IDLE` drone whose payload limit fits the demo package | **no** | Admin → Drone fleet → add drone |
| Logistics Manager account | **no** | Admin → Users → create staff account, role Logistics Manager |

A station operator can only work at the station assigned to them (the API answers 403 otherwise), which is why the destination needs its own operator.

### 0.3 Dry run

Run scenes 2–6 once with a throw-away order. If the drone does not take off, check `DroneSimulator__Enabled` and that the scheduled departure time has passed.

---

## 1. Scenes

| # | Actor and client | Steps | The audience sees | If it goes wrong |
|---|---|---|---|---|
| 1 | **System Administrator**, web (1 min) | Sign in as the administrator. Open **Users** (show the five roles), **Landing stations** (HCM-01/02 operational, HCM-03 in maintenance, operator assignments) and **Drone fleet** (the idle drone). | Role-based menu for the administrator; stations and the fleet ready. | Missing operator or drone: section 0.2. |
| 2 | **Customer**, mobile app (3 min) | Sign in as the customer. Add an address, add a package (weight within the drone limit), create a delivery order from `HCM-01` to `HCM-02`. **Read the 6-digit pickup code aloud and write it down.** | Order `PENDING` with a tracking number; the pickup code is shown only on this screen ("Pickup secure code"). | Validation error: the message names the field; fix weight/dimensions. Choosing `HCM-03` shows the maintenance refusal (optional, good to show). |
| 3 | **Dispatcher**, web (2 min) | Sign in as the dispatcher. **Delivery approvals** → open the order → Approve. **Delivery scheduling** → save departure **at least 16 minutes from now**, then assign an eligible idle drone (time for scene 4). | Order `APPROVED`, mission planned, drone assigned. | `400`: departure must be at least 15 minutes in the future. `409`: someone else changed the order or drone; refresh and choose again. |
| 4 | **Station Operator** of `HCM-01`, operator app (2 min) | **Origin Intake** → find the order by tracking number → choose an `EMPTY` slot → enter the measured weight/dimensions, confirm the visual check → confirm. | Order `READY_FOR_TAKEOFF`; the slot becomes `OCCUPIED`. | Slot taken meanwhile: the list refreshes, pick another `EMPTY` slot. |
| 5 | **System + Customer + Dispatcher** (3 min) | Wait for the departure time. Dispatcher: **Delivery tracking** / **Delivery map**. Customer: the order detail (web or app). | The simulator takes off (`IN_TRANSIT`), the position moves on the map in real time (SignalR), then `ARRIVED` at `HCM-02`; the customer gets notifications; arrival estimate shows. | No take-off: departure time not reached or simulator off (0.1). Map frozen: refresh once; the page falls back to polling. |
| 6a | **Station Operator** of `HCM-02`, operator app (1 min) | **Confirm Pickup** → find the arrived order → enter the customer's 6-digit code → **Confirm Pickup & Release Slot**. | Order `DELIVERED`; the destination slot returns to `EMPTY`. | Wrong code: the remaining attempts are shown. The **third** wrong code locks the order for 10 minutes (`423`), so type carefully. |
| 6b | *(instead of 6a)* **Customer**, customer app | Open the arrived order → confirm delivery with the pickup code (customer app only; the web portal has no confirm screen). | Order `DELIVERED`. | Same lockout rule. |
| 7 | **Customer**, web (1 min) | Open the delivered order: status history and **Delivery summaries** / **Arrival estimates**. | The complete timeline. AI-generated text is off by default; say that the model features wait for the quality and retention approval. | — |
| 8 | **Logistics Manager**, web (1 min) | **Dashboard & reports**: today's period. | The delivered order counted in the metrics and the delivery report. | Choose a period that includes today (Vietnam time). |
| 9 | **System Administrator**, web (1 min) | **Audit logs**: filter by the customer, then by action (`ORDER_APPROVAL`, `DRONE_ASSIGNMENT`, `PACKAGE_INTAKE_CONFIRMATION`, ...). Optionally show a `LOGIN_FAILED` from a deliberate wrong password. | Who did what and when for the demo order, read-only. | Empty list: clear the date filter. |

Optional extras when time allows (about 2 minutes each):
- **Failure handling** (Dispatcher, **Failed deliveries**): needs a second order flown with `DroneSimulator__AbortAfterSeconds` set; shows the abort, diversion to the nearest operational station and the dispatcher's resolution. Prepare it before the demo; do not change settings live.
- **External integration** (`/api/v1/delivery-orders` with an integration key created by the administrator through `POST /api/admin/integration-keys`): there is no web screen for it, show it with Swagger (Development) or curl.
- **Health** (`/health/ready`) and `docker compose ps` to show the deployment package (LA-47/LA-178).

## 2. Talking points per requirement

| Scene | Requirements shown |
|---|---|
| 1, 9 | User and role management, RBAC, audit logging and search (FR-01–05, FR-32/33) |
| 2 | Customer addresses, packages, order creation and the one-time pickup code |
| 3 | Approval and drone scheduling |
| 4, 6 | Station operations: intake, slot occupancy, pickup with secure code and lockout |
| 5, 7 | Simulated flight, real-time tracking, notifications, ETA (default-off model) |
| 8 | Analytics dashboard and reports |

## 3. Reset after a rehearsal

- Docker: `docker compose down -v` deletes all data (including the accounts you created); the next `up` seeds again. Keep the volume if you want to keep the drone, manager and second operator.
- Do not reuse an order across runs: create a new one in scene 2.

## 4. Evidence to keep

Record each dry-run in the LA-188 rehearsal log (date, participants, problems, fixes). A dry-run that has not happened must stay marked as pending.
