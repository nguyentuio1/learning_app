# Function Descriptions - User Management and Analytics

**Project:** SmartDroneDelivery

**Parent Ticket:** [LA-195](https://hungndemail.atlassian.net/browse/LA-195) - Document the detailed functional requirements per module

**Ticket:** [LA-219](https://hungndemail.atlassian.net/browse/LA-219) - Functions of User Management and Analytics modules

**Status:** Proposed for review; unresolved decisions are listed in Section 7.

## 1. Purpose and scope

This document describes authentication, authorization, profile management, administration of users and roles, delivery analytics and AI-assisted services. Each function specifies its trigger, actors, purpose, input, validation, processing, saving, output, business rules and screen. The structure follows the fields stated in LA-195; a separate original course-template file was not available in the repository materials reviewed.

SmartDroneDelivery manages delivery operations, not drone flight control. Viewing an ETA, summary or dashboard does not approve an order, change a delivery status or command a drone.

The primary scope is FR-01 to FR-05 and FR-28 to FR-31. FR-32 and FR-33 are included as supporting administration functions so user and role changes have a traceable audit requirement. Order decisions are only referenced as producers of audit events; their workflows remain with the Delivery Workflow module. FR-06 to FR-27 and FR-34 are not specified here. Completing this document does not by itself complete all FR coverage for LA-195.

This is a module-level requirements document, not a new web or mobile implementation. Customer and Station Operator functions retain their existing client allocation; references to their screens do not authorize additional Customer or Operator web pages.

### 1.1 Source basis

| Source | Use in this document |
|---|---|
| [SRS](SRS.md), Sections 3.1, 3.6 and 3.7 | Functional requirement identifiers, priorities and scope |
| [Use cases](use-cases.md) | UC-01, UC-02, UC-09, UC-19, UC-20, UC-21 and UC-22; actor responsibilities |
| [Authentication API contract](../api/auth-api.md) | Authentication/profile inputs, responses, roles and validation; this source labels itself proposed |
| [Architecture](../architecture/architecture.md), [ERD](../architecture/erd.md), [database design](../architecture/erd.dbml) | Components, entity names, persisted fields and relationships |
| [Authentication/profile activities](../design/activity-auth-and-profile.md) | Login, renewal, logout and profile branches; discrepancies are noted in Section 7 |
| [Analytics/AI activities](../design/activity-analytics-and-ai-assistant.md) | Supporting workflows, with extensions distinguished from SRS requirements |
| [Manager flow](../design/user-flows/logistics-manager.md), [Administrator flow](../design/user-flows/system-administrator.md) | Existing screen navigation and role boundaries |
| [AuthService](../../backend/SmartDroneDelivery.Api/Services/AuthService.cs), [UserService](../../backend/SmartDroneDelivery.Api/Services/UserService.cs), [AuthDtos](../../backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs), [UsersController](../../backend/SmartDroneDelivery.Api/Controllers/UsersController.cs) | Current authentication/profile behavior, not evidence that future analytics/admin APIs exist |

The supplied proposal and Jira screenshots provide task context. Local documents and the source files above were read directly. No original course template or additional document from an earlier chat is claimed as verified. API paths are stated only where the current authentication/profile contract defines them; admin and AI paths remain to be specified by their owners.

## 2. Actors and conventions

| Actor | Role code | Responsibilities in scope |
|---|---|---|
| Customer | `CUSTOMER` | Register; manage own session/profile; view own delivery insights and use own-data AI chat |
| Dispatcher | `DISPATCHER` | Manage own session/profile; view delivery ETA and summary within operational permissions |
| Station Operator | `STATION_OPERATOR` | Manage own session/profile; station operations are specified elsewhere |
| Logistics Manager | `MANAGER` | Manage own session/profile; view statistics, reports and delivery insights |
| System Administrator | `ADMIN` | Manage own session/profile; manage users/roles and inspect audit logs |

- “All users” means the five roles above. An authenticated request must also pass account-status, role and resource-ownership checks appropriate to that function.
- Field names in API descriptions are camelCase; database fields use snake_case. Dates returned by the authentication/profile API use ISO 8601 UTC.
- `400` denotes validation failure, `401` missing/invalid authentication or invalid credentials, `403` denied access, and `409` a uniqueness conflict. Password verification has the specific exception described in F-07.
- Screens show loading, success, empty and failure states as applicable. A failed request must not be shown as a completed save or a valid zero-valued report.
- Business rules are numbered BR-UA-01 to BR-UA-24 **within this document**. These are local identifiers pending consolidation by LA-220, not a claim that the project-wide rule numbers have been assigned. Cross-document references should include `LA-219` until that consolidation is approved.
- **Baseline** rules restate SRS, API or existing flow requirements. **Derived** rules make safe processing/error behavior explicit and require team review. **Proposed** decisions are not approved business policy.

## 3. Requirements traceability

| SRS requirement | Priority | Use case | Matching function descriptions |
|---|---|---|---|
| FR-01 Register and log in | M | UC-01 | F-01, F-02 |
| FR-02 Token issue, renewal and revocation | M | UC-01 | F-02, F-03, F-04 |
| FR-03 Restrict endpoints and screens by role | M | UC-01 | F-05; authorization applies to all protected functions |
| FR-04 View/update profile, password and avatar | M | UC-02 | F-06, F-07, F-08 |
| FR-05 Create/deactivate users and assign roles | M | UC-21 | F-09, F-10, F-11, F-12 |
| FR-28 Statistics with date filters | M | UC-19 | F-13, F-14 |
| FR-29 Estimate and update delivery time | M | UC-20 | F-15 |
| FR-30 Summarize delivery history | S | UC-20 | F-16 |
| FR-31 Customer-owned-data chat assistant | M | UC-09 | F-17 |
| FR-32 Record sensitive actions | M | UC-22 | F-18, referenced by authentication and administration functions |
| FR-33 Search audit logs | S | UC-22 | F-19 |

### 3.1 Coverage boundaries

FR-06 to FR-12 belong to Customer/Order descriptions, FR-13 to FR-19 to Delivery Workflow, and FR-20 to FR-23 to Landing Station descriptions. The LA-195 owner must allocate FR-24 to FR-27 (tracking/confirmation) and FR-34 (external APIs) in the overall matrix; this document does not invent an owner for them. System monitoring and backups are UC-23/NFR-08 concerns, not extra functions assigned by LA-219's description.

## 4. User Management functions

### F-01 Register a Customer account

| Item | Description |
|---|---|
| Requirement / use case | FR-01, FR-02; UC-01 |
| Trigger | A visitor submits the registration form. |
| Actors / purpose | Prospective Customer; create an account for the delivery service. |
| Preconditions | Public registration is available; the visitor does not choose a staff role. |
| Input | `email`, `password`, `fullName`, `phoneNumber`. |
| Validation | Required fields; email format/length; password rules; full name length; phone digits; unique normalized email and phone. |
| Processing | Normalize email, trim name/phone, hash the password, assign `CUSTOMER` and initial `ACTIVE` status, and issue an authenticated session. |
| Saving | Create `users`, a `refresh_tokens` row and a registration audit event. Do not store the plain password. |
| Output / exceptions | `POST /api/auth/register`: `201` with `AuthResponse`; `400` for invalid input; `409` for an existing email/phone. No successful registration is reported before persistence succeeds. |
| Business rules | BR-UA-01, BR-UA-02, BR-UA-03, BR-UA-19. |
| Screen | Customer registration in the allocated Customer client, then its authorized entry screen. |

### F-02 Log in

| Item | Description |
|---|---|
| Requirement / use case | FR-01, FR-02; UC-01 |
| Trigger | User submits email and password on Login. |
| Actors / purpose | All users; establish an authenticated session. |
| Preconditions | An account exists and is eligible to sign in. |
| Input | `email`, `password`. |
| Validation | Both values present; normalize email; verify stored password hash and `ACTIVE` status. |
| Processing | Load the user and role; issue an access token and refresh token; navigate only to a permitted screen. |
| Saving | Insert a refresh token and `LOGIN` audit event; client holds session state without persisting the password. |
| Output / exceptions | `POST /api/auth/login`: `200` with `AuthResponse`; `400` for missing input; the same `401` message for unknown email and wrong password; `403` for inactive/suspended accounts. Network failure leaves the user on Login. |
| Business rules | BR-UA-03, BR-UA-04, BR-UA-05, BR-UA-19. |
| Screen | Login and the role-appropriate entry screen; no shared unrestricted dashboard is implied. |

### F-03 Renew a session

| Item | Description |
|---|---|
| Requirement / use case | FR-02; UC-01 |
| Trigger | Client needs to restore a session or receives an authentication-related `401` for an expired access token. |
| Actors / purpose | Client acting for any user; continue an authorized session without requesting the password again. |
| Preconditions | Client holds a refresh token. |
| Input | `refreshToken`. |
| Validation | Token exists, is not expired/revoked, and belongs to an active account. |
| Processing | Revoke the old refresh token, issue a new pair using current account/role data, replace client tokens and retry the original eligible request once. |
| Saving | Update the old `refresh_tokens` row and insert the new one. |
| Output / exceptions | `POST /api/auth/refresh`: `200` with `AuthResponse`; missing token gives `400`; unknown, expired, revoked or ineligible token gives `401`. Failed renewal returns the client to Login. Do not loop on refresh or treat a wrong current password as session expiry. |
| Business rules | BR-UA-05, BR-UA-06, BR-UA-07, BR-UA-10. |
| Screen | Background session-restoration/loading state; Login when renewal fails. |

### F-04 Log out

| Item | Description |
|---|---|
| Requirement / use case | FR-02; UC-01 |
| Trigger | User selects Sign out. |
| Actors / purpose | All users; end the local session and revoke its refresh token. |
| Preconditions | A local session exists; server logout requires a valid access token. |
| Input | Bearer access token and `refreshToken`. |
| Validation | Authenticate the caller; only revoke a refresh token belonging to that caller. |
| Processing | Revoke the eligible token; clear local credentials and user-specific state; return to Login. |
| Saving | Mark the token revoked and write `LOGOUT` to `audit_logs`. |
| Output / exceptions | `POST /api/auth/logout`: `204`; an unknown/already-revoked token is handled idempotently, and a foreign token is not revoked. If server revocation cannot be confirmed, local sign-out may still complete but the client must not claim remote revocation succeeded. |
| Business rules | BR-UA-05, BR-UA-08, BR-UA-19. |
| Screen | Sign-out action on authenticated screens; Login afterwards. |

### F-05 Authorize access by role and ownership

| Item | Description |
|---|---|
| Requirement / use case | FR-03; UC-01 and all protected use cases |
| Trigger | A user opens a protected screen or calls a protected API. |
| Actors / purpose | All users and the authorization service; prevent unauthorized operations/data access. |
| Preconditions | The screen/endpoint has an explicit access policy. |
| Input | Access token, requested operation and resource identifier where applicable. |
| Validation | Token validity and expiry; allowed role; account eligibility and ownership constraints required by the resource. |
| Processing | Resolve identity from the validated token, never a submitted user ID or a client role selector; allow or deny access before returning protected data or mutating records. |
| Saving | No business-data write is required for authorization itself. Sensitive successful actions are logged by F-18. |
| Output / exceptions | Permitted action, `401` for missing/invalid authentication, or `403` for denied role/status. Hiding a menu does not replace API enforcement. |
| Business rules | BR-UA-01, BR-UA-04, BR-UA-07, BR-UA-15, BR-UA-18. |
| Screen | Role-appropriate navigation; Login for missing session; Access denied for insufficient permissions. |

### F-06 View and update own profile

| Item | Description |
|---|---|
| Requirement / use case | FR-04; UC-02 |
| Trigger | User opens My profile or selects Save profile. |
| Actors / purpose | All users; maintain their own personal details. |
| Preconditions | Signed-in, active account; identity comes from the session. |
| Input | Read: no target-user field. Update: `fullName`, `phoneNumber`. |
| Validation | Non-empty name, at most 150 characters; phone contains 9–11 digits and is not used by another user. The user's current phone is allowed. |
| Processing | Read the current user; on save, validate/trim the editable fields and update only those fields. Email, role, status and creation date are read-only here. |
| Saving | Update `users.full_name`, `phone_number`, `updated_at`; viewing does not write data. |
| Output / exceptions | `GET /api/users/me` and `PUT /api/users/me`: `200` with User; `400` field validation, `409` duplicate phone, `401/403` access errors. Keep unsaved input after a failed save; reload shows persisted values. |
| Business rules | BR-UA-02, BR-UA-04, BR-UA-09. |
| Screen | My profile: editable personal details and read-only account information. |

### F-07 Change own password

| Item | Description |
|---|---|
| Requirement / use case | FR-04; UC-02 |
| Trigger | User submits Change password. |
| Actors / purpose | All users; replace their current password securely. |
| Preconditions | Signed-in, active account. |
| Input | `currentPassword`, `newPassword`; confirmation is a client-side field only. |
| Validation | Verify current password; new password meets BR-UA-03, differs from the current one and matches confirmation in the UI. |
| Processing | Hash the new password; replace the stored hash; revoke all of this user's refresh tokens. |
| Saving | Update `users.password_hash`/`updated_at`, revoke tokens, write `PASSWORD_CHANGE` audit event as used by the current backend. |
| Output / exceptions | `PUT /api/users/me/password`: `204`; `400` for an invalid/unchanged new password. The specific `401` detail `Current password is incorrect.` is a field error, not a reason to refresh or automatically sign out. Other authentication-related `401` responses still follow F-03. Clear password inputs after success. |
| Business rules | BR-UA-03, BR-UA-08, BR-UA-10, BR-UA-19. |
| Screen | Change password section of My profile. |

### F-08 Upload own avatar

| Item | Description |
|---|---|
| Requirement / use case | FR-04; UC-02 |
| Trigger | User chooses a photo and confirms upload. |
| Actors / purpose | All users; set or replace their profile image. |
| Preconditions | Signed-in, active account; object storage is available. |
| Input | Multipart field `file`; a local preview is not a saved avatar. |
| Validation | Non-empty JPEG or PNG, at most 5 MiB (5 × 1024 × 1024 bytes). Backend checks image signature rather than trusting filename/MIME alone. |
| Processing | Upload to MinIO, update the user's avatar URL, then display the returned image. Handle an overwritten URL without displaying a stale cached image. |
| Saving | Store the image in the configured avatar bucket; update `users.avatar_url`/`updated_at`. Current backend attempts cleanup when the previous image has a different URL. |
| Output / exceptions | `POST /api/users/me/avatar`: `200` with `avatarUrl`; `400` with `errors.file` for invalid input; storage/server failure must not be reported as a saved photo. Replacement of the same storage object is not assumed to be transactional with the database. |
| Business rules | BR-UA-09, BR-UA-11. |
| Screen | Avatar picker and preview in My profile, upload confirmation and feedback. No delete-avatar operation is defined by the current API. |

### F-09 Find users and view account details

| Item | Description |
|---|---|
| Requirement / use case | Supports FR-05; UC-21 |
| Trigger | Administrator opens Users or selects an account. |
| Actors / purpose | System Administrator; locate the target for user-management actions. |
| Preconditions | Administrator authorization succeeds. |
| Input | Search/filter values and page parameters; selected user identifier. Exact supported filters await the admin API contract. |
| Validation | Validate page parameters and target existence; never accept a client-supplied role as permission. |
| Processing | Query a paginated list of permitted account fields or load one account and its assigned role. |
| Saving | Read-only; no changes to users or roles. |
| Output / exceptions | List/detail or empty state; access denial or missing-user message. Password hashes and refresh tokens are never displayed. Admin endpoint paths are not defined by this document. |
| Business rules | BR-UA-01, BR-UA-12, BR-UA-22. |
| Screen | Administration dashboard → Users and roles → User detail. |

### F-10 Create a user as Administrator

| Item | Description |
|---|---|
| Requirement / use case | FR-05; UC-21 |
| Trigger | Administrator submits Create user. |
| Actors / purpose | System Administrator; provision an account, including staff accounts unavailable through public registration. |
| Preconditions | Administrator authorization; selected role exists. |
| Input | Full name, email, phone and one role. Credential provisioning is unresolved in Section 7; no invitation/reset endpoint is assumed. |
| Validation | Required identity fields and unique normalized email/phone; validate role against the five-role catalog. Any assigned password must follow BR-UA-03. |
| Processing | Validate the chosen role and identity data, create the user through the approved credential-provisioning process and record the actor. |
| Saving | Insert `users` with a password hash when credentials are established; link `role_id`; record an audit event. Initial status and provisioning sequence require the admin contract. |
| Output / exceptions | Created account after persistence; field errors or uniqueness conflict without a false success. Do not display stored credentials. |
| Business rules | BR-UA-01, BR-UA-02, BR-UA-03, BR-UA-12, BR-UA-19. |
| Screen | Users and roles → Create user → User detail/list. |

### F-11 Assign a role

| Item | Description |
|---|---|
| Requirement / use case | FR-05; UC-21 |
| Trigger | Administrator selects a role for a user and confirms. |
| Actors / purpose | System Administrator; assign the account's permitted business responsibilities. |
| Preconditions | Administrator authorization; target account and role exist. |
| Input | Target user ID and one of the five role IDs/codes. |
| Validation | Reject unknown/multiple roles; check current target state at save time. Self-demotion/last-admin safeguards require the policy decision in Section 7. |
| Processing | Replace the role assignment and record the acting administrator. Existing tokens do not magically acquire new role claims. |
| Saving | Update `users.role_id`/`updated_at`; record the role-change audit event. |
| Output / exceptions | Updated role or validation/access/conflict message; permissions in newly issued tokens reflect the assignment. Immediate access-token revocation is not promised. |
| Business rules | BR-UA-01, BR-UA-07, BR-UA-12, BR-UA-19. |
| Screen | User detail / Roles selection with confirmation. This is not a custom-role or permission-editor feature. |

### F-12 Deactivate a user

| Item | Description |
|---|---|
| Requirement / use case | FR-05; UC-21 |
| Trigger | Administrator confirms Deactivate on User detail. |
| Actors / purpose | System Administrator; prevent further account use while retaining business history. |
| Preconditions | Administrator authorization and existing target account. |
| Input | Target user ID and confirmation. A mandatory reason is not established by the current sources. |
| Validation | Re-check target status and administrator rights. Handling an already-inactive target and self/last-admin safeguards must be agreed in the admin contract. |
| Processing | Set the account to `INACTIVE`; prevent subsequent login/refresh and protected operations that require an active account. |
| Saving | Update `users.status`/`updated_at` and write an audit event; retain linked orders, packages and historical records. |
| Output / exceptions | Updated account status or explicit failure; no physical deletion. The handling of already-issued JWTs must follow the agreed authorization policy, not a UI-only block. |
| Business rules | BR-UA-04, BR-UA-12, BR-UA-13, BR-UA-19. |
| Screen | User detail → Deactivation confirmation → Updated user list/detail. |

## 5. Analytics and AI functions

### F-13 View filtered delivery dashboard

| Item | Description |
|---|---|
| Requirement / use case | FR-28; UC-19 |
| Trigger | Manager opens the dashboard or applies a date filter. |
| Actors / purpose | Logistics Manager; inspect delivery performance. Administrator access to this business dashboard is not assumed. |
| Preconditions | Manager authorization; reporting definitions approved before metric acceptance. |
| Input | Reporting date range; optional station/status/fleet filters need the analytics contract. |
| Validation | Dates are valid and start is not after end; use the agreed reporting timezone and boundary convention. |
| Processing | Query authorized orders and station-related records; compute orders by status, success rate, average delivery time and station usage for the same selected period. Section 7 identifies definitions that the SRS does not specify. |
| Saving | No operational records are changed. Caching/persisted aggregates are implementation decisions, not new business entities required here. |
| Output / exceptions | Metric cards/charts with period, units and data freshness. Empty data shows zero counts but undefined ratios/durations as N/A, not a fabricated success rate; invalid filters and service failure show distinct errors. |
| Business rules | BR-UA-14, BR-UA-15, BR-UA-16, BR-UA-23. |
| Screen | Logistics dashboard → Date filter → Delivery statistics. |

### F-14 View a filtered report and delivery detail

| Item | Description |
|---|---|
| Requirement / use case | FR-28; UC-19, with UC-20 drill-down |
| Trigger | Manager opens Reports, changes the reporting period or selects a delivery. |
| Actors / purpose | Logistics Manager; inspect the records behind operational statistics. |
| Preconditions | Manager authorization. |
| Input | Reporting period and selected delivery ID for drill-down. |
| Validation | Same period rules as F-13; validate that selected records remain accessible and exist. |
| Processing | Build a read-only report using the same definitions and filters as the dashboard; drill down to delivery details and F-15/F-16 insights. |
| Saving | No order, mission or station mutation; report-file persistence is not required by this baseline. |
| Output / exceptions | Report/table, empty state or failure; missing delivery is handled without changing report filters. File export formats/storage are pending, not silently included. |
| Business rules | BR-UA-14, BR-UA-15, BR-UA-16, BR-UA-23. |
| Screen | Reports → Filtered report → Delivery detail → ETA and delivery summary. |

### F-15 Estimate, update and view delivery ETA

| Item | Description |
|---|---|
| Requirement / use case | FR-29; UC-20 |
| Trigger | An eligible order needs an estimate, relevant conditions change, or an authorized user opens its latest ETA. Exact recalculation events/cadence require agreement. |
| Actors / purpose | ETA service; Customer, Dispatcher and Logistics Manager as viewers; provide an estimated arrival time. |
| Preconditions | Order exists; viewer has access; sufficient valid inputs are available. |
| Input | Order/stations, relevant schedule or departure time, route-distance inputs and available mission/telemetry/context data. Weather is optional where a verified source exists. |
| Validation | Ownership for Customers; operational authorization for staff; validate timestamps, coordinates and freshness. Reject impossible/negative durations; optional confidence must be in [0, 1]. |
| Processing | Calculate distance/duration with the selected model, derive the arrival timestamp using an agreed time anchor and refresh it when qualifying conditions change. Reading the estimate alone need not invoke the model again. |
| Saving | Store the current prediction in `ai_eta_predictions` and the applicable `delivery_orders.estimated_delivery_time`. Current EF mapping supports one prediction per order; history is not assumed. Synchronization and time-anchor rules need the AI contract. |
| Output / exceptions | Estimated arrival with calculation time and optional confidence; missing/stale inputs or model failure produce an unavailable/stale indication. Do not present a past estimate as live or overwrite actual delivery time. |
| Business rules | BR-UA-15, BR-UA-17, BR-UA-18, BR-UA-23. |
| Screen | Customer delivery detail/tracking; Dispatcher delivery detail/tracking; Manager delivery detail. |

### F-16 Generate and view a delivery-history summary

| Item | Description |
|---|---|
| Requirement / use case | FR-30 (should have); UC-20 |
| Trigger | Authorized user requests a summary of one delivery. |
| Actors / purpose | Customer, Dispatcher, Logistics Manager; understand the delivery's recorded history quickly. |
| Preconditions | Accessible order with recorded history. |
| Input | Order ID; backend retrieves its status logs, recorded times and relevant verified delivery facts. |
| Validation | Check access before building AI context; order and related history must refer to the same delivery. |
| Processing | Sort relevant events chronologically, generate a short grounded summary and distinguish actual events from predictions. Do not invent reasons for delays or outcomes. |
| Saving | Read existing order/history data. There is no dedicated summary entity in the current ERD; persistent summaries require a separate design decision. |
| Output / exceptions | Labeled generated summary; no-history state or generation error with the original timeline still available. A failed generation must not alter the order. |
| Business rules | BR-UA-15, BR-UA-18, BR-UA-23, BR-UA-24. |
| Screen | Delivery detail → Delivery summary, alongside the factual status timeline. |

### F-17 Ask the Customer AI assistant

| Item | Description |
|---|---|
| Requirement / use case | FR-31; UC-09 |
| Trigger | Customer opens a chat and sends a message. |
| Actors / purpose | Customer and AI assistant service; answer delivery/service questions using that customer's authorized data. |
| Preconditions | Active Customer session; existing chat belongs to the caller, or a new owned session is created. |
| Input | Non-empty message, optional chat-session ID and order/tracking reference. Message/context limits require the AI contract. |
| Validation | Authenticate Customer; verify session ownership and each referenced order before retrieving its contents. User text cannot override server-side access checks. |
| Processing | Resolve the intended owned order; ask for clarification if ambiguous; retrieve only authorized facts and relevant ETA/history; generate a grounded answer. With no matching owned order, show an empty/help response rather than inventing one. |
| Saving | Create/use `ai_chat_sessions` linked to the authenticated user; save USER/BOT messages in `ai_chat_messages`. If generation fails, do not store a fabricated successful BOT answer; retry/idempotency behavior requires the API contract. |
| Output / exceptions | Answer and owned conversation history. For an unknown or foreign reference, respond without revealing another customer's data or confirming the foreign order's existence. Service timeout/failure offers retry. No human handoff workflow is assumed. |
| Business rules | BR-UA-15, BR-UA-18, BR-UA-21, BR-UA-24. |
| Screen | Customer AI Assistant in the allocated Customer client; authorized links may lead to its order detail/tracking. |

## 6. Supporting administration and business rules

### F-18 Record a sensitive action

| Item | Description |
|---|---|
| Requirement / use case | FR-32; UC-22 |
| Trigger | A sensitive action occurs, including login, role change or an order decision. |
| Actors / purpose | Backend on behalf of the acting user; provide accountability and traceability. |
| Preconditions | The owning function identifies the action and target entity. |
| Input | Server-resolved actor, action, target entity/type, timestamp and applicable request metadata. |
| Validation | Do not trust a client-supplied acting-user identity; sanitize metadata and exclude credentials and secure pickup codes. |
| Processing | Produce an event that accurately reflects the operation outcome. Use stable action names; current auth events include REGISTER, LOGIN, LOGOUT and PASSWORD_CHANGE. |
| Saving | Insert into `audit_logs` with actor, action, entity, ID, IP/user agent where available, and timestamp. Coordinated persistence/failure handling must prevent silently losing mandatory audit records. |
| Output / exceptions | Durable audit entry for review. If logging fails, the owning operation must use its agreed failure/recovery policy; no universal transaction mechanism is asserted here. |
| Business rules | BR-UA-19, BR-UA-20. |
| Screen | No standalone entry screen; events appear in Administrator Audit logs. |

### F-19 Search audit logs

| Item | Description |
|---|---|
| Requirement / use case | FR-33 (should have); UC-22 |
| Trigger | Administrator opens Audit logs or applies filters. |
| Actors / purpose | System Administrator; inspect sensitive actions. |
| Preconditions | Administrator authorization. |
| Input | User, action and date filters; pagination. |
| Validation | Date range and page parameters; validate supplied identifiers and filter values under the audit API contract. |
| Processing | Query matching events and return a stable chronological ordering with enough context to identify the action/target. |
| Saving | Read-only; no edit/delete action is defined for the log viewer. |
| Output / exceptions | Paginated results or empty state; clear invalid-filter/access/service errors. Do not expose password hashes, tokens or pickup codes. |
| Business rules | BR-UA-14, BR-UA-19, BR-UA-20, BR-UA-22. |
| Screen | Administration dashboard → Audit logs → Filtered results/event detail. |

### 6.1 Business rules referenced by the functions

| ID | Rule | Basis / status |
|---|---|---|
| BR-UA-01 | Each user has one of CUSTOMER, DISPATCHER, STATION_OPERATOR, MANAGER or ADMIN. Public registration assigns CUSTOMER; it cannot grant a staff role. | Baseline: auth contract, ERD, Administrator flow |
| BR-UA-02 | Registration identity fields follow the auth contract: unique email up to 255 characters, required full name up to 150, unique phone of 9–11 digits. Profile updates may retain the user's own phone number. | Baseline: auth contract/DTOs |
| BR-UA-03 | New passwords contain 8–100 characters, at least one letter and one digit; store only a BCrypt hash, never log/return the plain password. | Baseline: SRS and auth contract |
| BR-UA-04 | Only ACTIVE accounts may log in, refresh and use the own-profile APIs. Unknown email and wrong login password receive the same message. | Baseline: AuthService/UserService; wider status enforcement tracked in Section 7 |
| BR-UA-05 | Access tokens are short-lived; refresh tokens rotate on renewal. The API contract proposes 15 minutes and 7 days; use agreed server configuration, not hard-coded client assumptions. | Baseline: FR-02/auth contract, durations proposed there |
| BR-UA-06 | An unknown, expired or revoked refresh token cannot renew a session. Concurrent client requests should share renewal and retry at most once, without an infinite authentication loop. | Baseline token validity; derived client coordination |
| BR-UA-07 | Validate authorization at the API as well as the UI. Role changes apply to newly issued tokens; existing access tokens may retain old claims until expiry. | Baseline: FR-03/auth contract |
| BR-UA-08 | Logout revokes the caller's submitted refresh token; password change revokes all their refresh tokens. Neither operation is a claim of immediate JWT access-token revocation. | Baseline: auth contract/current services |
| BR-UA-09 | Own-profile operations resolve the user from authentication. Only name/phone and the dedicated password/avatar operations are editable; email, role and status are not editable through profile update. | Baseline: FR-04/auth contract |
| BR-UA-10 | Password change verifies the current password and rejects an unchanged new password. The exact wrong-current-password 401 is a field error, not automatic session expiry. | Baseline: auth contract/UserService |
| BR-UA-11 | Avatar upload accepts non-empty JPEG/PNG up to 5 MiB with server-side content checks and stores the image in MinIO. No avatar-delete endpoint is implied. | Baseline: auth contract/UserService |
| BR-UA-12 | Only ADMIN creates staff accounts, assigns roles or deactivates accounts. The system revalidates the target and authorization when saving. | Baseline: FR-05/Administrator flow; derived revalidation |
| BR-UA-13 | Deactivation sets INACTIVE rather than deleting an account's business history. Reactivation, suspension and last-admin safeguards need separate policy decisions. | Baseline deactivation; unspecified extensions excluded |
| BR-UA-14 | A date range must be valid and ordered. UI/API must agree on timezone, inclusion boundaries and the date field used to select records. | Derived; precise analytics definitions pending |
| BR-UA-15 | Analytics, ETA viewing, summaries and chat are informational: they do not approve/cancel deliveries, confirm pickup or control drone flight. | Baseline: SRS scope and Manager flow |
| BR-UA-16 | FR-28 requires all four measures: orders by status, success rate, average delivery time and station usage. Dashboard/report definitions must agree; zero-denominator measures show N/A. | Baseline measures; derived empty-value behavior |
| BR-UA-17 | ETA is an estimate, not actual arrival. Validate input freshness and mark unavailable/stale results. The current EF design stores one prediction per order, not prediction history. | Baseline FR-29/ERD; derived freshness behavior |
| BR-UA-18 | Customers access only their own orders, sessions and messages; staff access follows the relevant operational role. Verify scope before retrieving AI context, not after generating an answer. | Baseline FR-31/UC-20; derived enforcement placement |
| BR-UA-19 | Sensitive actions have server-attributed audit events. Logs must not include passwords, refresh/access tokens or pickup secure codes. | Baseline FR-32/auth contract; derived data minimization |
| BR-UA-20 | Administrator audit views are read-only and support user/action/date searches. Retention and privileged maintenance policies are outside the viewer. | Baseline FR-33/Administrator flow |
| BR-UA-21 | A chat session belongs to one user; messages belong to that session and use USER/BOT sender types. Never accept an arbitrary session ID as proof of ownership. | Baseline ERD; derived ownership enforcement |
| BR-UA-22 | User and audit lists use bounded pagination; never expose password hashes or token records in account details or list results. Exact limits belong to their API contracts. | Baseline architecture; derived minimum disclosure |
| BR-UA-23 | Keep these order states distinct: PENDING, APPROVED, READY_FOR_TAKEOFF, IN_TRANSIT, ARRIVED, DELIVERED, CANCELLED, FAILED. ARRIVED is not completed delivery; mission statuses are not order statuses. | Baseline SRS/ERD |
| BR-UA-24 | AI summaries/answers use verified authorized facts, distinguish estimates from actual events and report missing data or service failure honestly. AI output does not become an authoritative status update. | Derived from FR-29 to FR-31 and business-oriented scope |

## 7. Open decisions and source discrepancies

These items need leader/module-owner agreement before affected implementation or acceptance tests can be finalized. They do not change the existing SRS in this commit.

| Item | Finding | Treatment / decision needed |
|---|---|---|
| Course template | The task screenshot lists the required fields, but the original course template was not available. | This document includes every listed field. Supply the original if an exact institutional layout is required. |
| Global rule numbering | The BR-UA prefix distinguishes this module's rules from BR-DW, BR-LS and BR-CO; LA-220 owns project-wide consolidation. | Consolidate local BR-UA-01–BR-UA-24 and update references without treating these numbers as a reserved global range. |
| Client allocation | SRS actor table includes Customer/Operator web; its prose and the supplied proposal emphasize their mobile clients; architecture includes Customer web. | Confirm screen allocation. Shared module functions here do not settle that conflict or authorize extra web screens. |
| Analytics traceability | LA-215's matrix maps chat to FR-32/33, unlike the SRS. | Use FR-28 dashboard, FR-29 ETA, FR-30 delivery summary, FR-31 chat; FR-32/33 remain audit. |
| Role names | LA-215 uses LOGISTICS_MANAGER/SYSTEM_ADMIN. | Use MANAGER/ADMIN from the auth contract and current role data. Admin analytics access still needs an explicit decision. |
| Password activity details | LA-213 uses CHANGE_PASSWORD and a different wrong-password message. | Current UserService uses PASSWORD_CHANGE and `Current password is incorrect.`; keep API behavior aligned with the contract/current backend, and review the older activity doc separately. |
| Account administration | Credential provisioning, initial status, self-deactivation/demotion, last active admin protection and concurrent updates are not fully specified. | Admin owner must define them; no invented invitation, reset-password or custom-role feature. Confirm status checks/revocation policy for already-issued tokens on all affected endpoints. |
| Metric definitions | SRS names the four measures but does not define date cohort, denominators or station-usage formula. | Approve the proposal below or replace it before metric acceptance. Do not equate success rate with on-time rate or average delivery time with flight time. |
| Historical station usage | `station_slots` stores current state, not occupancy history. | Historical occupied-slot percentage cannot be reconstructed from that snapshot alone; choose a metric supported by available events or approve additional history. |
| ETA integration | Model, available condition inputs, freshness thresholds, recalculation events and arrival-time anchor are unspecified. | Agree the AI contract, including how duration and `estimated_delivery_time` stay consistent. Terminal states must not be presented as active deliveries. |
| ETA cardinality | DBML's reference permits multiple rows conceptually, while ERD notes and EF mapping describe one current prediction. | Follow current one-to-one implementation for this description; history requires a reviewed model change. |
| Summary persistence | No summary table/field is present in the current model. | Default description is read/generate/return. Approve persistence/versioning separately if required. |
| AI/analytics extensions | LA-215 mentions Redis, read replicas, fixed cache/link TTLs, two-second telemetry, PDF/CSV exports, human handoff and AI operational summaries. | Treat as design proposals, not new committed requirements from this document. Confirm scope, permissions, APIs and storage before including them. Do not pass pickup codes or credentials to AI context. |
| Chat limits/lifecycle | Message limits, retention, model/provider, retry deduplication and timeout policy are not specified. | Agree in AI/API design; failures must not fabricate responses or leak other users' data. |

### 7.1 Proposed metric definitions for review — not approved rules

A concrete candidate is provided to make review actionable. It must not be implemented as approved policy merely because it appears here.

| Measure | Candidate definition | Decision to confirm |
|---|---|---|
| Reporting cohort | Orders with `created_at` in a half-open interval `[start, end)`, derived from user-selected dates in an agreed reporting timezone. Current status is evaluated at report generation time. | Created-date versus completed-date cohort; historical snapshot expectations; timezone |
| Orders by status | Count each distinct order once in its current status within the cohort. | Current status versus status-at-period-end |
| Success rate | `DELIVERED / (DELIVERED + FAILED) × 100` for the cohort; N/A when denominator is zero. | Treatment of cancellations, rescheduled failures and unfinished orders |
| Average delivery time | For DELIVERED orders with valid actual timestamps, mean of `actual_delivery_time - actual_departure_time`, in minutes. Exclude missing/negative durations and show the excluded count. | Departure-to-confirmation versus creation-to-confirmation; treatment of missing data |
| Station usage | Number of distinct cohort orders associated with each station as origin or destination, with origin/destination counts labeled separately. This is throughput, not occupied-slot percentage. | Whether station usage means throughput or historical utilization; additional data needed for utilization |

Example under this proposal only: 6 DELIVERED, 2 FAILED, 1 CANCELLED and 1 IN_TRANSIT produce a success rate of 75%, not 60%. A zero-sized cohort produces zero counts and N/A rate/time. Confirming a different denominator changes this expected result and must update dashboard, reports and their tests together.

## 8. Review and acceptance checklist

These are acceptance scenarios for review, not test results. No application tests are claimed by this documentation task.

| Area | Review / acceptance scenario |
|---|---|
| Coverage | Each of FR-01–05, FR-28–31 and supporting FR-32–33 maps to at least one function; each function has all template fields and resolvable BR references. |
| Authentication | Registration cannot grant a staff role; duplicate identity fails; invalid credentials and inactive accounts are handled; revoked refresh tokens cannot renew a session. |
| Authorization | Direct API calls and direct navigation cannot bypass role restrictions; Customers cannot read another user's profile/order/chat; renewal uses current role data. |
| Profile | Saved name/phone persist; invalid/duplicate phone is rejected; wrong current password stays a field error; valid password change revokes refresh tokens. |
| Avatar | Invalid/empty/oversized files fail; upload success returns the stored URL; a failed upload is not reported as saved. |
| User administration | Only Admin can create/assign/deactivate; changes are auditable; deactivation preserves linked history; unresolved safety/provisioning rules are approved first. |
| Analytics | All four metrics respond to valid date filters; reports use the same definitions; empty data differs from backend failure; agreed calculations match a known dataset. |
| ETA and summary | Qualifying condition changes update ETA; unavailable inputs are explicit; summaries use actual ordered history and do not change status or invent events. |
| Chat | Foreign order/session references reveal no private data; ambiguous requests ask for clarification; model failure produces a retry/error state, not a fabricated answer. |
| Audit | Sensitive actions are attributable; Admin can search by user/action/date; logs contain no credentials or pickup codes and cannot be edited through the viewer. |
| Handoff | Leader/module owners resolve Section 7, LA-220 consolidates BR IDs, and the LA-195 owner completes cross-module FR coverage. |

The document's status is **Proposed for review**. Business approval, resolution of open policies, implementation and test completion are separate milestones; this document does not assert they have occurred.
