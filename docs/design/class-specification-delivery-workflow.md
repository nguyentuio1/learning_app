# Delivery Workflow — Class Specification

**Parent:** LA-199 - Design documents: Delivery workflow

**Subtask:** LA-234 - Class diagram and specification for the workflow module

**Status:** Proposed for review

The class diagram and the state diagrams are in [class-diagram-delivery-workflow.md](class-diagram-delivery-workflow.md).

---

## 1. Scope and conventions

This specification covers the entities, the state machine, the services and the DTOs of the **Delivery Workflow** module (SRS FR-13 to FR-19, FR-24, FR-25, FR-12, FR-14, FR-32; use cases UC-11 to UC-14). Sources: [functions-delivery-workflow.md](../requirements/functions-delivery-workflow.md) (F-01 to F-10), [business-processes.md](business-processes.md), [business rules](../requirements/business-rules.md) BR-DW-01 to BR-DW-25, the [ERD](../architecture/erd.md), and the entities in `backend/SmartDroneDelivery.Api/Entities/` and `Data/AppDbContext.cs`.

| C# type | PostgreSQL | Remark |
|---|---|---|
| `Guid` | `uuid` | Keys and foreign keys. |
| `long` | `bigint` | Identity keys of the log tables. |
| `decimal` | `numeric` | Coordinates, altitude, speed, distance. |
| `DateTime` | `timestamptz` | Always UTC. |
| `string` | `varchar(n)` / `text` | Statuses and codes are plain strings checked in code; there is no database enum. |

Controllers, services and DTOs below are **proposed**. The entities and their constraints are what the merged code and migrations contain today.

---

## 2. Entity specification

### 2.1 DeliveryOrder (`delivery_orders`)

| Property | Type | Key / nullability | Rule |
|---|---|---|---|
| `Id` | `Guid` | PK | Generated. |
| `TrackingNumber` | `string` `varchar(50)` | NOT NULL, UNIQUE | Server generated at creation. |
| `CustomerId` | `Guid` | FK `users`, `Restrict` | Owner of the order. |
| `PackageId` | `Guid` | FK `packages`, `Restrict`, one to one | One package, one order. |
| `OriginStationId`, `DestinationStationId` | `Guid` | FK `landing_stations`, `Restrict` | Different stations, both `OPERATIONAL` when the order is created and again when it is approved. |
| `DispatcherId` | `Guid?` | FK `users`, `SetNull` | The dispatcher who approved the order. Set by `PENDING` to `APPROVED`. |
| `DeliveryAddressId` | `Guid?` | FK `customer_addresses`, `SetNull` | Optional. |
| `Status` | `string` `varchar(30)` | NOT NULL, default `PENDING`, concurrency token | One of the 8 statuses in section 3. |
| `CancelReason` | `string?` | | Set when the order becomes `CANCELLED`; for a return it is `RETURNED_TO_ORIGIN`. |
| `PickupSecureCode` | `string` `varchar(10)` | NOT NULL | Never returned to staff, never logged (BR-DW-22). |
| `RecipientName` / `RecipientPhone` | `string` | NOT NULL, 150 / 20 | Entered by the customer. |
| `ScheduledDepartureTime` | `DateTime?` | | Planned departure. Lives on the order, not on the mission. Set by scheduling. |
| `ActualDepartureTime` | `DateTime?` | | Set at takeoff. |
| `EstimatedDeliveryTime` | `DateTime?` | | Computed by scheduling, later refined by the AI estimate. |
| `ActualDeliveryTime` | `DateTime?` | | Set when the pickup code is accepted. |
| `CreatedAt`, `UpdatedAt` | `DateTime` | NOT NULL, `UpdatedAt` is a concurrency token | |
| Navigations | | | `Customer`, `Package`, `OriginStation`, `DestinationStation`, `Dispatcher`, `DeliveryAddress`, `FlightMissions`, `AiEtaPrediction`, `StatusLogs`, `Notifications`. |

### 2.2 FlightMission (`flight_missions`)

| Property | Type | Key / nullability | Rule |
|---|---|---|---|
| `Id` | `Guid` | PK | |
| `OrderId` | `Guid` | FK `delivery_orders`, `Restrict` | One-to-many mission history; partial unique index permits at most one `PLANNED`/`IN_AIR` mission per order (LA-136). |
| `DroneId` | `Guid` | FK `drones`, `Restrict` | The assigned drone. |
| `StartSlotId` | `Guid?` | FK `station_slots`, `SetNull` | Origin slot. Filled at package intake, when the operator chooses the slot. |
| `DestinationSlotId` | `Guid?` | FK `station_slots`, `SetNull` | Destination slot. Reserved when the drone is assigned (BR-DW-06). |
| `FlightStatus` | `string` `varchar(30)` | NOT NULL, default `PLANNED` | `PLANNED`, `IN_AIR`, `COMPLETED`, `ABORTED`. |
| `StartTime`, `EndTime` | `DateTime?` | | Actual flight times only. They are not used to store a planned window. |

LA-136 adds `CreatedAt` (UTC, database default current timestamp) for stable history ordering and nullable `ScheduledDepartureTime`/`EstimatedArrivalTime` snapshots. Assignment copies the order window into the new mission. Actual `StartTime`/`EndTime` remain actual flight times. Legacy active windows are copied from the order during migration; inactive history never inherits a later rescheduled order window.

### 2.3 DeliveryStatusLog (`delivery_status_logs`)

| Property | Type | Key / nullability | Rule |
|---|---|---|---|
| `Id` | `long` | PK, identity | |
| `OrderId` | `Guid` | FK `delivery_orders`, `Cascade` | |
| `FromStatus` | `string?` `varchar(30)` | | `null` for the first entry. |
| `ToStatus` | `string` `varchar(30)` | NOT NULL | |
| `ChangedBy` | `Guid?` | FK `users`, `SetNull` | `null` for an automated change (takeoff, landing, timeout). |
| `Remarks` | `string?` | | Reason, resolution note or short text. Never contains the secure code. |
| `CreatedAt` | `DateTime` | NOT NULL | Rows are append-only (BR-DW-04). |

### 2.4 FlightTelemetryLog (`flight_telemetry_logs`)

| Property | Type | Rule |
|---|---|---|
| `Id` | `long` | PK, identity. |
| `MissionId` | `Guid` | FK `flight_missions`, `Cascade`. Index `(MissionId, Timestamp)`. |
| `Latitude`, `Longitude` | `decimal` | -90 to 90, -180 to 180 (BR-DW-24). |
| `AltitudeMeters`, `SpeedMps` | `decimal` | Metres above sea level, metres per second. |
| `BatteryPercentage` | `int` | 0 to 100. This is the only place a charge level exists. |
| `Timestamp` | `DateTime` | Time of the sample. |

### 2.5 Other entities used by the workflow

| Entity | Fields used | Role in the workflow |
|---|---|---|
| `Notification` | `UserId`, `OrderId`, `Type`, `Title`, `Message` | One row for the customer on every key transition (BR-DW-21). `Type` is `ORDER_STATUS_CHANGED`. |
| `AuditLog` | `UserId`, `Action`, `EntityName`, `EntityId`, `IpAddress`, `UserAgent`, `Timestamp` | One row for every decision of a person (BR-DW-19). There is no column for a reason or a before and after value. |
| `AiEtaPrediction` | `OrderId` (unique), `CalculatedDistanceKm`, `PredictedDurationMinutes` | Route distance for the range check and the estimated duration. Optional until the AI service exists (Sprint 7). |
| `Drone`, `StationSlot` | `Status`, `CurrentStationId` | Changed together with the mission. Specified in [class-specification-stations-slots-drones.md](class-specification-stations-slots-drones.md). |

Audit actions used by this module: `ORDER_APPROVAL`, `ORDER_REJECTION`, `ORDER_CANCELLATION`, `DRONE_ASSIGNMENT`, `DELIVERY_FAILURE_DETECTED`, `FAILED_DELIVERY_RESOLVED`.

---

## 3. State machine

`OrderStateMachine.CanTransition` accepts exactly the arrows below. Anything else is refused with `409 Conflict`. `DELIVERED` and `CANCELLED` have no outgoing arrow (BR-DW-18).

| # | From | To | Who or what triggers it | Guard | Changes made in the same transaction |
|---|---|---|---|---|---|
| 1 | — | `PENDING` | Customer (already merged) | Valid package and operational stations | Order, first status log, notification to dispatchers. |
| 2 | `PENDING` | `APPROVED` | Dispatcher, Admin | Both stations `OPERATIONAL` | `DispatcherId`; status log; notification; audit `ORDER_APPROVAL`. |
| 3 | `PENDING` | `CANCELLED` | Dispatcher, Admin (reject) | Reason 1 to 500 characters | `CancelReason`; status log; notification; audit `ORDER_REJECTION`. |
| 4 | `PENDING` | `CANCELLED` | Customer (already merged) | Reason 1 to 500 characters | `CancelReason`; status log; notification. |
| 5 | `APPROVED` | `CANCELLED` | Customer, Dispatcher | Reason | As 4. If a `PLANNED` mission exists: mission `ABORTED`, drone `IDLE`, destination slot `EMPTY`. Audit `ORDER_CANCELLATION`. |
| 6 | `APPROVED` | `READY_FOR_TAKEOFF` | Station operator (intake) | Measures within limits, origin slot `EMPTY` | Origin slot `OCCUPIED`, `StartSlotId` set; status log; notification. See [sequence-package-pickup-confirmation.md](sequence-package-pickup-confirmation.md). |
| 7 | `READY_FOR_TAKEOFF` | `IN_TRANSIT` | System (takeoff) | Scheduled time reached, mission `PLANNED` | `ActualDepartureTime`; mission `IN_AIR` with `StartTime`; drone `FLYING` and `CurrentStationId = null`; origin slot `EMPTY`; status log; notification. |
| 8 | `READY_FOR_TAKEOFF` | `CANCELLED` | Dispatcher, Admin | Reason | As 5, plus origin slot `EMPTY` after the operator hands the package back. |
| 9 | `IN_TRANSIT` | `ARRIVED` | System (landing) | Mission `IN_AIR` | Mission `COMPLETED` with `EndTime`; destination slot `OCCUPIED`; drone `CurrentStationId` = destination and status `CHARGING`; status log; notification to the customer. |
| 10 | `IN_TRANSIT` | `FAILED` | System or Dispatcher | Failure type and reason | Mission `ABORTED` with `EndTime`; status log; notification; audit `DELIVERY_FAILURE_DETECTED` when a person marks it. See [sequence-failed-delivery-handling.md](sequence-failed-delivery-handling.md). |
| 11 | `ARRIVED` | `DELIVERED` | Station operator (pickup code) | Code correct, not locked | `ActualDeliveryTime`; destination slot `EMPTY`; drone released; status log; notification. |
| 12 | `ARRIVED` | `FAILED` | System (24 hour timeout) | 24 hours after arrival | Status log; notification; the parcel stays in its slot, which stays `OCCUPIED` (BR-LS-20). |
| 13 | `FAILED` | `APPROVED` | Dispatcher (reschedule) | Resolution note | `ScheduledDepartureTime`, `EstimatedDeliveryTime` cleared; reserved slot released; old mission and drone released (section 5, item 1); status log; notification; audit `FAILED_DELIVERY_RESOLVED`. |
| 14 | `FAILED` | `CANCELLED` | Dispatcher (cancel), Station operator (returned package received) | Resolution note | `CancelReason` (`RETURNED_TO_ORIGIN` for a return); slots and drone released; status log; notification; audit `FAILED_DELIVERY_RESOLVED`. |
| 15 | `APPROVED` | `FAILED` | Origin station operator (intake rejection, already merged) | Parcel fails the check; reason 5 to 500 characters | `CancelReason`; status log; notification; audit `PACKAGE_INTAKE_REJECT`; no slot changes. See [station-api.md](../api/station-api.md) section 4.3. |

### Rules that apply to every transition

1. **One writer.** `IOrderTransitionWriter` changes `Status` after checking `OrderStateMachine.CanTransition(from, to)`. `ApplyAsync` saves status, log and customer notification. Existing operations use `StageAsync` in their own transaction and supply the operation-specific notification, audit and resource effects before saving and committing together. The operation service remains responsible for role authorization and the guards/effects of its row. Initial creation writes its own first `PENDING` log.
2. **Conditional update, not a long lock.** The write is `UPDATE delivery_orders SET Status = @to ... WHERE Id = @id AND Status = @expectedFrom`. Zero rows changed means someone else moved the order first, and the answer is `409` with no log, audit entry or notification. This is the pattern of `DeliveryOrderMutationService`, and its behaviour under concurrent requests is tested on PostgreSQL (LA-322). The functions document says `SELECT ... FOR UPDATE`; the conditional update gives the same guarantee and is what the merged code does.
3. **All or nothing.** Status, mission, drone, slots, status log, audit entry and notification are saved in one transaction. A notification that cannot be pushed afterwards (SignalR, mobile push) never undoes the change; the stored row is the source.
4. **What a response may contain.** Responses never contain `PickupSecureCode` or audit data.

---

## 4. Service interfaces (proposed)

```csharp
namespace SmartDroneDelivery.Api.Services;

public interface IOrderTransitionWriter
{
    // expectedFrom is the status the caller saw. Throws ApiException 409 when the order has moved or the arrow is not allowed.
    Task ApplyAsync(Guid orderId, string expectedFrom, string to, Guid? actorId, string? remarks, CancellationToken ct = default);
    // Existing operations: caller owns the relational transaction, notification/resource effects, SaveChanges and commit.
    Task StageAsync(DeliveryOrder order, string expectedFrom, string to, Guid? actorId, string? remarks, DateTime now, CancellationToken ct = default);
}

public interface IOrderReviewService
{
    Task<PagedResult<PendingOrderDto>> ListPendingAsync(PendingOrderFilter filter, PagingDto paging, CancellationToken ct = default);
    Task<OrderDecisionDto> ApproveAsync(Guid orderId, Guid dispatcherId, CancellationToken ct = default);
    Task<OrderDecisionDto> RejectAsync(Guid orderId, Guid dispatcherId, string reason, CancellationToken ct = default);
}

public interface IDeliverySchedulingService
{
    Task<ScheduleDto> ScheduleAsync(Guid orderId, Guid dispatcherId, DateTime departureUtc, CancellationToken ct = default);
}

public interface IDroneAssignmentService
{
    Task<IReadOnlyList<DroneCandidateDto>> GetCandidatesAsync(Guid orderId, CancellationToken ct = default);
    Task<FlightMissionDto> AssignAsync(Guid orderId, Guid droneId, Guid dispatcherId, CancellationToken ct = default);
}

public interface IActiveDeliveryService
{
    Task<IReadOnlyList<ActiveDeliveryDto>> ListActiveAsync(ActiveDeliveryFilter filter, CancellationToken ct = default);
    Task<TrackingDto> GetTrackingAsync(Guid orderId, Guid callerId, string callerRole, CancellationToken ct = default);
    Task IngestTelemetryAsync(Guid missionId, TelemetrySampleDto sample, CancellationToken ct = default);
}

public interface IFailureResolutionService
{
    Task<OrderDecisionDto> MarkFailedAsync(Guid orderId, Guid? actorId, string failureType, string reason, CancellationToken ct = default);
    Task<OrderDecisionDto> ResolveAsync(Guid orderId, Guid dispatcherId, string action, string note, bool packageVerified, CancellationToken ct = default);
    Task ConfirmParcelRemovedAsync(Guid stationId, string trackingNumber, Guid operatorId, CancellationToken ct = default);
}

public interface IPickupTimeoutSweeper
{
    Task<int> SweepExpiredArrivedOrdersAsync(CancellationToken ct = default);
}
```

---

## 5. DTOs and proposed endpoints

Endpoints are proposed and will be fixed in the Sprint 5 contract. Customer endpoints stay under `/api/delivery-orders` (see [customer-order-api.md](../api/customer-order-api.md)). The two drone routes keep the names already used in [sequence-drone-management.md](sequence-drone-management.md).

| Proposed endpoint | Role | Service method | Function |
|---|---|---|---|
| `GET /api/dispatch/pending-orders` | `DISPATCHER`, `ADMIN` | `ListPendingAsync` | F-01 |
| `POST /api/delivery-orders/{id}/approve` | `DISPATCHER`, `ADMIN` | `ApproveAsync` | F-02 |
| `POST /api/delivery-orders/{id}/reject` | `DISPATCHER`, `ADMIN` | `RejectAsync` | F-02 |
| `PUT /api/delivery-orders/{id}/schedule` | `DISPATCHER`, `ADMIN` | `ScheduleAsync` | F-04 |
| `GET /api/delivery-orders/{id}/available-drones` | `DISPATCHER`, `ADMIN` | `GetCandidatesAsync` | F-05 |
| `POST /api/delivery-orders/{id}/flight-mission` | `DISPATCHER`, `ADMIN` | `AssignAsync` | F-05 |
| `GET /api/dispatch/active-deliveries` | `DISPATCHER`, `MANAGER`, `ADMIN` | `ListActiveAsync` | F-06 |
| `GET /api/delivery-orders/{id}/tracking` | owner `CUSTOMER`, staff | `GetTrackingAsync` | F-07 |
| `POST /api/delivery-orders/{id}/fail` | `DISPATCHER`, `ADMIN` | `MarkFailedAsync` | F-08 |
| `POST /api/delivery-orders/{id}/resolve-failure` | `DISPATCHER`, `ADMIN` | `ResolveAsync` | F-09 |
| `POST /api/landing-stations/{stationId}/operations/parcel-removal` | `STATION_OPERATOR` of the station, `ADMIN` | `ConfirmParcelRemovedAsync` | F-09 (closes a return, empties a slot). To be added to [station-api.md](../api/station-api.md). |

| DTO | Properties | Validation |
|---|---|---|
| `RejectRequest` | `reason` | Required, 1 to 500 characters. |
| `ScheduleRequest` | `scheduledDepartureTime` | UTC, at least 15 minutes in the future (BR-DW-05). |
| `ScheduleDto` | `orderId`, `scheduledDepartureTime`, `estimatedDeliveryTime`, `estimatedFlightMinutes` | |
| `DroneCandidateDto` | `droneId`, `serialNumber`, `modelName`, `maxPayloadGram`, `maxFlightRangeKm`, `currentStationId`, `status`, `reasonsNotEligible` | Advisory; reserves nothing. |
| `AssignDroneRequest` | `droneId` | Required UUID. |
| `FlightMissionDto` | `id`, `orderId`, `droneId`, `flightStatus`, `destinationSlotId`, `scheduledDepartureTime` | `scheduledDepartureTime` is read from the order. |
| `MarkFailedRequest` | `failureType`, `reason` | `failureType` is `MISSION_ABORTED`, `EMERGENCY_LANDING`, `COLLECTION_TIMEOUT` or `HARDWARE_FAULT`. `reason` 1 to 500 characters. |
| `ResolveFailureRequest` | `action`, `note`, `packageVerified` | `action` is `RESCHEDULE`, `RETURN_TO_ORIGIN` or `CANCEL`. `note` required, 1 to 500 characters. `packageVerified` must be `true` for `RESCHEDULE`. |
| `OrderDecisionDto` | `orderId`, `trackingNumber`, `status`, `updatedAt` | Never contains the secure code. |
| `TelemetrySampleDto` | `latitude`, `longitude`, `altitudeMeters`, `speedMps`, `batteryPercentage`, `timestamp` | Ranges of BR-DW-24. |

---

## 6. Findings that affect the design and open points

The functions document was written before the schema and the station and drone designs were final. These points differ from it or from each other. Each states the proposed answer.

1. **Mission history (implemented by LA-136).** The order has a mission collection and the active partial unique index allows history while preventing a second `PLANNED`/`IN_AIR` mission. Historical missions are never overwritten to reschedule.
2. **Planned times (LA-136).** The order remains the current scheduling source. Assignment also retains the departure/arrival interval on the mission so subsequent order rescheduling cannot change historical overlap calculations. Actual flight times remain separate.
3. **Battery level.** BR-DW-07 requires 80 percent but `drones` has no charge column and telemetry exists only for a mission. Proposed: a drone is a candidate only when `IDLE`, and the dispatcher confirms readiness in the maintenance action ([drone-api.md](../api/drone-api.md) section 3). `CHARGING` drones are not offered until a trusted source exists (the telemetry simulator, LA-330).
4. **Origin slot at assignment.** Sequence-drone-management section 4 reserves "the selected slots" at both stations. BR-DW-06 and F-05 reserve the **destination** slot only, and [station-api.md](../api/station-api.md) intake needs an `EMPTY` origin slot chosen by the operator. Proposed: reserve only the destination slot at assignment, and set `StartSlotId` at intake. Section 4 of sequence-drone-management should be corrected to match.
5. **Return to origin.** There is no status for "returning" and no task entity. Proposed: the order stays `FAILED` after the dispatcher chooses `RETURN_TO_ORIGIN`, with `cancel_reason = RETURN_REQUESTED` as the marker, and a notification goes to the station operator. The order becomes `CANCELLED` with `RETURNED_TO_ORIGIN` when the operator confirms the parcel with the `parcel-removal` operation. A task table is only worth adding if returns must be listed. See [sequence-failed-delivery-handling.md](sequence-failed-delivery-handling.md).
6. **Customer cancel with a mission (implemented by LA-136).** Cancelling an `APPROVED` order aborts its current unflown planned mission and releases its assigned drone and reserved destination slot atomically. Historical slot claims are not cleared. An occupied current parcel slot prevents cancellation until physical removal is confirmed.
7. **Takeoff and landing triggers.** The takeoff is automatic at the scheduled time and the landing is derived from telemetry (the last sample is within a distance of the destination and the speed is near zero). The distance and the speed thresholds are not defined and depend on the simulator.
8. **Reschedule when the parcel is not at the origin.** Row 13 sends the order back to `APPROVED`, and the next steps assume the package can be handed in at the origin station. After a timeout failure the parcel sits in a destination slot. Proposed: `RESCHEDULE` is refused with `409` until the dispatcher has set `packageVerified` and the operator has emptied the slot that still holds the parcel.
