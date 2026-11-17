# LA-183: SmartDroneDelivery API Documentation

**Baseline:** `develop` (`2148e50`, ASP.NET Core 8)  
**Interactive Explorer:** Swagger UI at `http://localhost:5055/swagger` (enabled when `ASPNETCORE_ENVIRONMENT=Development`)  
**OpenAPI Specs:** [sprint-6-openapi.json](sprint-6-openapi.json), [sprint-7-openapi.json](sprint-7-openapi.json)

---

## 1. Authentication & Authorization

### 1.1 JWT Bearer Authentication
All first-party endpoints require an `Authorization: Bearer <accessToken>` header obtained from `POST /api/auth/login` or `POST /api/auth/register` (unless explicitly marked anonymous such as `/api/auth/*` and `/health/*`). Every protected endpoint verifies the **current persisted account status (`ACTIVE`) and role** in PostgreSQL rather than trusting stale JWT claims.

### 1.2 External Partner API Key (`X-API-Key`)
External integration endpoints under `/api/v1/delivery-orders` accept `X-API-Key: <secret>` provisioned by an Administrator via `POST /api/admin/integration-keys`. Keys are scoped (`orders:create`, `orders:read`), rate-limited (`429 Too Many Requests`), and cannot access first-party routes or `/hubs/tracking`.

### 1.3 Standard Error Responses (RFC 7807 `ProblemDetails`)

| HTTP Status | Meaning | Common Causes |
| :---: | :--- | :--- |
| `400 Bad Request` | Validation / malformed input | Invalid UUID, missing required field, invalid query bounds, invalid password/phone |
| `401 Unauthorized` | Unauthenticated | Missing/expired JWT, revoked/expired `X-API-Key`, invalid login credentials |
| `403 Forbidden` | Forbidden | Disallowed role, inactive account, unowned station (`STATION_OPERATOR`) |
| `404 Not Found` | Hidden or missing resource | Unknown ID or foreign-owned resource (identical `404` to prevent enumeration) |
| `409 Conflict` | State / concurrency conflict | Invalid order transition, slot occupied/reserved, duplicate drone serial, source changed during AI inference |
| `413 Payload Too Large` | Request body too large | Assistant `POST /api/ai-assistant/messages` body exceeds 32 KiB |
| `423 Locked` | OTP attempt lockout | 3 consecutive wrong `pickupSecureCode` guesses (10-minute lock, returns `Retry-After` header) |
| `429 Too Many Requests` | Rate / concurrency limit | Concurrent AI generation for same account (`Retry-After: 5`) or external API key quota exceeded |
| `503 Service Unavailable` | Dependency / provider unavailable | `/health/ready` dependency failure, or disabled/unconfigured AI model gateway |
| `504 Gateway Timeout` | Upstream timeout | AI inference exceeded 10-second deadline |

---

## 2. Quick Swagger & `curl` Examples

### Using Swagger UI
1. Start the API in Development mode: `dotnet run --project backend/SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj --launch-profile http`
2. Open `http://localhost:5055/swagger`.
3. Call `POST /api/auth/login` to obtain `accessToken`, click **Authorize**, and enter `Bearer <accessToken>`.

### Example: Login (`POST /api/auth/login`)

```bash
curl -X POST http://localhost:5055/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"customer@example.com","password":"Passw0rd123"}'
```

Response (`200 OK`):
```json
{
  "accessToken": "<jwt>",
  "refreshToken": "<refresh-token>",
  "expiresAt": "2026-11-17T16:00:00Z",
  "user": { "id": "uuid", "email": "customer@example.com", "role": { "code": "CUSTOMER" } }
}
```

### Example: Create Delivery Order (`POST /api/delivery-orders`)

```bash
curl -X POST http://localhost:5055/api/delivery-orders \
  -H "Authorization: Bearer <jwt>" \
  -H "Content-Type: application/json" \
  -d '{
    "packageId": "11111111-2222-4333-8444-555555555555",
    "originStationId": "22222222-2222-4333-8444-555555555555",
    "destinationStationId": "33333333-2222-4333-8444-555555555555",
    "recipientName": "Nguyen Van B",
    "recipientPhone": "0912345678"
  }'
```

---

## 3. Complete Endpoint Reference

### 3.1 Health Checks & Auth (`auth-api.md`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `GET` | `/health/live` | Anonymous | Process liveness probe (`200 OK`) |
| `GET` | `/health/ready` | Anonymous | Readiness probe checking PostgreSQL & MinIO (`200` / `503`) |
| `POST` | `/api/auth/register` | Anonymous | Register `CUSTOMER` account |
| `POST` | `/api/auth/login` | Anonymous | Sign in and receive JWT + refresh token (audits failed sign-ins) |
| `POST` | `/api/auth/refresh` | Anonymous | Rotate refresh token and issue new access token |
| `POST` | `/api/auth/logout` | Authenticated | Revoke refresh token |
| `GET` | `/api/users/me` | Authenticated | Get current user profile |
| `PUT` | `/api/users/me` | Authenticated | Update own profile |
| `PUT` | `/api/users/me/password` | Authenticated | Change password (revokes active sessions) |
| `POST` | `/api/users/me/avatar` | Authenticated | Upload profile avatar to MinIO |

### 3.2 User Management & Audit Logs (`ADMIN`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `GET` | `/api/users` | `ADMIN` | List paginated users with search/role/status filters |
| `GET` | `/api/users/{id:guid}` | `ADMIN` | Get user details |
| `POST` | `/api/users` | `ADMIN` | Create staff or customer account |
| `PUT` | `/api/users/{id:guid}` | `ADMIN` | Update user account |
| `PUT` | `/api/users/{id:guid}/deactivate` | `ADMIN` | Deactivate user account |
| `PUT` | `/api/users/{id:guid}/role` | `ADMIN` | Reassign user role |
| `GET` | `/api/audit-logs` | `ADMIN` | Search immutable audit logs (`userId`, `action`, `fromDate`, `toDate`, pagination) |
| `GET` | `/api/audit-logs/actions` | `ADMIN` | List distinct audit action codes |

### 3.3 Customer Addresses, Packages, Orders & Notifications (`customer-order-api.md`, `notification-api.md`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `GET` | `/api/customer-addresses` | `CUSTOMER` | List owned saved addresses |
| `GET` | `/api/customer-addresses/{id:guid}` | `CUSTOMER` | Get owned address detail |
| `POST` | `/api/customer-addresses` | `CUSTOMER` | Create saved address |
| `PUT` | `/api/customer-addresses/{id:guid}` | `CUSTOMER` | Update saved address |
| `PUT` | `/api/customer-addresses/{id:guid}/default` | `CUSTOMER` | Set default address |
| `DELETE` | `/api/customer-addresses/{id:guid}` | `CUSTOMER` | Delete saved address |
| `GET` | `/api/packages` | `CUSTOMER` | List owned packages |
| `GET` | `/api/packages/{id:guid}` | `CUSTOMER` | Get owned package detail |
| `POST` | `/api/packages` | `CUSTOMER` | Create package (`weightGram`, dimensions) |
| `PUT` | `/api/packages/{id:guid}` | `CUSTOMER` | Update package (when not attached to active order) |
| `DELETE` | `/api/packages/{id:guid}` | `CUSTOMER` | Delete package |
| `POST` | `/api/packages/{id:guid}/image` | `CUSTOMER` | Upload package photo to MinIO |
| `GET` | `/api/delivery-orders` | `CUSTOMER` | List owned orders (paginated, filterable) |
| `GET` | `/api/delivery-orders/{id:guid}` | `CUSTOMER` | Get owned order detail (includes `aiEtaPrediction`, omits `pickupSecureCode`) |
| `POST` | `/api/delivery-orders` | `CUSTOMER` | Create order (`PENDING`, returns one-time `pickupSecureCode`) |
| `PUT` | `/api/delivery-orders/{id:guid}` | `CUSTOMER` | Update `PENDING` order |
| `POST` | `/api/delivery-orders/{id:guid}/cancel` | `CUSTOMER` | Cancel `PENDING` or `APPROVED` order with `reason` |
| `POST` | `/api/delivery-orders/{id}/confirm-delivery` | `CUSTOMER` | Confirm receipt (`ARRIVED -> DELIVERED`) using `pickupSecureCode` (`423` on 3 wrong guesses) |
| `GET` | `/api/notifications` | `CUSTOMER` | List owned notifications (filterable by `isRead`) |
| `GET` | `/api/notifications/unread-count` | `CUSTOMER` | Get unread notification count |
| `PUT` | `/api/notifications/{id:guid}/read` | `CUSTOMER` | Mark single notification as read |
| `PUT` | `/api/notifications/read-all` | `CUSTOMER` | Mark all unread notifications as read |

### 3.4 Landing Stations, Slots, Drones & Station Operations (`station-api.md`, `drone-api.md`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `GET` | `/api/landing-stations` | Authenticated | List landing stations |
| `GET` | `/api/landing-stations/{id:guid}` | Authenticated (`OPERATOR` own only) | Get station details |
| `POST` | `/api/landing-stations` | `ADMIN` | Create landing station |
| `PUT` | `/api/landing-stations/{id:guid}` | `ADMIN` | Update landing station |
| `PUT` | `/api/landing-stations/{id:guid}/status` | `ADMIN` | Change station status (`OPERATIONAL` / `MAINTENANCE` / `OFFLINE`) |
| `GET` | `/api/landing-stations/{id:guid}/availability` | Authenticated | Get real-time station slot availability |
| `GET` | `/api/landing-stations/{stationId:guid}/slots` | Authenticated (`OPERATOR` own only) | List slots for a station |
| `GET` | `/api/station-slots/{id:guid}` | Authenticated (`OPERATOR` own only) | Get single slot detail |
| `POST` | `/api/landing-stations/{stationId:guid}/slots` | `ADMIN` | Add slot to station (max 20) |
| `DELETE` | `/api/station-slots/{id:guid}` | `ADMIN` | Remove `EMPTY` slot |
| `PUT` | `/api/station-slots/{id:guid}/status` | `ADMIN`, `OPERATOR` (own) | Toggle slot between `EMPTY` and `ERROR` |
| `GET` | `/api/drones` | `ADMIN`, `DISPATCHER`, `MANAGER` | List drone fleet |
| `GET` | `/api/drones/{id:guid}` | `ADMIN`, `DISPATCHER`, `MANAGER` | Get drone details |
| `POST` | `/api/drones` | `ADMIN` | Register drone (`409` on duplicate serial) |
| `PUT` | `/api/drones/{id:guid}` | `ADMIN` | Update drone specifications |
| `PUT` | `/api/drones/{id:guid}/maintenance` | `ADMIN`, `DISPATCHER` | Toggle drone maintenance (`409` if active mission) |
| `GET` | `/api/landing-stations/{stationId:guid}/operations/orders/{trackingNumber}` | `ADMIN`, `OPERATOR` (own) | Look up order at station (masks phone & pickup code) |
| `POST` | `/api/landing-stations/{stationId:guid}/operations/intake` | `ADMIN`, `OPERATOR` (own) | Physical intake & dimension check (`APPROVED -> READY_FOR_TAKEOFF`, slot `OCCUPIED`) |
| `POST` | `/api/landing-stations/{stationId:guid}/operations/intake-reject` | `ADMIN`, `OPERATOR` (own) | Reject intake (`APPROVED -> FAILED`) |
| `POST` | `/api/landing-stations/{stationId:guid}/operations/pickup` | `ADMIN`, `OPERATOR` (own) | Confirm recipient pickup (`ARRIVED -> DELIVERED`, releases slot, `423` on lockout) |
| `POST` | `/api/landing-stations/{stationId:guid}/operations/validate-code` | `ADMIN`, `OPERATOR` (own) | Pre-validate `pickupSecureCode` (shares 3-attempt lockout state) |
| `GET` | `/api/landing-stations/{stationId:guid}/operations/expired-parcels` | `ADMIN`, `OPERATOR` (own) | List expired/24h-timeout parcels still occupying destination slots |
| `POST` | `/api/landing-stations/{stationId:guid}/operations/clear-expired-parcel` | `ADMIN`, `OPERATOR` (own) | Clear expired parcel and release destination slot |

### 3.5 Dispatch Workflow, Telemetry, Tracking & Analytics (`delivery-workflow-api.md`, `sprint-6-api-contract.md`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `GET` | `/api/dispatch/pending-orders` | `DISPATCHER`, `ADMIN` | List `PENDING` orders awaiting review |
| `POST` | `/api/delivery-orders/{id:guid}/approve` | `DISPATCHER`, `ADMIN` | Approve `PENDING` order (`PENDING -> APPROVED`) |
| `POST` | `/api/delivery-orders/{id:guid}/reject` | `DISPATCHER`, `ADMIN` | Reject `PENDING` order (`PENDING -> CANCELLED`) |
| `GET` | `/api/dispatch/approved-orders` | `DISPATCHER`, `ADMIN` | List `APPROVED` queue with assignment status |
| `GET` | `/api/dispatch/orders/{id}` | `DISPATCHER`, `ADMIN` | Get dispatch order detail |
| `PUT` | `/api/delivery-orders/{id}/schedule` | `DISPATCHER`, `ADMIN` | Set `scheduledDepartureTime` (`>= 15` min future) |
| `GET` | `/api/delivery-orders/{id}/available-drones` | `DISPATCHER`, `ADMIN` | Evaluate drone payload/range/schedule eligibility |
| `POST` | `/api/delivery-orders/{id}/flight-mission` | `DISPATCHER`, `ADMIN` | Assign drone (`PLANNED` mission, reserves destination slot) |
| `GET` | `/api/dispatch/active-deliveries` | `DISPATCHER`, `ADMIN` | List active `IN_TRANSIT` deliveries |
| `POST` | `/api/delivery-orders/{id}/fail` | `DISPATCHER`, `ADMIN` | Report flight failure (`IN_TRANSIT/ARRIVED -> FAILED`) |
| `GET` | `/api/dispatch/failed-orders` | `DISPATCHER`, `ADMIN` | List `FAILED` orders awaiting resolution |
| `GET` | `/api/dispatch/failed-orders/{id}` | `DISPATCHER`, `ADMIN` | Get failure episode & resolution history |
| `POST` | `/api/delivery-orders/{id}/resolve-failure` | `DISPATCHER`, `ADMIN` | Resolve failure (`RESCHEDULE`, `CANCEL`, `RETURN_TO_ORIGIN`) |
| `POST` | `/api/flight-missions/{missionId:guid}/telemetry` | `ADMIN` | Ingest telemetry sample (for simulator/admin demo) |
| `GET` | `/api/delivery-orders/{id}/tracking` | `CUSTOMER` (owner), `DISPATCHER`, `MANAGER` | Get tracking snapshot (`lastPosition`, `telemetryStale`) |
| `SignalR` | `/hubs/tracking` | Authenticated | Real-time `Subscribe(orderId)` push (`PositionUpdated`, `StatusChanged`) |
| `GET` | `/api/analytics/dashboard` | `MANAGER` | Date-bounded (`UTC+07:00`, `<= 366` days) KPI dashboard |
| `GET` | `/api/analytics/reports/deliveries` | `MANAGER` | Paginated delivery report rows |

### 3.6 External Partner v1 API & Integration Keys (`sprint-6-api-contract.md`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `POST` | `/api/admin/integration-keys` | `ADMIN` | Provision scoped external API key |
| `GET` | `/api/admin/integration-keys` | `ADMIN` | List integration keys |
| `DELETE` | `/api/admin/integration-keys/{id}` | `ADMIN` | Revoke integration key |
| `POST` | `/api/v1/delivery-orders` | `X-API-Key` (`orders:create`) | Create external delivery order |
| `GET` | `/api/v1/delivery-orders/{id}` | `X-API-Key` (`orders:read`) | Read scoped order detail |
| `GET` | `/api/v1/delivery-orders/{id}/tracking` | `X-API-Key` (`orders:read`) | Read scoped order tracking |

### 3.7 AI Assistance Endpoints (`sprint-7-api-contract.md`)

| Method | Endpoint | Auth / Roles | Description |
| :---: | :--- | :--- | :--- |
| `GET` | `/api/delivery-orders/{id:guid}/eta` | `CUSTOMER` (owner), `DISPATCHER`, `MANAGER` | Read stored order ETA and `aiEtaPrediction` (`Cache-Control: no-store`) |
| `POST` | `/api/delivery-orders/{id:guid}/ai-summary` | `CUSTOMER` (owner), `DISPATCHER`, `MANAGER` | Transient grounded delivery summary (`429` concurrent, `503` if adapter disabled) |
| `POST` | `/api/ai-assistant/messages` | `CUSTOMER` | Grounded assistant exchange (32 KiB limit, `503` if generation/retention disabled) |
| `GET` | `/api/ai-assistant/sessions` | `CUSTOMER` | List owned assistant sessions |
| `GET` | `/api/ai-assistant/sessions/{id:guid}/messages` | `CUSTOMER` | Read owned session messages (bigint decimal-string IDs) |
| `DELETE` | `/api/ai-assistant/sessions/{id:guid}` | `CUSTOMER` | Delete owned session and messages atomically |
