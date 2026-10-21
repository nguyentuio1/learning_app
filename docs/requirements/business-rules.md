# Business Rules Register — SmartDroneDelivery

**Task:** LA-220 · **Parent:** LA-195

**Status:** Proposed for review

**Snapshot date:** 2026-10-21

**Scope:** Customer/order, delivery workflow, landing stations, user management, analytics/AI, audit and supporting integration/operations.

## 1. Purpose, authority and identifiers

This register collects rule IDs, rule text and the functions to which each rule applies. It does not implement APIs, change SRS policy, or approve numerical limits. Rule coverage is not proof that a function is implemented.

- Keep existing BR-DW, BR-LS and BR-UA identifiers; a reference must include its module. Do not renumber another module's source document.
- Assign BR-CO identifiers to the numbered, previously unlabelled business-rule items in LA-216, in section/item order. Section 3 records that crosswalk explicitly.
- Function references DW:F-01, LS:F-01 and UA:F-01 are distinct. CO:2.1 identifies the source subsection, not an invented function ID.
- **Baseline** means supported by a source requirement, not a claim of PR approval. **Derived/Proposed** means review is required. **Conflict** means do not hard-code the conflicting value until the owner resolves it.
- Imported text below records the source assertion. The resolution notes in section 8 qualify it, including source rows that call unsupported numeric policy “Baseline”.
- Sources already on develop take precedence for currently implemented behavior. Feature documents describe planned behavior. LA-92's documented admin-specific safeguards qualify the older UA rules without changing unrelated JWT endpoints.

## 2. Source snapshots and function directory

Pinned links remain readable before the source branches merge.

| Key | Source | Snapshot / function scope |
|---|---|---|
| SRS | [SRS](SRS.md), [Use cases](use-cases.md) | develop 609f609; FR-01–34, UC-01–23 |
| BP | [Business processes](../design/business-processes.md), [ERD](../architecture/erd.md) | develop 609f609; lifecycle and persisted states |
| CO | [LA-216 functional specification](https://github.com/nguyentuio1/learning_app/blob/4a1602e7444ebab9cc6a791d2de0f56b6919a490/docs/design/customer-order-management/functional-specification.md) | 4a1602e; 2.1 addresses, 2.2 packages, 2.3 create order, 2.4 update, 2.5 cancel, 2.6 list/details |
| DW | [LA-217 delivery workflow](https://github.com/nguyentuio1/learning_app/blob/8f994d972f8eeb908f293cedad9b1006ccb67b82/docs/requirements/functions-delivery-workflow.md) | 8f994d9; F-01–F-10 |
| LS | [LA-218 landing stations](https://github.com/nguyentuio1/learning_app/blob/30d97a4e4b017f8e0606f21e38dd816ba03185dd/docs/requirements/functions-landing-station.md) | 30d97a4; F-01–F-08 |
| UA | [LA-219 users and analytics](https://github.com/nguyentuio1/learning_app/blob/bedf982054c67eed29474e34e02ca5dc0c21aec0/docs/requirements/functions-user-management-and-analytics.md) | bedf982; F-01–F-19 |
| API | [Auth/profile contract at develop](../api/auth-api.md) | Existing registration, login, renewal, logout and profile behavior |
| ADMIN | [LA-92 contract](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/docs/api/auth-api.md#7-administrator-user-and-role-management-la-20--la-92) | aee5333; proposed merge, six user-management endpoints |

### DW functions

- DW:F-01 List pending delivery requests
- DW:F-02 Review and approve or reject delivery request
- DW:F-03 Enforce order status state machine and log history
- DW:F-04 Schedule delivery departure time
- DW:F-05 Validate and assign drone to flight mission
- DW:F-06 Monitor active deliveries and operational dashboard
- DW:F-07 Real-time telemetry tracking and map visualization
- DW:F-08 Detect and mark delivery failure
- DW:F-09 Resolve failed delivery (Reschedule, Return, or Cancel)
- DW:F-10 Maintain drone fleet operational status and readiness

### LS functions

- LS:F-01 List and inspect landing stations
- LS:F-02 Create, update, and manage landing station operational mode
- LS:F-03 Manage landing station slots inventory
- LS:F-04 Override slot operational state and maintenance isolation
- LS:F-05 Monitor real-time station and slot availability
- LS:F-06 Process package intake and confirm origin pickup
- LS:F-07 Verify pickup secure code and confirm destination delivery
- LS:F-08 Process destination pickup timeout and expired parcel exception

### UA functions

- UA:F-01 Register a Customer account
- UA:F-02 Log in
- UA:F-03 Renew a session
- UA:F-04 Log out
- UA:F-05 Authorize access by role and ownership
- UA:F-06 View and update own profile
- UA:F-07 Change own password
- UA:F-08 Upload own avatar
- UA:F-09 Find users and view account details
- UA:F-10 Create a user as Administrator
- UA:F-11 Assign a role
- UA:F-12 Deactivate a user
- UA:F-13 View filtered delivery dashboard
- UA:F-14 View a filtered report and delivery detail
- UA:F-15 Estimate, update and view delivery ETA
- UA:F-16 Generate and view a delivery-history summary
- UA:F-17 Ask the Customer AI assistant
- UA:F-18 Record a sensitive action
- UA:F-19 Search audit logs

## 3. Customer and Order rules (LA-216 crosswalk)

Each row identifies the original subsection and numbered Business Rules item. Where the source specifies a proposed limit or conflicts with another module, the rule remains unresolved.

| ID | Rule text | Applies to / original item | Classification |
|---|---|---|---|
| BR-CO-01 | Required fields must not be null or empty. Validate character limits, phone number format, and coordinate ranges. | CO:2.1, item 1 | Source requirement / derived validation |
| BR-CO-02 | Each customer may have at most one address with `IsDefault = true`. Setting an address as the default must unset the previous default address at the same time. | CO:2.1, item 2 | Source requirement / derived validation |
| BR-CO-03 | Reading, updating, or deleting another customer's address is prohibited. | CO:2.1, item 3 | Source requirement / derived validation |
| BR-CO-04 | The deletion policy for addresses referenced by orders must be confirmed; deletion must not remove delivery history or information. | CO:2.1, item 4 | Proposed |
| BR-CO-05 | A limit of 10 addresses per customer is proposed; confirm it as described in Section 4. | CO:2.1, item 5 | Proposed |
| BR-CO-06 | Maximum dimensions are **40 × 30 × 30 cm** for `LengthCm`, `WidthCm`, and `HeightCm`; each dimension and the weight must be greater than 0. | CO:2.2, item 1 | Conflict / design policy — D-01 to D-03 |
| BR-CO-07 | Maximum payload is **5.0 kg** (`WeightGram <= 5000`). Revalidate the limit when creating an order. | CO:2.2, item 2 | Conflict / design policy — D-01 to D-03 |
| BR-CO-08 | The relationship is **One Package One Order**: a package may be linked to at most one order. | CO:2.2, item 3 | Conflict / design policy — D-01 to D-03 |
| BR-CO-09 | Deleting a package referenced by an order is prohibited (**RESTRICT**). | CO:2.2, item 4 | Source requirement / derived validation |
| BR-CO-10 | Only the package owner may view or update it. An update must not violate the limits or the validity of an order to which the package is linked. | CO:2.2, item 5 | Source requirement / derived validation |
| BR-CO-11 | The server validates package/address ownership, both station statuses, and drone limits of **5.0 kg, 40 × 30 × 30 cm**. | CO:2.3, item 1 | Conflict / design policy — D-01 to D-03 |
| BR-CO-12 | The origin and destination stations must be different and both `OPERATIONAL` when the request is processed. | CO:2.3, item 2 | Source requirement / derived validation |
| BR-CO-13 | Generate a unique `TrackingNumber` and a random secure code consisting of exactly 6 digits. The specific tracking number format remains to be confirmed as described in Section 4. | CO:2.3, item 3 | Proposed |
| BR-CO-14 | A new order has `PENDING` status. | CO:2.3, item 4 | Source requirement / derived validation |
| BR-CO-15 | Write the initial `DeliveryStatusLog` (`FromStatus = null`, `ToStatus = PENDING`) in the same transaction as the order. | CO:2.3, item 5 | Source requirement / derived validation |
| BR-CO-16 | After successful order creation, create a notification for the customer according to FR-12/UC-10. Do not create an incorrect status notification if saving the order fails. | CO:2.3, item 6 | Source requirement / derived validation |
| BR-CO-17 | A package cannot be linked to a second order. | CO:2.3, item 7 | Conflict / design policy — D-01 to D-03 |
| BR-CO-18 | Updates are allowed only if the status is still `PENDING` when the update is saved (FR-10). | CO:2.4, item 1 | Source requirement / derived validation |
| BR-CO-19 | Only the four fields in the DTO may be updated; changing the package or status is prohibited. | CO:2.4, item 2 | Source requirement / derived validation |
| BR-CO-20 | The new destination station must exist, be `OPERATIONAL`, and differ from the origin station. | CO:2.4, item 3 | Source requirement / derived validation |
| BR-CO-21 | If provided, the delivery address must belong to the CUSTOMER making the request. | CO:2.4, item 4 | Source requirement / derived validation |
| BR-CO-22 | If the request contains fields outside the allowed scope, reject it instead of silently ignoring them. | CO:2.4, item 5 | Source requirement / derived validation |
| BR-CO-23 | Cancellation is allowed only when the current status is `PENDING` or `APPROVED`. | CO:2.5, item 1 | Source requirement / derived validation |
| BR-CO-24 | Save `CancellationReason` on the order and write a `DeliveryStatusLog` from the previous status to `CANCELLED`, including the actor and timestamp. | CO:2.5, item 2 | Source requirement / derived validation |
| BR-CO-25 | Update the status and write the log in the same transaction. After success, create a notification for the customer according to FR-12. | CO:2.5, item 3 | Source requirement / derived validation |
| BR-CO-26 | Cancellation is prohibited for `IN_TRANSIT`, completed, or already-canceled orders. Repeated requests must not create duplicate logs or notifications. | CO:2.5, item 4 | Source requirement / derived validation |
| BR-CO-27 | Always scope results to the customer ID from the JWT; do not use a customer ID supplied by the client to expand the scope. | CO:2.6, item 1 | Source requirement / derived validation |
| BR-CO-28 | Support filtering by status, creation date, and keyword; validate `fromDate <= toDate`. By default, sort newest creation time first. | CO:2.6, item 2 | Source requirement / derived validation |
| BR-CO-29 | Apply pagination with `page >= 1` and `1 <= pageSize <= 100`. | CO:2.6, item 3 | Source requirement / derived validation |
| BR-CO-30 | Return `StatusLogs[]` in ascending chronological order. ETA/AI predictions may be unavailable, in which case return null. | CO:2.6, item 4 | Source requirement / derived validation |
| BR-CO-31 | Order-related notification information follows FR-12/UC-10; data belonging to other customers must never be returned. | CO:2.6, item 5 | Source requirement / derived validation |
| BR-CO-32 | Real-time drone location is an FR-24 tracking capability and is distinct from the ETA/history snapshot in the details response. | CO:2.6, item 6 | Source requirement / derived validation |

## 4. Delivery Workflow

| ID | Source rule text | Applies to | Classification / qualification |
|---|---|---|---|
| BR-DW-01 | The order status lifecycle follows strictly the 8 defined states: `PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`. No intermediate status (such as `PICKED_UP` or `SCHEDULED`) is permitted in the database. | DW:F-01, DW:F-02, DW:F-03 | Baseline: ERD (`erd.md`), `business-processes.md` |
| BR-DW-02 | Orders in `PENDING` state may only be reviewed by users with `DISPATCHER` or `ADMIN` role. Approval moves the order to `APPROVED`; rejection records a mandatory reason and moves the order to `CANCELLED`. | DW:F-01, DW:F-02 | Baseline: SRS FR-13, UC-11 |
| BR-DW-03 | Customers can only cancel an order while it is in `PENDING` or `APPROVED` status. Once an order reaches `READY_FOR_TAKEOFF`, only the Dispatcher can cancel it (requiring physical package offloading at the station). | DW:F-02 | Baseline: `business-processes.md` Section 3 |
| BR-DW-04 | Every single status change must append a record into `delivery_status_logs` containing `order_id`, `from_status`, `to_status`, `changed_by_user_id` (or NULL for automated triggers), `reason`, and UTC timestamp. Status log rows are append-only and cannot be updated or deleted. | DW:F-02, DW:F-03 | Baseline: SRS FR-14, ERD |
| BR-DW-05 | Departure time scheduling requires `scheduled_departure_time >= current_time + 15 minutes` to allow station operators physical package handling, inspection, and drone loading. | DW:F-04 | Proposed 15-minute buffer (D-04) |
| BR-DW-06 | A flight mission requires reserving an available landing slot at the destination station prior to takeoff. The destination slot status is set to `RESERVED` upon drone assignment and transitions to `OCCUPIED` upon drone touchdown. | DW:F-04, DW:F-05 | Baseline: `business-processes.md`, ERD `station_slots` |
| BR-DW-07 | A drone is eligible for mission assignment only if `drone.status IN ('IDLE', 'CHARGING')` and `drone.battery_percentage >= 80%` (or projected to reach $\ge 80\%$ by the scheduled departure time). Drones in `MAINTENANCE` or `FLYING` are strictly ineligible. | DW:F-05, DW:F-10 | Proposed battery eligibility (D-04) |
| BR-DW-08 | Physical payload constraint: `drone.max_payload_weight >= package.weight`. Package dimensions (length, width, height) must fit within the standardized drone cargo bay dimensions ($30 \times 20 \times 15\text{ cm}$). | DW:F-05, DW:F-10 | Conflict: package dimensions (D-01) |
| BR-DW-09 | Range constraint: `drone.max_flight_range >= route_distance_km * 1.30`. The 30% reserve distance is required by aviation safety rules to account for headwinds, holding patterns, and emergency alternate landing sites. | DW:F-05 | Proposed 30% margin; no verified aviation regulation cited (D-04) |
| BR-DW-10 | Single-flight non-overlap constraint: A drone can be assigned to at most one flight mission at any time. Any overlapping mission in `[scheduledDeparture - 15min, estimatedArrival + 20min]` results in immediate assignment rejection. | DW:F-05 | Baseline non-overlap; proposed 15/20-minute buffers (D-04) |
| BR-DW-11 | Transition to `READY_FOR_TAKEOFF` occurs only after the origin station operator physically confirms receipt of the package at the station locker (UC-18). | DW:F-06, DW:F-07 | Baseline: `business-processes.md` Section 2 |
| BR-DW-12 | Takeoff transitions the order to `IN_TRANSIT`, the flight mission to `IN_AIR`, the drone status to `FLYING`, and releases the origin station slot (`OCCUPIED` $\rightarrow$ `EMPTY`). | DW:F-06, DW:F-07 | Baseline: `business-processes.md` Section 2 |
| BR-DW-13 | During `IN_TRANSIT`, real-time telemetry must be streamed to clients over SignalR WebSockets. If telemetry is lost for $> 60\text{ seconds}$, a warning is flagged on the Dispatcher dashboard. | DW:F-07 | Baseline streaming; proposed 60-second timeout (D-04) |
| BR-DW-14 | Map tracking displays origin station, destination station, planned flight path, and live coordinates. Customers can only view live tracking for their own orders; Dispatchers and Managers can view all active flights. | DW:F-07 | Baseline: SRS FR-25, NFR-01 (RBAC/Ownership) |
| BR-DW-15 | An in-air mission abort automatically transitions order status to `FAILED` and flight mission to `ABORTED`. The drone must autonomously navigate to the nearest designated landing station. | DW:F-08 | Baseline failure state; hardware control is outside SRS scope (D-05) |
| BR-DW-16 | Upon drone landing at destination, status transitions to `ARRIVED`. If the recipient does not collect the package within 24 hours, the order transitions to `FAILED` with failure type `COLLECTION_TIMEOUT`. | DW:F-08, DW:F-09 | Baseline arrival; proposed 24-hour timeout (D-04) |
| BR-DW-17 | A `FAILED` order remains in `FAILED` status until a Dispatcher explicitly selects one of the three resolution paths: Reschedule (`APPROVED`), Return (`CANCELLED`), or Cancel (`CANCELLED`). | DW:F-09 | Baseline: SRS FR-18, UC-14 |
| BR-DW-18 | Terminal states `DELIVERED` and `CANCELLED` are immutable. No order in a terminal state may be transitioned to any other status. | DW:F-03, DW:F-09 | Baseline: `business-processes.md` Section 1 |
| BR-DW-19 | All order decisions made by human actors (Approval, Rejection, Drone Assignment, Cancellation, Failure Resolution) must generate an audit log entry in `audit_logs` storing user ID, action name, target entity ID, and client IP/user agent. | DW:F-02, DW:F-03, DW:F-05, DW:F-08, DW:F-09 | Baseline: SRS FR-32, UC-22 |
| BR-DW-20 | When an order is completed (`DELIVERED`) or cancelled (`CANCELLED`), all associated temporary resources (reserved slots, assigned drones) must be freed immediately to prevent system deadlocks. | DW:F-10 | Derived cleanup; release only when physical occupancy is cleared (D-06) |
| BR-DW-21 | Customers receive automated notification upon every key workflow transition: `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, and `FAILED`. | DW:F-02, DW:F-03, DW:F-08, DW:F-09 | Baseline: SRS FR-12, UC-10 |
| BR-DW-22 | Secure pickup code (6-digit alphanumeric) generated during order creation is never sent to Dispatchers or logged in telemetry/audit logs. It is delivered solely to the Customer and validated at destination locker arrival. | DW:F-07, LS:F-07 | Conflict: secure-code format (D-02) |

## 5. Landing Station

| ID | Source rule text | Applies to | Classification / qualification |
|---|---|---|---|
| BR-LS-01 | Landing stations must have one of three status values: `OPERATIONAL`, `MAINTENANCE`, `OFFLINE`. Stations in `MAINTENANCE` or `OFFLINE` are completely excluded from flight scheduling and cannot accept new customer delivery orders. | LS:F-01, LS:F-02 | Baseline: ERD (`erd.md`), SRS FR-20 |
| BR-LS-02 | Station codes (`station_code`) must be uppercase alphanumeric strings (3–10 characters) and globally unique across the system (enforced by database unique constraint). | LS:F-01, LS:F-02, LS:F-05 | Unique code baseline; uppercase/3–10 format requires confirmation (D-04) |
| BR-LS-03 | When a station is placed into `MAINTENANCE` while active missions are currently flying towards it (`flight_status == 'IN_AIR'`), the system must immediately raise an **Urgent Reroute Alert** on the Dispatcher dashboard so the flight can be redirected to a designated alternate station. | LS:F-02 | Baseline: Activity diagram (`activity-station-and-pickup.md`), Derived safety rule |
| BR-LS-04 | A station in `MAINTENANCE` can be restored to `OPERATIONAL` only after physical maintenance is completed and all active landing pads pass automated sensor diagnostic self-tests. | LS:F-02 | Proposed sensor integration; outside current software implementation (D-05) |
| BR-LS-05 | Station slots must strictly have one of four status values: `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`. No unapproved status string is permitted. | LS:F-03, LS:F-04, LS:F-05 | Baseline: ERD (`erd.md`), SRS FR-21 |
| BR-LS-06 | Slot maintenance isolation: An `EMPTY` slot may be set to `ERROR` for cleaning, sensor testing, or pad maintenance. A slot in `RESERVED` or `OCCUPIED` status cannot be placed into `ERROR` directly without rerouting the scheduled flight or physically removing the stored parcel. | LS:F-03, LS:F-04 | Baseline: Operator user flow (`station-operator.md`), Activity diagram |
| BR-LS-07 | A slot in `ERROR` status is returned to `EMPTY` only after the station operator completes physical maintenance and confirms sensor readiness in the mobile terminal. | LS:F-04 | Baseline: Operator user flow (`station-operator.md`) |
| BR-LS-08 | Real-time available landing capacity of a station is strictly the count of slots in `EMPTY` status. Slots in `RESERVED`, `OCCUPIED`, or `ERROR` do not count as available capacity. If a hub has zero `EMPTY` slots, new reservations are blocked. | LS:F-05 | Derived: Anti-deadlock capacity rule, SRS FR-22 |
| BR-LS-09 | Origin package intake requires the order to be in `APPROVED` status. An order that is `PENDING`, `CANCELLED`, or already departed cannot be checked in at the station. | LS:F-06 | Baseline: Business processes (`business-processes.md`) |
| BR-LS-10 | Physical weight safety rule: `actual_weight <= 3.0 kg` AND `actual_weight <= assigned_drone.max_payload_weight`. If actual weight exceeds the assigned drone's payload limit, intake is rejected with reason "Weight exceeds drone payload limit". | LS:F-06 | Conflict: 3 kg versus CO 5 kg (D-01) |
| BR-LS-11 | Physical dimensions safety rule: Package length $\le 30\text{ cm}$, width $\le 20\text{ cm}$, height $\le 15\text{ cm}$. Over-sized parcels that cannot fit inside the drone cargo bay are rejected at intake. | LS:F-06 | Conflict: dimensions (D-01) |
| BR-LS-12 | Origin pickup confirmation transitions the order to `READY_FOR_TAKEOFF`, sets the origin station slot to `OCCUPIED`, and sets the drone status to `ASSIGNED`. The origin slot remains `OCCUPIED` until drone liftoff. | LS:F-06 | Baseline: `business-processes.md` Section 2 |
| BR-LS-13 | Once package intake is confirmed (`READY_FOR_TAKEOFF`), the customer is prohibited from cancelling the order via the app. Only the Dispatcher can cancel the order, which requires the station operator to physically retrieve and return the parcel. | LS:F-06 | Baseline: `business-processes.md` Section 3 |
| BR-LS-14 | Package delivery handover at destination station requires the order to be in `ARRIVED` status and the destination slot to be `OCCUPIED`. | LS:F-07 | Baseline: Business processes (`business-processes.md`), UC-08 |
| BR-LS-15 | Secure code verification: The recipient must enter the exact 6-character alphanumeric code matching `delivery_orders.secure_code`. Case sensitivity is strictly enforced. | LS:F-07 | Conflict: alphanumeric versus numeric code (D-02) |
| BR-LS-16 | Brute-force lockout defense: Entering an incorrect secure code increments a failed attempt counter. After 3 consecutive incorrect attempts within 10 minutes, verification is locked out for 10 minutes, and a security alert notification is dispatched to the customer. | LS:F-07 | Proposed lockout thresholds; storage/API not implemented (D-04) |
| BR-LS-17 | Upon successful secure code verification: the locker compartment unlocks, order status transitions to `DELIVERED`, `actual_delivery_time` is recorded with the exact UTC timestamp, and the destination slot transitions to `EMPTY`. | LS:F-07 | Baseline: SRS FR-26, FR-27, ERD |
| BR-LS-18 | Drone post-landing state: Upon completion of unloading, the drone transitions from the destination pad to status `CHARGING` (if battery $< 90\%$) or `IDLE` (if battery $\ge 90\%$). | LS:F-07 | Baseline readiness states; proposed 90% threshold (D-04) |
| BR-LS-19 | Destination pickup timeout: A parcel in `ARRIVED` status that remains uncollected after 24 hours transitions to `FAILED` with failure type `COLLECTION_TIMEOUT`. | LS:F-08 | Proposed 24-hour collection timeout (D-04) |
| BR-LS-20 | An expired uncollected parcel (`FAILED`) keeps the destination locker compartment locked. The station operator must physically transfer the parcel to long-term storage or prepare it for a return flight as instructed by the Dispatcher. | LS:F-08 | Baseline: `business-processes.md` Section 4 |
| BR-LS-21 | Every station operational decision (station status change, slot maintenance override, package intake confirmation, delivery handover) must record an immutable audit log entry in `audit_logs`. | LS:F-02, LS:F-03, LS:F-04, LS:F-06, LS:F-07 | Baseline: SRS FR-32, UC-22 |
| BR-LS-22 | Confidentiality of secure code: The pickup secure code is never displayed in Station Operator task lists, Dispatcher dashboards, or audit logs. It is communicated exclusively to the Customer and validated blindly by the system. | LS:F-07 | Baseline: NFR-01, NFR-02 Security |

## 6. User Management and Analytics

| ID | Source rule text | Applies to | Classification / qualification |
|---|---|---|---|
| BR-UA-01 | Each user has one of CUSTOMER, DISPATCHER, STATION_OPERATOR, MANAGER or ADMIN. Public registration assigns CUSTOMER; it cannot grant a staff role. | UA:F-01, UA:F-05, UA:F-09, UA:F-10, UA:F-11 | Baseline: auth contract, ERD, Administrator flow |
| BR-UA-02 | Registration identity fields follow the auth contract: unique email up to 255 characters, required full name up to 150, unique phone of 9–11 digits. Profile updates may retain the user's own phone number. | UA:F-01, UA:F-06, UA:F-10 | Baseline: auth contract/DTOs |
| BR-UA-03 | New passwords contain 8–100 characters, at least one letter and one digit; store only a BCrypt hash, never log/return the plain password. | UA:F-01, UA:F-02, UA:F-07, UA:F-10 | Baseline: SRS and auth contract |
| BR-UA-04 | Only ACTIVE accounts may log in, refresh and use the own-profile APIs. Unknown email and wrong login password receive the same message. | UA:F-02, UA:F-05, UA:F-06, UA:F-12 | Baseline: AuthService/UserService; wider status enforcement tracked in Section 7 |
| BR-UA-05 | Access tokens are short-lived; refresh tokens rotate on renewal. The API contract proposes 15 minutes and 7 days; use agreed server configuration, not hard-coded client assumptions. | UA:F-02, UA:F-03, UA:F-04 | Baseline: FR-02/auth contract, durations proposed there |
| BR-UA-06 | An unknown, expired or revoked refresh token cannot renew a session. Concurrent client requests should share renewal and retry at most once, without an infinite authentication loop. | UA:F-03 | Baseline token validity; derived client coordination |
| BR-UA-07 | Validate authorization at the API as well as the UI. Role changes apply to newly issued tokens; existing access tokens may retain old claims until expiry. | UA:F-03, UA:F-05, UA:F-11 | Baseline JWT behavior; admin exception BR-AD-01 |
| BR-UA-08 | Logout revokes the caller's submitted refresh token; password change revokes all their refresh tokens. Neither operation is a claim of immediate JWT access-token revocation. | UA:F-04, UA:F-07 | Baseline: auth contract/current services |
| BR-UA-09 | Own-profile operations resolve the user from authentication. Only name/phone and the dedicated password/avatar operations are editable; email, role and status are not editable through profile update. | UA:F-06, UA:F-08 | Baseline: FR-04/auth contract |
| BR-UA-10 | Password change verifies the current password and rejects an unchanged new password. The exact wrong-current-password 401 is a field error, not automatic session expiry. | UA:F-03, UA:F-07 | Baseline: auth contract/UserService |
| BR-UA-11 | Avatar upload accepts non-empty JPEG/PNG up to 5 MiB with server-side content checks and stores the image in MinIO. No avatar-delete endpoint is implied. | UA:F-08 | Baseline: auth contract/UserService |
| BR-UA-12 | Only ADMIN creates staff accounts, assigns roles or deactivates accounts. The system revalidates the target and authorization when saving. | UA:F-09, UA:F-10, UA:F-11, UA:F-12 | Baseline: FR-05/Administrator flow; derived revalidation |
| BR-UA-13 | Deactivation sets INACTIVE rather than deleting an account's business history. Reactivation, suspension and last-admin safeguards need separate policy decisions. | UA:F-12 | Baseline history retention; admin safeguards now BR-AD-02/03 |
| BR-UA-14 | A date range must be valid and ordered. UI/API must agree on timezone, inclusion boundaries and the date field used to select records. | UA:F-13, UA:F-14, UA:F-19 | Derived; precise analytics definitions pending |
| BR-UA-15 | Analytics, ETA viewing, summaries and chat are informational: they do not approve/cancel deliveries, confirm pickup or control drone flight. | UA:F-05, UA:F-13, UA:F-14, UA:F-15, UA:F-16, UA:F-17 | Baseline: SRS scope and Manager flow |
| BR-UA-16 | FR-28 requires all four measures: orders by status, success rate, average delivery time and station usage. Dashboard/report definitions must agree; zero-denominator measures show N/A. | UA:F-13, UA:F-14 | Baseline measures; derived empty-value behavior |
| BR-UA-17 | ETA is an estimate, not actual arrival. Validate input freshness and mark unavailable/stale results. The current EF design stores one prediction per order, not prediction history. | UA:F-15 | Baseline FR-29/ERD; derived freshness behavior |
| BR-UA-18 | Customers access only their own orders, sessions and messages; staff access follows the relevant operational role. Verify scope before retrieving AI context, not after generating an answer. | UA:F-05, UA:F-15, UA:F-16, UA:F-17 | Baseline FR-31/UC-20; derived enforcement placement |
| BR-UA-19 | Sensitive actions have server-attributed audit events. Logs must not include passwords, refresh/access tokens or pickup secure codes. | UA:F-01, UA:F-02, UA:F-04, UA:F-07, UA:F-10, UA:F-11, UA:F-12, UA:F-18, UA:F-19 | Baseline FR-32/auth contract; derived data minimization |
| BR-UA-20 | Administrator audit views are read-only and support user/action/date searches. Retention and privileged maintenance policies are outside the viewer. | UA:F-18, UA:F-19 | Baseline FR-33/Administrator flow |
| BR-UA-21 | A chat session belongs to one user; messages belong to that session and use USER/BOT sender types. Never accept an arbitrary session ID as proof of ownership. | UA:F-17 | Baseline ERD; derived ownership enforcement |
| BR-UA-22 | User and audit lists use bounded pagination; never expose password hashes or token records in account details or list results. Exact limits belong to their API contracts. | UA:F-09, UA:F-19 | Derived pagination proposal; current admin API returns a full array (D-07) |
| BR-UA-23 | Keep these order states distinct: PENDING, APPROVED, READY_FOR_TAKEOFF, IN_TRANSIT, ARRIVED, DELIVERED, CANCELLED, FAILED. ARRIVED is not completed delivery; mission statuses are not order statuses. | UA:F-13, UA:F-14, UA:F-15, UA:F-16 | Baseline SRS/ERD |
| BR-UA-24 | AI summaries/answers use verified authorized facts, distinguish estimates from actual events and report missing data or service failure honestly. AI output does not become an authoritative status update. | UA:F-16, UA:F-17 | Derived from FR-29 to FR-31 and business-oriented scope |

## 7. Supplemental rules and implementation clarifications

These IDs cover input/ownership requirements outside the imported numbered tables and SRS integration/operations boundaries. They do not silently expand implementation scope.

| ID | Rule text | Applies to | Source / classification |
|---|---|---|---|
| BR-CO-33 | Address label/contact/address line are required; label <=50, contact name <=150, phone format per agreed contract <=20; latitude/longitude must both be omitted or provided, within ±90/±180. Ownership is derived from authentication. | CO:2.1 create/update/list/delete/default | CO inputs / proposed field contract |
| BR-CO-34 | Recipient name is required <=150, phone required <=20 and valid per agreed format; cancellation reason is nonblank <=500. Validate again when saving, not only on screen. | CO:2.3–2.5 | CO input tables |
| BR-CO-35 | Package name is required <=200; optional image reference must be valid; flag defaults false. Weight/dimensions are positive; upper bounds unresolved under D-01. | CO:2.2–2.3 | CO input table / conflict |
| BR-CO-36 | Customer-owned addresses/packages/orders require ownership checks; nonexistent and foreign resources must not reveal foreign details. | CO:2.1–2.6 | SRS FR-06–11 / CO access requirements |
| BR-DW-23 | Approval/rejection requires a current PENDING order; rejection/failure resolution notes are nonblank, maximum 500 as proposed by DW input tables. Validate station availability and state again on save. | DW:F-02, F-08, F-09 | SRS FR-13/18; derived validation |
| BR-DW-24 | Telemetry latitude/longitude are within ±90/±180 and battery within 0–100; reject invalid input and mark old/unavailable coordinates rather than fabricate a live position. | DW:F-06, F-07 | DW validation / derived freshness |
| BR-DW-25 | Drone payload and range must be positive; fleet maintenance cannot interrupt an in-air mission through a direct status edit. | DW:F-10 | SRS FR-19 / DW validation |
| BR-LS-23 | Slot numbers are unique within a station; occupied or reserved slots cannot be removed directly. Operator writes are scoped to the assigned station. | LS:F-01–F-04 | LS validation / ERD |
| BR-LS-24 | Intake requires the correct origin station, scheduled/assigned order, safe package checklist and a usable origin slot. Recheck prerequisites atomically when confirming. | LS:F-06 | UC-18/BP; clarifies that APPROVED alone is not sufficient |
| BR-AD-01 | Each admin management request requires ADMIN in the token and a current ACTIVE ADMIN account in the database. Demoted/deactivated actors cannot reuse an old admin token on these endpoints. | UA:F-09–F-12; admin update user | LA-92 implementation/contract; does not alter generic JWT semantics |
| BR-AD-02 | An admin cannot deactivate themselves or change their own admin role; reject with 409. Setting the same role is a no-op. | UA:F-11, F-12 | LA-92 implemented safeguard |
| BR-AD-03 | Keep at least one ACTIVE admin; serialize role/deactivation checks and saves so concurrent admin mutations cannot remove every admin. | UA:F-11, F-12 | LA-92 PostgreSQL transaction policy |
| BR-AD-04 | Admin creates ACTIVE users, hashes passwords, validates the five roles, rejects duplicate normalized email/phone with the exact 409 contract; admin detail update cannot change password/role/status/avatar. | UA:F-10; admin update user | LA-92 API |
| BR-AD-05 | Save CREATE_USER, USER_UPDATE, ROLE_CHANGE and USER_DEACTIVATE audits with successful changes. Reads/rejections/no-ops do not create mutation audits. Deactivation revokes refresh tokens but retains history. | UA:F-10–F-12, F-18; admin update user | LA-92 API |
| BR-INT-01 | External APIs must be documented, versioned and authenticated with API keys; an API key is not unrestricted ownership or administrative authority. | External order creation/tracking, UC-05/07 | SRS FR-34; authorization details proposed |
| BR-INT-02 | External integrations must validate inputs and obey the same ownership/status/uniqueness rules as first-party clients. API key provisioning, scopes, rotation and rate limits need an agreed contract. | External order creation/tracking | Derived; no new endpoint path invented |
| BR-OPS-01 | Only an authorized Administrator may inspect system health and initiate/view database backups. | UC-23; System monitoring screen | Use cases / NFR-08 |
| BR-OPS-02 | A failed or slow dependency must not appear healthy; failed/running backups must not appear completed. Do not expose credentials in health/error details. | UC-23; health/backup status | Derived truthful status/security |
| BR-OPS-03 | A backup's retention, schedule, restore authorization and recovery objectives require an operations policy; initiating backup is not evidence restore succeeds. | UC-23; backup/restore | Proposed operational policy |
| BR-NOT-01 | Notifications are scoped to the recipient; listing or marking read must not expose or modify another customer's notifications. State-change notifications follow a successful state save. | UC-10; CO:2.3/2.5; DW:F-03 | SRS FR-12 / derived ownership |
| BR-AI-01 | Empty analytics return zero counts and N/A undefined ratios; failed loads are errors, not empty data. Date cohort, reporting timezone and formulas must be approved before metric acceptance. | UA:F-13, F-14 | UA proposed metric definitions, D-08 |

## 8. Conflicts and decisions requiring approval

| ID | Conflicting / incomplete sources | Safe interpretation pending decision |
|---|---|---|
| D-01 | CO specifies 5 kg and 40×30×30 cm; LS specifies 3 kg and 30×20×15 cm; DW uses 30×20×15 cm. SRS only requires fitting the assigned drone. | Preserve each source value here; do not choose a global numeric limit. Agree fleet/packaging capacities and units (CO grams, LS kilograms) across creation, assignment and intake. |
| D-02 | CO specifies six digits; DW says “6-digit alphanumeric”; LS six case-sensitive alphanumeric characters. | Confirm alphabet/length, recipient channel, retry and lockout policy. Never expose a secure code to unauthorized staff/logs/AI. |
| D-03 | CO asserts one package per order and prohibits reuse; rescheduling creates a new mission and DW says clear prior mission link. | Preserve historical attempts; confirm ERD relationship and how return/retry history is represented. A return reaches CANCELLED only after origin receipt, not at the moment Return is chosen. |
| D-04 | DW/LS contain fixed battery, range, lead-time, turnaround, timeout, station-code and lockout values absent from SRS. | Treat numerical values as proposals, even when a source row calls them Baseline. No claim of regulatory compliance is made. |
| D-05 | DW/LS describe autopilot commands, locks and sensors while SRS excludes flight-hardware control. | Specify software events/operator confirmation; hardware actuation and identity-document collection need separate scope/privacy approval. |
| D-06 | DW immediate cleanup versus LS keeping an uncollected parcel in an occupied locker. | Do not mark physically occupied capacity EMPTY just because order status is terminal; confirm handover/unloading first. |
| D-07 | UA suggests bounded pagination; current LA-92 GET /api/users returns an array and web paginates locally. | Document actual API as-is. Server pagination is a future contract change, not an already implemented requirement. |
| D-08 | Success-rate denominator, date cohort, average delivery interval and station utilization are not fixed in SRS. | UA section 7.1 offers candidates only. Slot snapshots cannot supply historical occupied-slot percentages. |
| D-09 | UA leaves self/last-admin safeguards unresolved; LA-92 now supplies them. | Use BR-AD-01–05 for the reviewed LA-92 API; general RBAC remains token-based until renewal. |
| D-10 | Source labels COMPLETED in a CO exception; ERD order terminal state is DELIVERED. | Use DELIVERED for orders; COMPLETED is a mission state, not an added order state. |
| D-11 | SRS actor table includes Customer/Operator web but current web guides those roles to mobile. | Confirm client allocation without inventing implemented web screens. |
| D-12 | Address cap/default deletion, tracking-number format, multi-status filters, AI thresholds/retention, API-key lifecycle and backup policy remain open. | Keep source proposals visible; owners approve details before implementation acceptance. |

## 9. Requirement coverage and review

| Requirement group | Rule families / applicable functions |
|---|---|
| FR-01–05 | BR-UA-01–13, BR-AD-01–05; UA:F-01–12 |
| FR-06–12 | BR-CO-01–36, BR-NOT-01, BR-DW-21; CO:2.1–2.6 / UC-10 |
| FR-13–19 | BR-DW-01–25; DW:F-01–10 |
| FR-20–23 | BR-LS-01–24; LS:F-01–06 |
| FR-24–27 | BR-DW-11–14/22/24, BR-LS-14–22; DW:F-07, LS:F-07/08 |
| FR-28–31 | BR-UA-14–18/21/23/24, BR-AI-01; UA:F-13–17 |
| FR-32–33 | BR-UA-19/20/22, BR-AD-05, BR-DW-19, BR-LS-21; UA:F-18/19 |
| FR-34 | BR-INT-01/02; external UC-05/07 |
| NFR-01/02/08 | Security/ownership rules above; BR-OPS-01–03 / UC-23 |

Review checks: every row has a unique ID, rule text, applicable function and source/classification; all 24 UA, 22 DW and 22 LS source rule IDs are retained; all 32 numbered CO rules have an explicit crosswalk; supplemental rows cover embedded validation and integration/operations boundaries. No source module document is rewritten by LA-220. Product approval of open decisions remains separate from completion of this register.
