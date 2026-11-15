# Sprint 6 API contract - tracking, confirmation, analytics and integration

**Task:** [LA-341](https://hungndemail.atlassian.net/browse/LA-341). **Consumers:** web, Customer/Operator mobile and acceptance tests. Paths below include `/api`; a client whose base URL already ends in `/api` must not add that prefix again.

This is the Sprint 6 handoff contract. Implemented shapes are checked against the backend branches below. Analytics/external shapes are implementation proposals for client mocks and backend review; their business-policy decisions are explicitly listed in section 7. A document merge does not deploy an endpoint or approve those policies.

Machine-readable HTTP schemas: [sprint-6-openapi.json](sprint-6-openapi.json). SignalR is described in section 3 because its invocation/event protocol is not a normal REST operation.

## 1. Availability and task mapping

| Area | Backend / consumers | Availability when this contract was authored |
|---|---|---|
| Tracking + map data, LA-35/36 | LA-143; web LA-144/147; mobile LA-145/148; tests LA-146/149 | Implemented in `story/LA-35-realtime-delivery-tracking`, [PR #184](https://github.com/nguyentuio1/learning_app/pull/184). Not yet integrated into `develop` while other story tasks remain open. |
| Customer confirmation, LA-37 | LA-150; mobile LA-151; tests LA-152 | Implemented in `story/LA-37-secure-delivery-confirmation` through [PR #186](https://github.com/nguyentuio1/learning_app/pull/186). Use that story branch after merge, not an older `develop` checkout. |
| Operator confirmation | Existing LA-125/126/127 | Available on `develop`; existing [station contract](station-api.md) section 4.4. |
| Dashboard/reports, LA-38 | LA-153; web LA-154; tests LA-155 | Planned; mock the proposed schemas in section 5. No analytics route exists on the current `develop`. |
| External integration, LA-39 | LA-156; tests LA-157 | Planned; mock section 6. API-key provisioning and versioned routes are not implemented yet. |

Check the linked PR/branch before trying a new endpoint. A `404` from an older branch does not prove an authorization or client defect. Merge completed stories into `develop` promptly after review and CI; do not merge an incomplete story just to expose its backend.

## 2. Shared conventions

- First-party endpoints use `Authorization: Bearer <accessToken>` from [auth-api.md](auth-api.md). All listed roles also require an ACTIVE persisted account; a stale token does not retain a removed role or ownership right.
- UUIDs are strings. JSON uses camelCase. Date/time values are ISO 8601 UTC with `Z`; nullable timestamps mean unavailable, not the Unix epoch. Numeric coordinates are latitude -90..90 / longitude -180..180.
- Order statuses are exactly `PENDING`, `APPROVED`, `READY_FOR_TAKEOFF`, `IN_TRANSIT`, `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`. `COMPLETED` is a mission status, never an order status.
- `401`: missing/invalid/expired authentication. `403`: authenticated but inactive, wrong role or insufficient key scope. Unknown and foreign customer resources both use `404`. `400` is validation; `409` is an incompatible current state. Never infer success from an error or replace a failed load with empty data.
- Errors use the existing problem body: `type`, `title`, `status`, `detail`; validation may add `errors: { field: [message] }`. Confirmation adds the documented attempt/lockout fields below. Clients branch on status/field names, not exact English detail wording.
- Never put a pickup code, JWT or API key into URLs, analytics, screenshots of debug payloads, or application/network logs. The SignalR query-token transport exception below must be redacted in proxy/access logs.

## 3. Real-time tracking and map data - LA-143

### 3.1 GET /api/delivery-orders/{id}/tracking

Auth: owning `CUSTOMER`, or `DISPATCHER`, `MANAGER`, `ADMIN`. A `STATION_OPERATOR` is not granted this endpoint. Unknown/foreign customer order: identical `404`. Malformed UUID: `400`.

`200` returns `TrackingSnapshot`:

```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "status": "IN_TRANSIT",
  "originStation": { "id": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44", "name": "District 1 Hub", "latitude": 10.7769, "longitude": 106.7009 },
  "destinationStation": { "id": "a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21", "name": "Thu Duc Hub", "latitude": 10.8506, "longitude": 106.7719 },
  "estimatedDeliveryTime": "2026-11-15T08:24:00Z",
  "lastPosition": { "latitude": 10.8012, "longitude": 106.7311, "altitudeMeters": 80.0, "speedMps": 11.5, "batteryPercentage": 71, "timestamp": "2026-11-15T08:10:05Z" },
  "telemetryStale": false
}
```

All fields are required; only `estimatedDeliveryTime` and `lastPosition` are nullable. A position contains all six fields shown; battery is an integer percentage. The response contains no pickup code, recipient details, mission ID or drone serial number.

Use the newest mission by `createdAt` then mission ID descending, and its newest sample by timestamp then sample ID descending. Position is null if the order is not `IN_TRANSIT`, its current mission is not `IN_AIR`, or it has no sample. An older mission's position must never be reused. When an in-air delivery has no sample, `telemetryStale` is true; an available sample is stale only when strictly older than 60 seconds. Exactly 60 seconds is fresh. Outside an in-air delivery, `telemetryStale` is false.

LA-36 needs no separate map endpoint: use station coordinates and `lastPosition` from this snapshot. A straight line between stations is a display aid, not a verified flight plan; never extrapolate missing telemetry into a live position.

### 3.2 SignalR /hubs/tracking

Use the normal SignalR client protocol, not raw REST POSTs. Supply the same JWT via the client token factory. Browser WebSocket/SSE transports may use `access_token` on this hub path only; ordinary API routes do not accept query bearer tokens.

| Invocation / event | Shape and behavior |
|---|---|
| `Subscribe(orderId)` | One UUID string argument; selects exactly one authorized order per connection and replaces an earlier selection. Completion has no result body. A missing/foreign order gives the same failed invocation, `Tracking unavailable.` |
| `Unsubscribe()` | No arguments; clears the selected order. Disconnect also removes the server subscription. |
| `PositionUpdated(position)` | One argument: exactly the six-field `lastPosition` object above. It has no `orderId` or `missionId`. No fabricated/null position event is sent. |
| `StatusChanged(decision)` | One argument: `{ "orderId": "...", "trackingNumber": "...", "status": "ARRIVED", "updatedAt": "2026-11-15T08:24:00Z" }`. Refresh the canonical snapshot, including when a mission changed without an order-status change. |

Client sequence:

1. Register handlers, connect and subscribe to the selected order.
2. GET the snapshot after subscription to close the initial-read/subscription gap. Refresh again after reconnect/resubscription and each status event.
3. Keep the selected-order context stable while applying position events. Prefer a new connection when switching orders; clear old handlers/data so an uncorrelated old position cannot land on the new map.
4. On live transport failure, allow only one HTTP request at a time and poll the snapshot every 5 seconds. Stop polling after successful resubscription and refresh. Cancel timers/requests and dispose handlers when leaving the screen.
5. During an idle stream, refresh snapshots to reevaluate telemetry freshness; transport connectivity does not mean the last sample is fresh. Clear protected state on `401`/`403`/`404`.

The backend polls committed facts every second and pushes changed status/position. It revalidates persisted account/role/ownership before publication and aborts revoked or expired subscriptions. Position updates do not move backwards within the same flight. A mission replacement sends a status signal to trigger resnapshotting.

There is no durable replay or guarantee of every intermediate status between passes. Subscriptions are process-local; horizontal scaling needs a separate backplane design. A transport failure cannot roll back a saved delivery or cause a stored sample to be inserted twice.

### 3.3 Existing staff list

`GET /api/dispatch/active-deliveries?stationId={optionalUuid}` is already implemented on `develop`. Auth: active `DISPATCHER`, `MANAGER`, `ADMIN`; not customers/operators. It returns an unpaged array of PLANNED/IN_AIR missions. The exact `ActiveDelivery` fields and nullable legacy schedule/ETA fields remain in [delivery-workflow-api.md](delivery-workflow-api.md) section 5.1. Malformed station ID: `400`.

## 4. Delivery confirmation - LA-150

### 4.1 POST /api/delivery-orders/{id}/confirm-delivery

Auth: active owning `CUSTOMER` only. Staff use the existing operator route below. Body contains only `pickupSecureCode`; unknown fields are rejected.

```json
{ "pickupSecureCode": "593284" }
```

The trimmed code must be exactly six digits. Order must be `ARRIVED`, with its completed mission still owning an `OCCUPIED` destination slot. The server derives customer, tracking number and destination station; the client supplies none of them. Current account/role/ownership is checked again after obtaining the order lock.

`200` returns the existing `ConfirmPickupResponse`:

```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "trackingNumber": "DD-202611-0012",
  "status": "DELIVERED",
  "actualDeliveryTime": "2026-11-15T08:30:00Z",
  "slotId": "b2d4f6a8-1c3e-4a5b-9d7f-0e2c4a6b8d10",
  "slotReleased": true
}
```

All fields are required and non-null on success. Completion, actual delivery time, one status log, customer notification, confirmation audit and freeing the owned slot are atomic. It does not return the code. A repeated successful confirmation returns `409`, without duplicate completion effects. After a response is lost, refresh the order before deciding whether to retry; do not turn an uncertain write into a blind retry loop.

| Status | Body / client behavior |
|---|---|
| `400`, malformed/missing code or unknown body field | Problem body; validation may contain `errors.pickupSecureCode`. Does not consume a guess for malformed input. |
| `400`, well-formed wrong code | `{ "type": "https://httpstatuses.com/400", "title": "Bad Request", "status": 400, "detail": "Invalid pickup code.", "attemptsRemaining": 2 }`; next wrong guess leaves 1. |
| `401` / `403` | Require authentication / deny wrong role or inactive account. Do not show retained protected data after access loss. |
| `404` | Unknown or another customer's order; no code attempt is consumed. |
| `409` | Order not ARRIVED, already confirmed, missing/current slot mismatch, or slot no longer OCCUPIED. Refresh order/slot state. |
| `423` | Third wrong guess or an already locked order; integer-seconds `Retry-After` header plus `lockoutExpiresAt`. Disable confirmation until eligible; a correct code does not bypass an active lockout. |

Locked example:

```json
{ "type": "https://httpstatuses.com/423", "title": "Locked", "status": 423, "detail": "Too many wrong codes. Try again later.", "lockoutExpiresAt": "2026-11-15T08:40:00Z" }
```

The shared durable counter locks for ten minutes after three wrong codes across **customer confirmation, operator pickup and advisory validation**. Refreshing the app or switching routes does not reset it. Display zero attempts while locked; `attemptsRemaining` is not a field in the `423` body.

### 4.2 Existing operator routes

- `POST /api/landing-stations/{stationId}/operations/pickup`: active assigned `STATION_OPERATOR` or `ADMIN`, body `{ "trackingNumber": "DD-202611-0012", "pickupSecureCode": "593284" }`; same success/lockout bodies and counter.
- `POST /api/landing-stations/{stationId}/operations/validate-code`: advisory validation only, same authorization/counter; returns `{ isValid, attemptsRemaining, isLocked, lockoutExpiresAt }` under [station-api.md](station-api.md). A validation success does **not** mean DELIVERED or release a slot.
- Customer self-service and operator-assisted confirmation serialize on the same order: one can succeed; the other receives `409` after completion.

## 5. Dashboard and reports - proposed for LA-153/154/155

These URLs/DTOs are supplied now for mocks; no implementation is claimed. Metric/business decisions in section 7 remain proposed pending acceptance.

### 5.1 GET /api/analytics/dashboard?fromDate=2026-11-01&toDate=2026-11-30

Auth proposal: active `MANAGER` only, matching the analytics design; ADMIN access is not inferred. Both dates are required `YYYY-MM-DD`, inclusive calendar days in fixed reporting timezone `Asia/Ho_Chi_Minh` (UTC+07:00). Reject reversed/malformed/missing dates, more than 366 inclusive days, unrepresentable next-day boundaries and unknown filters with `400` plus field errors. No silent default period, timezone switch, station/status/fleet filter or export parameter is defined in this version.

`200` proposed `DashboardResult` example (all fields required; percentages/minutes explicitly nullable):

```json
{
  "period": { "fromDate": "2026-11-01", "toDate": "2026-11-30", "timeZone": "Asia/Ho_Chi_Minh", "startUtc": "2026-10-31T17:00:00Z", "endExclusiveUtc": "2026-11-30T17:00:00Z", "cohortField": "createdAt" },
  "metricDefinition": "created-current-v1",
  "asOf": "2026-12-01T02:00:00Z",
  "generatedAt": "2026-12-01T02:00:00Z",
  "totalOrders": 10,
  "ordersByStatus": [
    { "status": "PENDING", "count": 0 }, { "status": "APPROVED", "count": 0 },
    { "status": "READY_FOR_TAKEOFF", "count": 0 }, { "status": "IN_TRANSIT", "count": 1 },
    { "status": "ARRIVED", "count": 0 }, { "status": "DELIVERED", "count": 6 },
    { "status": "CANCELLED", "count": 1 }, { "status": "FAILED", "count": 2 }
  ],
  "successRate": { "deliveredCount": 6, "failedCount": 2, "eligibleCount": 8, "percent": 75.0 },
  "averageDelivery": { "minutes": 24.5, "sampleCount": 5, "excludedCount": 1 },
  "stationUsageMetric": "THROUGHPUT",
  "stationUsage": [ { "stationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44", "stationCode": "HCM-01", "stationName": "District 1 Hub", "originOrderCount": 7, "destinationOrderCount": 3, "totalDistinctOrders": 10 } ]
}
```

Definitions for this proposal:

- Cohort: distinct orders with `CreatedAt >= startUtc && CreatedAt < endExclusiveUtc`; count their current state at the read snapshot, not historical state at period end. Rescheduled missions/status logs never multiply an order.
- All eight status rows always exist, in the shown order, including zero values; their sum equals `totalOrders`.
- Success percentage is `100 * DELIVERED / (DELIVERED + FAILED)`. CANCELLED/unfinished orders are excluded from that denominator. Zero denominator means `percent: null`, not 0 or 100.
- Average minutes uses DELIVERED orders with both actual timestamps and nonnegative `actualDeliveryTime - actualDepartureTime`; exclude invalid/missing durations and expose the count. No valid samples means `minutes: null`. This includes time from takeoff to recipient confirmation, not creation-to-delivery or flight-only time.
- Station usage is throughput: origin/destination counts and deduplicated orders per station. It is not historical occupied-slot utilization. Omit zero-throughput stations; an empty cohort has `stationUsage: []`. Totals across different stations need not sum to `totalOrders`.
- Round displayed percent/minutes to two decimal places; underlying sample/count inputs remain explicit. Read all aggregates under one consistent database snapshot. `asOf` is read metadata, not a replay token.

Empty result: `totalOrders: 0`, eight zero status rows, zero numerator/denominator/sample/excluded counts, `percent: null`, `minutes: null`, `stationUsage: []`. A failed load is an error, never this empty success response. Errors: `400`, `401`, `403`; operational writes and code/recipient/customer detail are excluded. The under-three-second requirement is an implementation acceptance target, not a measured result from this contract.

### 5.2 GET /api/analytics/reports/deliveries

Auth and required date query are the same as dashboard. Additional query: `page` (default 1, minimum 1) and `pageSize` (default 20, 1..100). Sort `createdAt` descending, then order ID descending. No export file or historical `asOf` query is defined.

`200`: `{ "period": <same period object>, "metricDefinition": "created-current-v1", "asOf": "...Z", "items": [<row>], "page": 1, "pageSize": 20, "totalCount": 10, "totalPages": 1 }`. Empty data returns `items: []`, `totalCount: 0`, `totalPages: 0`; an out-of-range positive page returns an empty page with the actual totals.

Each row has required `orderId` (UUID), `trackingNumber`, `status`, `originStation` and `destinationStation` (`{ id, stationCode, name }`), `createdAt`, and nullable `actualDepartureTime`, `actualDeliveryTime`, `durationMinutes`. Duration is non-null only for DELIVERED rows with valid actual timestamps, using the same rule as dashboard. No recipient/contact information or pickup code is included.

Dashboard and report use the same cohort/formulas. Separate requests may see later committed states; label their returned period/read time and do not claim they are a historical immutable report. Ignore superseded responses when the user changes dates.

## 6. Versioned external integration - proposed for LA-156/157

These routes and key lifecycle are not yet implemented. They are a proposed bounded contract for external order creation and HTTP tracking under FR-34/BR-INT-01/02.

### 6.1 API-key authentication and lifecycle

- External routes use `X-API-Key: <opaqueKey>`, not a first-party bearer token. Proposed keys contain at least 32 cryptographically random bytes; store a hash and compare safely. Return plaintext only on creation, never in GET/list/logs.
- Each key is bound server-side to one ACTIVE `CUSTOMER`, named scopes and an expiry. Clients cannot supply/override `customerId` or assume admin rights. Recheck key revocation/expiry and persisted customer eligibility per request.
- Scopes: `orders:create` for creation; `orders:read` for reading/tracking that bound customer's orders. A valid key with insufficient scope or ineligible bound account returns `403`; missing/unknown/revoked/expired key returns `401`. Another customer's package/address/order is `404`.
- Proposed limit: 60 requests/minute/key, with at most 10 creates/minute/key; excess returns `429` with integer `Retry-After` seconds. Limits need business/operational acceptance before implementation.
- Do not send both `X-API-Key` and a bearer Authorization header to external routes; proposed response is `400` for ambiguous credentials. A key does not authenticate first-party `/api/...` or the SignalR hub.

Provisioning proposal, bearer-authenticated active `ADMIN` only:

| Endpoint | Request / response |
|---|---|
| `POST /api/admin/integration-keys` | `{ "customerId": "<uuid>", "name": "Warehouse A", "scopes": ["orders:create", "orders:read"], "expiresAt": "2027-01-01T00:00:00Z" }`; `201` returns `{ id, name, customerId, scopes, createdAt, expiresAt, revokedAt: null, apiKey }`, with plaintext shown once. |
| `GET /api/admin/integration-keys?customerId=<uuid>` | Unpaged metadata array with the same fields except **no `apiKey` or hash**; optional customer filter, empty result `[]`. |
| `DELETE /api/admin/integration-keys/{id}` | Revoke immediately; `204` for an existing key, including an already revoked key; `404` unknown ID. Preserve metadata/audit history. |

Name is trimmed, required and at most 100 characters. Scopes must be a nonempty unique subset of the two defined values; expiry is required UTC and in the future; customer must exist and be ACTIVE with role CUSTOMER. Unknown fields/scopes or invalid values return `400`; an ineligible target customer is `409`. Rotation means create a replacement key and explicitly revoke the old key; no hidden overlap or rotate endpoint is implied. Audit provisioning/revocation without storing the plaintext key.

### 6.2 POST /api/v1/delivery-orders

Auth: valid eligible key with `orders:create`. Reuse first-party order creation validation and ownership, one-package/one-order uniqueness, operational distinct stations and owned address rules from [customer-order-api.md](customer-order-api.md) section 5.1.

```json
{
  "packageId": "c41f8b0a-2d37-4a6b-8e1c-5f7a9d3b6e22",
  "originStationId": "7d2e5a90-1b4c-4f38-a6d1-9c8e0b3f5a44",
  "destinationStationId": "a1c3e5f7-2b4d-4a6c-8e0f-1b3d5f7a9c21",
  "deliveryAddressId": null,
  "recipientName": "Tran Thi Binh",
  "recipientPhone": "0912345678"
}
```

Package/stations/optional address are provisioned through existing first-party workflows; external package creation, customer registration and station administration are outside this sprint's external API. The integration owner must supply those resource IDs. External clients cannot override status, identity, tracking number, price or code; unknown fields are rejected.

`201 Created`, `Location: /api/v1/delivery-orders/{id}`:

```json
{ "id": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33", "trackingNumber": "DD-202611-0012", "status": "PENDING", "pickupSecureCode": "593284", "createdAt": "2026-11-15T08:00:00Z" }
```

The code is returned on this creation response only to the authorized integration acting for its customer. Never return it on a later read. No idempotency header/replay store is defined in this bounded proposal: do not blindly retry a timed-out creation, and do not interpret a package-already-used `409` as a confirmed creation response. Resolve uncertainty through the owning customer's existing order/support workflow.

Errors: `400` validation; `401` invalid key; `403` scope/account; `404` unavailable/foreign linked resource; `409` package already used or station/state rule; `429` limit. HTTP creation must use the same atomic initial status/log/notification behavior as the first-party service.

### 6.3 GET /api/v1/delivery-orders/{id}

Auth: eligible key with `orders:read`, owning customer's order only. `200` `ExternalOrder`: required `id`, `trackingNumber`, `status`, `packageId`, `originStationId`, `destinationStationId`, `createdAt`, `updatedAt`, plus required nullable `estimatedDeliveryTime` and `actualDeliveryTime`. No code, recipient/contact information, credentials or unrelated customer data. Errors: `400`, `401`, `403`, `404`, `429`.

### 6.4 GET /api/v1/delivery-orders/{id}/tracking

Auth/scope/ownership identical to 6.3. `200` uses exactly `TrackingSnapshot` from section 3.1, including null/stale/current-mission semantics. Poll no faster than every 5 seconds and honor `429`/`Retry-After`. External keys cannot subscribe to the first-party SignalR hub. Errors: `400`, `401`, `403`, `404`, `429`.

Versioning proposal: publish these routes as external version `v1`; retain current field/status meanings within v1. A breaking request/response or ownership-policy change requires a reviewed new major API version. External Swagger/OpenAPI documentation must expose these routes and `X-API-Key`, and clearly separate admin provisioning from externally callable operations.

## 7. Decisions and acceptance handoff

| Decision | Status / owner |
|---|---|
| Tracking DTOs, selected-order hub, expiry/revocation, null/stale rules | Implemented/checked against LA-143, commit `62e5692`, PR #184. |
| Customer confirmation and shared operator OTP budget | Implemented/checked against LA-150, commit `529bbf0`, PR #186. |
| Analytics URLs/schema, fixed Vietnam timezone, required dates and 366-day maximum | LA-341 implementation proposal; leader/module owner must accept before LA-153 is represented as approved behavior. |
| Created-date/current-status cohort, success denominator, departure-to-confirmation average and station throughput | Existing LA-240/UA section 7.1 candidates retained explicitly; require business acceptance before metric acceptance. Historical occupancy/status-at-period-end is outside this proposal. |
| API-key provisioning/scope/expiry/limits and v1 external routes | LA-341 proposal for LA-156; provisioning persistence, rate limiting and Swagger are future implementation work, not features delivered by this document. |

Teammates can build mocks against these exact field names now. Backend implementation must follow the accepted contract or change this document and its schemas in the same reviewed PR; do not silently rename fields. Before reporting a UI integrated, run it against the corresponding story/backend branch, not only mock fixtures.

Acceptance scenarios: foreign-order denial; deactivated/demoted token; no/current/stale/terminal telemetry; reconnect/switch/cleanup; shared third-guess lockout; duplicate confirmation; empty analytics/null ratios; exact Vietnam-day boundaries; duplicated mission history; invalid duration exclusions; revoked/scoped/foreign-customer API key; duplicate package creation; and honest network/unknown-write handling.
