# API Contract - Drone Fleet

Contract for the drone fleet (Sprint 4: LA-30 manage the drone fleet; FR-19, UC-15). It follows [sequence-drone-management.md](../design/sequence-drone-management.md) sections 2 and 3. Stations and slots are in [station-api.md](station-api.md).

> **Status: proposed, to be confirmed by the team.** Any change goes through a pull request that edits this file. Items that still need a decision are in section 5.

**Out of scope here:** the candidate list and the assignment of a drone to an order (`available-drones`, `flight-mission`, sequence section 4). They belong to the dispatch workflow of Sprint 5 and get their own contract. Until then no endpoint sets a drone to `ASSIGNED` or `FLYING`.

## 1. Conventions

Everything in section 1 of [auth-api.md](auth-api.md) applies (base URL, `/api`, camelCase, UUIDs, UTC, bearer token, RFC 7807 errors).

| Item | Rule |
|---|---|
| Roles | `DISPATCHER` and `ADMIN` can read and write (open point 1). Every other role gets `403`. |
| Status | `IDLE`, `ASSIGNED`, `FLYING`, `CHARGING`, `MAINTENANCE`. "Available" is not a status; it is computed when a drone is assigned. |
| Units | Grams, kilometres, mAh. `batteryCapacityMah` is the design capacity of the battery, **not** the charge level. |
| No hard delete | A drone is taken out of service with `MAINTENANCE`, never deleted (FR-19). |
| Audit | Create, update and both maintenance actions write `DRONE_CREATE`, `DRONE_UPDATE`, `DRONE_MAINTENANCE_START`, `DRONE_MAINTENANCE_COMPLETE` to `audit_logs`, in the same transaction. |

### Drone object
```json
{
  "id": "9a1c3e5f-7b92-4d04-a6c8-1e3a5c7e9b20",
  "serialNumber": "DRN-VTOL-2026-004",
  "modelName": "AeroDrone Express V2",
  "maxPayloadGram": 3000,
  "maxFlightRangeKm": 25,
  "batteryCapacityMah": 22000,
  "status": "IDLE",
  "currentStationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "currentStationName": "District 1 Hub",
  "createdAt": "2026-11-06T02:30:00Z"
}
```
`currentStationId` and `currentStationName` are `null` while the drone is `FLYING`.

## 2. Fleet records

Base path `/api/drones`.

### 2.1 GET /api/drones
Query: `status` (optional, one value), `stationId` (optional), `search` (optional, matches `serialNumber` or `modelName`). Response `200 OK`: array of `Drone` sorted by `serialNumber`. Not paged (open point 2). Errors: `400` unknown `status`, `403`.

### 2.2 GET /api/drones/{id}
Response `200 OK`: `Drone` plus `activeMissionId` (the `PLANNED` or `IN_AIR` mission, or `null`). Errors: `403`, `404`.

### 2.3 POST /api/drones
Request (`CreateDroneRequest`)
```json
{
  "serialNumber": "DRN-VTOL-2026-005",
  "modelName": "AeroDrone Express V2",
  "maxPayloadGram": 3000,
  "maxFlightRangeKm": 25,
  "batteryCapacityMah": 22000,
  "currentStationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44"
}
```

| Field | Rule |
|---|---|
| `serialNumber` | Required, at most 100 characters, unique. |
| `modelName` | Required, at most 100 characters. |
| `maxPayloadGram` | Required, greater than 0 and at most 3000 (the package limit of the project). |
| `maxFlightRangeKm` | Required, greater than 0, at most 9999.99. |
| `batteryCapacityMah` | Required, whole number greater than 0. |
| `currentStationId` | Optional, an existing station. |

The new drone starts as `MAINTENANCE`, not `IDLE`, because nobody has checked it yet; it is released with 3.1 (open point 3). `status` is not accepted in the request. Response `201 Created` with `Location: /api/drones/{id}` and the `Drone`. Errors: `400`, `403`, `409` the serial number is already used.

### 2.4 PUT /api/drones/{id}
Request: `modelName`, `maxPayloadGram`, `maxFlightRangeKm`, `batteryCapacityMah` (same rules). `serialNumber`, `status` and `currentStationId` cannot be changed here; unknown fields return `400`. Moving a drone to another station needs a checked presence, so it is not an edit (open point 4). Response `200 OK`: `Drone`. Errors: `400`, `403`, `404`, `409` the drone has a `PLANNED` or `IN_AIR` mission.

## 3. Maintenance

### 3.1 PUT /api/drones/{id}/maintenance
One endpoint for both directions. The drone row is locked while the request runs, so it cannot be assigned and sent to maintenance at the same time.

Request
```json
{ "action": "START", "expectedStatus": "IDLE", "reason": "Rotor inspection." }
```
or
```json
{ "action": "COMPLETE", "expectedStatus": "MAINTENANCE", "inspectionPassed": true, "readyForService": true, "reason": "Rotor replaced and tested." }
```

| Field | Rule |
|---|---|
| `action` | Required, `START` or `COMPLETE`. |
| `expectedStatus` | Required. The status the client saw. If the real status differs, the answer is `409` and nothing changes, so a stale screen cannot overwrite a newer state. |
| `reason` | Required, 5 to 500 characters (audit). |
| `inspectionPassed` | Required for `COMPLETE`, must be `true`. |
| `readyForService` | Required for `COMPLETE`. `true` = ground check done and the battery is ready, `false` = still has to charge. |

| Action | Allowed from | Result |
|---|---|---|
| `START` | `IDLE`, `CHARGING` | `MAINTENANCE` |
| `COMPLETE` with `readyForService = true` | `MAINTENANCE` | `IDLE` |
| `COMPLETE` with `readyForService = false` | `MAINTENANCE` | `CHARGING` |

Response `200 OK`: `Drone`. Maintenance never cancels or reassigns a mission and never frees a slot.

Errors: `400` missing field, or `inspectionPassed` is not `true`; `403`; `404`; `409` the drone is `ASSIGNED` or `FLYING`, has a `PLANNED` or `IN_AIR` mission, `expectedStatus` is stale, or the action does not fit the status. Repeating a request that already took effect returns `409` with the current status and writes no second audit entry.

## 4. Error example

```json
{ "type": "https://httpstatuses.com/409", "title": "Conflict", "status": 409, "detail": "The drone has an active mission. Resolve it in the dispatch workflow first." }
```

## 5. Open points

These need a team decision. The contract above states the proposed answer.

1. **Roles.** SRS and UC-15 give fleet maintenance to the Dispatcher; F-10 suggests the Administrator too. Proposed: both. `MANAGER` and `STATION_OPERATOR` get no access in this sprint.
2. **Paging.** Proposed: none, the fleet is small. Add `page` and `pageSize` later if the list grows.
3. **First status of a new drone.** The schema default is `IDLE`; the design proposes `MAINTENANCE` until the first check. Needs approval.
4. **Moving a drone between stations.** Not in this sprint. The design says a station change needs verified ground presence, and no workflow for it exists yet. A drone is created at a station and stays there until the dispatch workflow (Sprint 5) moves it.
5. **Charging.** There is no source for the charge level (no `batteryPercentage`; telemetry belongs to a mission). So whether a drone is ready is entered by the dispatcher as `readyForService`; it is not measured. This changes when the telemetry simulator (LA-330) exists.
6. **Maintenance history.** There is no table for inspection results or repair notes. `audit_logs` has no reason column today (see station-api.md open point 9). Proposed: the same nullable `details` column keeps the `reason`. A real maintenance history table is a separate schema decision.
7. **Payload limit.** `maxPayloadGram` is capped at 3000 to match the package limit; business rule D-01 about conflicting limits is still open.
