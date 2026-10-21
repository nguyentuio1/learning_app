# Functional Specification — Customer & Order Management

| Attribute | Value |
|---|---|
| Task | LA-216 — Functions of Customer and Order module |
| Scope | Customer & Order Management |
| Traceability Sources | [SRS](../../requirements/SRS.md), [Use Cases](../../requirements/use-cases.md) |
| Related Design Documents | [Class Specification](./class-specification.md), [Create Order Sequence](./sequence-create-order.md), [Update/Cancel Order Sequence](./sequence-update-cancel-order.md) |

## 1. Scope & Boundaries

This document defines the purpose, actors, preconditions, inputs/outputs, business rules, and exceptional responses for six functional areas:

1. Address Management — manage delivery addresses.
2. Package Management — manage packages.
3. Order Creation — create orders.
4. Order Update — update orders.
5. Order Cancellation — cancel orders.
6. Order Listing & Details — view order lists and details.

The primary actor for these functions is the customer (`CUSTOMER`). A valid JWT is required for authentication. Ownership of orders, addresses, and packages is determined from the authenticated identity, not from data supplied by the client.

This specification describes the functional contract; it does not include C# source code, API/controller implementation, UI components, or database migrations. The DTO and field names below define the logical contract. Any limit labeled as “proposed” must be confirmed if it is not specified in the source requirements. `Guid`, `decimal`, `bool`, `int`, `string`, and `DateTime` represent an identifier, decimal number, Boolean, integer, string, and ISO 8601/UTC timestamp, respectively.

### General Access Requirements

- Missing, invalid, or expired JWT: `401 Unauthorized`.
- An authenticated user without the `CUSTOMER` role, or an inactive account: `403 Forbidden`.
- Do not return another user's data. Both nonexistent resources and resources not owned by the caller may return `404 Not Found` to avoid revealing ownership (**BR-CO-36**).
- Use `400 Bad Request` for invalid input data/business conditions and `409 Conflict` for state, uniqueness, or resource-relationship conflicts.

## 2. Detailed Functional Specs

### 2.1 Address Management

#### Purpose / Actor

The CUSTOMER creates, lists, updates, and deletes delivery addresses, and selects a default address.

#### Requirements Traceability

- **FR-06**, **UC-03**.

#### Preconditions

- The general access requirements are satisfied.
- For operations on an existing address, the address must exist and belong to the current customer.

#### Inputs / Outputs

**Input — `CreateCustomerAddressDto` / `UpdateCustomerAddressDto`:**

| Field | Type | Requirements |
|---|---|---|
| `Label` | string | Required; maximum 50 characters. |
| `ContactName` | string | Required; maximum 150 characters. |
| `ContactPhone` | string | Required; valid phone number format; maximum 20 characters. |
| `AddressLine` | string | Required; must not be empty or whitespace. |
| `Latitude` | decimal? | Optional; -90 to 90. |
| `Longitude` | decimal? | Optional; -180 to 180. |
| `IsDefault` | bool | Optional; defaults to `false`. |

`UserId` is obtained from the JWT and must not be specified in the body. Coordinates must either both be provided or both be omitted.

**Output — `CustomerAddressResponseDto`:** `Id`, `Label`, `ContactName`, `ContactPhone`, `AddressLine`, `Latitude?`, `Longitude?`, `IsDefault`, `CreatedAt`, `UpdatedAt`. The list contains only addresses belonging to the current CUSTOMER.

#### Business Rules

1. **BR-CO-01, BR-CO-33** — Required fields must not be null or empty. Validate character limits, phone number format, and coordinate ranges; latitude and longitude must either both be provided or both be omitted.
2. **BR-CO-02** — Each customer may have at most one address with `IsDefault = true`. Setting an address as the default must unset the previous default address at the same time.
3. **BR-CO-03** — Reading, updating, or deleting another customer's address is prohibited.
4. **BR-CO-04** — The deletion policy for addresses referenced by orders must be confirmed; deletion must not remove delivery history or information.
5. **BR-CO-05** — A limit of 10 addresses per customer is proposed; confirm it as described in Section 4.

#### Exceptional Cases

| HTTP | Case |
|---|---|
| `401` | JWT is missing, expired, or invalid. |
| `403` | Incorrect role or inactive account. |
| `404` | Address does not exist or does not belong to the caller. |
| `400` | Required field is missing, character limit exceeded, phone number has an invalid format, coordinates are out of range, or only one coordinate is provided. |
| `409` | Conflict while setting the default, the confirmed address limit is exceeded, or the address is referenced by an order under the selected deletion policy. |

### 2.2 Package Management

#### Purpose / Actor

The CUSTOMER creates, views, and updates packages to select when creating a delivery order.

#### Requirements Traceability

- **FR-07**, **UC-04**.

#### Preconditions

- The general access requirements are satisfied.
- For updates, the package must exist and belong to the current CUSTOMER.

#### Inputs / Outputs

**Input — `CreatePackageDto` / `UpdatePackageDto`:**

| Field | Type | Requirements |
|---|---|---|
| `PackageName` | string | Required; maximum 200 characters. |
| `WeightGram` | decimal | Required; greater than 0 and at most 3000 grams (3.0 kg). |
| `LengthCm` | decimal | Required; greater than 0 and at most 30 cm. |
| `WidthCm` | decimal | Required; greater than 0 and at most 20 cm. |
| `HeightCm` | decimal | Required; greater than 0 and at most 15 cm. |
| `PackageImageUrl` | string? | Optional; if provided, must be a valid URL/image reference. |
| `IsFragile` | bool | Optional; defaults to `false`. |

`SenderId` is obtained from the JWT; the client must not assign ownership.

**Output — `PackageResponseDto`:** `Id`, `PackageName`, `WeightGram`, `LengthCm`, `WidthCm`, `HeightCm`, `PackageImageUrl?`, `IsFragile`, `CreatedAt`.

#### Business Rules

1. **BR-CO-06** — Per the latest group decision resolving D-01, maximum dimensions are **30 × 20 × 15 cm** for `LengthCm`, `WidthCm`, and `HeightCm`; each dimension and the weight must be greater than 0.
2. **BR-CO-07** — Per the latest group decision resolving D-01, maximum payload is **3.0 kg** (`WeightGram <= 3000`). Revalidate the limit when creating an order.
3. **BR-CO-08** — The relationship is **One Package One Order**: a package may be linked to at most one order.
4. **BR-CO-09** — Deleting a package referenced by an order is prohibited (**RESTRICT**).
5. **BR-CO-10, BR-CO-35** — Only the package owner may view or update it. Package name is required (maximum 200 characters), an optional image reference must be valid, and `IsFragile` defaults to `false`. An update must not violate the limits or the validity of an order to which the package is linked.

#### Exceptional Cases

| HTTP | Case |
|---|---|
| `401` | JWT is missing, expired, or invalid. |
| `403` | Incorrect role or inactive account. |
| `404` | Package does not exist or does not belong to the caller. |
| `400` | Name is missing/too long, weight or dimensions are non-positive/exceed limits, or image URL is invalid. |
| `409` | Package is already linked to another order; attempt to delete a package referenced by an order. |

### 2.3 Order Creation

#### Purpose / Actor

The CUSTOMER creates a delivery request by selecting a package, origin/destination stations, a recipient, and an optional delivery address.

#### Requirements Traceability

- **FR-08**, **FR-09**, **FR-12**, **FR-14**, **UC-05**, **UC-10**.
- Business rule references: **BR-CO-11–17**, **BR-CO-34**, **BR-LS-01**.

#### Preconditions

- The general access requirements are satisfied.
- The package exists, belongs to the current customer, meets drone limits, and has not been used for another order.
- Both stations exist, have `OPERATIONAL` status, and have different IDs.
- If an address is provided, it must belong to the current customer.

#### Inputs / Outputs

**Input — `CreateDeliveryOrderDto`:**

| Field | Type | Requirements |
|---|---|---|
| `PackageId` | Guid | Required; must belong to the caller and not be linked to another order. |
| `OriginStationId` | Guid | Required; station must be `OPERATIONAL`. |
| `DestinationStationId` | Guid | Required; station must be `OPERATIONAL` and different from the origin station. |
| `DeliveryAddressId` | Guid? | Optional; if provided, must belong to the caller. |
| `RecipientName` | string | Required; maximum 150 characters. |
| `RecipientPhone` | string | Required; valid phone number format; maximum 20 characters. |

The client must not send `CustomerId`, status, tracking number, secure code, or system timestamps.

**Output — `DeliveryOrderDetailResponseDto` (order creation response):** `Id`, `TrackingNumber`, `Status`, package/station summaries, delivery address if provided, `RecipientName`, `RecipientPhone`, timestamps, and initial status history. The secure code is included in the order creation response as specified by the UC-05 flow; do not write the code to logs.

#### Business Rules

1. **BR-CO-11, BR-CO-36** — The server validates package/address ownership and drone limits of **3.0 kg (`WeightGram <= 3000`), 30 × 20 × 15 cm**.
2. **BR-CO-12, BR-LS-01** — The origin and destination stations must be different and both `OPERATIONAL` when the request is processed.
3. **BR-CO-13** — Generate a unique `TrackingNumber` and a random secure code consisting of exactly 6 decimal digits. The specific tracking number format remains to be confirmed as described in Section 4.
4. **BR-CO-14** — A new order has `PENDING` status.
5. **BR-CO-15** — Write the initial `DeliveryStatusLog` (`FromStatus = null`, `ToStatus = PENDING`) in the same transaction as the order.
6. **BR-CO-16** — After successful order creation, create a notification for the customer according to FR-12/UC-10. Do not create an incorrect status notification if saving the order fails.
7. **BR-CO-17** — A package cannot be linked to a second order.
8. **BR-CO-34** — Recipient name is required (maximum 150 characters); recipient phone is required, valid per the agreed format, and at most 20 characters. Validate again when saving, not only on screen.

#### Exceptional Cases

| HTTP | Case |
|---|---|
| `401` | JWT is missing, expired, or invalid. |
| `403` | Incorrect role or inactive account. |
| `404` | Package, either station, or address was not found. |
| `400` | Recipient data is missing/invalid; package does not belong to the caller or exceeds the **3.0 kg / 30 × 20 × 15 cm** limits; a station is not `OPERATIONAL`; origin and destination stations are the same. |
| `409` | Package is already linked to another order or a uniqueness conflict occurs while assigning the tracking number. |

### 2.4 Order Update

#### Purpose / Actor

The CUSTOMER updates recipient information or the delivery destination before the order is approved.

#### Requirements Traceability

- **FR-10**, **UC-06**.
- Business rule references: **BR-CO-18–22**, **BR-CO-34**.

#### Preconditions

- The general access requirements are satisfied.
- The order exists, belongs to the current CUSTOMER, and has `PENDING` status.

#### Inputs / Outputs

**Input — `UpdateDeliveryOrderDto`:** contains only the following four fields:

| Field | Type | Requirements |
|---|---|---|
| `RecipientName` | string | Required; maximum 150 characters. |
| `RecipientPhone` | string | Required; valid phone number format; maximum 20 characters. |
| `DestinationStationId` | Guid | Required; valid destination station ID. |
| `DeliveryAddressId` | Guid? | Optional; if provided, must belong to the current CUSTOMER. |

`PackageId`, `OriginStationId`, `Status`, `CustomerId`, `TrackingNumber`, and `PickupSecureCode` are not part of the update DTO.

**Output — `DeliveryOrderDetailResponseDto`:** the updated order information, excluding the secure code.

#### Business Rules

1. **BR-CO-18** — Updates are allowed only if the status is still `PENDING` when the update is saved (FR-10).
2. **BR-CO-19, BR-CO-22** — Only the four fields in the DTO may be updated; changing the package or status is prohibited. Reject fields outside the allowed scope instead of silently ignoring them.
3. **BR-CO-20** — The new destination station must exist, be `OPERATIONAL`, and differ from the origin station.
4. **BR-CO-21** — If provided, the delivery address must belong to the CUSTOMER making the request.

#### Exceptional Cases

| HTTP | Case |
|---|---|
| `401` | JWT is missing, expired, or invalid. |
| `403` | Incorrect role or inactive account. |
| `404` | Order does not exist or does not belong to the caller; address or station not found. |
| `400` | Required field is missing/invalid; `PackageId`/`Status` or another disallowed field is supplied; destination station is inactive or the same as the origin station. |
| `409` | The order moved out of `PENDING` before the update was saved. |

### 2.5 Order Cancellation

#### Purpose / Actor

The CUSTOMER cancels an order that is still within the permitted cancellation window and provides a cancellation reason.

#### Requirements Traceability

- **FR-10**, **FR-12**, **FR-14**, **UC-06**, **UC-10**.
- Business rule references: **BR-CO-23–26**, **BR-CO-34**.

#### Preconditions

- The general access requirements are satisfied.
- The order exists and belongs to the current CUSTOMER.
- A CUSTOMER may cancel only orders with `PENDING` or `APPROVED` status; an `IN_TRANSIT` order cannot be canceled.

#### Inputs / Outputs

**Input — `CancelDeliveryOrderDto`:**

| Field | Type | Requirements |
|---|---|---|
| `CancellationReason` | string | Required; must not be empty or whitespace; maximum 500 characters. |

**Output — `DeliveryOrderResponseDto`:** order information after transition to `CANCELLED`, cancellation reason, and update time.

#### Business Rules

1. **BR-CO-23** — Cancellation is allowed only when the current status is `PENDING` or `APPROVED`.
2. **BR-CO-24, BR-CO-34** — Save the required, nonblank `CancellationReason` (maximum 500 characters) on the order and write a `DeliveryStatusLog` from the previous status to `CANCELLED`, including the actor and timestamp.
3. **BR-CO-25** — Update the status and write the log in the same transaction. After success, create a notification for the customer according to FR-12.
4. **BR-CO-26, D-10** — Cancellation is prohibited for `IN_TRANSIT`, `DELIVERED`, or already-canceled orders. Repeated requests must not create duplicate logs or notifications. `DELIVERED` is the order terminal state; `COMPLETED` is not an order status.

#### Exceptional Cases

| HTTP | Case |
|---|---|
| `401` | JWT is missing, expired, or invalid. |
| `403` | Incorrect role or inactive account. |
| `404` | Order does not exist or does not belong to the caller. |
| `400` | `CancellationReason` is missing, empty, or exceeds 500 characters. |
| `409` | Current status does not allow cancellation, including `IN_TRANSIT`, `DELIVERED`, or `CANCELLED`. |

### 2.6 Order Listing & Details

#### Purpose / Actor

The CUSTOMER views their orders, filters and paginates the list, and opens details that include status history and ETA data when available.

#### Requirements Traceability

- **FR-09**, **FR-11**, **FR-12**, **FR-24**, **FR-26**, **FR-29**, **FR-30**.
- **UC-05**, **UC-07**, **UC-08**, **UC-10**, **UC-20**. UC-08/FR-26 is an integrated delivery-confirmation flow; authorization for the actor receiving the code must be specified separately in the workflow specification.
- Business rule references: **BR-CO-27–32**, **BR-CO-36**, **BR-LS-15–17**, **BR-DW-01**, **BR-DW-18**.

#### Preconditions

- The general access requirements are satisfied.
- The order details must belong to the current CUSTOMER.

#### Inputs / Outputs

**List filter parameters:**

| Field | Type | Constraints |
|---|---|---|
| `status` | string or string[] | Optional; valid statuses only. Support for arrays is an open issue. |
| `fromDate` | DateTime? | Optional; ISO 8601. |
| `toDate` | DateTime? | Optional; ISO 8601, and not earlier than `fromDate`. |
| `searchKeyword` | string? | Optional; search by tracking number or a supported keyword. |
| `page` | int | Optional; defaults to 1; minimum 1. |
| `pageSize` | int | Optional; defaults to 20; range 1–100. |

**List output — `PagedResult<DeliveryOrderResponseDto>`:** `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`. Each item contains `Id`, `TrackingNumber`, `Status`, package/station information, and `CreatedAt`/`UpdatedAt`; it does not contain `PickupSecureCode`.

**Details output — `DeliveryOrderDetailResponseDto`:** order, package, station, address, and recipient information permitted by the caller's access; `StatusLogs[]` (each log has the previous/new status, remarks, actor if permitted, and timestamp); and `AiEtaPrediction?` according to UC-20/FR-29. The secure code must not be included in the general details DTO or logs. Code delivery/verification is restricted to actors whose authorization has been confirmed.

#### Secure-Code Verification Validation (UC-08 Integration)

**Input — `ConfirmDeliveryDto`:** `SecureCode: string`, required and exactly 6 decimal digits. The recipient submits this code through an authorized confirmation flow; it must not be exposed in staff task lists, dispatcher dashboards, telemetry, or audit logs (**BR-LS-15**, **BR-LS-22**).

**Business rules:**

1. **BR-LS-15** — Compare the submitted code exactly with the order's code; reject a mismatch.
2. **BR-LS-16** — Count consecutive incorrect attempts. After 3 consecutive incorrect attempts, lock verification for 10 minutes and dispatch a security-alert notification to the customer.
3. **BR-LS-17, BR-DW-01** — On successful verification, transition the order to `DELIVERED` and record the exact UTC delivery time. `DELIVERED` is the order status; mission completion state is separate.

#### Business Rules

1. **BR-CO-27** — Always scope results to the customer ID from the JWT; do not use a customer ID supplied by the client to expand the scope.
2. **BR-CO-28** — Support filtering by status, creation date, and keyword; validate `fromDate <= toDate`. By default, sort newest creation time first.
3. **BR-CO-29** — Apply pagination with `page >= 1` and `1 <= pageSize <= 100`.
4. **BR-CO-30** — Return `StatusLogs[]` in ascending chronological order. ETA/AI predictions may be unavailable, in which case return null.
5. **BR-CO-31** — Order-related notification information follows FR-12/UC-10; data belonging to other customers must never be returned.
6. **BR-CO-32** — Real-time drone location is an FR-24 tracking capability and is distinct from the ETA/history snapshot in the details response.

#### Exceptional Cases

| HTTP | Case |
|---|---|
| `401` | JWT is missing, expired, or invalid. |
| `403` | Incorrect role or inactive account. |
| `404` | Order was not found or does not belong to the caller. |
| `400` | Invalid status; date is malformed or `fromDate > toDate`; page/pageSize is out of range; keyword exceeds the configured limit; submitted secure code is malformed or incorrect. |
| `409` | Applicable only if the API contract defines a conflicting filter status as a conflict; by default, invalid filter parameters return `400`. |
| `423` | Secure-code verification is locked after 3 consecutive incorrect attempts; retry after the 10-minute lockout. |

## 3. Traceability Matrix

| Functional Area | Specific Function | Related FR / UC IDs | Source Confirmation Status |
|---|---|---|---|
| 1 | Address Management — create, update, delete, list, set default | FR-06 / UC-03 | SRS/use cases confirm the operations and actor; detailed fields/validation follow the class specification. The limit of 10 addresses is proposed and requires confirmation. |
| 2 | Package Management — create and update packages | FR-07 / UC-04 | SRS/use cases confirm business attributes; BR-CO-06–10 map to dimensions, payload and the 1-to-1 relationship. The source register's earlier conflicting limits are superseded here by the latest group decision. |
| 3 | Order Creation — create order, generate code, set initial status, and create notification | FR-08, FR-09, FR-12, FR-14 / UC-05, UC-10 | SRS/use cases and sequence-create-order confirm the flow; BR-CO-11–17 map to validation, `OPERATIONAL`, `PENDING`, the initial log, and notification. Earlier conflicting limits in the source register are superseded here by the latest group decision. The TrackingNumber format remains to be confirmed. |
| 4 | Order Update — update four fields before approval | FR-10 / UC-06 | The class specification and sequence-update-cancel-order specify that the DTO contains only RecipientName, RecipientPhone, DestinationStationId, and DeliveryAddressId; updates are allowed only while PENDING. |
| 5 | Order Cancellation — cancel with a reason, write a log, and create a notification | FR-10, FR-12, FR-14 / UC-06, UC-10 | BR-CO-23–26 and sequence-update-cancel-order confirm required CancellationReason, PENDING/APPROVED eligibility, transition to CANCELLED, log and notification; order terminal status is `DELIVERED`, not `COMPLETED` (D-10). |
| 6 | Order Listing & Details — filtering, pagination, history, and ETA | FR-09, FR-11, FR-12, FR-24, FR-26, FR-29, FR-30 / UC-05, UC-07, UC-08, UC-10, UC-20 | SRS/use cases confirm customer orders, tracking, notifications, secure-code confirmation, and ETA/summary. The secure-code format and BR-LS-16 lockout follow the latest group decision; status-array support and actor authorization still require confirmation. |

## 4. Open Issues

1. **Address limit (BR-CO-05):** a maximum of **10 addresses per customer** is proposed. Confirm how to handle exceeding the limit, deleting the default address, and deleting an address referenced by an order.
2. **Package reuse (BR-CO-08, BR-CO-17; D-03):** confirm whether a package may be attached to a new order after its previous order is terminal. The current rule is One Package One Order; do not reuse packages until a decision is made.
3. **Status arrays in the filter (BR-CO-28):** confirm whether the list filter accepts one or multiple statuses (for example, `status=PENDING,APPROVED`), how they are encoded, and the response when a status is invalid.
4. **Source-register synchronization (BR-CO-06, BR-CO-07, BR-CO-11; D-01):** this specification applies the latest group decision of 3.0 kg (3000 g) and 30 × 20 × 15 cm. The PR #50 register still records conflicting earlier CO/LS/DW values; synchronize the register in a separate requirements update.
5. **Source-register synchronization for secure code (BR-CO-13, BR-LS-15, BR-LS-16; D-02, D-04):** this specification applies the latest decision of exactly 6 decimal digits and lockout for 10 minutes after 3 consecutive wrong attempts. The source register marks alphanumeric format and lockout thresholds as conflicts/proposals; synchronize its decisions in a separate requirements update.
6. **Lockout response mapping:** the source register defines the lockout behavior but does not specify its HTTP response code; this specification uses `423 Locked`, which should be aligned with the API error contract.
