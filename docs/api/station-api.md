# API Contract - Landing Stations, Slots and Station Operations

Contract for the third API group of SmartDroneDelivery (Sprint 4: LA-26 manage landing stations, LA-27 manage station slots, LA-28 station availability, LA-29 confirm package pickup at the station). Backend, web and mobile all build against this document. The backend implements it exactly; the web and mobile teams use the examples as mock data until the real API is ready. Drones (LA-30) are in [drone-api.md](drone-api.md).

> **Status: proposed, to be confirmed by the team.** Any change goes through a pull request that edits this file, and the change is announced to the team. Items that still need a decision are collected in section 7.

Sources: [class specification](../design/class-specification-stations-slots-drones.md), [station and slot sequences](../design/sequence-station-and-slot-management.md), [pickup sequences](../design/sequence-package-pickup-confirmation.md), [station and pickup activity](../design/activity-station-and-pickup.md), [business rules](../requirements/business-rules.md) (BR-LS-01 to BR-LS-22), [ERD](../architecture/erd.md), and the read-only `GET /api/landing-stations` already merged for the order screens.

| Story | Endpoints |
|---|---|
| LA-26 manage stations | section 2 |
| LA-27 manage slots | section 3 |
| LA-28 availability | section 2.6 |
| LA-29 pickup at the station | section 4 |

## 1. Conventions

Everything in section 1 of [auth-api.md](auth-api.md) applies here (base URL, `/api` prefix, camelCase JSON, UUIDs, UTC ISO 8601, bearer token, RFC 7807 errors). Only the additions are listed below.

| Item | Rule |
|---|---|
| Roles | `ADMIN`, `DISPATCHER`, `STATION_OPERATOR`, `CUSTOMER` as stated per endpoint. An account that is not `ACTIVE` gets `403`. |
| Own station | A `STATION_OPERATOR` can only act on the station where `operatorId` equals the token `sub`. Another station returns `403`. `ADMIN` can act on every station. |
| Validation | Field problems return `400` with `errors` per field. State problems (duplicate code, slot in use, wrong order status) return `409`. |
| Reason | Every change of station status and of slot status needs a `reason` of 5 to 500 characters. The change itself is written to `audit_logs` (BR-LS-21). That table has no column for the reason today, so where the text is kept is open point 9. |
| No delete | A station is never deleted. Taking it out of service is `status = OFFLINE` (FR-20 "deactivate"). |
| Pickup code | The 6-digit `pickupSecureCode` is only an input here. It is never returned and never written to a log or audit entry (BR-LS-22). |
| Lockout | After 3 wrong pickup codes for the same tracking number, further tries return `423 Locked` for 10 minutes (BR-LS-12, same code as customer-order-api.md section 8 point 7). |

### Status values

| Object | Values |
|---|---|
| Station `status` | `OPERATIONAL`, `MAINTENANCE`, `OFFLINE`. Only `OPERATIONAL` stations accept new orders and flights (BR-LS-01). |
| Slot `status` | `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`. The API sets only `EMPTY` and `ERROR` by hand. `RESERVED` is set by drone assignment and `OCCUPIED` by package intake. |

### Shared objects

`Station` (a superset of `StationSummary` in customer-order-api.md, so clients that only read the summary fields keep working)
```json
{
  "id": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "stationCode": "HCM-01",
  "name": "District 1 Hub",
  "address": "1 Le Loi, District 1",
  "latitude": 10.7769,
  "longitude": 106.7009,
  "status": "OPERATIONAL",
  "totalSlots": 4,
  "emptySlots": 2,
  "operatorId": "3f6a1c8e-9b24-4d70-8c15-6e2d0a7b9f31",
  "operatorName": "Le Van Cuong",
  "createdAt": "2026-11-06T02:00:00Z",
  "updatedAt": "2026-11-06T02:00:00Z"
}
```
`operatorId` and `operatorName` are `null` when no operator is assigned.

`StationSlot`
```json
{
  "id": "b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10",
  "stationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "slotNumber": 1,
  "status": "OCCUPIED",
  "lastUpdated": "2026-11-06T03:10:00Z",
  "currentOrderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "currentMissionId": null
}
```
`currentOrderId` and `currentMissionId` are `null` unless the slot is `OCCUPIED` or `RESERVED` for an order. They are ids only; no secure code and no recipient data.

## 2. Landing stations (LA-26, LA-28)

Base path `/api/landing-stations`.

### 2.1 GET /api/landing-stations
Auth: yes, any role (the order screens need it). Query: `status` (optional), `search` (optional, matches `stationCode` or `name`, case-insensitive). Response `200 OK`: array of `Station` sorted by `name`. The list is not paged because the number of stations is small (open point 1). This keeps the shape already merged for LA-103.

### 2.2 GET /api/landing-stations/{id}
Auth: yes, `ADMIN`, `DISPATCHER`, `STATION_OPERATOR`. Response `200 OK`: `Station` plus `slots` (array of `StationSlot` sorted by `slotNumber`) and `hostedDroneCount`. Errors: `403`, `404`.

### 2.3 POST /api/landing-stations
Auth: yes, `ADMIN`. Creates the station and its slots in one transaction (if a slot cannot be saved, nothing is saved).

Request (`CreateStationRequest`)
```json
{
  "stationCode": "HCM-02",
  "name": "Thu Duc Hub",
  "address": "10 Vo Van Ngan, Thu Duc",
  "latitude": 10.8506,
  "longitude": 106.7719,
  "totalSlots": 4,
  "operatorId": null
}
```

| Field | Rule |
|---|---|
| `stationCode` | Required, 3 to 50 characters, uppercase letters, digits and `-` (`^[A-Z0-9-]+$`), unique. Length limit is open point 2. |
| `name` | Required, at most 150 characters. |
| `address` | Required, not blank. |
| `latitude` | Required, -90 to 90. |
| `longitude` | Required, -180 to 180. |
| `totalSlots` | Required, 1 to 20. Slots numbered `1..N` are created with status `EMPTY`. |
| `operatorId` | Optional. Must be an existing active user with role `STATION_OPERATOR`. |

Response `201 Created` with header `Location: /api/landing-stations/{id}` and the `Station` (status `OPERATIONAL`). Errors: `400` (also when `operatorId` is not a station operator), `403`, `409` the code is already used.

### 2.4 PUT /api/landing-stations/{id}
Auth: yes, `ADMIN`. Request: `name`, `address`, `latitude`, `longitude`, `operatorId` (same rules as above, all required except `operatorId`, which may be `null` to unassign). `stationCode`, `totalSlots` and `status` cannot be changed here; unknown fields return `400`. Response `200 OK`: `Station`. Errors: `400`, `403`, `404`.

### 2.5 PUT /api/landing-stations/{id}/status
Auth: yes, `ADMIN`, or the assigned `STATION_OPERATOR`.

Request
```json
{ "newStatus": "MAINTENANCE", "reason": "Landing pad sensors are being replaced." }
```

Response `200 OK`
```json
{
  "station": { "...": "Station object" },
  "rerouteWarning": true,
  "affectedMissionIds": ["5c7e9a1b-3d5f-4b70-8a2c-4e6f8a0b2c13"]
}
```
`rerouteWarning` is `true` when the new status is `MAINTENANCE` or `OFFLINE` and at least one mission with `flightStatus = IN_AIR` is flying to this station (BR-LS-03). The status still changes, the dispatchers get an urgent alert, and the ids of the affected missions are returned. Otherwise `rerouteWarning` is `false` and `affectedMissionIds` is `[]`. Setting the status it already has returns `200` with no new audit entry. Errors: `400` (`newStatus` not allowed, `reason` missing), `403`, `404`.

### 2.6 GET /api/landing-stations/{id}/availability (LA-28)
Auth: yes, any role. Counts are read live from the slots; they are never cached on the client.

Response `200 OK`
```json
{
  "stationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "stationStatus": "OPERATIONAL",
  "totalSlots": 4,
  "emptySlots": 2,
  "occupiedSlots": 1,
  "reservedSlots": 1,
  "errorSlots": 0,
  "isAvailableForArrival": true,
  "isAvailableForDeparture": true,
  "checkedAt": "2026-11-06T03:15:00Z"
}
```
`emptySlots + occupiedSlots + reservedSlots + errorSlots = totalSlots`. `isAvailableForArrival` and `isAvailableForDeparture` are both `true` only when `stationStatus` is `OPERATIONAL` and `emptySlots` is at least 1; otherwise both are `false`. Error: `404`.

## 3. Station slots (LA-27)

### 3.1 GET /api/landing-stations/{stationId}/slots
Auth: yes, `ADMIN`, `DISPATCHER`, `STATION_OPERATOR`. Response `200 OK`: array of `StationSlot` sorted by `slotNumber`. Error: `404`.

### 3.2 GET /api/station-slots/{id}
Auth: same. Response `200 OK`: `StationSlot`. Error: `404`.

### 3.3 POST /api/landing-stations/{stationId}/slots
Auth: yes, `ADMIN`. Adds one slot with status `EMPTY` and increases `totalSlots` by 1, in one transaction. Request: `{ "slotNumber": 5 }`, optional; if omitted it is the highest existing number plus 1. Response `201 Created`: `StationSlot`. Errors: `400` (`slotNumber` below 1), `404`, `409` the number is already used, or the station already has 20 slots.

### 3.4 DELETE /api/station-slots/{id}
Auth: yes, `ADMIN`. Removes the slot and decreases `totalSlots` by 1, in one transaction. The other slots keep their numbers. Response `204 No Content`. Errors: `404`, `409` the slot is `OCCUPIED` or `RESERVED`, is linked to a `PLANNED` or `IN_AIR` mission, or is the last slot of the station.

### 3.5 PUT /api/station-slots/{id}/status
Auth: yes, `ADMIN`, or the assigned `STATION_OPERATOR`.

Request
```json
{ "targetStatus": "ERROR", "reason": "Charging pin is damaged." }
```

| Current status | `targetStatus` | Result |
|---|---|---|
| `EMPTY` | `ERROR` | `200`, slot isolated |
| `ERROR` | `EMPTY` | `200`, maintenance finished |
| `OCCUPIED` or `RESERVED` | `ERROR` | `409` (BR-LS-06): move the parcel, or ask the dispatcher to change the flight, first |
| `EMPTY` | `EMPTY` or `ERROR` | `400` for `EMPTY` on `EMPTY`: the slot is not in maintenance |
| any | `OCCUPIED` or `RESERVED` | `400`: these are set by the system only |

Response `200 OK`: `StationSlot`. Errors: `400`, `403`, `404`, `409`.

## 4. Station operations (LA-29)

Base path `/api/landing-stations/{stationId}/operations`. Auth: yes, `STATION_OPERATOR` of that station, or `ADMIN`. Every success writes an `audit_logs` entry, an order status log and a notification to the customer, in the same transaction as the change (BR-LS-21).

### 4.1 GET .../operations/orders/{trackingNumber}
Used by the operator after scanning the barcode. `GET /api/delivery-orders` is for customers only, so this is the staff read.

Response `200 OK` (`StationOrderInfo`)
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0001",
  "status": "APPROVED",
  "role": "ORIGIN",
  "packageName": "Birthday gift",
  "declaredWeightGram": 850,
  "declaredLengthCm": 25,
  "declaredWidthCm": 18,
  "declaredHeightCm": 10,
  "isFragile": true,
  "originStationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "destinationStationId": "a5c9e2b7-3d18-4e60-b4f2-8d1a7c0e9b55"
}
```
`role` is `ORIGIN` when this station is the origin of the order and `DESTINATION` when it is the destination. The object never contains `pickupSecureCode`, the customer, or the recipient phone. Errors: `403`, `404` the order does not exist or the station is neither its origin nor its destination.

### 4.2 POST .../operations/intake (UC-18, FR-23)
The operator received the parcel at the **origin** station, checked it and loaded it into a slot.

Request (`PackageIntakeRequest`)
```json
{
  "trackingNumber": "DD-202611-0001",
  "slotId": "b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10",
  "measuredWeightGram": 860,
  "measuredLengthCm": 25,
  "measuredWidthCm": 18,
  "measuredHeightCm": 10,
  "visualInspectionPassed": true
}
```

| Field | Rule |
|---|---|
| `trackingNumber` | Required. The order's origin station must be `{stationId}`. |
| `slotId` | Required. A slot of `{stationId}` with status `EMPTY`. |
| `measuredWeightGram` | Required, greater than 0 and at most 3000. |
| `measuredLengthCm`, `measuredWidthCm`, `measuredHeightCm` | Required, greater than 0; at most 30, 20 and 15. |
| `visualInspectionPassed` | Required, must be `true`. If the seal is damaged, use 4.3. |

In one transaction: order `APPROVED` to `READY_FOR_TAKEOFF`, slot `EMPTY` to `OCCUPIED`, status log, audit entry, notification "Ready for takeoff" to the customer.

Response `200 OK`
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0001",
  "status": "READY_FOR_TAKEOFF",
  "slotId": "b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10",
  "slotNumber": 1,
  "confirmedAt": "2026-11-06T03:10:00Z"
}
```
Errors: `400` a measure is over the limit or `visualInspectionPassed` is not `true`; `403`; `404` order, slot or station not found, or the order does not start at this station; `409` the order is not `APPROVED`, or the slot is not `EMPTY` (including a second request for the same order).

### 4.3 POST .../operations/intake-reject
The parcel failed the check (too heavy, too big, damaged). Request: `{ "trackingNumber": "DD-202611-0001", "reason": "Weighs 3400 g, over the 3000 g limit." }` (`reason` 5 to 500 characters). Order `APPROVED` to `FAILED` with `cancelReason = reason`, status log, audit entry, notification. No slot changes. Response `200 OK`: `{ "orderId": "...", "trackingNumber": "...", "status": "FAILED" }`. Errors: `400`, `403`, `404`, `409` the order is not `APPROVED`.

### 4.4 POST .../operations/pickup (UC-08, FR-26, FR-27)
The recipient collects the parcel at the **destination** station. The operator types the tracking number and the 6-digit code the recipient shows.

Request (`ConfirmPickupRequest`)
```json
{ "trackingNumber": "DD-202611-0001", "pickupSecureCode": "482915" }
```
`trackingNumber` is required; `pickupSecureCode` is required and matches `^\d{6}$`. The destination slot is taken from the order's flight mission, so the client does not send a slot (open point 3).

In one transaction: order `ARRIVED` to `DELIVERED`, `actualDeliveryTime` set to now, destination slot `OCCUPIED` to `EMPTY`, status log, audit entry (without the code), notification "Package delivered" to the customer.

Response `200 OK`
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0001",
  "status": "DELIVERED",
  "actualDeliveryTime": "2026-11-06T05:42:00Z",
  "slotId": "d4f6a8b0-3e5a-4c7d-b9f1-2a4c6e8a0b12",
  "slotReleased": true
}
```

Errors:

| Status | When |
|---|---|
| `400` | The code is wrong. Body has `attemptsRemaining` (2, 1 or 0), for example `{ "...": "problem fields", "attemptsRemaining": 2 }`. The third wrong code returns `423`, not `400`. |
| `403` | Not the operator of this station. |
| `404` | Unknown tracking number, or the destination of the order is not this station. |
| `409` | The order is not `ARRIVED`. |
| `423` | Locked: 3 wrong codes for this tracking number. Body has `lockoutExpiresAt`; header `Retry-After` is in seconds. While locked, even the right code is refused. A right code resets the counter. |

### 4.5 POST .../operations/validate-code
Advisory check before the compartment opens. It does not change the order or the slot. It counts toward the same 3-attempt limit as 4.4, so it cannot be used to guess the code.

Request: same as 4.4. Response `200 OK`
```json
{ "isValid": false, "attemptsRemaining": 2, "isLocked": false, "lockoutExpiresAt": null }
```
When locked: `isLocked` is `true`, `attemptsRemaining` is 0, `lockoutExpiresAt` is set, and the status is still `200` so the screen can show the countdown. Errors: `403`, `404`, `409` the order is not `ARRIVED`.

### 4.6 Pickup timeout (no endpoint)
A background job moves an order from `ARRIVED` to `FAILED` 24 hours after it arrived, with `cancelReason = "Pickup window expired (24h timeout)"`, and alerts the dispatcher and the station operator (F-08, BR-LS-15). Clients only see the new status. The job is part of this sprint's backend work but has no API.

## 5. Error examples

```json
{ "type": "https://httpstatuses.com/409", "title": "Conflict", "status": 409, "detail": "Slot is occupied or reserved and cannot be removed." }
```
```json
{
  "type": "https://httpstatuses.com/400", "title": "Bad Request", "status": 400,
  "detail": "Invalid pickup code.", "attemptsRemaining": 2
}
```
```json
{
  "type": "https://httpstatuses.com/423", "title": "Locked", "status": 423,
  "detail": "Too many wrong codes. Try again later.", "lockoutExpiresAt": "2026-11-06T05:52:00Z"
}
```
Texts shown to the user come from [application-messages.md](../requirements/application-messages.md); the app decides the message from the status code and field, not from `detail`.

## 6. Differences with the code that is already merged

`GET /api/landing-stations` (LA-103) returns `StationSummary` objects and accepts `status`. This contract only adds fields (`totalSlots`, `emptySlots`, `operatorId`, `operatorName`, timestamps) and the `search` query, so the order screens keep working with no change. Its `[Authorize]` stays "any role". The other endpoints in this document do not exist yet. The `Drone`, `StationSlot` and `LandingStation` entities exist; no migration is needed for sections 2 to 4.

## 7. Open points

These need a team decision. The contract above states the proposed answer.

1. **Station list paging.** Proposed: no paging (bare array), to stay compatible with the merged endpoint. The class specification proposes a paged list; switching later would break the order screens.
2. **Station code length.** The entity and class specification allow 3 to 50 characters, BR-LS-02 says 3 to 10 (marked "to confirm"). Proposed: 3 to 50, because the example `HCM-01` already fits both.
3. **Destination slot at pickup.** `DeliveryOrder` has no slot column; the slot is `FlightMission.DestinationSlotId`. The class specification sends `slotId` in the pickup request. Proposed: the server finds it from the mission and the client sends nothing. This needs the flight mission of Sprint 5; until then a pickup on an order without a mission returns `409`, and the demo seed data (LA-325) must create one.
4. **Lockout code.** Design documents say `429`, customer-order-api.md section 8 point 7 says `423`. Proposed: `423`. The pickup sequence diagram should be edited to match.
5. **Reserved slot to ERROR.** Sequence 5 lets an operator mark a `RESERVED` slot as `ERROR` and alert the dispatcher. BR-LS-06 and the class specification refuse it with `409`. Proposed: follow BR-LS-06 and edit the sequence diagram.
6. **Who can confirm a pickup.** The class specification also lists `CUSTOMER` for `pickup` and `validate-code`. BR-LS-22 says the code is "validated blindly by the system" at the station. Proposed: operator and admin only; the customer shows the code at the station.
7. **Restoring a station.** BR-LS-04 asks for sensor self-tests before `OPERATIONAL`, which is outside the software (D-05). Proposed: no check, only a `reason`.
8. **Intake without a drone.** Intake works on an `APPROVED` order. Drone assignment (Sprint 5) is not required first, so the response has no `assignedDroneId`. If the team wants assignment before intake, `409` is added for an order with no mission.
9. **Where the reason is stored.** `audit_logs` has `userId`, `action`, `entityName`, `entityId`, `ipAddress`, `userAgent`, `timestamp` and nothing for a reason. Station status, slot status and intake rejection all take a `reason`. Proposed: the order rejection reason goes to `cancelReason` and the status log `remarks`; for station and slot changes add a nullable `details` text column to `audit_logs` (one small migration). Without it, the reason is validated but not kept. Needs a decision before LA-26 starts.
10. **Realtime.** Slot and station changes are also pushed over SignalR (`station-slots-{stationId}`, `dispatcher-alerts`). The hub is not part of this REST contract; web screens refresh with the REST endpoints until it exists.
