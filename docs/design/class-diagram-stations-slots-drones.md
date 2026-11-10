# Landing Stations, Slots and Drones — Class Diagram

**Parent:** LA-198 - Design documents: Landing stations, slots and drones

**Subtask:** LA-230 - Class diagram and specification for stations, slots and drones

**Status:** Proposed for review

---

## 1. Scope and architecture

This document provides the architectural class diagram and structural design for the **Landing Stations, Slots and Drones** module of the SmartDroneDelivery ASP.NET Core 8 Web API. It models the core physical infrastructure, fleet assets, operational slots, and their application service orchestration.

In accordance with the project architecture ([architecture.md](../architecture/architecture.md)), the module is structured into distinct layers:

| Layer | Responsibility | Key Components |
|---|---|---|
| **Presentation** | Authenticated HTTP endpoints, routing, role-based authorization (`ADMIN`, `STATION_OPERATOR`, `DISPATCHER`), model binding, and HTTP status code translation | `LandingStationsController`, `StationSlotsController`, `StationOperationsController`, `DronesController` |
| **Application** | Use-case orchestration, domain validation, physical safety rules, status transitions, atomic transaction boundaries, and event notifications | `ILandingStationService` / `LandingStationService`, `IStationSlotService` / `StationSlotService`, `IStationOperationService` / `StationOperationService`, `IDroneService` / `DroneService` |
| **Domain** | Physical and aeronautical entities, core status enumerations, validation rules, and relational navigations | `LandingStation`, `StationSlot`, `Drone`, `FlightMission`, `DeliveryOrder`, `StationStatus`, `SlotStatus`, `DroneStatus`, `FlightMissionStatus`, `OrderStatus` |
| **Data Access** | Entity Framework Core persistence, PostgreSQL relational mapping, transaction execution, and concurrency control | `AppDbContext`, EF Core configuration, PostgreSQL 16 |
| **DTO Boundary** | Strongly-typed request/response data transfer contracts at the API boundary; entities are never directly exposed to HTTP clients | Station request/response DTOs, Slot request/response DTOs, Intake/Pickup DTOs, Drone DTOs |

---

## 2. Class diagram

```mermaid
classDiagram
    direction LR

    %% Presentation Layer
    class LandingStationsController {
        -ILandingStationService _stationService
        +GetAllAsync(filters, paging) Task~PagedResponse~LandingStationResponseDto~~
        +GetByIdAsync(id) Task~LandingStationDetailResponseDto~
        +CreateAsync(dto) Task~LandingStationResponseDto~
        +UpdateAsync(id, dto) Task~LandingStationResponseDto~
        +SetStatusAsync(id, dto) Task~LandingStationResponseDto~
        +GetAvailabilityAsync(id) Task~StationAvailabilityResponseDto~
    }

    class StationSlotsController {
        -IStationSlotService _slotService
        +GetByStationIdAsync(stationId) Task~IReadOnlyList~StationSlotResponseDto~~
        +GetByIdAsync(id) Task~StationSlotResponseDto~
        +AddSlotAsync(stationId, dto) Task~StationSlotResponseDto~
        +RemoveSlotAsync(id) Task
        +UpdateStatusAsync(id, dto) Task~StationSlotResponseDto~
    }

    class StationOperationsController {
        -IStationOperationService _operationService
        +IntakePackageAsync(stationId, dto) Task~PackageIntakeResponseDto~
        +ConfirmPickupAsync(stationId, dto) Task~PickupConfirmationResponseDto~
        +ValidateSecureCodeAsync(stationId, dto) Task~SecureCodeValidationResponseDto~
    }

    class DronesController {
        -IDroneService _droneService
        +GetAllAsync(filters) Task~IReadOnlyList~DroneResponseDto~~
        +GetByIdAsync(id) Task~DroneResponseDto~
        +CreateAsync(dto) Task~DroneResponseDto~
        +UpdateAsync(id, dto) Task~DroneResponseDto~
        +SetMaintenanceAsync(id, dto) Task~DroneResponseDto~
    }

    %% Application Service Interfaces
    class ILandingStationService {
        <<interface>>
        +GetAllStationsAsync(filters, paging) Task~PagedResponse~LandingStationResponseDto~~
        +GetStationByIdAsync(id) Task~LandingStationDetailResponseDto~
        +CreateStationAsync(dto, adminUserId) Task~LandingStationResponseDto~
        +UpdateStationAsync(id, dto, adminUserId) Task~LandingStationResponseDto~
        +ChangeStationStatusAsync(id, dto, userId) Task~LandingStationResponseDto~
        +GetStationAvailabilityAsync(id) Task~StationAvailabilityResponseDto~
    }

    class IStationSlotService {
        <<interface>>
        +GetSlotsByStationAsync(stationId) Task~IReadOnlyList~StationSlotResponseDto~~
        +GetSlotByIdAsync(id) Task~StationSlotResponseDto~
        +AddSlotAsync(stationId, dto, adminUserId) Task~StationSlotResponseDto~
        +RemoveSlotAsync(id, adminUserId) Task
        +OverrideSlotStatusAsync(id, dto, userId) Task~StationSlotResponseDto~
    }

    class IStationOperationService {
        <<interface>>
        +IntakeOriginPackageAsync(stationId, dto, operatorId) Task~PackageIntakeResponseDto~
        +ConfirmDestinationPickupAsync(stationId, dto, operatorId) Task~PickupConfirmationResponseDto~
        +ValidateSecureCodeAsync(stationId, dto) Task~SecureCodeValidationResponseDto~
    }

    class IDroneService {
        <<interface>>
        +GetAllDronesAsync(filters) Task~IReadOnlyList~DroneResponseDto~~
        +GetDroneByIdAsync(id) Task~DroneResponseDto~
        +CreateDroneAsync(dto, userId) Task~DroneResponseDto~
        +UpdateDroneAsync(id, dto, userId) Task~DroneResponseDto~
        +SetDroneMaintenanceAsync(id, dto, userId) Task~DroneResponseDto~
    }

    %% Domain Entities
    class LandingStation {
        +Guid Id
        +string StationCode
        +string Name
        +decimal Latitude
        +decimal Longitude
        +string Address
        +int TotalSlots
        +string Status
        +Guid? OperatorId
        +DateTime CreatedAt
        +DateTime UpdatedAt
        +User? Operator
        +ICollection~StationSlot~ Slots
        +ICollection~Drone~ StationDrones
        +ICollection~DeliveryOrder~ OriginOrders
        +ICollection~DeliveryOrder~ DestinationOrders
    }

    class StationSlot {
        +Guid Id
        +Guid StationId
        +int SlotNumber
        +string Status
        +DateTime LastUpdated
        +LandingStation Station
        +ICollection~FlightMission~ DepartureMissions
        +ICollection~FlightMission~ DestinationMissions
    }

    class Drone {
        +Guid Id
        +string SerialNumber
        +string ModelName
        +decimal MaxPayloadGram
        +decimal MaxFlightRangeKm
        +int BatteryCapacityMah
        +string Status
        +Guid? CurrentStationId
        +DateTime CreatedAt
        +LandingStation? CurrentStation
        +ICollection~FlightMission~ FlightMissions
    }

    class FlightMission {
        +Guid Id
        +Guid OrderId
        +Guid DroneId
        +Guid? StartSlotId
        +Guid? DestinationSlotId
        +string FlightStatus
        +DateTime? StartTime
        +DateTime? EndTime
        +DeliveryOrder Order
        +Drone Drone
        +StationSlot? StartSlot
        +StationSlot? DestinationSlot
    }

    class DeliveryOrder {
        +Guid Id
        +string TrackingNumber
        +Guid CustomerId
        +Guid PackageId
        +Guid OriginStationId
        +Guid DestinationStationId
        +string Status
        +string PickupSecureCode
        +DateTime? ScheduledDepartureTime
        +DateTime? ActualDeliveryTime
        +LandingStation OriginStation
        +LandingStation DestinationStation
        +FlightMission? FlightMission
    }

    class User {
        +Guid Id
        +string FullName
        +string Email
        +string PhoneNumber
        +string Status
        +Guid RoleId
    }

    %% Data Access
    class AppDbContext {
        +DbSet~LandingStation~ LandingStations
        +DbSet~StationSlot~ StationSlots
        +DbSet~Drone~ Drones
        +DbSet~FlightMission~ FlightMissions
        +DbSet~DeliveryOrder~ DeliveryOrders
        +DbSet~AuditLog~ AuditLogs
        +DbSet~DeliveryStatusLog~ DeliveryStatusLogs
        +SaveChangesAsync(cancellationToken) Task~int~
    }

    %% Relationships
    LandingStationsController ..> ILandingStationService : uses
    StationSlotsController ..> IStationSlotService : uses
    StationOperationsController ..> IStationOperationService : uses
    DronesController ..> IDroneService : uses

    ILandingStationService <|.. LandingStationService : implements
    IStationSlotService <|.. StationSlotService : implements
    IStationOperationService <|.. StationOperationService : implements
    IDroneService <|.. DroneService : implements

    LandingStationService --> AppDbContext : persists via
    StationSlotService --> AppDbContext : persists via
    StationOperationService --> AppDbContext : persists via
    DroneService --> AppDbContext : persists via

    LandingStation "1" *-- "1..*" StationSlot : contains Slots
    LandingStation "0..1" o-- "0..*" Drone : hosts StationDrones
    LandingStation "1" o-- "0..1" User : assigned Operator
    StationSlot "1" o-- "0..*" FlightMission : StartSlot / DestinationSlot
    Drone "1" o-- "0..*" FlightMission : executes
    DeliveryOrder "1" o-- "0..1" FlightMission : links
    LandingStation "1" o-- "0..*" DeliveryOrder : Origin / Destination
```

---

## 3. Structural mapping and domain rules

### 3.1 Status domain vocabularies

| Entity | Allowed statuses (`varchar(30)`) | Initial / Default | State semantics |
|---|---|---|---|
| `LandingStation` | `OPERATIONAL`, `MAINTENANCE`, `OFFLINE` | `OPERATIONAL` | `OPERATIONAL`: Available for flight departures, arrivals, and storage. <br/>`MAINTENANCE`: Hub isolated for repairs; no new missions scheduled. <br/>`OFFLINE`: Hub decommissioned or shut down. |
| `StationSlot` | `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR` | `EMPTY` | `EMPTY`: Free for landing, charging, or parcel placement. <br/>`OCCUPIED`: Contains a drone or stored parcel. <br/>`RESERVED`: Booked by an in-flight mission en route. <br/>`ERROR`: Hardware fault, debris, or maintenance isolation. |
| `Drone` | `IDLE`, `ASSIGNED`, `FLYING`, `CHARGING`, `MAINTENANCE` | `IDLE` | Aligned with ERD and `LA-233`. Only `IDLE` drones are offered for assignment. `CHARGING` drones are not offered until a trusted battery source exists (the `drones` table has no charge column; see class-specification-delivery-workflow.md, open point 3). |
| `FlightMission` | `PLANNED`, `IN_AIR`, `COMPLETED`, `ABORTED` | `PLANNED` | Aligned with ERD. Created when drone is assigned to an approved delivery order. |
| `DeliveryOrder` | `PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED` | `PENDING` | Approved delivery order transitions to `READY_FOR_TAKEOFF` upon origin intake, and `ARRIVED` to `DELIVERED` upon pickup confirmation. |

### 3.2 Relational integrity rules

1. **Station — Slot Composition:** Every `StationSlot` belongs strictly to exactly one `LandingStation`. When a station is deleted, cascade delete applies to its slots (`OnDelete(DeleteBehavior.Cascade)`), provided no active mission references exist.
2. **Unique Slot Index:** `(station_id, slot_number)` forms a unique composite index in PostgreSQL. Slot numbers are consecutive integers ($1 \le \text{slotNumber} \le \text{totalSlots}$).
3. **Station Coordinates:** `Latitude` ($-90.000000$ to $90.000000$) and `Longitude` ($-180.000000$ to $180.000000$) use `numeric(9,6)` precision for sub-meter geographic resolution.
4. **Drone Fleet Hosting:** `Drone.CurrentStationId` is a nullable foreign key (`OnDelete(DeleteBehavior.SetNull)`). A drone in flight (`status = 'FLYING'`) has `CurrentStationId = null`.
5. **Slot Reservation Consistency:** A destination slot transition to `RESERVED` occurs upon flight mission scheduling. Upon drone touchdown, it transitions to `OCCUPIED`. Upon customer pickup, it transitions to `EMPTY`.
