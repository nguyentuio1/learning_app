# SmartDroneDelivery — Landing Station Module Function Descriptions

**Task:** LA-218 — Functions of Landing Station module  
**Parent task:** LA-195 — Function descriptions for system modules  
**Scope owner:** Landing stations: station inventory & lifecycle, slot state management, real-time availability monitoring, origin package intake & pickup confirmation, and destination package handover with secure code verification  
**Status:** Proposed for review against SRS, Use Cases, ERD, and Station Operator Activity/User Flow  
**Primary actors:** Station Operator (`STATION_OPERATOR`), System Administrator (`ADMIN`), Customer / Recipient (`CUSTOMER`), Dispatcher (`DISPATCHER`), System Core / Telemetry Hub  

---

## 1. Objective and scope

This document defines the functional requirements and detailed function descriptions for the **Landing Station module** of SmartDroneDelivery. It covers the complete operational management of physical infrastructure (landing stations and landing/locker slots), real-time availability calculation, origin package intake with 3-point physical safety inspection, and destination package handover with secure code verification.

Landing stations in the SmartDroneDelivery platform serve dual functions:
1. **Aeronautical landing & recharging pads:** Fixed landing slots providing precision landing targets, charging interfaces, and wind/weather telemetry sensors.
2. **Automated locker compartments / Service counters:** Ground-level storage where customers drop off parcels and recipients retrieve arriving deliveries.

The primary functional requirements in scope are **FR-20 to FR-23** (Landing station management) and **FR-26 to FR-27** (Delivery confirmation with secure code), supported by **FR-14** (State machine logging) and **FR-32** (Audit logging for sensitive station/slot modifications). Flight scheduling and drone corridor tracking (FR-15 to FR-17, FR-24 to FR-25) are managed by the Delivery Workflow module (LA-217) and interface with this module via slot reservations.

### 1.1 Source basis

| Source | Use in this document |
|---|---|
| [SRS](SRS.md), Sections 3.4, 3.5, 3.7 | Requirements FR-20 to FR-23, FR-26 to FR-27, FR-14, FR-32; priority and actor allocation |
| [Use cases](use-cases.md) | UC-16, UC-17, UC-18, UC-08; preconditions, main flows, and alternative exception flows |
| [Station & Pickup Activity Diagram](../design/activity-station-and-pickup.md) | Workflows for station/slot maintenance, origin intake (UC-18), and secure code pickup (UC-08) |
| [Station Operator user flow](../design/user-flows/station-operator.md) | Screen flow: Dashboard $\rightarrow$ Slot grid $\rightarrow$ Maintenance toggle $\rightarrow$ Intake $\rightarrow$ Destination pickup |
| [Business process models](../design/business-processes.md) | Order status transitions: `APPROVED` $\rightarrow$ `READY_FOR_TAKEOFF`, `ARRIVED` $\rightarrow$ `DELIVERED`, timeout to `FAILED` |
| [Mobile wireframes (Station Operator)](../design/wireframes/app/index.md) | Screens `O01`–`O16` (Home `O02`, Tasks `O03`/`O04`, Scan `O05`, Pickup `O07`/`O08`, Arrival `O09`/`O10`, Slots `O11`/`O12`, Maintenance `O13`) |
| [Architecture](../architecture/architecture.md), [ERD](../architecture/erd.md), [database design](../architecture/erd.dbml) | Entities: `landing_stations`, `station_slots`, `delivery_orders`, `drones`, `delivery_status_logs`, `audit_logs` |

---

## 2. Actors and conventions

| Actor / Component | Role code / Type | Operational responsibility in Landing Station module |
|---|---|---|
| **Station Operator** | `STATION_OPERATOR` | Operates assigned hub via mobile tablet or kiosk; scans parcels, conducts physical weight/dimension checks, loads drone bays, toggles slot maintenance, verifies recipient secure codes |
| **System Administrator** | `ADMIN` | Creates new stations, configures slot counts/coordinates, assigns staff operators, activates station-wide maintenance or offline modes |
| **Dispatcher** | `DISPATCHER` | Monitors station availability for flight scheduling; receives urgent reroute alerts if a station or reserved slot is placed into maintenance while flights are en route |
| **Customer (Sender)** | `CUSTOMER` | Brings package to origin landing station; presents tracking QR/barcode for operator scan |
| **Recipient** | Human Actor | Receives arrival notification; visits destination station counter/kiosk; inputs pickup secure code (`delivery_orders.pickup_secure_code` — mã 6 chữ số / 6 ký tự chữ-số, xem Mục 6) to unlock compartment and retrieve package |
| **System Core / Kiosk** | System Component | Enforces slot state consistency, validates code attempts with brute-force lockout, streams real-time updates over SignalR, monitors 24h pickup timeout |

### Conventions
- **Identifiers:** Primary keys are UUIDs (`landing_stations.id`, `station_slots.id`, `delivery_orders.id`), except integer `station_slots.slot_number` (1..N per station).
- **Coordinates:** WGS 84 latitude/longitude as decimal degrees with 6 decimal places (approx. 0.1 meter ground resolution).
- **Physical Limits:** Standardized drone cargo bay maximum weight $\le 3.0\text{ kg}$ (theo SRS 2.4 / Activity diagram; đang có đề xuất 5.0 kg từ Delivery Workflow cần cả nhóm chốt trong SRS, xem Mục 6), maximum volume dimensions $30 \times 20 \times 15\text{ cm}$.
- **Database Schema Alignment:** Toàn bộ tên thực thể và thuộc tính tuân thủ ERD (`erd.md`, `erd.dbml`). Các trường dữ liệu đo đạc runtime hoặc đề xuất mở rộng schema được chú thích rõ `[Cột đề xuất / Proposed schema extension]`.
- **Business Rule Identifiers:** Numbered `BR-LS-01` to `BR-LS-22` within this document (prefixed `LS` for Landing Station) to prevent cross-document collisions.
- **Rule Categorization:**
  - **Baseline:** Derived directly from SRS, ERD, and approved activity diagrams.
  - **Derived:** Engineering rules required for sensor consistency, deadlock prevention, and security.
  - **Proposed:** Operational policies requiring final product management sign-off (Section 6).

---

## 3. Requirements traceability matrix

| SRS Requirement | Priority | Use Case | Matching Function Descriptions |
|---|---|---|---|
| **FR-20** Create, update, deactivate and list landing stations with location and operating status | M | UC-16 | **F-01**, **F-02** |
| **FR-21** Manage the slots of each station and their status | M | UC-16 | **F-03**, **F-04** |
| **FR-22** Show the real-time availability of stations and slots | M | UC-17 | **F-05** |
| **FR-23** Confirm that a package was received at the station (origin intake) | M | UC-18 | **F-06** |
| **FR-26** Confirm delivery with secure code; wrong code is rejected | M | UC-08 | **F-07** |
| **FR-27** Record actual delivery time when order is completed | M | UC-08 | **F-07** |
| **FR-14** Log every valid status transition with acting user and time | M | UC-18, UC-08 | **F-06**, **F-07**, **F-08** |
| **FR-32** Record audit log entry for sensitive actions (station mode, slot override, handover) | M | UC-22 | **F-02**, **F-04**, **F-06**, **F-07** |

---

## 4. Landing Station function descriptions

### F-01 List and inspect landing stations

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-20; UC-16 |
| **Trigger** | Administrator, Dispatcher, or Station Operator opens the Stations list on Web or Mobile app. |
| **Actors / Purpose** | Administrator, Dispatcher, Station Operator; inspect network hub locations, operational modes, slot capacities, and assigned personnel. |
| **Preconditions** | Caller has authenticated session with valid staff role (`ADMIN`, `DISPATCHER`, `STATION_OPERATOR`). |
| **Input** | Optional filters: `status` (`OPERATIONAL`, `MAINTENANCE`, `OFFLINE`), search keyword (matches `name`, `station_code`, or `address`), pagination (`page`, `pageSize`, default 20). |
| **Validation** | Verify caller role. Station Operators are scoped to view all stations in read-only mode, with write access limited to their assigned station. |
| **Processing** | Query `landing_stations` joined with `users` (assigned operator name) and count slots by status (`EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`). Calculate real-time available slot capacity for each hub. |
| **Saving** | Read-only; no database write. |
| **Output / Exceptions** | Paginated station summaries with station ID, code, name, latitude/longitude, total slots, count of available slots, current status, and operator name. Return `401 Unauthorized` if unauthenticated; `403 Forbidden` if caller is a Customer. |
| **Business Rules** | BR-LS-01, BR-LS-02. |
| **Screen** | Web Admin: Stations catalog; Mobile Operator: `O16 · Profile & Station` ([O16-Profile.png](../design/wireframes/app/station-operator/O16-Profile.png)). |

---

### F-02 Create, update, and manage landing station operational mode

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-20, FR-32; UC-16 |
| **Trigger** | Administrator creates a hub, or Operator/Administrator toggles station mode to `MAINTENANCE` or `OFFLINE`. |
| **Actors / Purpose** | Administrator, Station Operator; establish station infrastructure or isolate an entire hub for physical maintenance/emergency. |
| **Preconditions** | Administrator authorization required for creation/deactivation; assigned Operator or Administrator for maintenance toggles. |
| **Input** | Create/Update: `stationCode` (unique, 3–10 chars), `name` (max 100 chars), `address`, `latitude`, `longitude`, `totalSlots` (int $\ge 1$), `operatorId` (optional UUID). <br/>Mode toggle: `stationId`, `newStatus` (`OPERATIONAL`, `MAINTENANCE`, `OFFLINE`), `reason` (required, max 500 chars). |
| **Validation** | `stationCode` must be unique across the platform. Coordinates must fall within valid geographic boundaries. If switching to `MAINTENANCE` or `OFFLINE`, system checks for in-flight missions (`flight_status == 'IN_AIR'`) headed to this station. |
| **Processing** | If in-flight missions exist when switching to `MAINTENANCE`: <br/>1. System updates status to `MAINTENANCE`. <br/>2. Immediately raises **Urgent Reroute Alert** to Dispatcher dashboard via SignalR to redirect active flights to backup landing hubs. <br/>If creating a station, auto-generate `totalSlots` records in `station_slots` numbered 1 to N, all initialized to `EMPTY`. |
| **Saving** | Transaction: <br/>1. Insert/Update `landing_stations` (`status`, `updated_at`, attributes). <br/>2. If new station, bulk insert `station_slots`. <br/>3. Insert `audit_logs` record `STATION_STATUS_CHANGE` with previous status, new status, and reason. |
| **Output / Exceptions** | `201 Created` for creation; `200 OK` with updated station object. Return `400 Bad Request` if coordinates are invalid or reason is missing; `409 Conflict` if `stationCode` already exists. |
| **Business Rules** | BR-LS-01, BR-LS-02, BR-LS-03, BR-LS-04, BR-LS-21. |
| **Screen** | Web Admin: Create/Edit Station; Mobile Operator: `O13 · Station Maintenance` ([O13-Station-Maintenance.png](../design/wireframes/app/station-operator/O13-Station-Maintenance.png)). |

---

### F-03 Manage landing station slots inventory

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-21; UC-16 |
| **Trigger** | Administrator provisions additional landing pads/locker bays or re-numbers slots for a hub. |
| **Actors / Purpose** | Administrator; maintain slot capacity reflecting physical pad hardware installed at the landing hub. |
| **Preconditions** | Administrator authorization. Target station exists in database. |
| **Input** | `stationId` (UUID), `action` (`ADD_SLOT`, `REMOVE_SLOT`), `slotNumber` (int). |
| **Validation** | `slotNumber` must be unique within the station (`(station_id, slot_number)` unique index). Cannot remove a slot that is currently `OCCUPIED` (holding package/drone) or `RESERVED` (booked by in-flight mission). |
| **Processing** | Add slot: insert new slot with status `EMPTY`. Remove slot: verify slot is in `EMPTY` or `ERROR` with no historical references blocking cascade, remove record and decrement `landing_stations.total_slots`. |
| **Saving** | Insert or delete in `station_slots`. Update `landing_stations.total_slots`. Record action in `audit_logs`. |
| **Output / Exceptions** | Updated slot inventory for the station. Return `400 Bad Request` if invalid slot index; `409 Conflict` if attempting to delete an occupied or reserved slot. |
| **Business Rules** | BR-LS-05, BR-LS-06, BR-LS-21. |
| **Screen** | Web Admin: Station Detail $\rightarrow$ Slot Configuration; Mobile Operator: `O11 · Slot Management` ([O11-Slots.png](../design/wireframes/app/station-operator/O11-Slots.png)). |

---

### F-04 Override slot operational state and maintenance isolation

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-21, FR-32; UC-16 |
| **Trigger** | Station Operator identifies pad debris, charging pin malfunction, locker lock jam, or completes repairs. |
| **Actors / Purpose** | Station Operator, Administrator; isolate individual faulty slots (`ERROR`) or restore repaired slots (`EMPTY`) without taking the entire hub offline. |
| **Preconditions** | Caller is assigned Station Operator or Administrator. Slot exists. |
| **Input** | `slotId` (UUID, định danh `station_slots.id` theo ERD), `targetStatus` (`EMPTY`, `ERROR`), `reason` (required, max 500 chars). |
| **Validation** | <br/>- **Moving to `ERROR`:** Allowed if current status is `EMPTY`. If current status is `RESERVED` or `OCCUPIED`, system rejects direct override and triggers conflict alert to Dispatcher (requires rerouting flight or removing parcel first). <br/>- **Moving to `EMPTY`:** Allowed only if current status is `ERROR` (confirming maintenance complete and diagnostic self-test passed). |
| **Processing** | Update slot status. If slot had a pending sensor error, clear error flags. Broadcast update to all connected clients over SignalR topic `station-slots-{stationId}` so Dispatchers instantly see updated pad availability. |
| **Saving** | Update `station_slots.status`, `last_updated = UtcNow`. Insert `audit_logs` record `SLOT_STATUS_OVERRIDE` with `station_slots.id` (ghi nhận `slot_id` trong payload log kiểm toán / audit details), `from_status`, `to_status`, and `reason`. |
| **Output / Exceptions** | `200 OK` with updated slot state. Return `400 Bad Request` if reason is omitted; `409 Conflict` if attempting to set an `OCCUPIED` or `RESERVED` slot to `ERROR` without dispatch clearance. |
| **Business Rules** | BR-LS-05, BR-LS-06, BR-LS-07, BR-LS-21. |
| **Screen** | Mobile Operator: `O12 · Set Slot Status` ([O12-Slot-Status.png](../design/wireframes/app/station-operator/O12-Slot-Status.png)) and `O14 · Report a Problem` ([O14-ReportProblem.png](../design/wireframes/app/station-operator/O14-ReportProblem.png)). |

---

### F-05 Monitor real-time station and slot availability

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-22; UC-17 |
| **Trigger** | Operator opens Station Dashboard, Dispatcher plans mission, or Customer views drop-off hubs. |
| **Actors / Purpose** | Station Operator, Dispatcher, Customer; display real-time availability of landing pads and locker compartments. |
| **Preconditions** | User is authenticated. Station exists and is accessible. |
| **Input** | `stationId` (UUID). |
| **Validation** | Verify station existence. |
| **Processing** | Query `station_slots` for the station. Group counts: <br/>- `available_slots`: count of slots with `status == 'EMPTY'`. <br/>- `occupied_slots`: count of slots with `status == 'OCCUPIED'`. <br/>- `reserved_slots`: count of slots with `status == 'RESERVED'`. <br/>- `error_slots`: count of slots with `status == 'ERROR'`. <br/>Check station mode (`OPERATIONAL`, `MAINTENANCE`, `OFFLINE`). If station is in `MAINTENANCE`, available capacity is reported as zero for new reservations. Subscribe client to SignalR hub for live push updates. |
| **Saving** | Read-only; no database write. |
| **Output / Exceptions** | Interactive visual grid showing each slot number, color-coded status badge, associated package/drone if occupied, and real-time occupancy percentage. Return `404 Not Found` if station does not exist. |
| **Business Rules** | BR-LS-02, BR-LS-05, BR-LS-08. |
| **Screen** | Mobile Operator: `O02 · Operator Home` ([O02-Home.png](../design/wireframes/app/station-operator/O02-Home.png)) and `O11 · Slot Management` ([O11-Slots.png](../design/wireframes/app/station-operator/O11-Slots.png)); Web Dispatcher: Station availability overlay. |

---

### F-06 Process package intake and confirm origin pickup

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-23, FR-14, FR-32; UC-18 |
| **Trigger** | Customer drops off package at origin station; Operator scans parcel tracking code and verifies contents. |
| **Actors / Purpose** | Station Operator, Customer (Sender); inspect parcel, take custody, place in assigned slot/drone bay, and authorize order for takeoff. |
| **Preconditions** | Order exists in database with status `APPROVED`. Origin station matches operator's assigned station. Scheduled departure time is set. |
| **Input** | `trackingNumber` (string / QR code), `actualWeightKg` (decimal từ cân bàn trạm intake, đối soát với `packages.weight_gram` [tham số đo runtime / cột đề xuất lưu vết `actual_weight`]), `lengthCm`, `widthCm`, `heightCm`, `checklistOk` (boolean: no hazardous materials, proper packaging, fragile tag verified). |
| **Validation** | <br/>**1. Status check:** Order must be strictly `APPROVED`. Cannot intake `PENDING` or `CANCELLED` orders. <br/>**2. Physical limit verification:** `actualWeightKg <= 3.0` kg (giá trị cơ sở theo SRS 2.4; đang có đề xuất 5.0 kg từ Delivery Workflow cần cả nhóm chốt trong SRS, xem Mục 6) và `actualWeightKg <= assignedDrone.max_payload_gram / 1000`. Kích thước kiện hàng phải vừa khoang hàng drone ($30 \times 20 \times 15\text{ cm}$). <br/>**3. Safety checklist:** `checklistOk` must be true. <br/>**4. Pad readiness:** Origin slot (`station_slots.id`) must be in `EMPTY` or `RESERVED` state. |
| **Processing** | If checks pass: <br/>1. Transition order status from `APPROVED` $\rightarrow$ `READY_FOR_TAKEOFF`. <br/>2. Transition origin slot status to `OCCUPIED`. <br/>3. Transition drone status to `ASSIGNED`. <br/>4. Notify flight dispatch engine that package is loaded and drone is ready for takeoff sequence. <br/>5. Send notification to Customer confirming package intake. |
| **Saving** | Atomic database transaction: <br/>1. Update `delivery_orders.status = 'READY_FOR_TAKEOFF'`. <br/>2. Update `station_slots.status = 'OCCUPIED'`. <br/>3. Insert `delivery_status_logs` row (`order_id`, `from_status = 'APPROVED'`, `to_status = 'READY_FOR_TAKEOFF'`, `changed_by = operator.id` [theo đúng tên cột `delivery_status_logs.changed_by` trong ERD], `remarks = 'Origin intake verified and package loaded'`). <br/>4. Insert customer notification. <br/>5. Insert `audit_logs` record `PACKAGE_INTAKE_CONFIRMED`. |
| **Output / Exceptions** | `200 OK` with intake confirmation and departure checklist. Return `400 Bad Request` if package exceeds 3.0 kg (hoặc vượt tải trọng drone) hoặc vượt kích thước khoang hàng; `404 Not Found` if tracking number unknown; `409 Conflict` if order status is not `APPROVED`. |
| **Business Rules** | BR-LS-09, BR-LS-10, BR-LS-11, BR-LS-12, BR-LS-13, BR-LS-21. |
| **Screen** | Mobile Operator: `O03 · Tasks (Pickups)` ([O03-Tasks.png](../design/wireframes/app/station-operator/O03-Tasks.png)), `O05 · Scan Order` ([O05-Scan.png](../design/wireframes/app/station-operator/O05-Scan.png)), `O07 · Confirm Pickup Checklist` ([O07-Pickup.png](../design/wireframes/app/station-operator/O07-Pickup.png)), `O08 · Pickup Completed` ([O08-Pickup-Done.png](../design/wireframes/app/station-operator/O08-Pickup-Done.png)). |

---

### F-07 Verify pickup secure code and confirm destination delivery

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-26, FR-27, FR-14, FR-32; UC-08, UC-18 |
| **Trigger** | Recipient enters pickup secure code (chuỗi 6 chữ số hoặc 6 ký tự chữ-số, xem Mục 6 để chốt chuẩn hóa) on station kiosk touchpanel or presents code to Station Operator. |
| **Actors / Purpose** | Recipient, Station Operator / Smart Kiosk, System; authenticate recipient, release package from secure locker slot, record delivery timestamp, and finalize order. |
| **Preconditions** | Order status is strictly `ARRIVED`. Drone has landed and destination slot is `OCCUPIED`. Recipient possesses the pickup secure code (`delivery_orders.pickup_secure_code`). Verification is not in lockout state. |
| **Input** | `trackingNumber` (string), `secureCode` (chuỗi 6 chữ số hoặc 6 ký tự chữ-số khớp với `delivery_orders.pickup_secure_code`). |
| **Validation** | <br/>**1. Status check:** Order must be in `ARRIVED` status. (If `FAILED` or already `DELIVERED`, reject with clear message). <br/>**2. Brute-force lockout check:** If failed attempts $\ge 3$ within last 10 minutes, reject immediately with `429 Too Many Requests` / Lockout notice. <br/>**3. Code match check:** Exact match between submitted code and `delivery_orders.pickup_secure_code` (phân biệt hoa thường nếu sử dụng mã chữ-số). |
| **Processing** | <br/>- **If code is INCORRECT:** Increment failed attempt counter in session cache. If attempts reach 3, trigger 10-minute security lockout and send security warning alert to customer's email/app. Return error showing remaining attempts. <br/>- **If code is CORRECT:** <br/>1. Send electronic pulse signal to unlock destination locker compartment door / station slot. <br/>2. Transition order status from `ARRIVED` $\rightarrow$ `DELIVERED`. <br/>3. Set `delivery_orders.actual_delivery_time = UtcNow`. <br/>4. Release destination slot status to `EMPTY`. <br/>5. Transition drone status from landing pad to `CHARGING` or `IDLE`. <br/>6. Send "Delivery Completed" confirmation notification to sender and recipient. |
| **Saving** | Atomic database transaction: <br/>1. Update `delivery_orders` (`status = 'DELIVERED'`, `actual_delivery_time = UtcNow`). <br/>2. Update `station_slots.status = 'EMPTY'`. <br/>3. Update `drones.status = 'CHARGING'` (or `'IDLE'`). <br/>4. Insert `delivery_status_logs` row (`order_id`, `from_status = 'ARRIVED'`, `to_status = 'DELIVERED'`, `changed_by = operator.id` hoặc `NULL` nếu tự phục vụ tại kiosk, `remarks = 'Recipient verified with pickup secure code'`). <br/>5. Insert `audit_logs` record `DELIVERY_COMPLETED`. <br/>6. Clear temporary failed-attempt cache for this order. |
| **Output / Exceptions** | Locker door unlocked signal, success receipt, and delivery completion confirmation. Return `400 Bad Request` if code format is invalid; `401 Unauthorized` / `Invalid Code` if code does not match; `429 Too Many Requests` if locked out; `409 Conflict` if order status is not `ARRIVED`. |
| **Business Rules** | BR-LS-14, BR-LS-15, BR-LS-16, BR-LS-17, BR-LS-18, BR-LS-21, BR-LS-22. |
| **Screen** | Mobile Operator: `O04 · Tasks (Arrivals)` ([O04-Tasks-Arrivals.png](../design/wireframes/app/station-operator/O04-Tasks-Arrivals.png)), `O09 · Confirm Arrival` ([O09-Arrival.png](../design/wireframes/app/station-operator/O09-Arrival.png)), `O10 · Arrival Completed` ([O10-Arrival-Done.png](../design/wireframes/app/station-operator/O10-Arrival-Done.png)); Customer Mobile: `C11 · Confirm Delivery` ([C11-ConfirmDelivery.png](../design/wireframes/app/customer/C11-ConfirmDelivery.png)), `C12 · Confirm Delivery (Wrong Code)` ([C12-ConfirmDelivery-Wrong.png](../design/wireframes/app/customer/C12-ConfirmDelivery-Wrong.png)), `C13 · Delivered` ([C13-Delivered.png](../design/wireframes/app/customer/C13-Delivered.png)). |

---

### F-08 Process destination pickup timeout and expired parcel exception

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-18, FR-14; UC-14, UC-18 |
| **Trigger** | Automated background cron worker runs hourly checking for arrived parcels whose pickup window has expired ($> 24\text{ hours}$ after landing). |
| **Actors / Purpose** | Automated Workflow Engine / Station Operator; transition uncollected parcels into an exception handling state so lockers are not blocked indefinitely. |
| **Preconditions** | Order has status `ARRIVED`, destination slot is `OCCUPIED`, và `UtcNow > arrival_time + 24 hours` (với `arrival_time` lấy từ `delivery_status_logs.created_at` khi `to_status = 'ARRIVED'`, hoặc cột đề xuất `delivery_orders.arrived_at` [Cột đề xuất / Proposed schema extension]). |
| **Input** | Automated cron sweep; evaluates order arrival timestamp (`delivery_status_logs.created_at` của trạng thái `ARRIVED` hoặc cột đề xuất `delivery_orders.arrived_at` [Cột đề xuất]) against configured pickup window. |
| **Validation** | Verify order is still in `ARRIVED` status and has not been picked up or cancelled. |
| **Processing** | 1. Transition order status from `ARRIVED` $\rightarrow$ `FAILED`. <br/>2. Record failure type `COLLECTION_TIMEOUT`. <br/>3. Keep destination slot locked and marked `OCCUPIED` until operator removes parcel. <br/>4. Generate urgent alert on Dispatcher portal (routing to F-09 failure resolution). <br/>5. Send notification to Customer and Recipient that pickup window expired. |
| **Saving** | Transaction: <br/>1. Update `delivery_orders.status = 'FAILED'`. <br/>2. Insert `delivery_status_logs` row (`order_id`, `from_status = 'ARRIVED'`, `to_status = 'FAILED'`, `changed_by = NULL` [cron hệ thống], `remarks = 'Package collection timeout expired (>24 hours)'`). <br/>3. Insert customer notification. <br/>4. Insert `audit_logs` record `PICKUP_TIMEOUT_EXPIRED`. |
| **Output / Exceptions** | Automated transition completed. Task created for Station Operator to move expired parcel to long-term storage or prepare for return flight. |
| **Business Rules** | BR-LS-19, BR-LS-20. |
| **Screen** | Web Dispatcher: `09 · Failed delivery resolution` ([09-failure-handling.svg](../design/wireframes/web/dispatcher/09-failure-handling.svg)); Mobile Operator: `O04 · Tasks (Arrivals)` alert indicator. |

---

## 5. Business rules referenced by the functions

| ID | Rule Statement | Basis / Classification |
|---|---|---|
| **BR-LS-01** | Landing stations must have one of three status values: `OPERATIONAL`, `MAINTENANCE`, `OFFLINE`. Stations in `MAINTENANCE` or `OFFLINE` are completely excluded from flight scheduling and cannot accept new customer delivery orders. | Baseline: ERD (`erd.md`), SRS FR-20 |
| **BR-LS-02** | Station codes (`station_code`) must be uppercase alphanumeric strings (3–10 characters) and globally unique across the system (enforced by database unique constraint). | Baseline: ERD (`erd.md`) |
| **BR-LS-03** | When a station is placed into `MAINTENANCE` while active missions are currently flying towards it (`flight_status == 'IN_AIR'`), the system must immediately raise an **Urgent Reroute Alert** on the Dispatcher dashboard so the flight can be redirected to a designated alternate station. | Baseline: Activity diagram (`activity-station-and-pickup.md`), Derived safety rule |
| **BR-LS-04** | A station in `MAINTENANCE` can be restored to `OPERATIONAL` only after physical maintenance is completed and all active landing pads pass automated sensor diagnostic self-tests. | Baseline: Activity diagram (`activity-station-and-pickup.md`) |
| **BR-LS-05** | Station slots must strictly have one of four status values: `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`. No unapproved status string is permitted. | Baseline: ERD (`erd.md`), SRS FR-21 |
| **BR-LS-06** | Slot maintenance isolation: An `EMPTY` slot may be set to `ERROR` for cleaning, sensor testing, or pad maintenance. A slot in `RESERVED` or `OCCUPIED` status cannot be placed into `ERROR` directly without rerouting the scheduled flight or physically removing the stored parcel. | Baseline: Operator user flow (`station-operator.md`), Activity diagram |
| **BR-LS-07** | A slot in `ERROR` status is returned to `EMPTY` only after the station operator completes physical maintenance and confirms sensor readiness in the mobile terminal. | Baseline: Operator user flow (`station-operator.md`) |
| **BR-LS-08** | Real-time available landing capacity of a station is strictly the count of slots in `EMPTY` status. Slots in `RESERVED`, `OCCUPIED`, or `ERROR` do not count as available capacity. If a hub has zero `EMPTY` slots, new reservations are blocked. | Derived: Anti-deadlock capacity rule, SRS FR-22 |
| **BR-LS-09** | Origin package intake requires the order to be in `APPROVED` status. An order that is `PENDING`, `CANCELLED`, or already departed cannot be checked in at the station. | Baseline: Business processes (`business-processes.md`) |
| **BR-LS-10** | Physical weight safety rule: Khối lượng thực tế đo tại trạm (`actual_weight` [tham số đo runtime / cột đề xuất lưu vết], quy đổi so khớp `packages.weight_gram`) phải thỏa mãn: `actual_weight <= 3.0 kg` (mức tải trọng tiêu chuẩn theo SRS 2.4 / Activity diagram; đang có đề xuất 5.0 kg từ Delivery Workflow cần cả nhóm chốt trong SRS, xem Mục 6) VÀ `actual_weight <= assigned_drone.max_payload_gram / 1000`. Nếu vượt quá giới hạn tải trọng cho phép, từ chối nhận hàng với lý do "Weight exceeds drone payload limit". | Baseline: SRS Section 2.4, Activity diagram, ERD |
| **BR-LS-11** | Physical dimensions safety rule: Package length $\le 30\text{ cm}$, width $\le 20\text{ cm}$, height $\le 15\text{ cm}$. Over-sized parcels that cannot fit inside the drone cargo bay are rejected at intake. | Baseline: Standard cargo specification, Activity diagram |
| **BR-LS-12** | Origin pickup confirmation transitions the order to `READY_FOR_TAKEOFF`, sets the origin station slot to `OCCUPIED`, and sets the drone status to `ASSIGNED`. The origin slot remains `OCCUPIED` until drone liftoff. | Baseline: `business-processes.md` Section 2 |
| **BR-LS-13** | Once package intake is confirmed (`READY_FOR_TAKEOFF`), the customer is prohibited from cancelling the order via the app. Only the Dispatcher can cancel the order, which requires the station operator to physically retrieve and return the parcel. | Baseline: `business-processes.md` Section 3 |
| **BR-LS-14** | Package delivery handover at destination station requires the order to be in `ARRIVED` status and the destination slot to be `OCCUPIED`. | Baseline: Business processes (`business-processes.md`), UC-08 |
| **BR-LS-15** | Secure code verification: Người nhận phải nhập chính xác mã nhận hàng khớp với `delivery_orders.pickup_secure_code` (trong ERD định nghĩa `varchar(10)`). Hệ thống hỗ trợ chuỗi 6 ký tự; nhóm cần chốt thống nhất giữa phương án "6 chữ số" (numeric OTP) và "6 ký tự chữ-số" (alphanumeric, phân biệt hoa thường) để ghi chuẩn hóa vào SRS (xem Mục 6). | Baseline: SRS FR-26, UC-08, ERD (`erd.dbml`) |
| **BR-LS-16** | Brute-force lockout defense: Entering an incorrect secure code increments a failed attempt counter. After 3 consecutive incorrect attempts within 10 minutes, verification is locked out for 10 minutes, and a security alert notification is dispatched to the customer. | Derived: NFR-01 Security, Activity diagram |
| **BR-LS-17** | Upon successful secure code verification: the locker compartment unlocks, order status transitions to `DELIVERED`, `actual_delivery_time` is recorded with the exact UTC timestamp, and the destination slot transitions to `EMPTY`. | Baseline: SRS FR-26, FR-27, ERD |
| **BR-LS-18** | Drone post-landing state: Upon completion of unloading, the drone transitions from the destination pad to status `CHARGING` (if battery $< 90\%$) or `IDLE` (if battery $\ge 90\%$). | Baseline: ERD (`erd.md`), `business-processes.md` Section 2 |
| **BR-LS-19** | Destination pickup timeout: A parcel in `ARRIVED` status that remains uncollected after 24 hours transitions to `FAILED` with failure type `COLLECTION_TIMEOUT`. | Baseline: `business-processes.md` Section 4, Proposed policy |
| **BR-LS-20** | An expired uncollected parcel (`FAILED`) keeps the destination locker compartment locked. The station operator must physically transfer the parcel to long-term storage or prepare it for a return flight as instructed by the Dispatcher. | Baseline: `business-processes.md` Section 4 |
| **BR-LS-21** | Every station operational decision (station status change, slot maintenance override, package intake confirmation, delivery handover) must record an immutable audit log entry in `audit_logs`. | Baseline: SRS FR-32, UC-22 |
| **BR-LS-22** | Confidentiality of secure code: Mã nhận hàng (`delivery_orders.pickup_secure_code`) không bao giờ được hiển thị trong danh sách nhiệm vụ của Station Operator, màn hình Dispatcher, hay nhật ký `audit_logs`/`delivery_status_logs`. Mã chỉ được gửi riêng cho khách hàng và hệ thống đối soát tự động. | Baseline: NFR-01, NFR-02 Security |

---

## 6. Open decisions and source discrepancies

These items document design nuances identified during Landing Station modeling that require team sign-off before hardware integration is finalized:

| Item | Finding in Sources | Proposed Solution / Agreement Needed |
|---|---|---|
| **Mâu thuẫn giới hạn cân nặng nhận hàng (3.0 kg vs. 5.0 kg)** | - SRS Mục 2.4 và Station Activity Diagram (`activity-station-and-pickup.md`) quy định tải trọng khoang drone chuẩn $\le 3.0\text{ kg}$ ($30 \times 20 \times 15\text{ cm}$). <br/>- Tài liệu Delivery Workflow (LA-217 của Nhân) đề cập mức tải trọng $5.0\text{ kg}$ cho drone tải nặng. Trong ERD, `packages.weight_gram` và `drones.max_payload_gram` lưu trữ theo đơn vị gram. | **Cần cả nhóm họp chốt một lần rồi ghi vào SRS:** <br/>1. Giữ mức $3.0\text{ kg}$ (3000g) làm giới hạn cơ sở cho các dòng drone dân dụng tiêu chuẩn. <br/>2. Nếu mở rộng lên $5.0\text{ kg}$ (5000g), cần cập nhật đồng bộ SRS Mục 2.4 và FR-19 để quy định cấu hình tải trọng linh hoạt theo từng model drone (`drones.max_payload_gram`). Hiện tại module trạm áp dụng kiểm tra động theo `drones.max_payload_gram` và chặn trên tối đa theo mức chuẩn SRS. |
| **Mâu thuẫn định dạng mã nhận hàng (6 chữ số vs. 6 ký tự chữ-số)** | - Bảng Actor (dòng 43), F-07 Trigger và tài liệu Delivery Workflow ghi "6 chữ số" (numeric OTP `0-9`, thuận tiện nhập liệu trên bàn phím số kiosk hoặc nhận OTP SMS). <br/>- BR-LS-15 và F-07 Input ghi "6 ký tự chữ-số" (alphanumeric `A-Z, 0-9`, tăng độ phức tạp chống dò mã). <br/>- ERD định nghĩa cột `delivery_orders.pickup_secure_code varchar(10)` (chứa được cả hai). | **Cần cả nhóm họp chốt một lần rồi ghi vào SRS:** <br/>1. *Phương án A (Khuyến nghị cho UX):* Chuỗi 6 chữ số thuần túy (`0-9`) để tối ưu trải nghiệm nhập mã trên màn hình cảm ứng kiosk trạm và bàn phím số di động. <br/>2. *Phương án B (Bảo mật cao):* Chuỗi 6 ký tự chữ-số (`A-Z, 0-9`) phân biệt hoa-thường. <br/>Sau khi nhóm chốt, cập nhật thống nhất vào SRS FR-26. Hiện tại tài liệu chuẩn hóa dùng đúng tên cột ERD `pickup_secure_code` và hỗ trợ cả hai định dạng. |
| **Chuẩn hóa tên cột ERD & Các cột đề xuất mở rộng** | Rà soát đối chiếu với ERD (`erd.md`, `erd.dbml`): <br/>- `secure_code` $\rightarrow$ Đã đổi thành `pickup_secure_code` (thuộc bảng `delivery_orders`). <br/>- `changed_by_user_id` $\rightarrow$ Đã đổi thành `changed_by` (thuộc bảng `delivery_status_logs`), ghi chú nội dung qua `remarks`. <br/>- `slot_id` $\rightarrow$ Khóa chính trong ERD là `station_slots.id` (và khóa ngoại trong `flight_missions`). <br/>- `arrived_at` $\rightarrow$ Không có cột này trong `delivery_orders`. Mốc thời gian đến được truy xuất từ `delivery_status_logs.created_at` (khi `to_status = 'ARRIVED'`). <br/>- `actual_weight` $\rightarrow$ ERD lưu `packages.weight_gram`. Khối lượng đo tại trạm là tham số runtime. | **Chuẩn hóa áp dụng:** <br/>- Dùng chính xác tên cột ERD cho toàn bộ luồng xử lý: `delivery_orders.pickup_secure_code`, `station_slots.id`, `delivery_status_logs.changed_by`, `delivery_status_logs.remarks`. <br/>- Đánh dấu rõ ràng các trường đo đạc hoặc mở rộng là `[Cột đề xuất / Proposed schema extension]`: `delivery_orders.arrived_at` (nếu muốn tối ưu truy vấn timeout không cần join bảng log) và `actual_weight` (tham số đo runtime cân trạm đối soát với `packages.weight_gram`). |
| **Hardware Sensor Integration vs. Manual Confirmation** | `activity-station-and-pickup.md` mentions slot sensor self-tests, while wireframe `O07` displays an operator checklist. | **Proposed:** Support dual verification: automated weight/pad sensors validate physically on smart hubs; manual checklist acts as fallback on legacy stations or when sensors flag calibration warnings. |
| **Locker Door Auto-Close & Sensor Lock** | When a recipient retrieves a parcel from a smart locker, the physical door must be closed before the slot is marked `EMPTY`. | **Proposed:** Slot remains in `OCCUPIED` until door contact sensor reports "Door Closed & Latched" AND optical sensor confirms compartment is empty, then transitions to `EMPTY`. |
| **Lockout Duration and Reset Policy** | Activity diagram specifies "Locked for 10 Mins" after 3 failed attempts, but customer self-service reset is not specified. | **Proposed:** 10-minute temporary lockout. Customer can view their secure code in the mobile app anytime or contact Station Operator with government photo ID for manual identity verification. |
| **Station Power & Drone Fast-Charging Capacity** | Total slots per station ranges from 2 to 8, but concurrent fast-charging electrical load is not defined in ERD. | **Proposed:** Firmware limits concurrent fast charging to at most 2 drones per hub to prevent substation overcurrent, queuing remaining drones in trickle charge mode. |
| **Return-to-Origin Intake Flow** | When a failed delivery is returned to origin by drone, the physical handover flow at origin station is not separately detailed in SRS. | **Proposed:** Operator at origin station receives returned parcel, scans tracking number, verifies package seal, and transitions order status to `CANCELLED` (with reason "Returned to sender"). |

---

## 7. Review and acceptance checklist

This checklist defines acceptance criteria for verifying the Landing Station functional documentation:

| Area | Review / Acceptance Scenario |
|---|---|
| **Scope Coverage** | Functions F-01 through F-08 comprehensively cover all required aspects: stations (F-01, F-02), slots (F-03, F-04), availability (F-05), and pickup (F-06 origin intake, F-07 destination secure handover, F-08 timeout). |
| **ERD & State Machine Alignment** | Strictly uses the 3 station statuses (`OPERATIONAL`, `MAINTENANCE`, `OFFLINE`) and 4 slot statuses (`EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`). All order status transitions match the approved 8-state model. Database schema names strictly conform to ERD (`delivery_orders.pickup_secure_code`, `station_slots.id`, `delivery_status_logs.changed_by`), with proposed schema extensions clearly marked. |
| **Safety Limits Enforcement** | Origin intake strictly validates package weight against drone limit ($\le 3.0\text{ kg}$, hoặc theo mức tải trọng chốt trong SRS) và kích thước khoang hàng ($30 \times 20 \times 15\text{ cm}$). |
| **Security & Brute-Force Defense** | Secure code verification đối soát với `delivery_orders.pickup_secure_code` và thực thi cơ chế khóa 3 lần thử / 10 phút. Mã nhận hàng được bảo mật tuyệt đối khỏi log nhân viên. |
| **Aeronautical Conflict Handling** | Setting a station or slot to maintenance triggers automated conflict checks against in-flight missions, raising immediate alerts to Dispatchers for rerouting. |
| **Auditability & Traceability** | Sensitive station mode toggles, slot overrides, and parcel intake/handover events write immutable records to `audit_logs` (FR-32) and `delivery_status_logs` (FR-14). |

---
