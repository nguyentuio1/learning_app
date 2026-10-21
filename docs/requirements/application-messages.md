# Application Messages Catalogue — SmartDroneDelivery

**Task:** LA-221 · **Parent:** LA-195

**Status:** Proposed for review

**Snapshot date:** 2026-10-21

## 1. Scope and reading guide

List success, error and validation messages shown by web/mobile, including shared API messages, confirmation/empty states and proposed messages for not-yet-implemented modules. This is documentation only: no screen, API or colleague's source is changed.

- **Observed** means text exists at the pinned source revision, not that the branch has merged or that a screenshot/runtime test was performed.
- **Proposed** means copy for the functional specification; it is not claimed to appear in the current app.
- Existing spelling, punctuation and differences between clients are retained. Do not change API strings merely to match the proposed wording.
- IDs are documentation identifiers, not currently implemented server error codes or localization keys.
- Ordinary navigation labels, icons and form field labels are not notification messages. Loading/accessibility text and empty results are included when they communicate state.
- Runtime framework validation and dynamic API/AI output are documented as bounded templates; an unbounded external error string cannot be exhaustively enumerated.
- UI success appears only after confirmed completion. A 204 has no server success sentence: the client supplies feedback or navigation.
- Use inline field feedback for validation, page/alert/snackbar for request failures, and confirmation dialogs before consequential changes. Do not leak credentials, secure codes, stack traces or another user's data.

## 2. Source inventory

| Source | Revision | Coverage |
|---|---|---|
| [SRS](SRS.md), [use cases](use-cases.md) | develop 609f609 | FR-01–34, UC-01–23 |
| [Backend auth/profile + LA-92](https://github.com/nguyentuio1/learning_app/tree/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api) | aee5333 | DTO validation, auth/profile service errors, admin conflicts and problem details |
| [Web auth/profile/admin](https://github.com/nguyentuio1/learning_app/tree/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src) | 316d51d | LA-18/83/93; client validation, alerts, route guard and session feedback |
| [Mobile authentication](https://github.com/nguyentuio1/learning_app/tree/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib) | 12bb6de | LA-19; login and RFC problem-details mapping |
| [Mobile profile](https://github.com/nguyentuio1/learning_app/tree/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib) | 2eaed77 | LA-84; profile/password messages, separate older error mapper |
| [CO specification](https://github.com/nguyentuio1/learning_app/blob/4a1602e7444ebab9cc6a791d2de0f56b6919a490/docs/design/customer-order-management/functional-specification.md) | 4a1602e | Proposed address/package/order messages |
| [DW specification](https://github.com/nguyentuio1/learning_app/blob/8f994d972f8eeb908f293cedad9b1006ccb67b82/docs/requirements/functions-delivery-workflow.md) | 8f994d9 | Proposed dispatch/tracking/failure messages |
| [LS specification](https://github.com/nguyentuio1/learning_app/blob/30d97a4e4b017f8e0606f21e38dd816ba03185dd/docs/requirements/functions-landing-station.md) | 30d97a4 | Proposed station/slot/intake/confirmation messages |
| [UA specification](https://github.com/nguyentuio1/learning_app/blob/bedf982054c67eed29474e34e02ca5dc0c21aec0/docs/requirements/functions-user-management-and-analytics.md) | bedf982 | Proposed analytics/AI/audit messages |

Future-module rows use qualified functions CO:2.1–2.6, DW:F-01–10, LS:F-01–08, UA:F-01–19, or explicit UC IDs. The source documents above define these references. This catalogue has no merge dependency on LA-220.

## 3. Observed API messages available to clients

Clients may replace a server detail with safer/friendlier text; web overrides are in section 4 and mobile behavior is in section 5. These rows are **not** a claim that every backend detail is currently displayed by both clients.

| ID | Exact text | Type / HTTP / trigger | Field or function | Verified source |
|---|---|---|---|---|
| MSG-API-001 | Email is required. | Validation / 400 / omitted or blank | Register/login: email | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L8) |
| MSG-API-002 | Email is not valid. | Validation / 400 / invalid format | Register: email | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L9) |
| MSG-API-003 | Email must be at most 255 characters. | Validation / 400 / length >255 | Register: email | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L10) |
| MSG-API-004 | Password is required. | Validation / 400 / missing | Register/login: password | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L13) |
| MSG-API-005 | Password must be 8 to 100 characters. | Validation / 400 / length outside range | Register/password change | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L14) |
| MSG-API-006 | Password must contain at least one letter and one digit. | Validation / 400 / pattern mismatch | Register/password change/admin create | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L15), [UserManagementDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/UserManagementDtos.cs#L10) |
| MSG-API-007 | Full name is required. | Validation / 400 / missing | Register/profile: fullName | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L18) |
| MSG-API-008 | Full name must be at most 150 characters. | Validation / 400 / length >150 | Register/profile: fullName | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L19) |
| MSG-API-009 | Phone number is required. | Validation / 400 / missing | Register/profile: phoneNumber | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L22) |
| MSG-API-010 | Phone number must be 9 to 11 digits. | Validation / 400 / pattern mismatch | Register/profile: phoneNumber | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L23) |
| MSG-API-011 | Phone number must contain 9 to 11 digits. | Validation / 400 / pattern mismatch | Admin: phoneNumber | [UserManagementDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/UserManagementDtos.cs#L13) |
| MSG-API-012 | Refresh token is required. | Validation / 400 / missing | Refresh/logout: refreshToken | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L38) |
| MSG-API-013 | Current password is required. | Validation / 400 / missing | Password change: currentPassword | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L61) |
| MSG-API-014 | New password is required. | Validation / 400 / missing | Password change: newPassword | [AuthDtos.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/DTOs/AuthDtos.cs#L64) |
| MSG-API-015 | New password must be different from the current password. | Validation / 400 / unchanged password | Password change: newPassword | [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L57) |
| MSG-API-016 | One or more validation errors occurred. | Validation / 400 / aggregate detail | All model validation; errors[field][] accompanies detail | [ApiProblemDetails.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Middleware/ApiProblemDetails.cs#L42), [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L56) |
| MSG-API-017 | Invalid value. | Validation / 400 / model error without text | Any model field | [ApiProblemDetails.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Middleware/ApiProblemDetails.cs#L40) |
| MSG-API-018 | Invalid email or password. | Error / 401 / unknown email or incorrect password | Login; deliberately indistinguishable | [AuthService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/AuthService.cs#L73) |
| MSG-API-019 | This account is not active. | Error / 403 / inactive or suspended account | Login/profile | [AuthService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/AuthService.cs#L76), [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L119) |
| MSG-API-020 | Invalid refresh token. | Error / 401 / missing stored, revoked, expired or inactive user's token | Refresh | [AuthService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/AuthService.cs#L92) |
| MSG-API-021 | Missing, invalid or expired token. | Error / 401 / token challenge or missing profile identity | Protected endpoints | [UsersController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UsersController.cs#L44), [Program.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Program.cs#L56), [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L116) |
| MSG-API-022 | You do not have permission to perform this action. | Error / 403 / denied role | Role-protected endpoint; stale admin role | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L246), [Program.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Program.cs#L59) |
| MSG-API-023 | Email is already registered. | Conflict / 409 / duplicate normalized email | Register/admin create/update: email | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L141), [AuthService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/AuthService.cs#L39) |
| MSG-API-024 | Phone number is already registered. | Conflict / 409 / duplicate phone | Register/profile/admin: phoneNumber | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L146), [AuthService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/AuthService.cs#L41), [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L39) |
| MSG-API-025 | Current password is incorrect. | Field error / 401 / current password mismatch | Password change; do NOT trigger refresh/sign-out for this exact detail | [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L53) |
| MSG-API-026 | A file is required. | Validation / 400 / missing or empty file | Avatar: file | [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L85) |
| MSG-API-027 | The file must be at most 5 MB. | Validation / 400 / file >5 MiB | Avatar: file | [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L87) |
| MSG-API-028 | Only JPEG and PNG images are allowed. | Validation / 400 / unrecognized content signature | Avatar: file | [UserService.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Services/UserService.cs#L96) |
| MSG-API-029 | User not found. | Error / 404 / absent target | Admin get/update/role/deactivate | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L45) |
| MSG-API-030 | Invalid role. | Validation / 400 / unsupported role | Admin create/role | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L229) |
| MSG-API-031 | Select a supported role code. | Validation / 400 / unsupported role | Admin: errors.role[] | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L230) |
| MSG-API-032 | Invalid user identity. | Error / 401 / invalid sub claim | Admin actor | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L237) |
| MSG-API-033 | User no longer exists. | Error / 401 / actor absent from DB | Admin actor | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L242) |
| MSG-API-034 | User account is not active. | Error / 403 / actor inactive | Admin actor | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L244) |
| MSG-API-035 | You cannot deactivate your own account. | Conflict / 409 / self-deactivation | Admin deactivate | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L107) |
| MSG-API-036 | You cannot change your own administrator role. | Conflict / 409 / self-demotion | Admin role | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L175) |
| MSG-API-037 | The last active administrator cannot be removed. | Conflict / 409 / no remaining active admin | Admin role/deactivate | [UserManagementController.cs](https://github.com/nguyentuio1/learning_app/blob/aee5333802ede20561733daa09f4c9a2a8a4fa22/backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs#L218) |

### Framework-generated API validation

The admin DTO uses .NET DataAnnotations defaults for some attributes. Templates below are English defaults, not stable machine-readable codes. Actual culture and JSON binding may alter formatting. Display returned field errors rather than reconstructing them.

| ID | Template / allowed substitutions | Trigger / field |
|---|---|---|
| MSG-API-038 | The {Field} field is required. | Required admin Email, Password, Role, FullName, PhoneNumber |
| MSG-API-039 | The {Field} field is not a valid e-mail address. | Invalid admin Email |
| MSG-API-040 | The field {Field} must be a string with a maximum length of {max}. | Email 255, Role 50, FullName 150 |
| MSG-API-041 | The field Password must be a string with a minimum length of 8 and a maximum length of 100. | Admin initial password length |
| MSG-API-042 | {bindingError} in errors[field][] | Malformed JSON, type mismatch or missing body; framework-generated, do not echo supplied secrets |
| MSG-API-043 | Bad Request / Unauthorized / Forbidden / Not Found / Conflict / Error | Problem title corresponding to 400/401/403/404/409/other status; prefer detail or mapped client message |

Source: admin DTO attributes and ApiProblemDetails at aee5333. Auth/profile DTOs use the explicit texts above.
No backend success message is defined for login/refresh/register/profile reads, admin reads or 204 mutations; responses are objects, arrays or empty bodies.

## 4. Observed web messages

### 4.1 Authentication, session and route access

| ID | Exact text | Trigger / display and expected action | Source |
|---|---|---|---|
| MSG-WEB-001 | Invalid email or password. | Login 401; error alert; correct credentials | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L49) |
| MSG-WEB-002 | This account is not active. Contact your administrator. | Login 403; error alert; contact admin | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L49) |
| MSG-WEB-003 | Check your email and password. | Login 400; error alert; correct input | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L50) |
| MSG-WEB-004 | Unable to reach the server. Please try again. | Login without response; error alert; retry | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L50) |
| MSG-WEB-005 | Sign-in is temporarily unavailable. Please try again later. | Other login HTTP error; error alert | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L51) |
| MSG-WEB-006 | Unable to sign in. | Non-Error unknown login failure; fallback alert | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L52) |
| MSG-WEB-007 | Enter your email. | Required email; inline validation | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L61) |
| MSG-WEB-008 | Enter a valid email. | Invalid email; inline validation (also admin forms) | [UserDetailsFields.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/users/UserDetailsFields.tsx#L9), [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L61) |
| MSG-WEB-009 | Enter your password. | Required password; inline validation | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L64) |
| MSG-WEB-010 | Your session has expired. Please sign in again. | Missing refresh or unrecoverable 401; login info alert | [apiClient.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/apiClient.ts#L45), [authSession.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/authSession.ts#L61), [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L10) |
| MSG-WEB-011 | Your session could not be renewed. Please sign in again. | Refresh failure; login info alert | [authSession.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/authSession.ts#L77) |
| MSG-WEB-012 | Signed out on this device. Server revocation could not be confirmed. | Logout network/server failure; local sign-out already done | [authSession.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/authSession.ts#L102) |
| MSG-WEB-013 | The API address is not configured. | Missing API configuration; displayed Error fallback | [authSession.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/authSession.ts#L24), [profileApi.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/profileApi.ts#L6), [userManagementApi.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/userManagementApi.ts#L6) |
| MSG-WEB-014 | The server returned an invalid session. | Invalid auth response; login error alert | [authSession.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/authSession.ts#L29) |
| MSG-WEB-015 | Please use the mobile app | Info result for CUSTOMER/STATION_OPERATOR on web-only route | [RequireAuth.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/routes/RequireAuth.tsx#L16) |
| MSG-WEB-016 | Use the SmartDroneDelivery mobile app to access Customer and Station Operator features. | Info result body; open mobile client | [RequireAuth.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/routes/RequireAuth.tsx#L17) |
| MSG-WEB-017 | Access denied | 403 result heading | [RequireAuth.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/routes/RequireAuth.tsx#L20) |
| MSG-WEB-018 | Your role cannot access this page. | 403 result body; no forced refresh | [RequireAuth.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/routes/RequireAuth.tsx#L20) |
| MSG-WEB-019 | The page you requested was not found. | 404 result body; return home | [NotFoundPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/NotFoundPage.tsx#L10) |
| MSG-WEB-020 | Restoring session | Loading/accessibility state during session restoration | [LoginPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/LoginPage.tsx#L23), [RequireAuth.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/routes/RequireAuth.tsx#L10) |

### 4.2 Profile success, validation and failures

| ID | Exact text | Trigger / display and expected action | Source |
|---|---|---|---|
| MSG-WEB-021 | Profile saved. | Successful profile PUT; success alert | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L47) |
| MSG-WEB-022 | Profile photo saved. | Successful avatar POST; success alert | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L189) |
| MSG-WEB-023 | Enter your full name. | Required/blank name; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L67) |
| MSG-WEB-024 | Full name must be at most 150 characters. | Name >150; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L68) |
| MSG-WEB-025 | Enter your phone number. | Missing phone; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L73) |
| MSG-WEB-026 | Phone number must contain 9 to 11 digits. | Invalid phone; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L74) |
| MSG-WEB-027 | Enter your current password. | Missing current password; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L124) |
| MSG-WEB-028 | Enter a new password. | Missing new password; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L128) |
| MSG-WEB-029 | Password must be 8 to 100 characters. | New password length; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L129) |
| MSG-WEB-030 | Include at least one letter and one number. | New password composition; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L130) |
| MSG-WEB-031 | Choose a password different from your current one. | New equals current; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L133) |
| MSG-WEB-032 | Confirm your new password. | Missing confirmation; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L139) |
| MSG-WEB-033 | Passwords do not match. | Confirmation mismatch; inline field error | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L142) |
| MSG-WEB-034 | Choose a JPEG or PNG image. | Client file-type check; upload alert | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L167) |
| MSG-WEB-035 | Choose a non-empty image no larger than 5 MB. | Client empty/size check; upload alert | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L170) |
| MSG-WEB-036 | No photo selected | Empty avatar-selection state | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L206) |
| MSG-WEB-037 | Current profile photo | Avatar-picker current selection state | [ProfileForms.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/profile/ProfileForms.tsx#L206) |
| MSG-WEB-038 | Something went wrong. Please try again. | Unknown profile error; fallback alert | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L15) |
| MSG-WEB-039 | The request timed out. Please try again. | Timeout; alert (also admin) | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L22), [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L9) |
| MSG-WEB-040 | Unable to reach the server. Check your connection and try again. | Network failure; alert (also admin) | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L23), [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L9) |
| MSG-WEB-041 | Sign in to view your profile. Your session may be missing or expired. | Profile 401 access error | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L36) |
| MSG-WEB-042 | Your account is inactive or you do not have permission to access this profile. | Profile 403 access error | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L37) |
| MSG-WEB-043 | Phone number is already registered. | Profile 409; inline phone error plus alert | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L41), [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L23) |
| MSG-WEB-044 | Current password is incorrect. | Password 401; inline currentPassword, no refresh/sign-out | [apiClient.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/apiClient.ts#L42), [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L28) |
| MSG-WEB-045 | Please check the highlighted fields. | 400 without server detail; field-error summary | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L47) |
| MSG-WEB-046 | The service is temporarily unavailable. Please try again later. | Profile 5xx; retry later | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L50) |
| MSG-WEB-047 | Your request could not be completed. Please try again. | Other profile HTTP failure | [profileErrors.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/profileErrors.ts#L51) |
| MSG-WEB-048 | Account access required | Profile access-error heading | [ProfilePage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/ProfilePage.tsx#L49) |
| MSG-WEB-049 | Unable to load profile | Profile load-error heading | [ProfilePage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/ProfilePage.tsx#L49) |
| MSG-WEB-050 | Loading profile | Profile loading/accessibility state | [ProfilePage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/ProfilePage.tsx#L47) |
| MSG-WEB-051 | The API address is not configured. Set VITE_API_BASE_URL before loading your profile. | Configuration error before API call | [profileApi.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/services/profileApi.ts#L6) |

A successful password change currently resets the form and logs out; there is **no dedicated web success sentence**. Do not invent a currently displayed “Password changed” toast.

### 4.3 User administration

| ID | Exact text | Trigger / display and expected action | Source |
|---|---|---|---|
| MSG-WEB-052 | Account created successfully. | Successful create; detail page success alert | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L31) |
| MSG-WEB-053 | Account details saved. | Successful detail PUT; success alert | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L71) |
| MSG-WEB-054 | Role updated. It applies when the user receives their next access token. | Successful role PUT; success alert; wording caveat in section 8 | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L71) |
| MSG-WEB-055 | Account deactivated. Its records and delivery history are retained. | Successful deactivate; success alert | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L71) |
| MSG-WEB-056 | Enter a full name. | Required/blank admin form name | [UserDetailsFields.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/users/UserDetailsFields.tsx#L5) |
| MSG-WEB-057 | Enter an email. | Required admin form email | [UserDetailsFields.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/users/UserDetailsFields.tsx#L9) |
| MSG-WEB-058 | Enter a phone number. | Required admin form phone | [UserDetailsFields.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/users/UserDetailsFields.tsx#L12) |
| MSG-WEB-059 | Enter 9 to 11 digits. | Admin phone pattern | [UserDetailsFields.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/components/users/UserDetailsFields.tsx#L12) |
| MSG-WEB-060 | Include at least one letter and one digit. | Admin initial password pattern | [CreateUserPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/CreateUserPage.tsx#L44) |
| MSG-WEB-061 | Unable to complete this request. | Unknown administration failure | [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L5) |
| MSG-WEB-062 | Administrator access is required, or your account is no longer active. | Admin 403; access blocked | [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L11) |
| MSG-WEB-063 | This user could not be found. | Admin 404; stop editing and return to list | [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L12) |
| MSG-WEB-064 | The service is temporarily unavailable. Please try again. | Admin 5xx; retry | [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L13) |
| MSG-WEB-065 | Please review the account details. | Admin 400/409 without detail | [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L15) |
| MSG-WEB-066 | Email is already registered. | Admin 409; maps exact detail to email field | [userManagement.ts](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/utils/userManagement.ts#L22) |
| MSG-WEB-067 | No matching users | Filtered/empty user table | [UsersPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UsersPage.tsx#L42) |
| MSG-WEB-068 | Change this role? | Confirmation heading before role PUT | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L118) |
| MSG-WEB-069 | Deactivate this account? | Confirmation heading before deactivation | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L118) |
| MSG-WEB-070 | This account will be marked inactive. This does not delete the user. | Deactivation confirmation body | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L124) |
| MSG-WEB-071 | You cannot change your own administrator role. Ask another administrator to manage your access. | Own-account safety explanation; role control disabled | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L106) |
| MSG-WEB-072 | You cannot deactivate your own account. | Own-account safety explanation; deactivate disabled | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L111) |
| MSG-WEB-073 | Account inactive | Existing inactive account button state | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L113) |
| MSG-WEB-074 | Deactivation blocks sign-in and retains this account's records and delivery history. | Account-access explanation | [UserDetailPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/UserDetailPage.tsx#L110) |
| MSG-WEB-075 | New accounts are active. Customers register through the customer application. | Account-creation guidance | [CreateUserPage.tsx](https://github.com/nguyentuio1/learning_app/blob/316d51db0de35d96cb103cb0a7f14ea65f146af0/frontend/web/src/pages/CreateUserPage.tsx#L53) |

### 4.4 Dynamic web messages and generated validation

| ID | Template / message family | Where / handling |
|---|---|---|
| MSG-WEB-D01 | {serverDetail}; errors.{email,fullName,phoneNumber,password,role}[] | Admin 400/409; preserve detail and allowlisted string field arrays. Self/last-admin details are MSG-API-035–037. |
| MSG-WEB-D02 | {serverDetail}; errors.{fullName,phoneNumber,currentPassword,newPassword,file}[] | Profile 400 / special password 401; show appropriate inline errors, never treat wrong-current-password as session expiry. |
| MSG-WEB-D03 | {field} is required; {field} cannot be longer than {max} characters; {field} must be between {min} and {max} characters | Ant Design/rc-field-form defaults where no custom message is set: admin role/initial password, name 150, email 255; actual wording follows installed library/locale. |
| MSG-WEB-D04 | {currentRole} → {selectedRole}; {fullName} · {email} | Role-change confirmation; only authorized target data, rendered as text. |
| MSG-WEB-D05 | {count} users; {count} users · View accounts | List pagination / role-card count; zero is a valid empty count, not a failed request. |
| MSG-WEB-D06 | {Error.message} | Existing non-Axios error fallback; not an approved unrestricted user-copy channel. Sanitize unexpected exceptions in a separate implementation task. |

## 5. Observed mobile messages

Both mobile branches share login validators and generic fallbacks, but they are **different revisions**, not a proven merged client. Profile messages below are verified at LA-84.

| ID | Exact text | Trigger / display and expected action | Source |
|---|---|---|---|
| MSG-MOB-001 | Enter a valid email | Login inline validator; missing email or no @ | [login_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/auth/login_page.dart#L68), [login_page.dart (LA-19)](https://github.com/nguyentuio1/learning_app/blob/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib/presentation/auth/login_page.dart#L68) |
| MSG-MOB-002 | At least 6 characters | Login inline validator; password length <6 (not registration password policy) | [login_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/auth/login_page.dart#L77), [login_page.dart (LA-19)](https://github.com/nguyentuio1/learning_app/blob/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib/presentation/auth/login_page.dart#L77) |
| MSG-MOB-003 | Network problem. Check your connection and try again. | Dio connection/timeout; snackbar or AsyncValue error | [api_exception.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/core/error/api_exception.dart#L24), [api_exception.dart (LA-19)](https://github.com/nguyentuio1/learning_app/blob/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib/core/error/api_exception.dart#L24) |
| MSG-MOB-004 | Request cancelled. | Dio cancellation; error mapper text | [api_exception.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/core/error/api_exception.dart#L28), [api_exception.dart (LA-19)](https://github.com/nguyentuio1/learning_app/blob/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib/core/error/api_exception.dart#L28) |
| MSG-MOB-005 | Something went wrong. Please try again. | Unknown error or missing recognized server text | [api_exception.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/core/error/api_exception.dart#L38), [error_message.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/core/error/error_message.dart#L10), [api_exception.dart (LA-19)](https://github.com/nguyentuio1/learning_app/blob/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib/core/error/api_exception.dart#L48), [error_message.dart (LA-19)](https://github.com/nguyentuio1/learning_app/blob/12bb6de8f405f3cfd4f6219850366585b7cc16a5/frontend/mobile/lib/core/error/error_message.dart#L10) |
| MSG-MOB-006 | Profile updated successfully | Profile save completed; success snackbar | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L62) |
| MSG-MOB-007 | Password changed successfully | Password change completed; success snackbar | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L45) |
| MSG-MOB-008 | Avatar updated | Avatar URL changed in local state only; not proof of upload/persistence | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L120) |
| MSG-MOB-009 | Full name is required | Profile inline validation | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L193) |
| MSG-MOB-010 | Full name must be at most 150 characters | Profile name length validation | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L196) |
| MSG-MOB-011 | Phone number is required | Profile inline validation | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L217) |
| MSG-MOB-012 | Phone number must be 9 to 11 digits | Profile phone validation | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L221) |
| MSG-MOB-013 | Current password is required | Password form inline validation | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L150) |
| MSG-MOB-014 | New password is required | Password form inline validation | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L178) |
| MSG-MOB-015 | Must be 8 to 100 characters | New password length validation | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L181) |
| MSG-MOB-016 | Must contain at least one letter | New password letter validation | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L184) |
| MSG-MOB-017 | Must contain at least one number | New password digit validation | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L187) |
| MSG-MOB-018 | New password must be different from current password | New/current mismatch requirement | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L190) |
| MSG-MOB-019 | Please confirm your new password | Password confirmation required | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L219) |
| MSG-MOB-020 | Passwords do not match | Password confirmation mismatch | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L222) |
| MSG-MOB-021 | Are you sure you want to sign out of your account? | Sign-out confirmation dialog | [profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/profile_page.dart#L41) |
| MSG-MOB-022 | Email cannot be changed | Read-only profile email helper | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L239) |
| MSG-MOB-023 | Enter an image URL for your profile avatar: | Current avatar URL dialog; not a file-upload success | [edit_profile_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/edit_profile_page.dart#L83) |
| MSG-MOB-024 | • 8 to 100 characters long | Password-requirements helper line | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L118) |
| MSG-MOB-025 | • At least one letter and at least one number | Password-requirements helper line | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L119) |
| MSG-MOB-026 | • Must be different from your current password | Password-requirements helper line | [change_password_page.dart (LA-84)](https://github.com/nguyentuio1/learning_app/blob/2eaed778eb625959af59ffe0b5a8a96a36a5f723/frontend/mobile/lib/presentation/profile/change_password_page.dart#L120) |

| ID | Dynamic behavior | Revision / display |
|---|---|---|
| MSG-MOB-D01 | detail → message → first errors[field][] item → title → generic fallback | LA-19 ApiException mapper; text appears in login snackbar/AsyncValueView |
| MSG-MOB-D02 | message → generic fallback | LA-84 older ApiException mapper does not read detail/errors; backend problem details may collapse to generic failure |
| MSG-MOB-D03 | errorMessage(error) | Profile/edit/password snackbars and AsyncValueView use mapped text; Retry offered when callback exists |
| MSG-MOB-D04 | No explicit success text | Login navigates when authentication state changes; sign-out clears local session/navigation |
| MSG-MOB-D05 | No text during loading by default | CircularProgressIndicator/disabled action rather than an invented “Loading…” sentence |

## 6. Proposed messages for remaining modules

The following are **proposed copy**, not implemented strings. Source function and trigger are recorded per row. “Web/mobile” means the authorized client for that actor, not a commitment to implement every function on both clients. Reuse common errors below where appropriate; do not claim an absent API path exists.

Placeholders: {resource}/{action} use allowlisted human-readable labels; {trackingNumber} only for authorized orders; {max}/{min}/{limit}/{duration} use confirmed configuration; {time}/{date} use the agreed locale/timezone; {reason} is sanitized authorized text; {count} is nonnegative. Never insert raw tokens, passwords, secure codes or stack traces.

| ID | Proposed text | Type / trigger / function | Client / recovery |
|---|---|---|---|
| MSG-P-GEN-01 | {resource} saved successfully. | Success after confirmed save; all mutating forms | Authorized client; refresh persisted view |
| MSG-P-GEN-02 | Unable to load {resource}. Please try again. | Error loading list/detail; all read functions | Web/mobile; retain filters and offer Retry |
| MSG-P-GEN-03 | Unable to save {resource}. Your changes were not saved. | Error mutation failure known not saved | Web/mobile; retain draft |
| MSG-P-GEN-04 | The request timed out. Check the latest status before trying again. | Error unknown mutation outcome | Web/mobile; reload status to avoid duplicate actions |
| MSG-P-GEN-05 | No {resource} found. | Empty successful query | Web/mobile; clear filters/create if allowed |
| MSG-P-GEN-06 | {field} is required. | Validation missing/blank field | Inline; focus field |
| MSG-P-GEN-07 | {field} must be at most {max} characters. | Validation length | Inline; correct input |
| MSG-P-GEN-08 | Enter a valid {field}. | Validation format/type | Inline; correct input |
| MSG-P-GEN-09 | The end date must not be earlier than the start date. | Validation filter dates; CO:2.6, DW:F-01, UA:F-13/14/19 | Web/mobile; correct range |
| MSG-P-GEN-10 | This item has changed. Reload it before continuing. | Conflict stale state/concurrent update | Web/mobile; reload and revalidate |
| MSG-P-CO-01 | Address saved successfully. | Success CO:2.1 create/update | Customer mobile; reload addresses |
| MSG-P-CO-02 | Address deleted successfully. | Success CO:2.1 delete | Customer mobile |
| MSG-P-CO-03 | Default address updated. | Success CO:2.1 default | Customer mobile; only one default |
| MSG-P-CO-04 | Delete this address? | Confirmation CO:2.1 delete | Customer mobile; cancel/confirm |
| MSG-P-CO-05 | This address is in use and cannot be deleted. | Conflict CO:2.1 referenced address; deletion policy pending | Customer mobile; choose another action |
| MSG-P-CO-06 | Enter both latitude and longitude within the allowed ranges. | Validation CO:2.1 coordinates | Customer mobile; latitude ±90, longitude ±180 |
| MSG-P-CO-07 | You have reached the limit of {limit} addresses. | Validation CO:2.1 only if address cap approved | Customer mobile; do not hard-code proposed 10 |
| MSG-P-CO-08 | Package saved successfully. | Success CO:2.2 | Customer mobile |
| MSG-P-CO-09 | Weight and dimensions must be greater than zero. | Validation CO:2.2 | Customer mobile inline fields |
| MSG-P-CO-10 | This package exceeds the selected drone's capacity. | Validation CO:2.2/2.3, DW:F-05, LS:F-06 | Customer/operator mobile or dispatcher web; capacity policy pending |
| MSG-P-CO-11 | This package is already linked to an order. | Conflict CO:2.2/2.3 | Customer mobile; select another package |
| MSG-P-CO-12 | This package is in use and cannot be deleted. | Conflict CO:2.2 restricted deletion | Customer mobile |
| MSG-P-CO-13 | Order {trackingNumber} submitted successfully. | Success CO:2.3 | Customer mobile; open details |
| MSG-P-CO-14 | Select different origin and destination stations. | Validation CO:2.3/2.4 | Customer mobile |
| MSG-P-CO-15 | The selected station is unavailable. Choose another station. | Validation/conflict CO:2.3/2.4 | Customer mobile; reload availability |
| MSG-P-CO-16 | Order updated successfully. | Success CO:2.4 | Customer mobile |
| MSG-P-CO-17 | Only pending orders can be edited. | Conflict CO:2.4 | Customer mobile; reload details |
| MSG-P-CO-18 | These order fields cannot be changed. | Validation CO:2.4 forbidden update fields | Customer mobile; show allowed fields |
| MSG-P-CO-19 | Cancel this order? | Confirmation CO:2.5 | Customer mobile; require reason |
| MSG-P-CO-20 | Enter a cancellation reason. | Validation CO:2.5 | Customer mobile inline |
| MSG-P-CO-21 | Order cancelled successfully. | Success CO:2.5 | Customer mobile |
| MSG-P-CO-22 | This order can no longer be cancelled. | Conflict CO:2.5 current state | Customer mobile; show latest state |
| MSG-P-CO-23 | Select a valid order status. | Validation CO:2.6 filters | Customer mobile |
| MSG-P-CO-24 | Page must be at least 1 and page size must be within the allowed range. | Validation CO:2.6 pagination | Customer mobile; approved bounds from API |
| MSG-P-CO-25 | Order not found or unavailable to your account. | Error CO:2.6 ownership/not found | Customer mobile; no foreign metadata |
| MSG-P-DW-01 | Order approved successfully. | Success DW:F-02 | Dispatcher web |
| MSG-P-DW-02 | Order rejected successfully. | Success DW:F-02 | Dispatcher web; cancelled state |
| MSG-P-DW-03 | Enter a rejection reason. | Validation DW:F-02 | Dispatcher web inline |
| MSG-P-DW-04 | Only pending orders can be approved or rejected. | Conflict DW:F-02 | Dispatcher web; reload |
| MSG-P-DW-05 | This status transition is not allowed. | Conflict DW:F-03 | Dispatcher web; keep persisted state |
| MSG-P-DW-06 | Delivery scheduled successfully. | Success DW:F-04 | Dispatcher web |
| MSG-P-DW-07 | Choose a departure time within station operating hours and after the required preparation period. | Validation DW:F-04 | Dispatcher web; confirmed configuration |
| MSG-P-DW-08 | Drone assigned successfully. | Success DW:F-05 | Dispatcher web |
| MSG-P-DW-09 | This drone is unavailable for the selected time. | Conflict DW:F-05 overlapping mission/status | Dispatcher web; choose time/drone |
| MSG-P-DW-10 | The drone does not have enough range for this route. | Validation DW:F-05 | Dispatcher web; agreed reserve margin |
| MSG-P-DW-11 | No landing slot is available at the destination. | Conflict DW:F-05 | Dispatcher web; refresh/select alternative |
| MSG-P-DW-12 | Drone position has not been updated since {time}. | Warning DW:F-06/07 stale telemetry | Dispatcher web/customer mobile; show timestamp |
| MSG-P-DW-13 | Live tracking is temporarily unavailable. Reconnecting… | Error/recovery DW:F-07 connection loss | Web/mobile; preserve last-known label |
| MSG-P-DW-14 | Delivery marked as failed. | Success DW:F-08 | Dispatcher web; show reason and resolution |
| MSG-P-DW-15 | Enter a failure reason. | Validation DW:F-08 | Dispatcher web inline |
| MSG-P-DW-16 | Delivery rescheduled successfully. | Success DW:F-09 reschedule | Dispatcher web; schedule new attempt |
| MSG-P-DW-17 | Return requested. Waiting for receipt at the origin station. | Success DW:F-09 return initiation | Web/mobile; not terminal until receipt |
| MSG-P-DW-18 | Returned package received. Order cancelled. | Success DW:F-09 / LS return receipt | Operator mobile/dispatcher web |
| MSG-P-DW-19 | Select a resolution and enter a note. | Validation DW:F-09 | Dispatcher web inline |
| MSG-P-DW-20 | Only failed deliveries can use this resolution. | Conflict DW:F-09 | Dispatcher web; reload |
| MSG-P-DW-21 | Drone details saved successfully. | Success DW:F-10 | Dispatcher web |
| MSG-P-DW-22 | A flying drone cannot be placed into maintenance. | Conflict DW:F-10 | Dispatcher web; wait for safe completion |
| MSG-P-LS-01 | Station saved successfully. | Success LS:F-02 | Admin web/operator mobile as permitted |
| MSG-P-LS-02 | Station code is already in use. | Conflict LS:F-02 | Admin web/operator mobile inline |
| MSG-P-LS-03 | Station coordinates are outside the allowed ranges. | Validation LS:F-02 | Web/mobile; latitude ±90, longitude ±180 |
| MSG-P-LS-04 | Incoming missions need attention before changing station availability. | Warning/conflict LS:F-02 | Web/mobile; dispatcher coordination |
| MSG-P-LS-05 | Slot saved successfully. | Success LS:F-03 | Web/mobile |
| MSG-P-LS-06 | Slot number is already used at this station. | Conflict LS:F-03 | Web/mobile inline |
| MSG-P-LS-07 | This slot is reserved or occupied and cannot be removed. | Conflict LS:F-03 | Web/mobile; resolve occupancy |
| MSG-P-LS-08 | Slot maintenance status updated. | Success LS:F-04 | Operator mobile/admin web |
| MSG-P-LS-09 | Resolve the reservation or occupancy before placing this slot into maintenance. | Conflict LS:F-04 | Web/mobile |
| MSG-P-LS-10 | Station availability is temporarily unavailable. | Error LS:F-05 | Operator mobile; Retry, do not display zero capacity as fact |
| MSG-P-LS-11 | Package receipt confirmed. | Success LS:F-06 | Operator mobile |
| MSG-P-LS-12 | This order is not ready for package intake at this station. | Conflict LS:F-06 | Operator mobile; verify schedule/station/status |
| MSG-P-LS-13 | Complete the package safety checklist before confirming. | Validation LS:F-06 | Operator mobile inline |
| MSG-P-LS-14 | Delivery confirmed successfully. | Success LS:F-07 | Customer/operator mobile; actual completion recorded |
| MSG-P-LS-15 | The pickup code is incorrect. Please try again. | Validation LS:F-07 | Customer/operator mobile; never echo correct code |
| MSG-P-LS-16 | Too many attempts. Try again after {time}. | Lockout LS:F-07 if policy approved | Mobile; server-derived retry time |
| MSG-P-LS-17 | This order is not ready for delivery confirmation. | Conflict LS:F-07 state not ARRIVED | Mobile; reload |
| MSG-P-LS-18 | The collection period has expired. Contact station staff. | Warning LS:F-08 | Customer/operator mobile; timeout policy pending |
| MSG-P-UA-01 | No deliveries match the selected period. | Empty UA:F-13/14 | Manager web; adjust dates |
| MSG-P-UA-02 | N/A | Undefined metric (zero denominator/missing timestamps); UA:F-13/14 | Manager web; never imply backend failure is zero |
| MSG-P-UA-03 | Statistics could not be loaded. Please try again. | Error UA:F-13/14 | Manager web; Retry |
| MSG-P-UA-04 | Estimated arrival: {time}. | Info UA:F-15 available prediction | Authorized web/mobile; explicitly an estimate |
| MSG-P-UA-05 | An arrival estimate is not available yet. | Unavailable UA:F-15 | Web/mobile; no fabricated ETA |
| MSG-P-UA-06 | This estimate is out of date. Refresh to check for updates. | Warning UA:F-15 stale result | Web/mobile |
| MSG-P-UA-07 | Delivery summary generated. | Success UA:F-16 | Authorized web/mobile |
| MSG-P-UA-08 | A summary could not be generated. Please try again. | Error UA:F-16 | Web/mobile |
| MSG-P-UA-09 | Enter a message. | Validation UA:F-17 blank chat | Customer mobile |
| MSG-P-UA-10 | Your message exceeds the limit of {max} characters. | Validation UA:F-17 only after limit agreed | Customer mobile |
| MSG-P-UA-11 | I could not access that order. Select one of your own orders. | Privacy-safe UA:F-17 scope failure | Customer mobile |
| MSG-P-UA-12 | The assistant is temporarily unavailable. Please try again. | Error UA:F-17 | Customer mobile; keep unsent input |
| MSG-P-UA-13 | No audit events match these filters. | Empty UA:F-19 | Admin web |
| MSG-P-OPS-01 | A service is degraded or unavailable. Check the service details. | Warning UC-23 health | Admin web |
| MSG-P-OPS-02 | Backup started. | Success UC-23 job acceptance | Admin web; does not claim completion |
| MSG-P-OPS-03 | Backup completed successfully. | Success UC-23 confirmed completion | Admin web |
| MSG-P-OPS-04 | Backup failed. Review the details and try again. | Error UC-23 | Admin web; no secret logs |
| MSG-P-OPS-05 | A backup is already running. | Conflict UC-23 duplicate request | Admin web; inspect current job |
| MSG-P-NOT-01 | Order {trackingNumber}: {statusLabel}. | Success/info UC-10 after persisted order transition | Customer mobile; allowed statuses only |
| MSG-P-NOT-02 | Notifications marked as read. | Success UC-10 | Customer mobile |
| MSG-P-NOT-03 | No notifications yet. | Empty UC-10 | Customer mobile |
| MSG-P-NOT-04 | Unable to update notifications. Please try again. | Error UC-10 | Customer mobile |
| MSG-P-INT-01 | The integration credentials are missing or invalid. | Error FR-34 external authentication | Integrating client/web configuration if exposed; no key value |
| MSG-P-INT-02 | This integration is not allowed to access the requested resource. | Error FR-34 authorization | Integrating client; request scope review |

## 7. Display and acceptance rules

1. A field error stays attached to its field; a user must not lose their draft on recoverable validation/network failure.
2. Never show success before the API/job confirms the corresponding outcome. A requested return or started backup is not completion.
3. Preserve the exact duplicate-field details “Email is already registered.” and “Phone number is already registered.” until clients migrate away from text matching.
4. Password-change 401 “Current password is incorrect.” is not session expiry. Other protected 401 responses use coordinated refresh at most once and then login.
5. Do not display a successful empty list or zero metrics after load failure. Preserve existing data with an explicit stale/error state where supported.
6. Dynamic values are rendered as text, not HTML. Do not use messages as an authorization control or expose foreign-resource existence.
7. Success, error and validation feedback must be perceptible without color alone and accessible to assistive technology; apply role/status/live announcements as appropriate in implementation.
8. Translate via stable message IDs if localization is added. Do not silently normalize the observed English/Vietnamese source text in this documentation task.
9. Validate observed rows by following their pinned source links; validate proposed rows against the source functions and approve text before UI implementation. No UI automation or device execution is claimed by this catalogue.

## 8. Known discrepancies / owner follow-up

| Item | Observed evidence | Required follow-up (not a source change in LA-221) |
|---|---|---|
| Mobile problem details | LA-19 mapper handles detail/errors; LA-84 snapshot reads message only. | Merge/reconcile through mobile owner so backend field errors are not hidden by generic fallback. |
| Mobile avatar success | LA-84 edit page calls setAvatarUrl and shows “Avatar updated”; it does not invoke the existing upload controller there. | Treat message as local preview only; wire upload before claiming persistence. |
| Mobile login password wording | “At least 6 characters” is current login validation; registration/change-password contract is 8–100 with letter/digit. | Owner to align login UX intentionally; do not mistake this message for a new backend password policy. |
| Web role-update success | Text says changes apply on next token; LA-92 admin APIs additionally recheck DB role immediately. | Consider refined UI copy in a separate web change; preserve observed wording here. |
| Web password success | Web logs out without a dedicated success sentence; mobile shows a success snackbar. | Agree post-change/session explanation; this document does not invent an implemented web toast. |
| Dynamic error exposure | Some web non-Axios Error.message paths pass through raw text. | Review sanitization separately; production diagnostics must not become user-facing secrets. |
| Future numerical policy | Package capacity, retry limits, timeouts and reporting formulas conflict or remain proposed. | Approve policy across CO/DW/LS/UA before freezing numeric message text. |
| Missing future clients | Address/package/order/dispatch/station/analytics/AI/monitoring catalogue rows are proposed requirements, not currently shipped screens in these source snapshots. | Implement and verify on authorized clients in the owning tasks. |

## 9. Coverage checklist

| Area | Message coverage |
|---|---|
| Authentication / FR-01–03 | API validation/login/refresh errors; web login/session/role results; mobile login/errors; success navigation noted |
| Profile / FR-04 | Save/upload/password success behavior; field validation; special 401; conflict; file limits; inaccessible profile; mobile differences |
| Administration / FR-05 | Create/update/role/deactivate success; confirmation; self/last-admin conflicts; load/empty/permission states |
| Customer/orders / FR-06–12 | Proposed CO and NOT rows; common validation/network/empty messages |
| Workflow / FR-13–19, FR-24/25 | Proposed DW rows; scheduling/capacity/overlap, tracking loss, failure/retry/return |
| Stations / FR-20–23, FR-26/27 | Proposed LS rows; station/slot validation, intake, handover/code/lockout |
| Analytics/AI / FR-28–31 | Proposed UA rows; empty/failure/N/A, ETA freshness, summary/chat failure and ownership |
| Audit/integration/operations / FR-32–34, UC-23 | Proposed UA audit, INT, OPS rows; sensitive log contents excluded |

The catalogue is complete for the identified source snapshots and the planned module/function scope above. Future source changes or approved policy changes require updating these rows. Product approval and implementation of proposed text remain separate from documentation completion.
