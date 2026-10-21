# SmartDroneDelivery — Delivery Workflow Function Descriptions

**Task:** LA-217 — Functions of Delivery Workflow module  
**Parent task:** LA-195 — Function descriptions for system modules  
**Scope owner:** Delivery workflow: order review & approval/rejection, scheduling, drone assignment, active delivery & map tracking, and failed delivery resolution  
**Status:** Proposed for review against SRS, Use Cases, ERD, Business Process Models, and Dispatcher User Flow  
**Primary actors:** Dispatcher (Web Portal), System / Automated Flight Engine, Station Operator (Mobile/Web), Customer (Mobile/Web)  

---

## 1. Objective and scope

This document defines the functional requirements and detailed function descriptions for the **Delivery Workflow module** of SmartDroneDelivery, covering the end-to-end operational lifecycle from when a customer's order is submitted in `PENDING` state until delivery completion or failure resolution.

SmartDroneDelivery manages delivery logistics, scheduling, fleet allocation, and status transitions. It does not replace onboard autopilot hardware (e.g., PX4/ArduPilot flight controllers) but interfaces with telemetry streams and station sensors to coordinate missions safely.

The primary functional requirements in scope are **FR-13 to FR-19** (Delivery workflow) and **FR-24 to FR-25** (Delivery tracking and map visualization), supported by **FR-12** (Customer notifications), **FR-14** (State machine logging), and **FR-32** (Audit logging for sensitive order decisions). Customer-side order creation (FR-06 to FR-10) and physical locker pickup (FR-23, FR-26 to FR-27) are referenced at workflow boundaries but their detailed function descriptions are maintained in their respective module documents.

### 1.1 Source basis

| Source | Use in this document |
|---|---|
| [SRS](SRS.md), Sections 3.3, 3.5, 3.7 | Requirements FR-13 to FR-19, FR-24 to FR-27, FR-32; priority and actor allocation |
| [Use cases](use-cases.md) | UC-11, UC-12, UC-13, UC-14, UC-15, UC-07, UC-08; preconditions and main/alternative flows |
| [Business process models](../design/business-processes.md) | Delivery lifecycle (LA-70), cancellation and failure flows (LA-71), 8 ERD order statuses |
| [Architecture](../architecture/architecture.md), [ERD](../architecture/erd.md), [database design](../architecture/erd.dbml) | `delivery_orders`, `flight_missions`, `drones`, `station_slots`, `delivery_status_logs`, `flight_telemetry_logs`, `audit_logs` |
| [Dispatcher user flow](../design/user-flows/dispatcher.md) | Navigation paths: Pending orders $\rightarrow$ Approval $\rightarrow$ Schedule $\rightarrow$ Drone assign $\rightarrow$ Active deliveries $\rightarrow$ Failure resolution |
| [Web wireframes (Dispatcher)](../design/wireframes/web/index.md) | Screens `02` (Dashboard), `03` (Pending), `04` (Approval), `05` (Schedule), `06` (Assign), `07` (Active), `08` (Tracking), `09` (Failure) |
| [Customer user flow](../design/user-flows/customer.md), [Operator flow](../design/user-flows/station-operator.md) | External interaction boundaries for pickup confirmation, live order tracking, and locker arrival |

---

## 2. Actors and conventions

| Actor / Component | Role code / Type | Operational responsibility in Delivery Workflow |
|---|---|---|
| **Dispatcher** | `DISPATCHER` | Primary human operator on the Web portal; reviews pending orders, approves/rejects, schedules departure, selects available drone, monitors telemetry, resolves failures |
| **Customer** | `CUSTOMER` | Places order, monitors real-time status and live map tracking; receives notifications on status changes; may cancel while order is in `PENDING` or `APPROVED` |
| **Station Operator** | `STATION_OPERATOR` | Inspects and receives package at origin station; physically loads drone or confirms pickup; receives returned package in failure flow |
| **Recipient** | Human Actor | Receives arrival notification at destination station; inputs secure pickup code at destination station slot/locker to complete delivery |
| **System / Flight Engine** | System Component | Enforces status transitions, checks payload/battery limits, prevents overlapping drone schedules, streams telemetry, triggers timeout alerts |
| **System Administrator** | `ADMIN` | Supervises system parameters, inspects audit logs, configures hub/drone fleets |

### Conventions
- **Identifiers:** Primary keys are UUIDs (`orders.id`, `missions.id`, `drones.id`), except `roles.id` and auto-increment `bigint` IDs on high-frequency log tables (`delivery_status_logs.id`, `flight_telemetry_logs.id`, `audit_logs.id`).
- **Timestamps:** Persisted in UTC (ISO 8601, e.g. `2026-10-16T08:30:00Z`).
- **Telemetry Coordinates:** WGS 84 latitude/longitude as decimal degrees, altitude in meters above mean sea level (MSL), speed in meters per second (m/s), battery State of Charge (SoC) in percent (0–100%).
- **Business Rule Identifiers:** Numbered `BR-DW-01` to `BR-DW-22` within this document (prefixed `DW` for Delivery Workflow) to avoid collision with other module-specific rule sets.
- **Rule Categorization:**
  - **Baseline:** Explicitly mandated by SRS, ERD, or approved business process diagrams.
  - **Derived:** Required engineering logic for data integrity, error safety, and resource cleanup.
  - **Proposed:** Operational policies requiring final product management sign-off (highlighted in Section 6).

---

## 3. Requirements traceability matrix

| SRS Requirement | Priority | Use Case | Matching Function Descriptions |
|---|---|---|---|
| **FR-13** List pending orders and approve or reject each with a reason | M | UC-11 | **F-01**, **F-02** |
| **FR-14** Allow only valid status transitions and log every transition with acting user and time | M | UC-11, UC-12, UC-13, UC-14 | **F-03** |
| **FR-15** Schedule departure time and assign an available drone, creating a flight mission | M | UC-12 | **F-04**, **F-05** |
| **FR-16** Reject a drone assignment that overlaps another mission of the same drone | M | UC-12 | **F-05** |
| **FR-17** See all active deliveries with status and position | M | UC-13 | **F-06** |
| **FR-18** Mark delivery as failed with a reason and choose reschedule, return or cancel | M | UC-14 | **F-08**, **F-09** |
| **FR-19** Create, update and list drones with model, payload capacity, range and status | M | UC-15 | **F-10** |
| **FR-24** Push status and drone position updates to clients in real time | M | UC-07, UC-13 | **F-07** |
| **FR-25** Show origin, destination and current drone position on a map | S | UC-07, UC-13 | **F-07** |
| **FR-12** Create notification for customer when order status changes | S | UC-10 | **F-02**, **F-03**, **F-05**, **F-08**, **F-09** |
| **FR-32** Record audit log entry for sensitive actions (order decisions, assignments) | M | UC-22 | **F-02**, **F-04**, **F-05**, **F-09** |

---

## 4. Delivery Workflow function descriptions

### F-01 List pending delivery requests

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-13; UC-11 |
| **Trigger** | Dispatcher navigates to the Pending Orders tab or opens the Dispatcher dashboard. |
| **Actors / Purpose** | Dispatcher; identify submitted customer orders awaiting operational review and feasibility check. |
| **Preconditions** | Dispatcher is authenticated with `DISPATCHER` or `ADMIN` role. |
| **Input** | Optional filters: origin station ID, destination station ID, date submitted range, pagination (`page`, `pageSize`, default 20), sort order (default `created_at ASC`). |
| **Validation** | Verify caller role. Filter date range must be valid (`start <= end`). Pagination parameters must be positive integers. |
| **Processing** | Query `delivery_orders` where `status = 'PENDING'`. Join `packages` (weight, dimensions, fragile), `landing_stations` (origin and destination names/codes), and `users` (customer name, phone). Calculate queue waiting time. |
| **Saving** | Read-only; no database write. |
| **Output / Exceptions** | Paginated list of pending orders with tracking number, customer info, package specs, origin/destination hubs, submission timestamp. Empty list if none pending. Return `401 Unauthorized` if unauthenticated, `403 Forbidden` if role is unauthorized. |
| **Business Rules** | BR-DW-01, BR-DW-02. |
| **Screen** | Web Dispatcher: `03 · Pending orders` ([03-pending-orders.svg](../design/wireframes/web/dispatcher/03-pending-orders.svg)). |

---

### F-02 Review and approve or reject delivery request

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-13, FR-14, FR-32; UC-11 |
| **Trigger** | Dispatcher clicks "Approve" or "Reject" on the Order Detail & Approval screen. |
| **Actors / Purpose** | Dispatcher; confirm technical/operational feasibility of an order or reject it with an explicit business reason. |
| **Preconditions** | Order exists, current status is strictly `PENDING`, and Dispatcher has active session. |
| **Input** | `orderId` (UUID), `decision` (`APPROVE` or `REJECT`), `reason` (required if `decision == REJECT`, optional if `APPROVE`, max 500 characters). |
| **Validation** | Verify order is currently `PENDING`. If `REJECT`, `reason` must be non-empty. Verify that origin and destination landing stations are in `OPERATIONAL` status. |
| **Processing** | If **APPROVE**: update status to `APPROVED`, record `dispatcher_id = currentUser.id`. If **REJECT**: update status to `CANCELLED`, store `cancel_reason = reason`. Trigger customer notification via SignalR/push. |
| **Saving** | In a single database transaction: <br/>1. Update `delivery_orders` (`status`, `dispatcher_id`, `updated_at`, `cancel_reason` if rejected). <br/>2. Insert `delivery_status_logs` row (`order_id`, `from_status = 'PENDING'`, `to_status = 'APPROVED'` or `'CANCELLED'`, `changed_by`, `remarks = reason`). <br/>3. Insert `notifications` record for customer. <br/>4. Insert `audit_logs` record with action `ORDER_APPROVAL` or `ORDER_REJECTION`. |
| **Output / Exceptions** | Updated order object with new status. Return `400 Bad Request` if rejection reason is missing; `404 Not Found` if order does not exist; `409 Conflict` if order was already reviewed or cancelled by another user. |
| **Business Rules** | BR-DW-01, BR-DW-02, BR-DW-03, BR-DW-04, BR-DW-19. |
| **Screen** | Web Dispatcher: `04 · Order detail and approval` ([04-order-detail-approval.svg](../design/wireframes/web/dispatcher/04-order-detail-approval.svg)). |

---

### F-03 Enforce order status state machine and log history

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-14, FR-32; UC-11, UC-12, UC-13, UC-14, UC-08 |
| **Trigger** | Any system component, dispatcher, operator, or customer triggers a status mutation on a delivery order. |
| **Actors / Purpose** | Delivery Workflow Engine / Database; guarantee system-wide lifecycle consistency and maintain an immutable historical audit trail. |
| **Preconditions** | Order exists in database. |
| **Input** | `orderId`, `newStatus`, `changedBy` (optional for automated system events), `remarks` (optional). |
| **Validation** | Current order status must allow transition to `newStatus` according to the 8-state model: <br/>- `PENDING` $\rightarrow$ `APPROVED`, `CANCELLED` <br/>- `APPROVED` $\rightarrow$ `READY_FOR_TAKEOFF`, `CANCELLED` <br/>- `READY_FOR_TAKEOFF` $\rightarrow$ `IN_TRANSIT`, `CANCELLED` <br/>- `IN_TRANSIT` $\rightarrow$ `ARRIVED`, `FAILED` <br/>- `ARRIVED` $\rightarrow$ `DELIVERED`, `FAILED` <br/>- `FAILED` $\rightarrow$ `APPROVED` (reschedule), `CANCELLED` (cancel/return) <br/>- `DELIVERED` $\rightarrow$ none (terminal) <br/>- `CANCELLED` $\rightarrow$ none (terminal). |
| **Processing** | Validate transition eligibility against current database record within an exclusive lock (`SELECT ... FOR UPDATE`). If valid, apply mutation and record timestamps (`actual_departure_time` on `IN_TRANSIT`, `actual_delivery_time` on `DELIVERED`). |
| **Saving** | Update `delivery_orders.status`. Insert immutable entry into `delivery_status_logs` with `(order_id, from_status, to_status, changed_by, remarks, created_at)`. |
| **Output / Exceptions** | Status updated successfully. If transition is invalid, throw `InvalidStateTransitionException` and return `409 Conflict` with RFC 7807 problem details explaining the disallowed transition. |
| **Business Rules** | BR-DW-01, BR-DW-04, BR-DW-18, BR-DW-19. |
| **Screen** | System-wide background service and visible on all timeline/history components across Web and Mobile. |

---

### F-04 Schedule delivery departure time

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-15; UC-12 |
| **Trigger** | Dispatcher initiates scheduling on an approved order. |
| **Actors / Purpose** | Dispatcher; establish planned dispatch window matching station operations, daylight/weather constraints, and customer expectation. |
| **Preconditions** | Order status is `APPROVED` or resubmitted from `FAILED` reschedule. |
| **Input** | `orderId` (UUID), `scheduledDepartureTime` (UTC DateTime). |
| **Validation** | `scheduledDepartureTime` must be at least 15 minutes in the future from current time (preparation buffer). It must fall within the operating schedule of origin and destination stations. |
| **Processing** | Verify order eligibility. Compute preliminary flight window based on origin-destination geodesic distance (assuming nominal drone cruise speed of 15 m/s / 54 km/h) plus 10 minutes terminal takeoff/landing clearance buffer. |
| **Saving** | Update `delivery_orders.scheduled_departure_time` and `updated_at`. |
| **Output / Exceptions** | Updated schedule time and estimated flight duration. Return `400 Bad Request` if departure time is in the past; `409 Conflict` if order is not in `APPROVED` status. |
| **Business Rules** | BR-DW-05, BR-DW-06. |
| **Screen** | Web Dispatcher: `05 · Schedule delivery` ([05-schedule-delivery.svg](../design/wireframes/web/dispatcher/05-schedule-delivery.svg)). |

---

### F-05 Validate and assign drone to flight mission

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-15, FR-16, FR-32; UC-12 |
| **Trigger** | Dispatcher selects a drone from the available fleet list and confirms assignment. |
| **Actors / Purpose** | Dispatcher and Flight Scheduler; assign a capable drone to execute the delivery flight mission without scheduling conflicts. |
| **Preconditions** | Order is `APPROVED` and has `scheduled_departure_time` set. Drone exists and is hosted at or en route to the origin station. |
| **Input** | `orderId` (UUID), `droneId` (UUID). |
| **Validation** | **1. Fleet status:** Drone must be in `IDLE` or `CHARGING` status. Not in `MAINTENANCE`. <br/>**2. Physical capacity:** `drones.max_payload_gram >= packages.weight_gram`. <br/>**3. Operational range:** `drones.max_flight_range_km >= route_distance_km * 1.3` (trong đó `route_distance_km` lấy từ `ai_eta_predictions.calculated_distance_km` hoặc khoảng cách tính toán giữa 2 trạm; bắt buộc 30% reserve cho gió/hạ cánh dự phòng). <br/>**4. Single-flight non-overlap:** The drone must not have any existing `flight_missions` where `flight_status IN ('PLANNED', 'IN_AIR')` overlapping the interval `[scheduledDeparture - 15min, estimatedArrival + 20min]` (turnaround/recharge buffer). <br/>**5. Destination slot:** Destination station must have at least one slot capable of reservation. |
| **Processing** | Set drone status to `ASSIGNED`. Reserve a slot at the destination landing station (`station_slots.status = 'RESERVED'`). Create `flight_missions` entry with `flight_status = 'PLANNED'`. Link mission to order. Trigger AI ETA calculation. |
| **Saving** | Single transaction: <br/>1. Insert `flight_missions` (`id`, `order_id`, `drone_id`, `flight_status = 'PLANNED'`, `start_slot_id`, `destination_slot_id`, `scheduled_departure_time`). <br/>2. Update `drones.status = 'ASSIGNED'`. <br/>3. Update `station_slots.status = 'RESERVED'` for destination slot. <br/>4. Insert `audit_logs` record `DRONE_ASSIGNMENT`. |
| **Output / Exceptions** | `201 Created` with mission details and flight route. Return `400 Bad Request` if payload exceeds limits or range is insufficient; `409 Conflict` if drone has an overlapping mission or destination station has zero available landing slots. |
| **Business Rules** | BR-DW-06, BR-DW-07, BR-DW-08, BR-DW-09, BR-DW-10, BR-DW-19. |
| **Screen** | Web Dispatcher: `06 · Assign drone` ([06-assign-drone.svg](../design/wireframes/web/dispatcher/06-assign-drone.svg)). |

---

### F-06 Monitor active deliveries and operational dashboard

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-17; UC-13 |
| **Trigger** | Dispatcher navigates to Active Deliveries or views the Dispatcher Dashboard. |
| **Actors / Purpose** | Dispatcher; maintain situational awareness across all airborne and active dispatch missions in real time. |
| **Preconditions** | Dispatcher is authenticated. |
| **Input** | Optional filters: origin station, destination station, status (`READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`), search by tracking number or drone serial number. |
| **Validation** | Role authorization check (`DISPATCHER`, `ADMIN`). |
| **Processing** | Query active orders joined with `flight_missions`, `drones`, and latest `flight_telemetry_logs`. Aggregate telemetry summary: current speed, altitude, battery percentage, distance remaining, latest AI ETA prediction. Flag anomalies (e.g. battery $< 25\%$, GPS drift, deviation from corridor). |
| **Saving** | Read-only; no database write. |
| **Output / Exceptions** | Grid and card summary of all in-progress deliveries with real-time status badges, drone model/battery indicators, origin/destination hubs, and quick actions to open Live Tracking or Report Failure. |
| **Business Rules** | BR-DW-11, BR-DW-12. |
| **Screen** | Web Dispatcher: `07 · Active deliveries` ([07-active-deliveries.svg](../design/wireframes/web/dispatcher/07-active-deliveries.svg)) and `02 · Dispatcher dashboard` ([02-dashboard.svg](../design/wireframes/web/dispatcher/02-dashboard.svg)). |

---

### F-07 Real-time telemetry tracking and map visualization

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-24, FR-25; UC-07, UC-13 |
| **Trigger** | Dispatcher or authorized Customer opens live tracking for an active order, or flight telemetry is pushed by the drone simulator/telemetry receiver. |
| **Actors / Purpose** | Dispatcher, Customer, Real-time Telemetry Service; provide live visual tracking of flight path, origin, destination, and current coordinates on an interactive map. |
| **Preconditions** | Order exists; status is `IN_TRANSIT` (or `READY_FOR_TAKEOFF` / `ARRIVED`). Callers must own the order or have staff role (`DISPATCHER`, `MANAGER`, `ADMIN`). |
| **Input** | Tracking request: `orderId` or `trackingNumber`. Telemetry ingestion: `missionId`, `latitude`, `longitude`, `altitude`, `speed`, `batteryPercentage`, `timestamp`. |
| **Validation** | Verify caller ownership or staff permissions. For telemetry ingestion, validate latitude ($-90$ to $90$), longitude ($-180$ to $180$), battery ($0$ to $100\%$). |
| **Processing** | Ingestion service records telemetry log and broadcasts updates over SignalR topic `delivery-tracking-{orderId}`. On client UI: render vector map with origin hub pin, destination hub pin, planned direct corridor/waypoints, and animated drone position icon oriented by bearing. Update ETA display dynamically. |
| **Saving** | High-throughput insert into `flight_telemetry_logs` (`mission_id`, `latitude`, `longitude`, `altitude`, `battery_percentage`, `speed`, `timestamp`). |
| **Output / Exceptions** | Map vector data, current coordinate payload, SignalR live subscription stream. If client lacks permission, return `403 Forbidden`. If order has not departed yet, show origin hub location and scheduled departure. |
| **Business Rules** | BR-DW-11, BR-DW-12, BR-DW-13, BR-DW-14. |
| **Screen** | Web Dispatcher: `08 · Live delivery tracking` ([08-live-tracking.svg](../design/wireframes/web/dispatcher/08-live-tracking.svg)); Mobile Customer: `C06 · Order Details (In-Transit)` ([C06-OrderDetails-InTransit.png](../design/wireframes/app/customer/C06-OrderDetails-InTransit.png)). |

---

### F-08 Detect and mark delivery failure

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-18; UC-14 |
| **Trigger** | Automated event: flight mission abort (battery critically low, adverse weather, loss of telemetry $> 60\text{s}$) OR package collection timeout at destination locker ($> 24\text{ hours}$ uncollected). Alternatively, Dispatcher manual trigger from Live Tracking. |
| **Actors / Purpose** | Automated Flight System / Dispatcher; transition an unviable or expired delivery into an actionable exception state. |
| **Preconditions** | Order is in `IN_TRANSIT` or `ARRIVED` status. |
| **Input** | `orderId` (UUID), `failureReason` (string, max 500 characters), `failureType` (`MISSION_ABORTED`, `EMERGENCY_LANDING`, `COLLECTION_TIMEOUT`, `HARDWARE_FAULT`). |
| **Validation** | Order must be in `IN_TRANSIT` or `ARRIVED`. |
| **Processing** | Change order status to `FAILED`. If in air: flight mission status changes to `ABORTED`, drone commands auto-return to nearest safe station. If arrived: locker slot remains locked until operator action. Generate urgent alert on Dispatcher dashboard. Send failure notification to customer. |
| **Saving** | Transaction: <br/>1. Update `delivery_orders.status = 'FAILED'`. <br/>2. Update `flight_missions.flight_status = 'ABORTED'` (if active). <br/>3. Insert `delivery_status_logs` row (`order_id`, `from_status`, `to_status = 'FAILED'`, `changed_by`, `remarks = failureReason`). <br/>4. Insert customer notification. <br/>5. Insert `audit_logs` record `DELIVERY_FAILURE_DETECTED`. |
| **Output / Exceptions** | Failure state confirmed. Dispatcher alert banner activated. Customer notified. Return `409 Conflict` if order was already completed or cancelled. |
| **Business Rules** | BR-DW-15, BR-DW-16, BR-DW-19. |
| **Screen** | Web Dispatcher: Alert banner and `09 · Failed delivery resolution` ([09-failure-handling.svg](../design/wireframes/web/dispatcher/09-failure-handling.svg)). |

---

### F-09 Resolve failed delivery (Reschedule, Return, or Cancel)

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-18, FR-32; UC-14 |
| **Trigger** | Dispatcher selects a resolution option on the Failed Delivery Resolution screen. |
| **Actors / Purpose** | Dispatcher; resolve a failed delivery by determining whether the package can be redelivered, returned to sender, or cancelled. |
| **Preconditions** | Order status is strictly `FAILED`. Dispatcher has authenticated session. |
| **Input** | `orderId` (UUID), `action` (`RESCHEDULE`, `RETURN_TO_ORIGIN`, `CANCEL`), `resolutionNote` (string, required, max 500 characters). |
| **Validation** | Verify order is currently `FAILED`. `resolutionNote` must not be blank. If `RESCHEDULE`, package must be verified undamaged at origin/hub. |
| **Processing** | <br/>- **Case 1: `RESCHEDULE`**: Transitions status from `FAILED` $\rightarrow$ `APPROVED`. Clears previous mission link and slot reservation. Order is routed back to F-04/F-05 for new departure schedule and drone selection. <br/>- **Case 2: `RETURN_TO_ORIGIN`**: Creates return task for Station Operator at destination or emergency station. Package is routed back to origin. Once confirmed received at origin, status transitions to `CANCELLED` with note "Returned to sender". <br/>- **Case 3: `CANCEL`**: Transitions status from `FAILED` $\rightarrow$ `CANCELLED`. Releases any reserved slots and unassigns drone. Order reaches terminal state. |
| **Saving** | Single transaction: <br/>1. Update `delivery_orders.status` (`APPROVED` for reschedule, `CANCELLED` for direct cancel/return completion). <br/>2. Update `delivery_orders.cancel_reason = resolutionNote` (if cancelled). <br/>3. Release associated `station_slots` if cancelled. <br/>4. Insert `delivery_status_logs` with resolution action and note in `remarks`. <br/>5. Insert `audit_logs` record `FAILED_DELIVERY_RESOLVED`. <br/>6. Send status update notification to customer. |
| **Output / Exceptions** | `200 OK` with updated order state and next navigation target. Return `400 Bad Request` if resolution note is empty; `409 Conflict` if order is not in `FAILED` status. |
| **Business Rules** | BR-DW-16, BR-DW-17, BR-DW-18, BR-DW-19. |
| **Screen** | Web Dispatcher: `09 · Failed delivery resolution` ([09-failure-handling.svg](../design/wireframes/web/dispatcher/09-failure-handling.svg)). |

---

### F-10 Maintain drone fleet operational status and readiness

| Item | Description |
|---|---|
| **Requirement / Use Case** | FR-19; UC-15 |
| **Trigger** | Dispatcher opens Fleet Management tab or drone telemetry updates charging/maintenance state. |
| **Actors / Purpose** | Dispatcher, System Administrator; manage drone fleet inventory, check battery health, and flag maintenance issues. |
| **Preconditions** | User is authenticated with `DISPATCHER` or `ADMIN` role. |
| **Input** | Drone query filters: status (`IDLE`, `ASSIGNED`, `FLYING`, `CHARGING`, `MAINTENANCE`), station ID, model. Drone update: `droneId`, `status`, `notes`. |
| **Validation** | Cannot change a drone's status to `MAINTENANCE` or take it offline while it is currently flying (`drones.status == 'FLYING'`). Payload capacity and range values must be positive numbers. |
| **Processing** | Read or update drone attributes. When a drone completes a flight, system sets status to `CHARGING` until battery $\ge 90\%$, then transitions to `IDLE`. |
| **Saving** | Update `drones` record (`status`, `current_station_id`, `battery_percentage`, `updated_at`). Insert `audit_logs` if manual maintenance toggle is executed. |
| **Output / Exceptions** | Fleet list with real-time battery bars, maintenance status, and assigned mission link. Return `409 Conflict` if trying to ground a drone during an in-air mission. |
| **Business Rules** | BR-DW-07, BR-DW-08, BR-DW-20. |
| **Screen** | Web Dispatcher: Drone fleet list / modal on `06 · Assign drone` ([06-assign-drone.svg](../design/wireframes/web/dispatcher/06-assign-drone.svg)). |

---

## 5. Business rules referenced by the functions

| ID | Rule Statement | Basis / Classification |
|---|---|---|
| **BR-DW-01** | The order status lifecycle follows strictly the 8 defined states: `PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`. No intermediate status (such as `PICKED_UP` or `SCHEDULED`) is permitted in the database. | Baseline: ERD (`erd.md`), `business-processes.md` |
| **BR-DW-02** | Orders in `PENDING` state may only be reviewed by users with `DISPATCHER` or `ADMIN` role. Approval moves the order to `APPROVED`; rejection records a mandatory reason and moves the order to `CANCELLED`. | Baseline: SRS FR-13, UC-11 |
| **BR-DW-03** | Customers can only cancel an order while it is in `PENDING` or `APPROVED` status. Once an order reaches `READY_FOR_TAKEOFF`, only the Dispatcher can cancel it (requiring physical package offloading at the station). | Baseline: `business-processes.md` Section 3 |
| **BR-DW-04** | Every single status change must append a record into `delivery_status_logs` containing `order_id`, `from_status`, `to_status`, `changed_by` (or NULL for automated triggers), `remarks`, and UTC timestamp. Status log rows are append-only and cannot be updated or deleted. | Baseline: SRS FR-14, ERD |
| **BR-DW-05** | Departure time scheduling requires `scheduled_departure_time >= current_time + 15 minutes` to allow station operators physical package handling, inspection, and drone loading. | Derived: Dispatcher User Flow, operational preparation |
| **BR-DW-06** | A flight mission requires reserving an available landing slot at the destination station prior to takeoff. The destination slot status is set to `RESERVED` upon drone assignment and transitions to `OCCUPIED` upon drone touchdown. | Baseline: `business-processes.md`, ERD `station_slots` |
| **BR-DW-07** | A drone is eligible for mission assignment only if `drone.status IN ('IDLE', 'CHARGING')` and `drone.battery_percentage >= 80%` (or projected to reach $\ge 80\%$ by the scheduled departure time). Drones in `MAINTENANCE` or `FLYING` are strictly ineligible. | Derived: Fleet safety and battery degradation limits |
| **BR-DW-08** | Physical payload constraint: `drones.max_payload_gram >= packages.weight_gram`. Package dimensions (`packages.length_cm`, `packages.width_cm`, `packages.height_cm`) must fit within the standardized drone cargo bay dimensions ($30 \times 20 \times 15\text{ cm}$). | Baseline: SRS 2.4, UC-05, ERD `drones`, `packages` |
| **BR-DW-09** | Range constraint: `drones.max_flight_range_km >= route_distance_km * 1.30` (trong đó `route_distance_km` là khoảng cách lộ trình lấy từ `ai_eta_predictions.calculated_distance_km` hoặc tính toán theo tọa độ hai trạm hạ cánh). The 30% reserve distance is required by aviation safety rules to account for headwinds, holding patterns, and emergency alternate landing sites. | Derived: Aeronautical safety rule, ERD `drones`, `ai_eta_predictions` |
| **BR-DW-10** | Single-flight non-overlap constraint: A drone can be assigned to at most one flight mission at any time. Any overlapping mission in `[scheduledDeparture - 15min, estimatedArrival + 20min]` results in immediate assignment rejection. | Baseline: SRS FR-16, UC-12 |
| **BR-DW-11** | Transition to `READY_FOR_TAKEOFF` occurs only after the origin station operator physically confirms receipt of the package at the station locker (UC-18). | Baseline: `business-processes.md` Section 2 |
| **BR-DW-12** | Takeoff transitions the order to `IN_TRANSIT`, the flight mission to `IN_AIR`, the drone status to `FLYING`, and releases the origin station slot (`OCCUPIED` $\rightarrow$ `EMPTY`). | Baseline: `business-processes.md` Section 2 |
| **BR-DW-13** | During `IN_TRANSIT`, real-time telemetry must be streamed to clients over SignalR WebSockets. If telemetry is lost for $> 60\text{ seconds}$, a warning is flagged on the Dispatcher dashboard. | Baseline: SRS FR-24, Derived telemetry watchdog |
| **BR-DW-14** | Map tracking displays origin station, destination station, planned flight path, and live coordinates. Customers can only view live tracking for their own orders; Dispatchers and Managers can view all active flights. | Baseline: SRS FR-25, NFR-01 (RBAC/Ownership) |
| **BR-DW-15** | An in-air mission abort automatically transitions order status to `FAILED` and flight mission to `ABORTED`. The drone must autonomously navigate to the nearest designated landing station. | Baseline: `business-processes.md` Section 4 |
| **BR-DW-16** | Upon drone landing at destination, status transitions to `ARRIVED`. If the recipient does not collect the package within 24 hours, the order transitions to `FAILED` with failure type `COLLECTION_TIMEOUT`. | Baseline: `business-processes.md` Section 4, Proposed timeout |
| **BR-DW-17** | A `FAILED` order remains in `FAILED` status until a Dispatcher explicitly selects one of the three resolution paths: Reschedule (`APPROVED`), Return (`CANCELLED`), or Cancel (`CANCELLED`). | Baseline: SRS FR-18, UC-14 |
| **BR-DW-18** | Terminal states `DELIVERED` and `CANCELLED` are immutable. No order in a terminal state may be transitioned to any other status. | Baseline: `business-processes.md` Section 1 |
| **BR-DW-19** | All order decisions made by human actors (Approval, Rejection, Drone Assignment, Cancellation, Failure Resolution) must generate an audit log entry in `audit_logs` storing user ID, action name, target entity ID, and client IP/user agent. | Baseline: SRS FR-32, UC-22 |
| **BR-DW-20** | When an order is completed (`DELIVERED`) or cancelled (`CANCELLED`), all associated temporary resources (reserved slots, assigned drones) must be freed immediately to prevent system deadlocks. | Derived: Resource leak prevention |
| **BR-DW-21** | Customers receive automated notification upon every key workflow transition: `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, and `FAILED`. | Baseline: SRS FR-12, UC-10 |
| **BR-DW-22** | Secure pickup code (6 chữ số / 6-digit numeric OTP) generated during order creation is never sent to Dispatchers or logged in telemetry/audit logs. It is delivered solely to the Customer and validated at destination locker arrival. | Baseline: NFR-01, NFR-02 (Security) |

---

## 6. Open decisions and source discrepancies

These items represent engineering decisions identified during delivery workflow modeling that require consensus between the lead architect, product owner, and hardware/drone team. They do not invalidate existing SRS requirements.

| Item | Finding in Sources | Proposed Solution / Agreement Needed |
|---|---|---|
| **Takeoff Trigger** | `business-processes.md` notes "Drone takes off $\rightarrow$ System sets `IN_TRANSIT`". It is ambiguous whether takeoff is triggered automatically by scheduled time or requires a manual Dispatcher "Clear for Takeoff" button. | **Proposed:** Automated takeoff command sent by Flight Engine when scheduled time arrives, drone is `READY_FOR_TAKEOFF`, and weather telemetry is clear. Dispatcher retains emergency "Hold Flight" abort switch. |
| **Turnaround Buffer** | SRS FR-16 requires non-overlapping drone missions but does not specify the ground turnaround buffer. | **Proposed:** 20-minute post-flight buffer added to calculated arrival time to account for battery swap/recharge, rotor safety check, and locker unloading before drone can be booked for another flight. |
| **Collection Timeout** | `business-processes.md` states "Package not collected in time $\rightarrow$ FAILED", but timeout duration is unstated. | **Proposed:** 24 hours from arrival notification timestamp. Automated SMS/push reminder sent at 12h and 20h. At 24h, locker slot flags `COLLECTION_TIMEOUT`. |
| **Return-to-Origin Terminal State** | In `business-processes.md` Section 4, a package returned to origin ends with status `CANCELLED` (with note "Returned to sender"). | **Proposed:** Keep status `CANCELLED` as defined in the ERD to avoid adding a new unapproved enum state, but store `cancel_reason = 'RETURNED_TO_ORIGIN'` for accurate analytics reporting. |
| **Weather Telemetry Ingestion** | SRS FR-18 notes flight failure due to weather, but weather API source is not defined in ERD. | **Proposed:** Dispatcher dashboard ingests METAR/wind data for hub coordinates. In-flight abort triggered if sustained winds exceed 12 m/s (24 knots) or heavy precipitation is detected by telemetry sensors. |

---

## 7. Review and acceptance checklist

This checklist defines verification criteria for accepting the Delivery Workflow functional documentation:

| Area | Review / Acceptance Scenario |
|---|---|
| **Coverage** | Functions F-01 through F-10 cover 100% of the Delivery Workflow scope: order listing, review/approval, state machine logging, departure scheduling, drone assignment, active monitoring, real-time tracking, failure detection, failure resolution, and fleet readiness. |
| **State Machine Alignment** | Transitions conform exactly to the 8 ERD statuses (`PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`). No unmapped statuses (such as `PICKED_UP` or `SCHEDULED`) are introduced. |
| **Actor Responsibilities** | Dispatcher permissions are strictly enforced on review, schedule, drone assignment, active monitoring, and failure resolution. Customers can only cancel in `PENDING`/`APPROVED` and can only view tracking for their own orders. |
| **Assignment Constraints** | Drone assignment enforces payload capacity, 30% range reserve, available destination landing slot, and strict non-overlap with any other flight mission of the same drone. |
| **Real-time Tracking** | Live telemetry streaming over SignalR WebSockets updates map position and dynamic ETA without requiring full page polling. |
| **Failure Resolution** | Failed deliveries provide three distinct paths (Reschedule to `APPROVED`, Return to origin, or Cancel to `CANCELLED`) with mandatory failure and resolution note logging. |
| **Audit & Notifications** | Every human decision writes to `audit_logs` (FR-32). Every status transition writes to `delivery_status_logs` (FR-14) and notifies the customer (FR-12). Secure codes are protected from staff logs. |

---
