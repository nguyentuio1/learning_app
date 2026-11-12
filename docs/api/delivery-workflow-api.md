# API Contract - Delivery Workflow (Dispatcher, Scheduling, Tracking, Failures)

Documentation task: [LA-339 - Sprint 5 API contracts](https://hungndemail.atlassian.net/browse/LA-339). Covers the original contract in commit `0c9fddc` (PR #139) and the dispatcher read-side supplement below.

Contract for the fourth API group of SmartDroneDelivery (Sprint 5: LA-31 review and approve or reject, LA-32 order status state machine, LA-33 schedule and assign a drone, LA-34 failed deliveries, LA-330 telemetry simulator; plus the read side of LA-35 and LA-36 so Sprint 6 can start). Backend, web and mobile build against this document. Customer notifications (LA-326) are in [notification-api.md](notification-api.md).

> **Status: proposed, to be confirmed by the team.** Any change goes through a pull request that edits this file, and the change is announced to the team. Items that still need a decision are collected in section 9.

Sources: [class specification](../design/class-specification-delivery-workflow.md), [order review](../design/sequence-order-review.md), [scheduling and drone assignment](../design/sequence-scheduling-and-drone-assignment.md), [failed delivery handling](../design/sequence-failed-delivery-handling.md), [business rules](../requirements/business-rules.md) (BR-DW-01 to BR-DW-25), [customer-order-api.md](customer-order-api.md), [station-api.md](station-api.md), [drone-api.md](drone-api.md). The endpoint names are the ones proposed in section 5 of the class specification; this file fixes them.

| Story or task | Endpoints |
|---|---|
| LA-31 review | section 2 |
| LA-33 schedule and assign | section 3 |
| LA-32 state machine | section 4 (rules shared by every endpoint) |
| LA-35, LA-36 read side, LA-330 simulator | section 5 |
| LA-34 failed deliveries | section 6 |
| LA-136 backend / LA-137 web scheduling reads | sections 3.4 and 3.5 |
| LA-139 backend / LA-140 web failure reads | sections 6.4 and 6.5 |

Read-side supplement checked against Jira on 2026-11-12: [LA-137](https://hungndemail.atlassian.net/browse/LA-137), [LA-140](https://hungndemail.atlassian.net/browse/LA-140), their parent stories [LA-33](https://hungndemail.atlassian.net/browse/LA-33) and [LA-34](https://hungndemail.atlassian.net/browse/LA-34), and backend tasks [LA-136](https://hungndemail.atlassian.net/browse/LA-136), [LA-139](https://hungndemail.atlassian.net/browse/LA-139), [LA-336](https://hungndemail.atlassian.net/browse/LA-336). The stories require scheduling approved orders and showing failure/resolution history; they do not specify read routes or DTOs. The four read endpoints below are proposed additions, not existing backend endpoints. LA-140 concerns failed deliveries, not network communication failures; a telemetry gap alone does not fail an order.

## 1. Conventions

Everything in section 1 of [auth-api.md](auth-api.md) and of [customer-order-api.md](customer-order-api.md) applies here (base URL, `/api` prefix, camelCase JSON, UUIDs, UTC ISO 8601, bearer token, RFC 7807 errors, `PagedResult`). Only the additions are listed below.

| Item | Rule |
|---|---|
| Roles | `DISPATCHER` and `ADMIN` for sections 2, 3 and 6 unless stated. An account that is not `ACTIVE`, or another role, gets `403`. |
| Validation | Field problems return `400` with `errors` per field. State problems (wrong order status, drone or slot taken, order moved meanwhile) return `409`. |
| Notes and reasons | `reason` and `note` are trimmed and must have 1 to 500 characters (BR-DW-23). |
| One writer | Only the order transition writer changes `Status`. It checks the arrow and uses `UPDATE ... WHERE Status = expected`; zero rows changed means `409` and nothing is saved: no status log, audit entry or notification. |
| All or nothing | Status, mission, drone, slots, status log, audit entry and notification are saved in one transaction. A push that fails afterwards never undoes the change. |
| Audit | Every decision of a person writes one `audit_logs` row: `ORDER_APPROVAL`, `ORDER_REJECTION`, `ORDER_CANCELLATION`, `DRONE_ASSIGNMENT`, `DELIVERY_FAILURE_DETECTED`, `FAILED_DELIVERY_RESOLVED` (BR-DW-19). |
| Pickup code | `pickupSecureCode` is never returned by any endpoint in this file and never written to a log, audit entry or telemetry (BR-DW-22). |

`OrderDecision` is the response of every endpoint that changes the status of an order.
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "status": "APPROVED",
  "updatedAt": "2026-11-12T03:15:00Z"
}
```

## 2. Reviewing orders (LA-31, F-01 and F-02)

### 2.1 GET /api/dispatch/pending-orders
Auth: `DISPATCHER`, `ADMIN`. Query: `originStationId`, `destinationStationId` (optional UUIDs), `fromDate`, `toDate` (optional, filter on `createdAt`), `page` (default 1), `pageSize` (default 20, 1 to 100). Orders are `PENDING` only, oldest first. Response `200 OK`: `PagedResult` of `PendingOrder`. An empty queue is `200` with `"items": []`. Errors: `400` bad UUID or date, `fromDate` after `toDate`, `page` or `pageSize` out of range; `403`.

`PendingOrder`
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "customerName": "Tran Thi Mai",
  "customerPhone": "0901234567",
  "packageName": "Books",
  "weightGram": 450,
  "lengthCm": 25,
  "widthCm": 18,
  "heightCm": 10,
  "isFragile": false,
  "originStation": { "id": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44", "stationCode": "HCM-01", "name": "District 1 Hub" },
  "destinationStation": { "id": "a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21", "stationCode": "HCM-02", "name": "Thu Duc Hub" },
  "createdAt": "2026-11-12T02:40:00Z"
}
```
The waiting time is computed by the client from `createdAt`. The read writes nothing.

### 2.2 POST /api/delivery-orders/{id}/approve
Auth: `DISPATCHER`, `ADMIN`. No body. Moves `PENDING` to `APPROVED`, sets `dispatcherId`, writes the status log, a notification to the customer and audit `ORDER_APPROVAL`. Approving does not choose a time or a drone (section 3). Response `200 OK`: `OrderDecision`.
Errors: `403`; `404` order not found; `409` status is not `PENDING` (already reviewed, or the customer cancelled), a station is not `OPERATIONAL` (the `detail` names the station), or the order moved while saving.

### 2.3 POST /api/delivery-orders/{id}/reject
Auth: `DISPATCHER`, `ADMIN`. Request
```json
{ "reason": "Destination station is closed for the next week." }
```
Moves `PENDING` to `CANCELLED`, stores `reason` as `cancelReason`, writes the status log, a notification and audit `ORDER_REJECTION`. Response `200 OK`: `OrderDecision` with `status = CANCELLED`. Errors: `400` reason missing or longer than 500; `403`; `404`; `409` status is not `PENDING` or the order moved while saving.

## 3. Scheduling and assigning a drone (LA-33, F-04 and F-05)

Two steps on an `APPROVED` order, in this order: schedule the departure, then choose a drone. The order status stays `APPROVED` until the origin station operator confirms the package ([station-api.md](station-api.md) section 4).

### 3.1 PUT /api/delivery-orders/{id}/schedule
Auth: `DISPATCHER`, `ADMIN`. Request
```json
{ "scheduledDepartureTime": "2026-11-12T08:00:00Z" }
```
Rules: UTC, at least 15 minutes after now (BR-DW-05). Status must be `APPROVED` and the order must have no `PLANNED` mission (to change the time, the assignment is cancelled first, open point 3). The server computes `estimatedDeliveryTime` from the route distance (open point 1) and stores both times on the order.
Response `200 OK`
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "scheduledDepartureTime": "2026-11-12T08:00:00Z",
  "estimatedDeliveryTime": "2026-11-12T08:24:00Z",
  "estimatedFlightMinutes": 24
}
```
Errors: `400` `errors.scheduledDepartureTime` missing, not UTC, or earlier than now + 15 minutes; `403`; `404`; `409` status is not `APPROVED`, a mission already exists, or the order moved while saving.

### 3.2 GET /api/delivery-orders/{id}/available-drones
Auth: `DISPATCHER`, `ADMIN`. Advisory only: it reserves nothing. The order must be `APPROVED` and scheduled. Response `200 OK`: array of `DroneCandidate`, eligible drones first, then by `serialNumber`.
```json
[
  {
    "droneId": "c4e6a8b0-3d5f-4b7a-9c1e-2a4c6e8f0b13",
    "serialNumber": "DR-0001",
    "modelName": "SkyCarrier S1",
    "maxPayloadGram": 3000,
    "maxFlightRangeKm": 15.0,
    "currentStationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
    "status": "IDLE",
    "isEligible": true,
    "reasonsNotEligible": []
  }
]
```
`reasonsNotEligible` holds zero or more of: `NOT_IDLE`, `PAYLOAD_EXCEEDED` (package weight above `maxPayloadGram`, BR-DW-08), `RANGE_INSUFFICIENT` (`maxFlightRangeKm` below 1.3 times the route distance, BR-DW-09), `TIME_OVERLAP` (another mission in `[departure - 15 min, arrival + 20 min]`, BR-DW-10), `NOT_AT_ORIGIN` (the drone is not at the origin station). Only `IDLE` drones are eligible (open point 4). Errors: `403`; `404`; `409` the order is not `APPROVED` or has no scheduled time.

### 3.3 POST /api/delivery-orders/{id}/flight-mission
Auth: `DISPATCHER`, `ADMIN`. Request
```json
{ "droneId": "c4e6a8b0-3d5f-4b7a-9c1e-2a4c6e8f0b13" }
```
In one transaction: the drone goes `IDLE` to `ASSIGNED`, the lowest `EMPTY` slot of the **destination** station becomes `RESERVED` (BR-DW-06; the origin slot is chosen by the operator at intake), a `FlightMission` is created as `PLANNED`, and audit `DRONE_ASSIGNMENT` is written. The order status does not change.
Response `201 Created` with `Location: /api/delivery-orders/{id}/flight-mission`
```json
{
  "id": "d5f7b9c1-4e6a-4c8b-8d2f-3b5d7f9a1c24",
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "droneId": "c4e6a8b0-3d5f-4b7a-9c1e-2a4c6e8f0b13",
  "flightStatus": "PLANNED",
  "destinationSlotId": "b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10",
  "scheduledDepartureTime": "2026-11-12T08:00:00Z"
}
```
Errors: `400` `droneId` missing, package heavier than the payload, or route beyond the range with the 30 percent reserve; `403`; `404` order or drone not found; `409` the order is not `APPROVED` or not scheduled, it already has a `PLANNED` or `IN_AIR` mission, the drone is not `IDLE` or has an overlapping mission, the destination station is not `OPERATIONAL`, or it has no `EMPTY` slot.

### 3.4 GET /api/dispatch/approved-orders (LA-136, LA-137)

Auth: `DISPATCHER`, `ADMIN`, active accounts only. Lists orders whose **current** status is `APPROVED`, across customers. Scheduling and assignment do not remove an order from this queue; intake does. This is independent of the customer-owned `GET /api/delivery-orders`.

| Query | Rule |
|---|---|
| `originStationId`, `destinationStationId` | Optional UUIDs, match the respective station |
| `searchKeyword` | Optional, trimmed, at most 50 characters; case-insensitive literal partial match on `trackingNumber` |
| `fromDate`, `toDate` | Optional UTC ISO 8601, inclusive bounds on `createdAt`; `fromDate <= toDate` |
| `scheduledFrom`, `scheduledTo` | Optional UTC ISO 8601, inclusive bounds on `scheduledDepartureTime`; `scheduledFrom <= scheduledTo`; excludes unscheduled orders when either is supplied |
| `assignmentStatus` | Optional `UNASSIGNED` or `ASSIGNED`; omit for both |
| `page`, `pageSize` | Default 1 and 20; `page >= 1`, `pageSize` 1 to 100 |

Filters combine with AND. Sort by `createdAt` ascending, then `orderId` ascending to break ties. Response `200 OK`: `PagedResult<ApprovedOrderSummary>` using `items/page/pageSize/totalCount/totalPages`. Counts are after filtering, before pagination. Empty results and pages beyond the last page return `items: []`; zero matches means `totalPages: 0`.

`ApprovedOrderSummary` example:

```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "status": "APPROVED",
  "packageId": "c41f8b0a-2d37-4a6b-8e1c-5f7a9d3b6e22",
  "packageName": "Books",
  "weightGram": 450,
  "originStation": { "id": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44", "stationCode": "HCM-01", "name": "District 1 Hub" },
  "destinationStation": { "id": "a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21", "stationCode": "HCM-02", "name": "Thu Duc Hub" },
  "scheduledDepartureTime": null,
  "estimatedDeliveryTime": null,
  "assignmentStatus": "UNASSIGNED",
  "missionId": null,
  "droneId": null,
  "droneSerialNumber": null,
  "createdAt": "2026-11-12T02:40:00Z",
  "updatedAt": "2026-11-12T03:15:00Z"
}
```

`ASSIGNED` means an active mission (`PLANNED` or `IN_AIR`) exists; otherwise `UNASSIGNED`, with all three mission/drone identifiers null. Old aborted or completed missions never count as an assignment. Schedule times are nullable independently of assignment: an order can be scheduled but unassigned.

Errors: `400` malformed UUID, date, reversed date range, unsupported assignment enum or invalid search/pagination; `401` missing/invalid token; `403` inactive account or other role. A valid station UUID with no matching orders returns an empty page.

### 3.5 GET /api/dispatch/orders/{id} (LA-136, LA-137)

Auth: `DISPATCHER`, `ADMIN`. Staff detail across customers, readable in every order status so an open screen can refresh after intake, cancellation or another dispatcher's action. Response `200 OK`: `DispatchOrderDetail` with these required keys:

| Field | Shape / meaning |
|---|---|
| `orderId`, `trackingNumber`, `status` | UUID, string, current order status |
| `package` | `Package` from customer-order-api.md section 1, including dimensions, weight, fragility and nullable image URL |
| `originStation`, `destinationStation` | `StationSummary` from customer-order-api.md section 1, including code, address, coordinates and status |
| `scheduledDepartureTime`, `estimatedDeliveryTime`, `actualDepartureTime`, `actualDeliveryTime` | Nullable UTC timestamps |
| `assignmentStatus` | Same derivation as 3.4 |
| `currentMission` | Null when no active mission; otherwise the object below |
| `statusLogs` | `StatusLog[]` from customer-order-api.md section 1; oldest first, database log id ascending for timestamp ties; empty array if none |
| `createdAt`, `updatedAt` | UTC timestamps |

`currentMission` when assigned:

```json
{
  "id": "d5f7b9c1-4e6a-4c8b-8d2f-3b5d7f9a1c24",
  "flightStatus": "PLANNED",
  "drone": { "id": "c4e6a8b0-3d5f-4b7a-9c1e-2a4c6e8f0b13", "serialNumber": "DR-0001", "modelName": "SkyCarrier S1", "status": "ASSIGNED" },
  "startSlotId": null,
  "destinationSlotId": "b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10",
  "startTime": null,
  "endTime": null
}
```

Slot ids and start/end times are nullable. This read makes no reservation and does not promise that a write will succeed. The UI uses this detail, then 3.1, 3.2 and 3.3; it refreshes detail and queue after writes, including a `409`. Errors: `400` invalid UUID; `401`; `403`; `404` unknown order. A known order outside `APPROVED` still returns `200`; the UI disables scheduling/assignment and shows its current status.

## 4. State machine (LA-32)

The arrows below are the only ones allowed; anything else returns `409` (BR-DW-01, BR-DW-18). `DELIVERED` and `CANCELLED` have no outgoing arrow.

| # | From | To | Triggered by | Endpoint or source |
|---|---|---|---|---|
| 1 | - | `PENDING` | Customer | `POST /api/delivery-orders` (merged) |
| 2 | `PENDING` | `APPROVED` | Dispatcher, Admin | 2.2 |
| 3 | `PENDING` | `CANCELLED` | Dispatcher, Admin | 2.3 (reject) |
| 4 | `PENDING` | `CANCELLED` | Customer | `POST /api/delivery-orders/{id}/cancel` (merged) |
| 5 | `APPROVED` | `CANCELLED` | Customer, Dispatcher | cancel; also aborts a `PLANNED` mission, drone back to `IDLE`, destination slot back to `EMPTY` |
| 6 | `APPROVED` | `READY_FOR_TAKEOFF` | Station operator | intake, [station-api.md](station-api.md) section 4 |
| 7 | `READY_FOR_TAKEOFF` | `IN_TRANSIT` | System | takeoff at the scheduled time |
| 8 | `READY_FOR_TAKEOFF` | `CANCELLED` | Dispatcher, Admin | cancel (open point 5) |
| 9 | `IN_TRANSIT` | `ARRIVED` | System | landing |
| 10 | `IN_TRANSIT` | `FAILED` | System, Dispatcher | 6.1 |
| 11 | `ARRIVED` | `DELIVERED` | Station operator | pickup, [station-api.md](station-api.md) section 4 (merged) |
| 12 | `ARRIVED` | `FAILED` | System | 24 hour collection timeout (merged, LA-125) |
| 13 | `FAILED` | `APPROVED` | Dispatcher | 6.2 `RESCHEDULE` |
| 14 | `FAILED` | `CANCELLED` | Dispatcher, Station operator | 6.2 `CANCEL`, 6.3 |

Every arrow appends one `delivery_status_logs` row (`changedBy` is `null` for the system) and creates one notification for the customer (BR-DW-04, BR-DW-21). Effects on mission, drone and slots are listed per row in section 3 of the class specification.

## 5. Tracking and telemetry (read side of LA-35 and LA-36, LA-330)

### 5.1 GET /api/dispatch/active-deliveries
Auth: `DISPATCHER`, `MANAGER`, `ADMIN`. Query: `stationId` (optional, origin or destination). Response `200 OK`: array of `ActiveDelivery` for missions that are `PLANNED` or `IN_AIR`, not paged.
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "orderStatus": "IN_TRANSIT",
  "missionId": "d5f7b9c1-4e6a-4c8b-8d2f-3b5d7f9a1c24",
  "flightStatus": "IN_AIR",
  "droneSerialNumber": "DR-0001",
  "originStationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "destinationStationId": "a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21",
  "scheduledDepartureTime": "2026-11-12T08:00:00Z",
  "estimatedDeliveryTime": "2026-11-12T08:24:00Z",
  "lastTelemetryAt": "2026-11-12T08:10:05Z",
  "telemetryStale": false
}
```
`telemetryStale` is `true` when the last sample is older than 60 seconds (BR-DW-13). Errors: `403`.

### 5.2 GET /api/delivery-orders/{id}/tracking
Auth: the owning `CUSTOMER`, or `DISPATCHER`, `MANAGER`, `ADMIN`. A customer who does not own the order gets `404` (BR-CO-36). Response `200 OK`
```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "status": "IN_TRANSIT",
  "originStation": { "id": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44", "name": "District 1 Hub", "latitude": 10.7769, "longitude": 106.7009 },
  "destinationStation": { "id": "a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21", "name": "Thu Duc Hub", "latitude": 10.8506, "longitude": 106.7719 },
  "estimatedDeliveryTime": "2026-11-12T08:24:00Z",
  "lastPosition": {
    "latitude": 10.8012, "longitude": 106.7311, "altitudeMeters": 80.0,
    "speedMps": 11.5, "batteryPercentage": 71, "timestamp": "2026-11-12T08:10:05Z"
  },
  "telemetryStale": false
}
```
`lastPosition` is `null` when there is no sample yet or the mission is not `IN_AIR`; the server never invents a position (BR-DW-24). No drone serial number and no pickup code for the customer.

### 5.3 Live updates
The server pushes position and status changes over SignalR (BR-DW-13). Hub `/hubs/tracking`, authenticated with the same bearer token; a client calls `Subscribe(orderId)` and receives `PositionUpdated` (the `lastPosition` object above) and `StatusChanged` (an `OrderDecision`). The ownership rule of 5.2 applies to `Subscribe`. Until the hub exists, clients poll 5.2 every 5 seconds (open point 6).

### 5.4 Telemetry from the simulator (LA-330)
The simulator is software only; the platform does not control real drones. It runs inside the backend and calls the same ingestion service an external source would. For tests and demos it can also be driven through `POST /api/flight-missions/{missionId}/telemetry`, auth `ADMIN` only (open point 7). Request
```json
{ "latitude": 10.8012, "longitude": 106.7311, "altitudeMeters": 80.0, "speedMps": 11.5, "batteryPercentage": 71, "timestamp": "2026-11-12T08:10:05Z" }
```
Rules (BR-DW-24): latitude -90 to 90, longitude -180 to 180, battery 0 to 100, `altitudeMeters` and `speedMps` not negative, `timestamp` UTC and not in the future. The mission must be `IN_AIR`. Response `204 No Content`. Errors: `400` field errors; `403`; `404` mission not found; `409` the mission is not `IN_AIR`. The sample is stored in `flight_telemetry_logs` and never contains the pickup code.

## 6. Failed deliveries (LA-34, F-08 and F-09)

### 6.1 POST /api/delivery-orders/{id}/fail
Auth: `DISPATCHER`, `ADMIN`. A dispatcher reports a failure of a flight that is in the air. (The system uses the same service for an automatic abort; the collection timeout is done by the job already merged.) Request
```json
{ "failureType": "EMERGENCY_LANDING", "reason": "Drone landed in a field after a motor warning." }
```
`failureType` is `MISSION_ABORTED`, `EMERGENCY_LANDING` or `HARDWARE_FAULT` (`COLLECTION_TIMEOUT` is set by the system only). Moves `IN_TRANSIT` to `FAILED`, the mission to `ABORTED` with `endTime`, writes the status log, a notification and audit `DELIVERY_FAILURE_DETECTED`. Response `200 OK`: `OrderDecision` with `status = FAILED`.
Errors: `400` unknown `failureType`, reason missing or longer than 500; `403`; `404`; `409` status is not `IN_TRANSIT` (the drone landed, or another failure was recorded first).

### 6.2 POST /api/delivery-orders/{id}/resolve-failure
Auth: `DISPATCHER`, `ADMIN`. The order must be `FAILED`; it stays `FAILED` until a dispatcher chooses a path (BR-DW-17). Request
```json
{ "action": "RESCHEDULE", "note": "Package checked at the destination, no damage.", "packageVerified": true }
```
| `action` | Result | Resources |
|---|---|---|
| `RESCHEDULE` | `APPROVED`; `scheduledDepartureTime` and `estimatedDeliveryTime` cleared; the next assignment creates a new mission | Reserved destination slot released; old mission stays `ABORTED` as history; drone back to `IDLE` |
| `RETURN_TO_ORIGIN` | Stays `FAILED` with `cancelReason = RETURN_REQUESTED`; a notification goes to the origin station operator; closed by 6.3 | Released by 6.3 |
| `CANCEL` | `CANCELLED` | Slots and drone released |

`note` is required (1 to 500). `packageVerified` must be `true` for `RESCHEDULE`. Writes the status log (where the status changes), a notification and audit `FAILED_DELIVERY_RESOLVED`. Response `200 OK`: `OrderDecision`.
Errors: `400` unknown `action`, note missing or too long, `packageVerified` not `true` for `RESCHEDULE`; `403`; `404`; `409` status is not `FAILED`, a return was already requested, or for `RESCHEDULE` the parcel is still in a destination slot that the operator has not emptied (6.3 first).

### 6.3 POST /api/landing-stations/{stationId}/operations/parcel-removal
Auth: `STATION_OPERATOR` of that station, `ADMIN` (the own-station rule of [station-api.md](station-api.md) applies). The operator confirms that a parcel left the slot after a failure. Request
```json
{ "trackingNumber": "DD-202611-0012" }
```
Empties the slot that holds the parcel. If the order is `FAILED` with `RETURN_REQUESTED`, it becomes `CANCELLED` with `cancelReason = RETURNED_TO_ORIGIN`, with status log, notification and audit `FAILED_DELIVERY_RESOLVED`. Response `200 OK`: `OrderDecision`.
Errors: `400` tracking number missing; `403` not the operator of this station; `404` unknown tracking number; `409` the order is not `FAILED` or the parcel is not in a slot of this station. This endpoint is to be copied into section 4 of [station-api.md](station-api.md) when this contract is confirmed.

### 6.4 GET /api/dispatch/failed-orders (LA-139, LA-140)

Auth: `DISPATCHER`, `ADMIN`. Response `200 OK`: `PagedResult<FailedOrderSummary>`, with the same pagination/count/empty-page rules as 3.4. One row per order, representing its **latest failure episode** (latest status log entering `FAILED`, ordered by `createdAt`, then log id). Resolved orders remain discoverable through the resolution filter; an order that fails again is represented by the new episode.

| Query | Rule |
|---|---|
| `originStationId`, `destinationStationId`, `searchKeyword`, `page`, `pageSize` | Same rules as 3.4 |
| `fromDate`, `toDate` | Optional inclusive UTC bounds on `failedAt`, not order creation; validate ordering |
| `failureType` | Optional `MISSION_ABORTED`, `EMERGENCY_LANDING`, `HARDWARE_FAULT`, `COLLECTION_TIMEOUT` or `UNKNOWN` |
| `resolutionStatus` | Optional `UNRESOLVED`, `RETURN_REQUESTED`, `RESOLVED`, `ALL`; default `UNRESOLVED` |

Filters combine with AND. Sort by `failedAt` descending, then `orderId` ascending. `UNKNOWN` represents legacy failures whose type was not stored; never infer a failure type by parsing human-written remarks. `FailedOrderSummary` has required fields:

| Field | Shape / meaning |
|---|---|
| `orderId`, `trackingNumber`, `status` | Order identity and current status (may have moved beyond `FAILED`) |
| `packageName` | String |
| `originStation`, `destinationStation` | `{ id, stationCode, name }`, as in 3.4 |
| `failureLogId` | Integer id of the status log entering `FAILED`; identifies the episode |
| `failureType`, `failureReason`, `failedAt` | Type above; nullable original failure log remarks; UTC timestamp |
| `resolutionStatus` | Derived state defined below |
| `lastResolutionAction` | Null, `RESCHEDULE`, `RETURN_TO_ORIGIN` or `CANCEL`; null when not recorded |
| `resolvedAt` | Nullable UTC timestamp; null until resolution completes |
| `updatedAt` | Current order update timestamp |

Resolution derivation for the latest episode: `FAILED` with `cancelReason = RETURN_REQUESTED` means `RETURN_REQUESTED`; other `FAILED` orders mean `UNRESOLVED`; an episode followed by a transition from `FAILED` to `APPROVED` or `CANCELLED` means `RESOLVED`, even if the order has progressed further. `RETURN_REQUESTED` is not resolved until return confirmation. `lastResolutionAction` is `RETURN_TO_ORIGIN` for a requested or completed return. A plain cancellation is `CANCEL`.

Errors: `400` invalid filters, UUID, dates, enum, search or pagination; `401`; `403`. Valid filters with no results return an empty page.

### 6.5 GET /api/dispatch/failed-orders/{id} (LA-139, LA-140)

`id` is the **order UUID**, not a mission or failure log id. Auth: `DISPATCHER`, `ADMIN`. Response `200 OK`: all fields of `DispatchOrderDetail` (3.5), plus the following required fields. Readable after resolution, so refreshing after `RESCHEDULE` or `CANCEL` does not lose the detail.

| Field | Shape / meaning |
|---|---|
| `latestFailure` | Object containing `failureLogId`, `failureType`, `failureReason`, `failedAt`, `resolutionStatus`, `lastResolutionAction`, `resolvedAt`, as defined in 6.4 |
| `returnToOriginStatus` | `NOT_REQUESTED`, `REQUESTED` or `CONFIRMED`, for the latest episode |
| `parcelLocation` | Null if unverified; otherwise `{ stationId, slotId }` for the station/slot currently holding the parcel; nullable `slotId` if held outside a slot |
| `failureHistory` | Array of episode objects with the same shape as `latestFailure`, oldest first, log id ascending for ties |
| `resolutionHistory` | Array of recorded handling events below, oldest first, persistent event id ascending for ties |
| `resolutionHistoryComplete` | Boolean; false for legacy episodes with missing structured handling records |

`resolutionHistory` event shape:

```json
{
  "failureLogId": 184,
  "action": "RETURN_TO_ORIGIN",
  "note": "Return the checked parcel to the origin operator.",
  "performedBy": "f9c7a501-1234-4567-8abc-123456789abc",
  "createdAt": "2026-11-12T09:00:00Z"
}
```

Event actions are `RESCHEDULE`, `RETURN_TO_ORIGIN`, `CANCEL`, `RETURN_CONFIRMED`, `PARCEL_CLEARED`; `performedBy` is a nullable actor UUID and `note` is nullable for legacy records. Preserve history across rescheduling and repeated failures. No fabricated event or note for missing legacy data; return recorded events only, possibly `[]`, with `resolutionHistoryComplete: false`. New failure/resolution writes must persist enough structured data to return complete history (section 8).

`returnToOriginStatus` is `REQUESTED` for the latest episode's return request and `CONFIRMED` only when receipt at the **origin** station is recorded (`RETURNED_TO_ORIGIN`); otherwise `NOT_REQUESTED`. Clearing a destination slot under LA-336 only creates a `PARCEL_CLEARED` event. It must never imply `RETURN_CONFIRMED` or a verified parcel location at the origin. Reads never change the order, release a slot or send notifications.

Errors: `400` malformed UUID; `401`; `403`; `404` unknown order or order with no recorded failure episode. A resolved episode still returns `200`. The UI submits the existing write routes in 6.1/6.2, then refreshes this detail and the queue; a concurrent-state `409` also requires a refresh. A `404` during refresh means the selection cannot be loaded and the UI returns to the list.

## 7. Error example

```json
{ "type": "https://httpstatuses.com/409", "title": "Conflict", "status": 409, "detail": "The order is no longer PENDING." }
```

## 8. Differences with the code that is already merged

- The customer list/detail controller explicitly authorizes `CUSTOMER` and scopes reads to the token owner. Sections 3.4, 3.5, 6.4 and 6.5 need separate dispatcher query endpoints/DTOs; do not widen customer authorization or make staff call customer-owned reads. Until implemented, UI work can use fixtures matching these proposed DTOs; do not treat manually entered order ids as a completed list integration.
- `delivery_status_logs` stores failure remarks but no structured `failureType`; `audit_logs` has no action payload for resolution notes, package verification or failure-episode association. LA-139 needs durable structured metadata (schema/migration to be agreed) written atomically with failure, resolution and parcel operations, keyed to the failure log id. Store original type/reason, handling action/note/actor/time and explicit return confirmation. Keep the existing status log and audit entries. Legacy types use `UNKNOWN` and incomplete history is explicit, rather than guessed from text. GET endpoints must not create or backfill records as a side effect.
- The unique index on `flight_missions.order_id` allows one mission per order forever. `RESCHEDULE` needs a second mission for the same order, so the index becomes a partial unique index `WHERE flight_status IN ('PLANNED','IN_AIR')` (migration, first change of LA-33).
- `POST /api/delivery-orders/{id}/cancel` (LA-107) changes only the order. When a mission exists it must also abort the mission, set the drone `IDLE` and the destination slot `EMPTY` (row 5 of section 4).
- Mobile code that calls `/fail` or `/resolve-failure` (drafted in PR #133 and removed from it) can be rebuilt on sections 6.1 and 6.2 once the backend exists.

## 9. Open points

1. **Cruise speed for the estimate.** `estimatedDeliveryTime` needs a speed or the AI estimate (Sprint 7). Proposed: use the AI estimate when it exists, otherwise geodesic distance divided by a configured cruise speed; the value is a configuration setting the team has to decide.
2. **Rows of the approval queue.** Proposed: show the customer name and phone to the dispatcher, but not the recipient data. To be confirmed.
3. **Changing a schedule.** Proposed: refused with `409` while a `PLANNED` mission exists. A `DELETE /api/delivery-orders/{id}/flight-mission` that cancels the assignment is not defined yet; it is needed before a dispatcher can fix a wrong assignment.
4. **Battery level.** BR-DW-07 asks for 80 percent charge but `drones` has no charge column. Proposed: only `IDLE` drones are candidates and the dispatcher confirms readiness in the maintenance action of [drone-api.md](drone-api.md). `CHARGING` drones are not offered until the simulator gives a trusted value.
5. **Dispatcher cancel of `READY_FOR_TAKEOFF`.** The customer cancel endpoint is for `CUSTOMER` only. Row 8 needs a staff route, for example `POST /api/dispatch/orders/{id}/cancel`, plus the operator handing the package back. Not defined here.
6. **SignalR in Sprint 5 or 6.** The hub can wait for Sprint 6 (LA-35); polling of 5.2 works until then.
7. **Telemetry over HTTP.** Proposed: only for tests and demos, `ADMIN` only. If the simulator always runs inside the backend, 5.4 can be dropped.
8. **Landing and takeoff triggers.** Takeoff happens at the scheduled time. Landing is derived from telemetry (near the destination and almost stopped); the distance and speed limits are not defined and depend on the simulator.
9. **Parcel clearing versus return confirmation (LA-336).** Jira LA-336 proposes `clear-expired-parcel` with `slotId` and `reason` at the destination, leaving the order `FAILED`. Section 6.3 currently combines parcel removal with closing a requested return. These operations must be reconciled with station-api.md before implementation: destination clearing cannot confirm origin receipt; explicit origin receipt must be required to set `RETURNED_TO_ORIGIN`. The read fields in 6.5 distinguish the two facts.
10. **Failure metadata persistence.** Confirm the schema/migration for section 8 before implementing LA-139. The read contract defines history and unknown-data behavior; the current entities alone cannot supply complete structured history or failure-type filtering for new records.
