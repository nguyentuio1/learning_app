# Sequence Diagrams - Landing Station and Slot Management

**Parent:** LA-198 - Design documents: Landing stations, slots and drones

**Subtask:** LA-231 - Sequence diagram for station and slot management

**Status:** Proposed for review

This document specifies the sequence diagrams for **Landing Station and Slot Management** in the SmartDroneDelivery platform. It models the end-to-end interactions across Web/Mobile clients, API controllers, application services, database transactions, and real-time SignalR notifications for the creation, update, operational mode change, deactivation, and slot lifecycle management.

---

## 1. Sources and design boundaries

| Source | Basis |
|---|---|
| [SRS](../requirements/SRS.md) | FR-20: create, update, deactivate and list landing stations; FR-21: manage slots of each station and their status; FR-22: real-time availability; FR-03: authorization; FR-32: sensitive-action audit logging. |
| [Landing Station Functions](../requirements/functions-landing-station.md) | F-01: List stations; F-02: Create/update station & mode toggle; F-03: Manage slots inventory; F-04: Slot operational override; F-05: Real-time availability monitoring. |
| [Station & Pickup Activity Diagram](activity-station-and-pickup.md) | Station/slot maintenance workflows, in-flight arrival conflict resolution, and rerouting alerts. |
| [Station Operator User Flow](user-flows/station-operator.md) | Screen flow: Dashboard $\rightarrow$ Slot grid $\rightarrow$ Maintenance toggle $\rightarrow$ Diagnostic check. |
| [Business Rules](../requirements/business-rules.md) | BR-LS-01 to BR-LS-06, BR-LS-21; conflict handling, unique codes, and audit integrity. |
| [AppDbContext / Entities](../../backend/SmartDroneDelivery.Api/Data/AppDbContext.cs) | `LandingStation`, `StationSlot`, `FlightMission`, `AuditLog`, `User`. Unique index on `(station_id, slot_number)` and `station_code`. |
| [Auth contract](../api/auth-api.md) | Bearer token authentication, role validation (`ADMIN`, `STATION_OPERATOR`, `DISPATCHER`), problem details error format. |

---

## 2. Sequence 1: Create Landing Station and Provision Slots

This flow models an Administrator creating a new landing hub. The system validates uniqueness, inserts the station record, and automatically provisions $N$ empty slots numbered 1 to $N$ in a single database transaction.

```mermaid
sequenceDiagram
    autonumber
    actor Admin
    participant UI as Admin Web Portal
    participant API as LandingStationsController
    participant Service as LandingStationService
    participant DB as AppDbContext / PostgreSQL
    participant Audit as AuditService / SignalR

    Admin->>UI: Enter station code, name, address, coordinates, totalSlots (N), operatorId
    UI->>API: POST /api/landing-stations (Bearer token, CreateLandingStationRequestDto)
    Note over API: Verify Bearer token and ADMIN role authorization
    alt Missing or invalid token / Unauthorized role
        API-->>UI: 401 Unauthorized / 403 Forbidden
        UI-->>Admin: Show access denied error
    end

    API->>Service: CreateStationAsync(dto, adminUserId)
    Service->>Service: Validate geographic coordinates (-90..90, -180..180) and totalSlots >= 1
    
    Service->>DB: Check if StationCode exists (landing_stations)
    alt StationCode already in use
        DB-->>Service: Existing record found
        Service-->>API: 409 Conflict (ProblemDetails: Station code already exists)
        API-->>UI: 409 Conflict
        UI-->>Admin: Show duplicate code error
    else StationCode is unique
        DB-->>Service: Null (unique)
        opt OperatorId specified
            Service->>DB: Verify User exists and Role == STATION_OPERATOR
            DB-->>Service: User verified
        end

        Service->>DB: Begin Database Transaction
        Service->>DB: Insert into landing_stations (id, station_code, name, address, lat, lon, total_slots, status='OPERATIONAL', operator_id, created_at)
        
        loop For slotNumber = 1 to totalSlots
            Service->>DB: Insert into station_slots (id, station_id, slot_number, status='EMPTY', last_updated)
        end

        Service->>DB: Insert into audit_logs (user_id, action='STATION_CREATE', entity_name='landing_stations', entity_id=station.id, timestamp)
        Service->>DB: Commit Transaction
        DB-->>Service: Transaction committed successfully

        Service->>Audit: Broadcast station added event over SignalR (hub/stations)
        Service-->>API: LandingStationResponseDto
        API-->>UI: 201 Created (Location: /api/landing-stations/{id}, payload)
        UI-->>Admin: Display success toast and navigate to station slot grid
    end
```

---

## 3. Sequence 2: Update Landing Station Attributes and Operator Assignment

This flow allows an Administrator to update physical attributes (name, address, coordinates) or reassign the operational staff for an existing hub.

```mermaid
sequenceDiagram
    autonumber
    actor Admin
    participant UI as Admin Web Portal
    participant API as LandingStationsController
    participant Service as LandingStationService
    participant DB as AppDbContext / PostgreSQL

    Admin->>UI: Modify station name, address, coordinates, or assigned operator
    UI->>API: PUT /api/landing-stations/{id} (Bearer token, UpdateLandingStationRequestDto)
    Note over API: Check ADMIN role authorization

    API->>Service: UpdateStationAsync(id, dto, adminUserId)
    Service->>DB: Query landing_stations by Id
    alt Station not found
        DB-->>Service: null
        Service-->>API: 404 Not Found
        API-->>UI: 404 Not Found
        UI-->>Admin: Show station not found error
    else Station exists
        DB-->>Service: LandingStation record
        Service->>Service: Validate coordinate ranges and required text fields
        
        opt OperatorId changed
            Service->>DB: Verify new operator has STATION_OPERATOR role
            DB-->>Service: Validated
        end

        Service->>DB: Begin Database Transaction
        Service->>DB: Update landing_stations (name, address, latitude, longitude, operator_id, updated_at=UtcNow)
        Service->>DB: Insert into audit_logs (user_id=adminUserId, action='STATION_UPDATE', entity_name='landing_stations', entity_id=id)
        Service->>DB: Commit Transaction
        DB-->>Service: Success

        Service-->>API: Updated LandingStationResponseDto
        API-->>UI: 200 OK (updated station data)
        UI-->>Admin: Show update success banner
    end
```

---

## 4. Sequence 3: Station Operational Mode Toggle, Emergency Shutdown, and Deactivation

This flow handles changing the station operational mode (`OPERATIONAL` $\rightarrow$ `MAINTENANCE` / `OFFLINE` or vice versa). It explicitly models the **aeronautical conflict check** for in-flight missions en route to the hub.

```mermaid
sequenceDiagram
    autonumber
    actor Staff as Admin / Station Operator
    participant UI as Web Portal / Mobile App
    participant API as LandingStationsController
    participant Service as LandingStationService
    participant DB as AppDbContext / PostgreSQL
    participant Hub as SignalR Hub / Dispatcher Dashboard

    Staff->>UI: Select station mode (MAINTENANCE or OFFLINE) and enter required reason
    UI->>API: PUT /api/landing-stations/{id}/status (Bearer token, StationStatusChangeRequestDto)
    Note over API: Verify ADMIN or assigned STATION_OPERATOR authorization

    API->>Service: ChangeStationStatusAsync(id, dto, userId)
    Service->>DB: Query landing_stations by Id
    alt Station does not exist
        DB-->>Service: null
        Service-->>API: 404 Not Found
        API-->>UI: 404 Not Found
    else Station found
        DB-->>Service: LandingStation record
        
        alt Switching to MAINTENANCE or OFFLINE
            Service->>DB: Query active in-flight missions (flight_status == 'IN_AIR' AND destination_slot.station_id == id)
            DB-->>Service: Active in-flight missions collection
            
            alt Active in-flight missions exist
                Note over Service,Hub: In-flight conflict detected! Urgent reroute required.
                Service->>DB: Begin Transaction
                Service->>DB: Update landing_stations set status = dto.NewStatus, updated_at = UtcNow
                Service->>DB: Insert into audit_logs (user_id, action='STATION_STATUS_CHANGE', entity_name='landing_stations', entity_id=id, remarks=dto.Reason)
                Service->>DB: Commit Transaction
                
                Service->>Hub: Broadcast Urgent Reroute Alert (topic: dispatcher-alerts)
                Note over Hub: Dispatchers receive visual pop-up alert with affected mission IDs
                Service-->>API: LandingStationResponseDto (warning: active flights alerted)
                API-->>UI: 200 OK with RerouteWarning flag
                UI-->>Staff: Warning banner: Station set to MAINTENANCE. Urgent reroute alert dispatched to flight controllers!
            else No active in-flight missions
                Service->>DB: Begin Transaction
                Service->>DB: Update landing_stations set status = dto.NewStatus, updated_at = UtcNow
                Service->>DB: Insert into audit_logs (user_id, action='STATION_STATUS_CHANGE', entity_name='landing_stations', entity_id=id, remarks=dto.Reason)
                Service->>DB: Commit Transaction
                
                Service->>Hub: Broadcast station status change (topic: hub/stations)
                Service-->>API: LandingStationResponseDto
                API-->>UI: 200 OK
                UI-->>Staff: Station operational status updated successfully
            end
        else Restoring to OPERATIONAL
            Service->>DB: Verify all critical pads are not in permanent hardware lock
            Service->>DB: Update landing_stations set status = 'OPERATIONAL', updated_at = UtcNow
            Service->>DB: Insert into audit_logs (user_id, action='STATION_STATUS_CHANGE', entity_id=id, remarks='Restored to operational')
            Service->>Hub: Broadcast station restored event
            Service-->>API: LandingStationResponseDto
            API-->>UI: 200 OK
            UI-->>Staff: Station restored to OPERATIONAL and re-enabled in flight scheduler pool
        end
    end
```

---

## 5. Sequence 4: Station Slot Inventory Management (Add & Remove Slots)

This flow models expanding or shrinking the slot capacity of a landing station.

```mermaid
sequenceDiagram
    autonumber
    actor Admin
    participant UI as Admin Web Portal
    participant API as StationSlotsController
    participant Service as StationSlotService
    participant DB as AppDbContext / PostgreSQL

    alt Add Slot to Station
        Admin->>UI: Click "Add Slot" on Station Detail page
        UI->>API: POST /api/landing-stations/{stationId}/slots (Bearer token, AddSlotRequestDto)
        Note over API: Check ADMIN role authorization
        API->>Service: AddSlotAsync(stationId, dto, adminUserId)
        Service->>DB: Query landing_stations including existing Slots
        DB-->>Service: Station record with Slots collection
        Service->>Service: Determine next SlotNumber = max(existing SlotNumber) + 1
        
        Service->>DB: Begin Transaction
        Service->>DB: Insert into station_slots (id=Guid.NewGuid(), station_id=stationId, slot_number=nextSlotNumber, status='EMPTY', last_updated=UtcNow)
        Service->>DB: Update landing_stations set total_slots = total_slots + 1, updated_at = UtcNow
        Service->>DB: Insert into audit_logs (user_id=adminUserId, action='SLOT_INVENTORY_CHANGE', entity_id=stationId, remarks='Added slot ' + nextSlotNumber)
        Service->>DB: Commit Transaction
        DB-->>Service: Success
        Service-->>API: StationSlotResponseDto
        API-->>UI: 201 Created (new slot details)
        UI-->>Admin: Slot added to grid successfully

    else Remove Slot from Station
        Admin->>UI: Click "Remove Slot" on an existing slot
        UI->>API: DELETE /api/station-slots/{id} (Bearer token)
        Note over API: Check ADMIN role authorization
        API->>Service: RemoveSlotAsync(id, adminUserId)
        Service->>DB: Query station_slots by Id with Station
        DB-->>Service: StationSlot record
        
        alt Slot is OCCUPIED or RESERVED
            Note over Service: Cannot delete active slot!
            Service-->>API: 409 Conflict (ProblemDetails: Slot is currently occupied or reserved by an active mission)
            API-->>UI: 409 Conflict
            UI-->>Admin: Error: Cannot remove slot holding a drone or reserved for arrival
        else Slot is EMPTY or ERROR
            Service->>DB: Check for active mission references (flight_status IN ('PLANNED', 'IN_AIR'))
            alt Active references found
                Service-->>API: 409 Conflict (Historical active reference)
                API-->>UI: 409 Conflict
            else No active references
                Service->>DB: Begin Transaction
                Service->>DB: Delete from station_slots where id = id
                Service->>DB: Update landing_stations set total_slots = total_slots - 1, updated_at = UtcNow
                Service->>DB: Insert into audit_logs (user_id=adminUserId, action='SLOT_INVENTORY_CHANGE', entity_id=id, remarks='Removed slot')
                Service->>DB: Commit Transaction
                DB-->>Service: Success
                Service-->>API: 204 No Content
                API-->>UI: 204 No Content
                UI-->>Admin: Slot removed from station configuration
            end
        end
    end
```

---

## 6. Sequence 5: Slot Operational Override and Maintenance Isolation

This flow models a Station Operator or Administrator isolating an individual faulty slot (`ERROR`) due to pad debris, charging pin malfunction, or locker door jam, and restoring it to `EMPTY` after repairs.

```mermaid
sequenceDiagram
    autonumber
    actor Operator as Station Operator / Admin
    participant UI as Mobile Tablet / Web UI
    participant API as StationSlotsController
    participant Service as StationSlotService
    participant DB as AppDbContext / PostgreSQL
    participant Hub as SignalR (station-slots-{stationId})

    Operator->>UI: Select slot, choose "Set to Maintenance/Error" and enter reason
    UI->>API: PUT /api/station-slots/{id}/status (Bearer token, UpdateSlotStatusRequestDto)
    Note over API: Verify assigned STATION_OPERATOR or ADMIN role

    API->>Service: OverrideSlotStatusAsync(id, dto, userId)
    Service->>DB: Query station_slots by Id
    alt Slot not found
        DB-->>Service: null
        Service-->>API: 404 Not Found
        API-->>UI: 404 Not Found
    else Slot found
        DB-->>Service: StationSlot record
        
        alt TargetStatus == 'ERROR'
            alt Current status is RESERVED (Flight en route to this slot)
                Note over Service: Slot reserved by incoming flight!
                Service->>DB: Query linked FlightMission and Dispatcher
                Service->>Hub: Broadcast SlotEmergencyAlert to Dispatcher (reassign landing slot immediately)
                Service->>DB: Begin Transaction
                Service->>DB: Update station_slots set status = 'ERROR', last_updated = UtcNow
                Service->>DB: Insert into audit_logs (user_id, action='SLOT_STATUS_OVERRIDE', entity_id=id, remarks=dto.Reason)
                Service->>DB: Commit Transaction
                Service->>Hub: Broadcast slot status change to station-slots-{stationId}
                Service-->>API: StationSlotResponseDto (Alerted Dispatcher)
                API-->>UI: 200 OK
                UI-->>Operator: Slot marked ERROR. Dispatcher notified to reassign incoming flight.
            else Current status is OCCUPIED
                Service-->>API: 409 Conflict (ProblemDetails: Slot is occupied. Remove parcel or drone before setting to ERROR.)
                API-->>UI: 409 Conflict
                UI-->>Operator: Error: Cannot isolate occupied slot. Relocate asset first.
            else Current status is EMPTY
                Service->>DB: Begin Transaction
                Service->>DB: Update station_slots set status = 'ERROR', last_updated = UtcNow
                Service->>DB: Insert into audit_logs (user_id, action='SLOT_STATUS_OVERRIDE', entity_id=id, remarks=dto.Reason)
                Service->>DB: Commit Transaction
                Service->>Hub: Broadcast slot status change to station-slots-{stationId}
                Service-->>API: StationSlotResponseDto
                API-->>UI: 200 OK
                UI-->>Operator: Slot isolated in ERROR mode
            end
        else TargetStatus == 'EMPTY' (Maintenance Complete)
            alt Current status is not ERROR
                Service-->>API: 400 Bad Request (Slot is not in maintenance)
                API-->>UI: 400 Bad Request
            else Current status is ERROR
                Service->>DB: Begin Transaction
                Service->>DB: Update station_slots set status = 'EMPTY', last_updated = UtcNow
                Service->>DB: Insert into audit_logs (user_id, action='SLOT_STATUS_OVERRIDE', entity_id=id, remarks='Maintenance complete, diagnostic passed')
                Service->>DB: Commit Transaction
                Service->>Hub: Broadcast slot status change to station-slots-{stationId}
                Service-->>API: StationSlotResponseDto
                API-->>UI: 200 OK
                UI-->>Operator: Slot restored to EMPTY and ready for operations
            end
        end
    end
```

---

## 7. Persistence alignment and atomic consistency

| Concern | Design Alignment |
|---|---|
| **Atomic Transactions** | Station creation and slot provisioning execute in a single database transaction. If slot insertion fails, station creation rolls back completely. |
| **Audit Compliance** | All sensitive administrative and operational state changes write immutable records to `audit_logs` storing user ID, action code, entity identifier, and reason (SRS FR-32). |
| **Real-Time Synchronization** | Slot state changes publish immediately over SignalR topic `station-slots-{stationId}`, keeping Dispatcher scheduling views and mobile tablet grids synchronized without client-side polling. |
| **Conflict Invariants** | An occupied or reserved slot cannot be deleted (`409 Conflict`). Deactivating a station with flights en route triggers an immediate high-priority reroute alert to flight dispatchers. |
