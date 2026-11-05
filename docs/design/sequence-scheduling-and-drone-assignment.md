# Sequence Diagrams - Scheduling and Drone Assignment

**Parent:** LA-199 - Design documents: Delivery workflow

**Subtask:** LA-236 - Sequence diagram for scheduling and drone assignment

**Status:** Proposed for review

This document specifies the sequence diagrams for the dispatcher steps between an approved order and the package intake: choosing the departure time, choosing a drone, and giving the resources back when an assigned order is cancelled (FR-15, FR-16, FR-14, FR-32; UC-12). [sequence-drone-management.md](sequence-drone-management.md) section 4 already describes the assignment from the **drone** side. This document describes it from the **order** side, adds the departure time and the cancellation, and corrects one point (section 6, item 1). Controllers and services are **proposed**; see [class-diagram-delivery-workflow.md](class-diagram-delivery-workflow.md).

---

## 1. Sources and design boundaries

| Source | Basis |
|---|---|
| [SRS](../requirements/SRS.md), [use cases](../requirements/use-cases.md) | FR-15 schedule departure and assign an available drone, creating a flight mission; FR-16 reject an overlapping assignment; UC-12. |
| [functions-delivery-workflow.md](../requirements/functions-delivery-workflow.md) | F-04 schedule departure time; F-05 validate and assign drone. |
| [business rules](../requirements/business-rules.md) | BR-DW-05 to BR-DW-10, BR-DW-19, BR-DW-20, BR-DW-23, BR-LS-06. |
| [sequence-drone-management.md](sequence-drone-management.md) | Candidate query is advisory, the submit rechecks everything under locks, drone states, no hard-coded limits. |
| [class-specification-delivery-workflow.md](class-specification-delivery-workflow.md) | Entities and the rule that the planned times are on the order, not on the mission. |
| [station-api.md](../api/station-api.md), [drone-api.md](../api/drone-api.md) | Slot statuses, drone statuses, who sets `RESERVED`. |

Every request passes authentication and authorization first: `401` for no or invalid token, `403` for a role other than `DISPATCHER` or `ADMIN`. The diagrams begin after that gate.

What changes when a drone is assigned (the order does **not** change status):

| Object | Before | After |
|---|---|---|
| `delivery_orders.status` | `APPROVED` | `APPROVED` |
| `flight_missions` | none | one row, `PLANNED`, with `DroneId` and `DestinationSlotId`; `StartSlotId` empty |
| `drones.status` | `IDLE` | `ASSIGNED` |
| Destination `station_slots.status` | `EMPTY` | `RESERVED` |

---

## 2. Sequence 1: Schedule the departure (F-04)

```mermaid
sequenceDiagram
    autonumber
    actor Dispatcher
    participant UI as Dispatcher web (screen 05)
    participant API as OrderSchedulingController
    participant Service as DeliverySchedulingService
    participant DB as AppDbContext / PostgreSQL

    Dispatcher->>UI: Pick a departure date and time for an approved order
    UI->>API: PUT /api/delivery-orders/{id}/schedule (scheduledDepartureTime)
    API->>Service: ScheduleAsync(orderId, dispatcherId, departureUtc)
    Service->>DB: Read the order, both stations and any flight mission
    alt Order not found
        DB-->>Service: null
        Service-->>API: 404 Not Found
        API-->>UI: 404
    else Order found
        DB-->>Service: Order, stations, mission or null
        alt Status is not APPROVED
            Service-->>API: 409 Conflict
            API-->>UI: 409
            UI-->>Dispatcher: Refresh the order and show its status
        else A mission already exists
            Service-->>API: 409 Conflict (the drone was chosen for another time)
            API-->>UI: 409
            UI-->>Dispatcher: Cancel the assignment first, then schedule again
        else Departure less than 15 minutes from now
            Service-->>API: 400 with errors.scheduledDepartureTime
            API-->>UI: 400
            UI-->>Dispatcher: Keep the form and show the 15 minute rule
        else Departure accepted
            Service->>Service: Distance = AI estimate if present, otherwise geodesic distance between the stations
            Service->>Service: Flight minutes = distance at the nominal speed, plus 10 minutes for takeoff and landing
            Service->>DB: UPDATE delivery_orders SET ScheduledDepartureTime, EstimatedDeliveryTime, UpdatedAt WHERE Id AND Status = APPROVED
            alt No row changed
                Service-->>API: 409 Conflict (the order moved meanwhile)
                API-->>UI: 409
            else One row changed
                Service-->>API: ScheduleDto
                API-->>UI: 200 OK
                UI-->>Dispatcher: Show the departure, the estimated arrival and the drone step
            end
        end
    end
```

Scheduling is a plain update: it changes no status, so it writes no status log and no notification. `UpdatedAt` is the concurrency token that makes a stale save lose (open point 2).

---

## 3. Sequence 2: Candidate drones and assignment (F-05)

The candidate list reserves nothing and can be out of date when the dispatcher confirms. The assignment therefore checks everything again, and the decisive steps are conditional updates inside one transaction.

```mermaid
sequenceDiagram
    autonumber
    actor Dispatcher
    participant UI as Dispatcher web (screen 06)
    participant API as OrderSchedulingController
    participant Service as DroneAssignmentService
    participant DB as AppDbContext / PostgreSQL

    Dispatcher->>UI: Open Assign drone for a scheduled order
    UI->>API: GET /api/delivery-orders/{id}/available-drones
    API->>Service: GetCandidatesAsync(orderId)
    Service->>DB: Read the order, the package and the stations, then the IDLE drones with no PLANNED or IN_AIR mission
    DB-->>Service: Order data and drones
    Service->>Service: Mark each drone that cannot carry the weight or cover 1.3 times the distance
    Service-->>UI: 200 list with reasonsNotEligible per drone
    UI-->>Dispatcher: Show the drones, the ineligible ones greyed out with the reason

    Dispatcher->>UI: Choose a drone and confirm
    UI->>API: POST /api/delivery-orders/{id}/flight-mission (droneId)
    API->>Service: AssignAsync(orderId, droneId, dispatcherId)
    Service->>DB: Begin transaction
    Service->>DB: Read the order, the drone, the package
    alt Order or drone not found
        Service->>DB: Roll back
        Service-->>UI: 404 Not Found
    else Order is not APPROVED, has no departure time, or already has a mission
        Service->>DB: Roll back
        Service-->>UI: 409 Conflict (order conflict)
    else Weight over max payload, or 1.3 times the distance over max range
        Service->>DB: Roll back
        Service-->>UI: 400 (suitability error)
    else Order passes the checks
        Service->>DB: UPDATE drones SET Status = ASSIGNED WHERE Id AND Status = IDLE
        alt No row changed
            Note over Service,DB: Maintenance or another assignment won, or the drone is not IDLE
            Service->>DB: Roll back
            Service-->>UI: 409 Conflict (drone conflict)
            UI-->>Dispatcher: Refresh the candidates and choose another drone
        else Drone reserved
            Service->>DB: UPDATE station_slots SET Status = RESERVED for the lowest EMPTY slot of the destination station, skipping rows locked by others
            alt No empty slot or the destination is not OPERATIONAL
                Service->>DB: Roll back
                Service-->>UI: 409 Conflict (station or slot conflict)
            else Slot reserved
                Service->>DB: Insert flight_missions (PLANNED, order, drone, destination slot)
                alt Unique order id violated
                    Service->>DB: Roll back
                    Service-->>UI: 409 Conflict (order already has a mission)
                else Mission inserted
                    Service->>DB: Insert audit_logs (DRONE_ASSIGNMENT, flight_missions, mission id)
                    Service->>DB: Commit
                    DB-->>Service: Committed
                    Service-->>API: FlightMissionDto
                    API-->>UI: 201 Created
                    UI-->>Dispatcher: Show the assignment, waiting for the package at the origin station
                end
            end
        end
    end
```

How the rules of the functions document map to this flow:

| Rule | Where it is enforced |
|---|---|
| Drone `IDLE` only (BR-DW-07, with the change in [class-specification-delivery-workflow.md](class-specification-delivery-workflow.md) section 6, item 3) | Conditional `UPDATE drones ... WHERE Status = IDLE`; the candidate list is only advice. |
| Payload (BR-DW-08) | `max_payload_gram >= package.weight_gram`, read inside the transaction. The cargo envelope was already checked when the order was created. |
| Range with a 30 percent reserve (BR-DW-09) | `max_flight_range_km >= distance * 1.3`, using the AI estimate when it exists, otherwise the geodesic distance. |
| No overlap (BR-DW-10, FR-16) | A drone with an active mission is `ASSIGNED` or `FLYING`, so the status guard rejects a second one. In this version a drone holds one active mission at a time; booking a drone for several future windows is not supported (open point 3). |
| Destination slot (BR-DW-06) | One `EMPTY` slot of the destination station becomes `RESERVED`. The origin slot is not reserved here (section 6, item 1). |
| Audit (BR-DW-19) | `DRONE_ASSIGNMENT` in the same transaction. |

Two assignments at the same time for the same order or the same drone cannot both pass: the second `UPDATE drones` finds the drone already `ASSIGNED`, and the second mission insert breaks the unique `order_id`. A maintenance request and an assignment for one drone are ordered the same way ([sequence-drone-management.md](sequence-drone-management.md) section 3).

---

## 4. Sequence 3: Release the resources when an assigned order is cancelled

Cancelling an `APPROVED` order (customer or dispatcher) or a `READY_FOR_TAKEOFF` order (dispatcher only, BR-DW-03) must give back the drone and the slots at once (BR-DW-20). The merged customer cancel does not do this yet, because no mission can exist today; this is a required change when assignment is built.

```mermaid
sequenceDiagram
    autonumber
    actor Person as Customer or Dispatcher
    participant API as Cancel endpoint
    participant Service as Cancellation service
    participant Writer as OrderTransitionWriter
    participant DB as AppDbContext / PostgreSQL

    Person->>API: POST /api/delivery-orders/{id}/cancel (reason)
    API->>Service: CancelAsync(orderId, actor, reason)
    Service->>Service: Check the reason, and that a customer is not cancelling READY_FOR_TAKEOFF
    alt Reason invalid
        Service-->>API: 400
    else Customer and status is READY_FOR_TAKEOFF
        Service-->>API: 409 Conflict (only the dispatcher can cancel now)
    else Allowed
        Service->>Writer: ApplyAsync(orderId, expectedStatus, CANCELLED, actorId, reason)
        Writer->>DB: Begin transaction
        Writer->>DB: UPDATE delivery_orders SET Status = CANCELLED, CancelReason WHERE Id AND Status = expected
        alt No row changed
            Writer->>DB: Roll back
            Writer-->>API: 409 Conflict
        else One row changed
            opt The order has a PLANNED mission
                Writer->>DB: UPDATE flight_missions SET FlightStatus = ABORTED WHERE OrderId AND FlightStatus = PLANNED
                Writer->>DB: UPDATE drones SET Status = IDLE WHERE Id = mission drone AND Status = ASSIGNED
                Writer->>DB: UPDATE station_slots SET Status = EMPTY for the destination slot WHERE Status = RESERVED
            end
            opt Status was READY_FOR_TAKEOFF
                Writer->>DB: Origin slot to EMPTY after the operator hands the package back
            end
            Writer->>DB: Insert delivery_status_logs, notifications and audit_logs (ORDER_CANCELLATION)
            Writer->>DB: Commit
            Writer-->>API: Done
            API-->>Person: 200 OK with CANCELLED
        end
    end
```

Every `UPDATE` of the drone and of the slot is conditional on the status it expects. If another process already changed it, the update affects no row and the cancel still completes; the dispatcher sees the difference in the audit log. A drone that is `FLYING` is never touched by a cancel, because a cancel is not allowed once the order is `IN_TRANSIT` (BR-DW-03).

---

## 5. Persistence alignment

| Topic | Interpretation |
|---|---|
| Planned times | `scheduled_departure_time` and `estimated_delivery_time` are on `delivery_orders`. `flight_missions.start_time` and `end_time` are the actual flight and stay empty until takeoff. |
| Status log | Scheduling and assignment are not status changes, so they add no row to `delivery_status_logs`. The assignment is in `audit_logs` (`DRONE_ASSIGNMENT`). |
| Slot status | The API sets `RESERVED` only through assignment, `OCCUPIED` only through intake and landing, `EMPTY` through takeoff, pickup, cancellation and release ([station-api.md](../api/station-api.md) section 1). |
| Notifications | None for scheduling or assignment: the customer is told at `APPROVED`, `READY_FOR_TAKEOFF` and the later statuses (BR-DW-21). |
| After commit | Any push to dispatchers is sent after the commit and never undoes it. |

---

## 6. Corrections and decisions to confirm

1. **Origin slot at assignment.** Section 4 of [sequence-drone-management.md](sequence-drone-management.md) says the assignment reserves "the selected `EMPTY` slots" of both stations. This document reserves only the **destination** slot, as BR-DW-06 and F-05 say, and leaves `StartSlotId` empty until the operator loads the package: the intake in [station-api.md](../api/station-api.md) needs an `EMPTY` origin slot, so a slot reserved earlier would make it fail with `409`. That section of the drone document should be corrected to match.
2. **Schedule an order that has a mission.** Proposed: refused with `409`; the dispatcher cancels the assignment and schedules again. Changing the time under an existing mission would need the drone checks to be repeated for the new window. A dedicated "unassign" action is not part of this design; in this version the way to remove a mission is to cancel the order, or to resolve a failure.
3. **One active mission per drone.** BR-DW-10 describes an interval check with 15 and 20 minute buffers, which only matters when a drone can hold several future missions. This version keeps the simpler rule (one active mission, status guard). The interval check and the buffers (still proposed, business rule D-04) are for a later change, together with a column or table for the planned window.
4. **Operating hours of a station.** F-04 asks the departure to fall within the operating schedule of both stations. No such schedule exists in the schema. Proposed: not checked in this version.
5. **Nominal speed and the clearance minutes.** 15 m/s and 10 minutes come from F-04 and are proposals. They should be constants in one place, so the AI estimate can replace them later.
6. **Reschedule after a failure.** The order returns to `APPROVED` and goes through these sequences again; this needs the schema change of [class-specification-delivery-workflow.md](class-specification-delivery-workflow.md) section 6, item 1 (one mission per order is unique today).
