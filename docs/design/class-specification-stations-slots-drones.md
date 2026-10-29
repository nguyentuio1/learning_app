# Landing Stations, Slots and Drones — Class Specification

**Parent:** LA-198 - Design documents: Landing stations, slots and drones

**Subtask:** LA-230 - Class diagram and specification for stations, slots and drones

**Status:** Proposed for review

---

## 1. Scope and mapping conventions

This specification defines the proposed ASP.NET Core 8 Web API class, entity, service, and DTO contracts for **Landing Stations, Slots, Drones, and Station Operations** (SRS FR-15, FR-16, FR-19, FR-20 to FR-23, FR-26 to FR-27, and use cases UC-08, UC-12, UC-15, UC-16, UC-17, UC-18).

The entity attributes and relational mappings below strictly adhere to the project's PostgreSQL ERD (`erd.md`, `erd.dbml`) and existing EF Core entity definitions in `backend/SmartDroneDelivery.Api/Entities/`.

| C# / CLR Type | PostgreSQL Storage Type | Remarks |
|---|---|---|
| `Guid` | `uuid` | Primary and foreign keys use UUIDv4 format. |
| `decimal` | `numeric(9,6)` / `numeric(8,2)` / `numeric(6,2)` | Coordinates use `numeric(9,6)`. Payload weights use `numeric(8,2)`. Distances use `numeric(6,2)`. |
| `int` | `integer` | Counts, slot numbers, and battery capacities (`mAh`). |
| `DateTime` | `timestamp with time zone` (`timestamptz`) | UTC timestamps (`DateTime.UtcNow`). Model defaults map to PostgreSQL `now()`. |
| `string` | `varchar(n)` / `text` | Statuses, codes, addresses, and model names. Specific length constraints are detailed per property. |
| `bool` | `boolean` | Flags (e.g. `is_active`, `is_fragile`). |

`PK` designates Primary Key; `FK` designates Foreign Key. C# properties follow PascalCase; PostgreSQL storage columns follow snake_case.

---

## 2. Entity specification

### 2.1 LandingStation (`landing_stations`)

| Property | CLR / DB Type | Key / Nullability | Rule / Constraint / Relationship |
|---|---|---|---|
| `Id` | `Guid` / `uuid` | PK, NOT NULL | Automatically generated UUIDv4. |
| `StationCode` | `string` / `varchar(50)` | NOT NULL, UNIQUE | Unique identifier (e.g. `STA-SGN-01`), 3 to 50 characters, uppercase alphanumeric with hyphens. |
| `Name` | `string` / `varchar(150)` | NOT NULL | Human-readable station name (max 150 characters). |
| `Latitude` | `decimal` / `numeric(9,6)` | NOT NULL | WGS 84 geographic latitude; range $-90.000000 \le \text{lat} \le 90.000000$. |
| `Longitude` | `decimal` / `numeric(9,6)` | NOT NULL | WGS 84 geographic longitude; range $-180.000000 \le \text{lon} \le 180.000000$. |
| `Address` | `string` / `text` | NOT NULL | Detailed physical street address where the hub is situated. |
| `TotalSlots` | `int` / `integer` | NOT NULL | Configured capacity of physical pads/lockers; defaults to 2; must be $\ge 1$. |
| `Status` | `string` / `varchar(30)` | NOT NULL | Operational status; defaults to `OPERATIONAL`. Allowed: `OPERATIONAL`, `MAINTENANCE`, `OFFLINE`. |
| `OperatorId` | `Guid?` / `uuid` | FK $\rightarrow$ `User.Id`, NULL | Nullable assigned Station Operator (`OnDelete(DeleteBehavior.SetNull)`). |
| `CreatedAt` | `DateTime` / `timestamptz` | NOT NULL | UTC creation timestamp; default `now()`. |
| `UpdatedAt` | `DateTime` / `timestamptz` | NOT NULL | UTC update timestamp; default `now()`. |
| `Operator` | Navigation | — | Nullable navigation to assigned `User` (Role `STATION_OPERATOR`). |
| `Slots` | Navigation | — | One-to-many collection navigation to `StationSlot`. |
| `StationDrones` | Navigation | — | One-to-many collection navigation to `Drone` hosted currently at this hub. |
| `OriginOrders` | Navigation | — | One-to-many collection navigation to `DeliveryOrder` departing from here. |
| `DestinationOrders` | Navigation | — | One-to-many collection navigation to `DeliveryOrder` terminating here. |

### 2.2 StationSlot (`station_slots`)

| Property | CLR / DB Type | Key / Nullability | Rule / Constraint / Relationship |
|---|---|---|---|
| `Id` | `Guid` / `uuid` | PK, NOT NULL | Automatically generated UUIDv4. |
| `StationId` | `Guid` / `uuid` | FK $\rightarrow$ `LandingStation.Id`, NOT NULL | Parent landing station identifier (`OnDelete(DeleteBehavior.Cascade)`). |
| `SlotNumber` | `int` / `integer` | NOT NULL | Consecutive slot index ($1, 2, \dots, N$); `(StationId, SlotNumber)` is UNIQUE. |
| `Status` | `string` / `varchar(30)` | NOT NULL | Current slot state; defaults to `EMPTY`. Allowed: `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`. |
| `LastUpdated` | `DateTime` / `timestamptz` | NOT NULL | UTC timestamp of last status transition; default `now()`. |
| `Station` | Navigation | — | Required parent `LandingStation` navigation. |
| `DepartureMissions` | Navigation | — | Flight missions departing from this slot (`FlightMission.StartSlotId`). |
| `DestinationMissions` | Navigation | — | Flight missions landing into this slot (`FlightMission.DestinationSlotId`). |

### 2.3 Drone (`drones`)

| Property | CLR / DB Type | Key / Nullability | Rule / Constraint / Relationship |
|---|---|---|---|
| `Id` | `Guid` / `uuid` | PK, NOT NULL | Automatically generated UUIDv4. |
| `SerialNumber` | `string` / `varchar(100)` | NOT NULL, UNIQUE | Manufacturer serial identifier (e.g. `DRN-VTOL-2026-004`). |
| `ModelName` | `string` / `varchar(100)` | NOT NULL | Model / airframe designation (e.g. `AeroDrone Express V2`). |
| `MaxPayloadGram` | `decimal` / `numeric(8,2)` | NOT NULL | Maximum carrying capacity in grams; standard baseline $\le 3000.00\text{ g}$ ($3.0\text{ kg}$). |
| `MaxFlightRangeKm` | `decimal` / `numeric(6,2)` | NOT NULL | Maximum operational flight range in kilometers (e.g. $25.00\text{ km}$). |
| `BatteryCapacityMah` | `int` / `integer` | NOT NULL | Total battery design capacity in milliampere-hours (e.g. $22000\text{ mAh}$). |
| `Status` | `string` / `varchar(30)` | NOT NULL | Fleet status; defaults to `IDLE`. Allowed: `IDLE`, `ASSIGNED`, `FLYING`, `CHARGING`, `MAINTENANCE`. |
| `CurrentStationId` | `Guid?` / `uuid` | FK $\rightarrow$ `LandingStation.Id`, NULL | Current physical location; null when airborne (`FLYING`). |
| `CreatedAt` | `DateTime` / `timestamptz` | NOT NULL | UTC record creation timestamp; default `now()`. |
| `CurrentStation` | Navigation | — | Nullable navigation to current host `LandingStation`. |
| `FlightMissions` | Navigation | — | One-to-many collection navigation to executed `FlightMission` records. |

### 2.4 FlightMission (`flight_missions`)

| Property | CLR / DB Type | Key / Nullability | Rule / Constraint / Relationship |
|---|---|---|---|
| `Id` | `Guid` / `uuid` | PK, NOT NULL | Automatically generated UUIDv4. |
| `OrderId` | `Guid` / `uuid` | FK $\rightarrow$ `DeliveryOrder.Id`, NOT NULL | Delivery order being transported; unique per active mission. |
| `DroneId` | `Guid` / `uuid` | FK $\rightarrow$ `Drone.Id`, NOT NULL | Assigned drone asset. |
| `StartSlotId` | `Guid?` / `uuid` | FK $\rightarrow$ `StationSlot.Id`, NULL | Physical takeoff slot at origin landing station. |
| `DestinationSlotId`| `Guid?` / `uuid` | FK $\rightarrow$ `StationSlot.Id`, NULL | Reserved landing slot at destination landing station. |
| `FlightStatus` | `string` / `varchar(30)` | NOT NULL | Mission lifecycle state; default `PLANNED`. Allowed: `PLANNED`, `IN_AIR`, `COMPLETED`, `ABORTED`. |
| `StartTime` | `DateTime?` / `timestamptz` | NULL | Actual flight departure time (`DateTime.UtcNow`). |
| `EndTime` | `DateTime?` / `timestamptz` | NULL | Actual flight touchdown time (`DateTime.UtcNow`). |
| `Order`, `Drone` | Navigation | — | Required navigations to order and drone. |
| `StartSlot`, `DestinationSlot` | Navigation | — | Nullable navigations to departure and destination slots. |
| `TelemetryLogs` | Navigation | — | One-to-many telemetry streams (`flight_telemetry_logs`). |

---

## 3. Presentation layer: Controller specification

### 3.1 LandingStationsController (`/api/landing-stations`)

| Method / Endpoint | HTTP / Route | Auth / Role | Description & Status Codes |
|---|---|---|---|
| `GetAllAsync` | `GET /api/landing-stations` | `ADMIN`, `DISPATCHER`, `STATION_OPERATOR` | Lists hubs with optional status/search filters and pagination. <br/>`200 OK`: Paged list of station summaries. <br/>`401 Unauthorized`, `403 Forbidden`. |
| `GetByIdAsync` | `GET /api/landing-stations/{id}` | `ADMIN`, `DISPATCHER`, `STATION_OPERATOR` | Retrieves full station details including current slot breakdown. <br/>`200 OK`: Station detail DTO. <br/>`404 Not Found`. |
| `CreateAsync` | `POST /api/landing-stations` | `ADMIN` | Provisions a new hub and auto-generates `totalSlots` empty slots. <br/>`201 Created`: Station response DTO with location header. <br/>`400 Bad Request`: Validation failure. <br/>`409 Conflict`: Duplicate `stationCode`. |
| `UpdateAsync` | `PUT /api/landing-stations/{id}` | `ADMIN` | Modifies name, address, coordinates, and operator assignment. <br/>`200 OK`: Updated station DTO. <br/>`400 Bad Request`, `404 Not Found`. |
| `SetStatusAsync` | `PUT /api/landing-stations/{id}/status` | `ADMIN`, `STATION_OPERATOR` | Changes operational mode (`OPERATIONAL`, `MAINTENANCE`, `OFFLINE`). Triggers reroute alerts if flights en route. <br/>`200 OK`: Updated station DTO. <br/>`400 Bad Request`: Missing mandatory reason. |
| `GetAvailabilityAsync` | `GET /api/landing-stations/{id}/availability` | All authenticated roles | Real-time pad/locker capacity query for scheduling. <br/>`200 OK`: Availability metrics (`totalSlots`, `availableSlots`, `occupiedSlots`, `reservedSlots`). |

### 3.2 StationSlotsController (`/api/landing-stations/{stationId}/slots` & `/api/station-slots/{id}`)

| Method / Endpoint | HTTP / Route | Auth / Role | Description & Status Codes |
|---|---|---|---|
| `GetByStationIdAsync` | `GET /api/landing-stations/{stationId}/slots` | `ADMIN`, `DISPATCHER`, `STATION_OPERATOR` | Returns all slots for a specific landing station. <br/>`200 OK`: Collection of slot DTOs. <br/>`404 Not Found`. |
| `GetByIdAsync` | `GET /api/station-slots/{id}` | `ADMIN`, `DISPATCHER`, `STATION_OPERATOR` | Retrieves detailed slot state and active mission linkage. <br/>`200 OK`: Slot DTO. <br/>`404 Not Found`. |
| `AddSlotAsync` | `POST /api/landing-stations/{stationId}/slots` | `ADMIN` | Adds an additional slot to the station, incrementing `totalSlots`. <br/>`201 Created`: Slot DTO. <br/>`400 Bad Request`, `409 Conflict`. |
| `RemoveSlotAsync` | `DELETE /api/station-slots/{id}` | `ADMIN` | Decommissions an empty/error slot and decrements `totalSlots`. <br/>`204 No Content`. <br/>`409 Conflict`: Slot is currently `OCCUPIED` or `RESERVED`. |
| `UpdateStatusAsync` | `PUT /api/station-slots/{id}/status` | `ADMIN`, `STATION_OPERATOR` | Overrides slot state to `ERROR` (maintenance) or `EMPTY` (restored). <br/>`200 OK`: Updated slot DTO. <br/>`409 Conflict`: Slot is occupied or reserved. |

### 3.3 StationOperationsController (`/api/landing-stations/{stationId}/operations`)

| Method / Endpoint | HTTP / Route | Auth / Role | Description & Status Codes |
|---|---|---|---|
| `IntakePackageAsync` | `POST /api/landing-stations/{stationId}/operations/intake` | `STATION_OPERATOR`, `ADMIN` | Confirms package receipt at origin station (UC-18). Validates 3-point check, sets slot `OCCUPIED`, order `READY_FOR_TAKEOFF`. <br/>`200 OK`: Intake confirmation DTO. <br/>`400 Bad Request`: Payload/dimension limit exceeded. <br/>`404 Not Found`, `409 Conflict`: Order not in `APPROVED` state. |
| `ConfirmPickupAsync` | `POST /api/landing-stations/{stationId}/operations/pickup` | `STATION_OPERATOR`, `ADMIN`, `CUSTOMER` | Validates 6-digit `pickup_secure_code` and completes delivery handover (UC-08). Sets slot `EMPTY`, order `DELIVERED`, records `actual_delivery_time`. <br/>`200 OK`: Pickup confirmation DTO. <br/>`400 Bad Request`: Incorrect secure code. <br/>`429 Too Many Requests`: 3 failed attempts lockout (10 mins). |
| `ValidateSecureCodeAsync` | `POST /api/landing-stations/{stationId}/operations/validate-code` | `STATION_OPERATOR`, `CUSTOMER` | Advisory validation of 6-digit code before opening physical locker compartment. <br/>`200 OK`: Validation status (`isValid`, `attemptsRemaining`, `isLocked`). |

---

## 4. Application layer: Service interfaces

```csharp
namespace SmartDroneDelivery.Api.Services;

public interface ILandingStationService
{
    Task<PagedResponse<LandingStationResponseDto>> GetAllStationsAsync(
        StationFilterDto filters, PagingDto paging, CancellationToken ct = default);

    Task<LandingStationDetailResponseDto> GetStationByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<LandingStationResponseDto> CreateStationAsync(
        CreateLandingStationRequestDto dto, Guid adminUserId, CancellationToken ct = default);

    Task<LandingStationResponseDto> UpdateStationAsync(
        Guid id, UpdateLandingStationRequestDto dto, Guid adminUserId, CancellationToken ct = default);

    Task<LandingStationResponseDto> ChangeStationStatusAsync(
        Guid id, StationStatusChangeRequestDto dto, Guid userId, CancellationToken ct = default);

    Task<StationAvailabilityResponseDto> GetStationAvailabilityAsync(
        Guid id, CancellationToken ct = default);
}

public interface IStationSlotService
{
    Task<IReadOnlyList<StationSlotResponseDto>> GetSlotsByStationAsync(
        Guid stationId, CancellationToken ct = default);

    Task<StationSlotResponseDto> GetSlotByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<StationSlotResponseDto> AddSlotAsync(
        Guid stationId, AddSlotRequestDto dto, Guid adminUserId, CancellationToken ct = default);

    Task RemoveSlotAsync(
        Guid id, Guid adminUserId, CancellationToken ct = default);

    Task<StationSlotResponseDto> OverrideSlotStatusAsync(
        Guid id, UpdateSlotStatusRequestDto dto, Guid userId, CancellationToken ct = default);
}

public interface IStationOperationService
{
    Task<PackageIntakeResponseDto> IntakeOriginPackageAsync(
        Guid stationId, PackageIntakeRequestDto dto, Guid operatorId, CancellationToken ct = default);

    Task<PickupConfirmationResponseDto> ConfirmDestinationPickupAsync(
        Guid stationId, ConfirmPickupRequestDto dto, Guid operatorId, CancellationToken ct = default);

    Task<SecureCodeValidationResponseDto> ValidateSecureCodeAsync(
        Guid stationId, ValidateSecureCodeRequestDto dto, CancellationToken ct = default);
}

public interface IDroneService
{
    Task<IReadOnlyList<DroneResponseDto>> GetAllDronesAsync(
        DroneFilterDto filters, CancellationToken ct = default);

    Task<DroneResponseDto> GetDroneByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<DroneResponseDto> CreateDroneAsync(
        CreateDroneRequestDto dto, Guid userId, CancellationToken ct = default);

    Task<DroneResponseDto> UpdateDroneAsync(
        Guid id, UpdateDroneRequestDto dto, Guid userId, CancellationToken ct = default);

    Task<DroneResponseDto> SetDroneMaintenanceAsync(
        Guid id, DroneMaintenanceRequestDto dto, Guid userId, CancellationToken ct = default);
}
```

---

## 5. Data Transfer Objects (DTO) specification

### 5.1 Request DTOs

| DTO Name | Properties | Types & Validation Rules |
|---|---|---|
| `CreateLandingStationRequestDto` | `StationCode`, `Name`, `Latitude`, `Longitude`, `Address`, `TotalSlots`, `OperatorId` | `StationCode`: Required, 3–50 chars, regex `^[A-Z0-9\-]+$`. <br/>`Name`: Required, max 150 chars. <br/>`Latitude`: $-90 \le \text{lat} \le 90$. <br/>`Longitude`: $-180 \le \text{lon} \le 180$. <br/>`Address`: Required, non-empty. <br/>`TotalSlots`: Required, $1 \le N \le 20$. <br/>`OperatorId`: Optional UUID referencing a user with `STATION_OPERATOR` role. |
| `UpdateLandingStationRequestDto` | `Name`, `Latitude`, `Longitude`, `Address`, `OperatorId` | `Name`: Required, max 150 chars. <br/>`Latitude`, `Longitude`: Valid WGS 84. <br/>`Address`: Required. <br/>`OperatorId`: Optional UUID. |
| `StationStatusChangeRequestDto` | `NewStatus`, `Reason` | `NewStatus`: Required; one of `OPERATIONAL`, `MAINTENANCE`, `OFFLINE`. <br/>`Reason`: Required, 5 to 500 characters explaining the operational reason. |
| `AddSlotRequestDto` | `SlotNumber` | Optional int; if omitted, automatically assigned to $\text{max}(\text{SlotNumber}) + 1$. |
| `UpdateSlotStatusRequestDto` | `TargetStatus`, `Reason` | `TargetStatus`: Required; one of `EMPTY`, `ERROR`. <br/>`Reason`: Required, max 500 characters. |
| `PackageIntakeRequestDto` | `TrackingNumber`, `SlotId`, `MeasuredWeightGram`, `MeasuredLengthCm`, `MeasuredWidthCm`, `MeasuredHeightCm`, `VisualInspectionPassed` | `TrackingNumber`: Required, max 50 chars. <br/>`SlotId`: Required UUID referencing an `EMPTY` slot at origin station. <br/>`MeasuredWeightGram`: $\le 3000.00\text{ g}$. <br/>`Dimensions`: $\le 30 \times 20 \times 15\text{ cm}$. <br/>`VisualInspectionPassed`: Required boolean `true`. |
| `ConfirmPickupRequestDto` | `TrackingNumber`, `PickupSecureCode`, `SlotId` | `TrackingNumber`: Required tracking number. <br/>`PickupSecureCode`: Required 6-digit numeric string regex `^\d{6}$`. <br/>`SlotId`: Required destination slot UUID. |
| `ValidateSecureCodeRequestDto` | `TrackingNumber`, `PickupSecureCode` | `TrackingNumber`: Required. <br/>`PickupSecureCode`: Required 6-digit numeric OTP. |

### 5.2 Response DTOs

| DTO Name | Properties | Types & Semantics |
|---|---|---|
| `LandingStationResponseDto` | `Id`, `StationCode`, `Name`, `Latitude`, `Longitude`, `Address`, `TotalSlots`, `Status`, `OperatorId`, `OperatorName`, `CreatedAt`, `UpdatedAt` | Summary record of landing station with assigned operator name and operational status. |
| `LandingStationDetailResponseDto` | Inherits `LandingStationResponseDto`, plus `Slots` (`IReadOnlyList<StationSlotResponseDto>`), `AvailableSlotCount`, `HostedDroneCount` | Detailed station profile including all individual slot statuses and count of available landing pads. |
| `StationAvailabilityResponseDto` | `StationId`, `TotalSlots`, `EmptySlots`, `OccupiedSlots`, `ReservedSlots`, `ErrorSlots`, `IsAvailableForArrival`, `IsAvailableForDeparture` | Real-time pad capacity metrics consumed by Dispatchers and AI flight scheduler. |
| `StationSlotResponseDto` | `Id`, `StationId`, `SlotNumber`, `Status`, `LastUpdated`, `CurrentMissionId`, `CurrentOrderId` | Single slot representation showing current hardware status (`EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`). |
| `PackageIntakeResponseDto` | `OrderId`, `TrackingNumber`, `Status`, `SlotId`, `SlotNumber`, `AssignedDroneId`, `ConfirmedAt`, `Message` | Returned upon successful origin parcel handover; order status is `READY_FOR_TAKEOFF`. |
| `PickupConfirmationResponseDto` | `OrderId`, `TrackingNumber`, `Status`, `ActualDeliveryTime`, `SlotId`, `SlotReleased`, `Message` | Returned upon destination pickup; order status is `DELIVERED`; slot status is `EMPTY`. |
| `SecureCodeValidationResponseDto` | `IsValid`, `AttemptsRemaining`, `IsLocked`, `LockoutExpiresAt`, `Message` | Secure code validation status with brute-force defense indicators. |
| `DroneResponseDto` | `Id`, `SerialNumber`, `ModelName`, `MaxPayloadGram`, `MaxFlightRangeKm`, `BatteryCapacityMah`, `Status`, `CurrentStationId`, `CurrentStationName`, `CreatedAt` | Fleet unit record aligned with `LA-233`. |
