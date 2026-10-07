# API Contract - Authentication, Roles and Profile

Contract for the first API group of SmartDroneDelivery (Sprint 2: LA-15 authentication, LA-16 role-based access control, LA-17 user profile). Backend, web and mobile all build against this document. The backend implements it exactly; the web and mobile teams use the examples as mock data until the real API is ready.

> **Status: proposed, to be confirmed by the team.** Any change goes through a pull request that edits this file, and the change is announced to the team.

## 1. Conventions

| Item | Rule |
|---|---|
| Base URL (local) | `http://localhost:5055` |
| Path prefix | `/api` |
| Format | JSON, UTF-8, property names in camelCase |
| Identifiers | UUID strings, except `role.id` which is an integer |
| Dates | ISO 8601 in UTC, for example `2026-10-15T08:30:00Z` |
| Authentication | `Authorization: Bearer <accessToken>` on every endpoint marked "Auth: yes" |
| Content type | `Content-Type: application/json` (the avatar upload uses `multipart/form-data`) |

### Error format

Every error returns the same body (RFC 7807 problem details):

```json
{
  "type": "https://httpstatuses.com/401",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password.",
  "errors": { "email": ["Email is required."] }
}
```

`errors` is only present for validation failures (`400`) and lists the messages per field.

| Status | Meaning |
|---|---|
| 400 | Validation failed |
| 401 | Missing, invalid or expired token, or wrong credentials |
| 403 | Authenticated but the role or account status is not allowed |
| 404 | Resource not found |
| 409 | Conflict, for example email or phone already used |

### Roles

| id | code | Name |
|---|---|---|
| 1 | `CUSTOMER` | Customer |
| 2 | `DISPATCHER` | Dispatcher |
| 3 | `STATION_OPERATOR` | Station Operator |
| 4 | `MANAGER` | Logistics Manager |
| 5 | `ADMIN` | System Administrator |

Public registration always creates a `CUSTOMER`. Staff accounts are created by an administrator (LA-20).

### Tokens

| Token | Lifetime (proposed) | Notes |
|---|---|---|
| Access token | 15 minutes | JWT with claims `sub` (user id), `email`, `role` (role code), `exp` |
| Refresh token | 7 days | Random string stored in `refresh_tokens`, rotated on every refresh, revoked on logout |

When a request returns `401` because the access token expired, the client calls `POST /api/auth/refresh` once, retries the original request, and goes to the login screen if the refresh also fails.

### Shared objects

`User`
```json
{
  "id": "6f1c8c1e-3a3e-4a52-9b47-0c3f4a8d9e11",
  "email": "an.nguyen@example.com",
  "fullName": "Nguyen Van An",
  "phoneNumber": "0901234567",
  "avatarUrl": null,
  "status": "ACTIVE",
  "role": { "id": 1, "code": "CUSTOMER", "name": "Customer" },
  "createdAt": "2026-10-15T08:30:00Z"
}
```

`AuthResponse`
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI2ZjFj...",
  "refreshToken": "q3Zk8vV0m1X0uJb2o9n4yQ7gF5tRwLhC",
  "expiresIn": 900,
  "user": { "...": "User object as above" }
}
```
`expiresIn` is the access token lifetime in seconds.

Validation rules used below:

| Field | Rule |
|---|---|
| `email` | required, valid email, at most 255 characters, unique |
| `password` | required, 8 to 100 characters, at least one letter and one digit |
| `fullName` | required, at most 150 characters |
| `phoneNumber` | required, 9 to 11 digits, unique |

## 2. Authentication (LA-15)

### POST /api/auth/register
Create a customer account. Auth: no.

Request
```json
{
  "email": "an.nguyen@example.com",
  "password": "Passw0rd123",
  "fullName": "Nguyen Van An",
  "phoneNumber": "0901234567"
}
```
Response `201 Created`: `AuthResponse` (the user is signed in right after registering).

Errors: `400` validation, `409` email or phone already used:
```json
{ "type": "https://httpstatuses.com/409", "title": "Conflict", "status": 409, "detail": "Email is already registered." }
```

### POST /api/auth/login
Sign in. Auth: no.

Request
```json
{ "email": "an.nguyen@example.com", "password": "Passw0rd123" }
```
Response `200 OK`: `AuthResponse`.

Errors:
- `400` email or password missing.
- `401` wrong email or password (the message does not say which one).
- `403` account status is `INACTIVE` or `SUSPENDED`:
```json
{ "type": "https://httpstatuses.com/403", "title": "Forbidden", "status": 403, "detail": "This account is not active." }
```

### POST /api/auth/refresh
Exchange a valid refresh token for a new pair of tokens. The old refresh token is revoked. Auth: no.

Request
```json
{ "refreshToken": "q3Zk8vV0m1X0uJb2o9n4yQ7gF5tRwLhC" }
```
Response `200 OK`: `AuthResponse`.

Errors: `400` token missing, `401` token unknown, expired or already revoked.

### POST /api/auth/logout
Revoke the refresh token. Auth: yes.

Request
```json
{ "refreshToken": "q3Zk8vV0m1X0uJb2o9n4yQ7gF5tRwLhC" }
```
Response `204 No Content`. Calling it with an unknown or already revoked token also returns `204`.

## 3. Role-based access control (LA-16)

No separate endpoint. Every endpoint states the roles that may call it. A call without a valid token returns `401`, and a call with a valid token but a role that is not allowed returns `403`:

```json
{ "type": "https://httpstatuses.com/403", "title": "Forbidden", "status": 403, "detail": "You do not have permission to perform this action." }
```

Access of the endpoints in this document:

| Endpoint | Allowed roles |
|---|---|
| `POST /api/auth/register`, `login`, `refresh` | anyone |
| `POST /api/auth/logout` | any signed-in user |
| `GET`, `PUT /api/users/me`, `PUT /api/users/me/password`, `POST /api/users/me/avatar` | any signed-in user |

## 4. User profile (LA-17)

All endpoints in this section: Auth: yes, roles: any signed-in user.

### GET /api/users/me
Response `200 OK`: `User`.

### PUT /api/users/me
Update the profile. Email and role cannot be changed here.

Request
```json
{ "fullName": "Nguyen Van An", "phoneNumber": "0907654321" }
```
Response `200 OK`: `User`.

Errors: `400` validation, `409` phone already used.

### PUT /api/users/me/password
Request
```json
{ "currentPassword": "Passw0rd123", "newPassword": "NewPassw0rd456" }
```
Response `204 No Content`. All refresh tokens of the user are revoked, so other devices must sign in again.

Errors: `400` new password too weak, `401` the current password is wrong.

### POST /api/users/me/avatar
Upload an avatar. Content type `multipart/form-data` with one file field named `file` (JPEG or PNG, at most 5 MB). The file is stored in MinIO.

Response `200 OK`:
```json
{ "avatarUrl": "http://localhost:9000/avatars/6f1c8c1e-3a3e-4a52-9b47-0c3f4a8d9e11.png" }
```

Errors: `400` wrong file type or file too large.

## 5. Mock data for web and mobile

Until the real API is ready, the clients return these fixtures. Use the same values so a switch to the real API changes nothing in the screens.

| Case | Request | Mock response |
|---|---|---|
| Login success | any email and password `Passw0rd123` | `200` with the `AuthResponse` example |
| Login wrong password | any other password | `401` with detail `Invalid email or password.` |
| Login inactive account | email `suspended@example.com` | `403` with detail `This account is not active.` |
| Register duplicate | email `an.nguyen@example.com` | `409` with detail `Email is already registered.` |
| Refresh success | any refresh token | `200` with a new `AuthResponse` |
| Refresh invalid | refresh token `invalid` | `401` |
| Get profile | valid access token | `200` with the `User` example |
| Get profile unauthenticated | no token | `401` |

## 6. Backend implementation notes

- Passwords are hashed with BCrypt; plain passwords are never stored or logged.
- The access token is signed with a secret read from configuration (environment variable), never committed to git.
- `POST /api/auth/refresh` marks the old row in `refresh_tokens` as revoked (`IsRevoked = true`) and inserts a new one.
- Password change and logout write an entry in `audit_logs`.
- The Swagger page at `/swagger` must show the same paths and field names as this document.
