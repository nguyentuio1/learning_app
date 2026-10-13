# Station Operator User Flow - SmartDroneDelivery

Screen flow for the Station Operator interface (mobile tablet or station kiosk terminal). It covers landing station availability monitoring, slot status management, origin package intake, and destination package pickup confirmation with secure code verification.

The flow uses the Station Operator use cases in [use-cases.md](../../requirements/use-cases.md) (`UC-16`, `UC-17`, `UC-18`, `UC-08`), the lifecycle and failure rules in [business-processes.md](../business-processes.md), and the landing station and slot models in [erd.md](../../architecture/erd.md).

## User flow

```mermaid
flowchart TB
    Start((Start)) --> Open["Open operator tablet app"]
    Open --> Login["Log in"]
    Login --> Credentials{"Credentials valid?"}
    Credentials -->|no| Error["Show login error"]
    Error --> Login
    Credentials -->|yes| Dashboard["Station Operator dashboard<br/>current station overview and activity"]

    %% Section 1: Station Slots & Availability
    Dashboard --> SlotsView["Station slots & availability"]
    SlotsView --> SlotGrid["View slot grid<br/>EMPTY, OCCUPIED, RESERVED, ERROR"]
    SlotGrid --> SelectSlot["Select a slot"]
    SelectSlot --> SlotState{"Current slot status"}
    
    SlotState -->|EMPTY| ToggleError["Mark slot for maintenance<br/>set status ERROR"]
    SlotState -->|ERROR| ClearError["Complete maintenance<br/>set status EMPTY"]
    SlotState -->|RESERVED or OCCUPIED| SlotConflict["Slot in use or booked<br/>cannot set to ERROR directly"]
    SlotConflict --> ConflictChoice{"Action needed?"}
    ConflictChoice -->|notify dispatcher| AlertDispatcher["Raise reroute alert to Dispatcher"]
    ConflictChoice -->|back| SlotGrid
    AlertDispatcher --> SlotGrid
    ToggleError --> UpdateSlot["System updates slot in database<br/>and broadcasts via SignalR"]
    ClearError --> UpdateSlot
    UpdateSlot --> SlotGrid
    SlotGrid --> Dashboard

    %% Section 2: Origin Package Intake (UC-18)
    Dashboard --> Intake["Origin package intake"]
    Intake --> ScanPackage["Scan tracking number or QR code"]
    ScanPackage --> LookupOrder{"Order found and status APPROVED?"}
    LookupOrder -->|no| OrderNotFound["Show error: order not approved<br/>or invalid tracking number"]
    OrderNotFound --> Intake
    LookupOrder -->|yes| Inspect["Physical inspection & station scale<br/>weigh and measure package"]
    Inspect --> LimitsValid{"Weight & dimensions within drone limits<br/>and non-hazardous?"}
    LimitsValid -->|no| RejectIntake["Reject handover with reason<br/>system logs intake failure"]
    RejectIntake --> Intake
    LimitsValid -->|yes| AssignSlot["Select assigned origin slot<br/>status EMPTY"]
    AssignSlot --> LoadDrone["Load package into drone bay / slot"]
    LoadDrone --> ConfirmPickup["Confirm package pickup in app"]
    ConfirmPickup --> SaveIntake["System updates order: READY_FOR_TAKEOFF<br/>slot: OCCUPIED, drone: ASSIGNED<br/>notifies customer and flight dispatcher"]
    SaveIntake --> Dashboard

    %% Section 3: Destination Package Pickup Confirmation (UC-08, UC-18)
    Dashboard --> DeliveryPickup["Destination package pickup"]
    DeliveryPickup --> EnterCode["Enter recipient secure code<br/>or scan pickup QR"]
    EnterCode --> CheckOrder{"Order at station<br/>with status ARRIVED?"}
    CheckOrder -->|no| ArrivalErr["Show error: order not arrived<br/>or already completed"]
    ArrivalErr --> DeliveryPickup
    CheckOrder -->|yes| CheckLockout{"Verification locked?<br/>too many failed attempts"}
    CheckLockout -->|yes| LockoutMsg["Show error: locked for 10 minutes<br/>due to 3 failed attempts"]
    LockoutMsg --> DeliveryPickup
    CheckLockout -->|no| VerifyCode{"Entered code matches<br/>order.secure_code?"}
    
    VerifyCode -->|no| IncFailed["Increment failed attempt counter"]
    IncFailed --> MaxAttempts{"Failed attempts >= 3?"}
    MaxAttempts -->|yes| LockCode["Lock verification for 10 minutes<br/>send security alert to customer"]
    MaxAttempts -->|no| RetryPrompt["Show error: invalid secure code<br/>display remaining attempts"]
    LockCode --> DeliveryPickup
    RetryPrompt --> EnterCode

    VerifyCode -->|yes| UnlockLocker["System sends unlock signal<br/>or operator opens slot compartment"]
    UnlockLocker --> Handover["Hand package to recipient<br/>recipient verifies parcel"]
    Handover --> ConfirmDelivery["Confirm pickup handover in app"]
    ConfirmDelivery --> FinalizeDelivery["System updates order: DELIVERED<br/>records actual delivery time<br/>releases slot: EMPTY<br/>drone: IDLE or CHARGING<br/>notifies customer"]
    FinalizeDelivery --> Dashboard

    %% Logout
    Dashboard --> Logout["Log out"]
    Logout --> End((End))
```

## Main screens and decisions

| Screen or decision | Purpose | Related use case |
|---|---|---|
| Station Operator dashboard | Central hub displaying landing station status, slot occupancy and incoming/outgoing tasks | UC-16, UC-17 |
| Station slots & availability | Grid view displaying all slots with status `EMPTY`, `OCCUPIED`, `RESERVED` or `ERROR` | UC-16, UC-17 |
| Slot maintenance toggle | Switch slot between `EMPTY` and `ERROR` for cleaning, sensor testing or mechanical repair | UC-16 |
| Origin package intake | Search or scan an approved delivery order when a customer drops off a parcel | UC-18 |
| Physical inspection & limits | Verify physical package weight and dimensions against drone limits and hazardous item rules | UC-18 |
| Confirm package pickup | Confirm parcel custody at origin, moving order to `READY_FOR_TAKEOFF` and slot to `OCCUPIED` | UC-18 |
| Destination package pickup | Search parcel and initiate delivery handover when recipient arrives at destination station | UC-08, UC-18 |
| Secure code verification | Validate 6-digit secure code against `order.secure_code` with brute-force lockout defense | UC-08 |
| Confirm delivery handover | Release package to recipient, moving order to `DELIVERED` and freeing slot to `EMPTY` | UC-08, UC-18 |

## Flow rules

- The Station Operator operates the assigned landing station and manages physical package handling at the pads.
- Slot status transitions:
  - An `EMPTY` slot can be put into `ERROR` for maintenance and returned to `EMPTY` once operational.
  - Slots with status `RESERVED` or `OCCUPIED` cannot be placed into maintenance without rerouting the scheduled drone or retrieving the stored parcel.
- Origin intake rules:
  - Only orders in `APPROVED` status can be received at the origin station.
  - Packages exceeding drone weight or size limits, or containing prohibited/hazardous goods, must be rejected with the reason logged.
  - Confirming intake sets order status to `READY_FOR_TAKEOFF`, slot to `OCCUPIED`, drone to `ASSIGNED`, and notifies both the customer and dispatcher.
- Destination pickup rules:
  - Only orders in `ARRIVED` status at the operator's station can be completed.
  - Entering an incorrect secure code increments a failure counter; reaching 3 consecutive failures locks code entry for 10 minutes (`423 Locked`) to protect against unauthorized collection.
  - Successful pickup sets order status to `DELIVERED`, records the timestamp, frees the destination slot to `EMPTY`, and sets the drone to `IDLE` or `CHARGING`.
