# LA-213: Activity Diagram for Authentication and Profile
**Sprint:** Sprint 1-2  
**Parent Ticket:** [LA-194](https://hungndemail.atlassian.net/browse/LA-194)  
**Ticket:** [LA-213](https://hungndemail.atlassian.net/browse/LA-213) - Activity diagram for authentication and profile  
**Task Description:** Model login, token refresh, logout and profile update with the decisions for invalid credentials and expired tokens.  
**Use Cases Covered:** [UC-01, UC-02](../requirements/use-cases.md)  
**API Contract:** [auth-api.md](../api/auth-api.md)  
**Sequence Diagram:** [sequence-auth.md](sequence-auth.md)  
**Entities:** `users`, `roles`, `refresh_tokens`, `audit_logs`  

---

## 1. Overview & Architectural Scope

This document specifies the UML Activity Diagrams with dedicated actor swimlanes for authentication and profile management in the **SmartDroneDelivery** platform.

The models strictly follow the merged API contract ([auth-api.md](../api/auth-api.md)), sequence diagrams ([sequence-auth.md](sequence-auth.md)), and PostgreSQL entity definitions ([erd.md](../architecture/erd.md)):
- Base path prefix is `/api`.
- Standard endpoints: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/users/me`, `PUT /api/users/me`, `PUT /api/users/me/password`, `POST /api/users/me/avatar`.
- Error responses follow RFC 7807 problem details (400 validation, 401 unauthorized, 403 forbidden, 409 conflict).

---

## 2. Actors & System Swimlanes

| Swimlane | Component / Actor | Responsibilities |
|---|---|---|
| **Client Lane** | Web Portal (ReactJS) / Mobile App (Flutter) | Collects credentials, traps 401 expired tokens, manages local tokens, calls refresh endpoint, and updates UI state. |
| **AuthController / UserController** | ASP.NET Core Controllers (`/api/auth/*`, `/api/users/me/*`) | Request body validation, route handling, HTTP status mapping, RFC 7807 problem responses. |
| **AuthService / UserService** | Business Logic Services (`SmartDroneDelivery.Api`) | Password hashing/verification via BCrypt, JWT generation, token rotation, and profile updates. |
| **Database & Storage** | PostgreSQL (`AppDbContext`) & MinIO | Entity persistence in `users`, `roles`, `refresh_tokens`, `audit_logs`, and avatar object storage. |

---

## 3. Detailed UML Activity Diagrams

---

### Process 1: User Login (with Invalid Credentials Decision)

#### Specification
- **Endpoint:** `POST /api/auth/login`
- **Use Case:** `UC-01`
- **Preconditions:** User has a registered account.
- **Postconditions:** User authenticated, issued JWT Access Token (15-min lifetime) and Refresh Token (7-day lifetime), row inserted into `refresh_tokens` and `audit_logs`.

#### Decision Logic:
1. **Request Body Validation:** If email or password is missing $\rightarrow$ Returns `400 Bad Request`.
2. **User Lookup by Email:** Query `users` by email. If not found $\rightarrow$ Returns `401 Unauthorized` ("Invalid email or password.").
3. **Password Hash Verification:** Verify with BCrypt. If password does not match $\rightarrow$ Returns `401 Unauthorized` ("Invalid email or password.").
4. **Account Status Check:**
   - If `status` is `INACTIVE` or `SUSPENDED` $\rightarrow$ Returns `403 Forbidden` ("This account is not active.").
   - If `status` is `ACTIVE` $\rightarrow$ Proceeds to token generation.
5. **Token Generation & Logging:**
   - Generate access token (JWT, 15 minutes) with claims `sub`, `email`, `role`, `exp`.
   - Generate random cryptographically secure refresh token (7 days).
   - Insert row into `refresh_tokens` (`is_revoked = false`).
   - Insert row into `audit_logs` with action `LOGIN`.
   - Return `200 OK` with `AuthResponse`.

#### UML Activity Diagram: Login Process

```mermaid
flowchart TB
    subgraph ClientLane["Client (Web / Mobile App)"]
        L_Start((Start)) --> L_Input["Enter email and password"]
        L_Input --> L_Submit["Send POST /api/auth/login {email, password}"]
        L_ShowErr["Display error message"] --> L_Input
        L_StoreTokens["Store access token (15m) & refresh token (7d)"]
        L_StoreTokens --> L_NavDashboard["Navigate to authorized dashboard"] --> L_EndSuccess((End))
    end

    subgraph ControllerLane["AuthController"]
        L_Submit --> C_ValBody{"Request body valid?<br/>(email and password present)"}
        C_ValBody -->|No| C_Resp400["Return 400 Bad Request with validation errors"]
        C_Resp400 --> L_ShowErr
        C_ValBody -->|Yes| C_CallService["Call AuthService.LoginAsync(email, password)"]
    end

    subgraph ServiceLane["AuthService & PasswordHasher"]
        C_CallService --> S_FindUser["Query user by email in PostgreSQL"]
        S_FindUser --> S_UserExists{"User found?"}
        
        S_UserExists -->|No| S_ErrNotFound["Return invalid credentials"]
        S_ErrNotFound --> C_Resp401["Return 401 Unauthorized<br/>(Invalid email or password)"]
        C_Resp401 --> L_ShowErr

        S_UserExists -->|Yes| S_VerifyHash["Verify password with BCrypt"]
        S_VerifyHash --> S_PwdMatch{"Password matches?"}
        
        S_PwdMatch -->|No| S_ErrWrongPwd["Return invalid credentials"]
        S_ErrWrongPwd --> C_Resp401

        S_PwdMatch -->|Yes| S_CheckStatus{"User status?"}
        S_CheckStatus -->|"INACTIVE or SUSPENDED"| S_ErrInactive["Return account not active"]
        S_ErrInactive --> C_Resp403["Return 403 Forbidden<br/>(This account is not active)"]
        C_Resp403 --> L_ShowErr

        S_CheckStatus -->|ACTIVE| S_CreateTokens["Create JWT Access Token (15 min)<br/>and random Refresh Token (7 days)"]
        S_CreateTokens --> S_SaveDB["Insert row into refresh_tokens<br/>Insert row into audit_logs (LOGIN)"]
        S_SaveDB --> S_ReturnOK["Return AuthResponse with tokens and User profile"]
        S_ReturnOK --> C_Resp200["Return 200 OK"]
        C_Resp200 --> L_StoreTokens
    end
```

---

### Process 2: Token Refresh (with Expired / Revoked Token Decision)

#### Specification
- **Endpoint:** `POST /api/auth/refresh`
- **Use Case:** `UC-01`
- **Preconditions:** Client has an expired access token and holds a refresh token.
- **Postconditions:** Old refresh token marked `is_revoked = true`, new refresh token inserted, new access token issued.

#### Decision Logic:
1. **Access Token Expiration:** Client API request receives `401 Unauthorized`. Client HTTP interceptor traps this and calls `POST /api/auth/refresh`.
2. **Request Body Validation:** If `refreshToken` is missing $\rightarrow$ Returns `400 Bad Request`.
3. **Token Lookup in Database:** Query `refresh_tokens` by token string.
   - If token not found $\rightarrow$ Returns `401 Unauthorized`.
   - If `is_revoked == true` $\rightarrow$ Returns `401 Unauthorized`.
   - If `expires_at < UtcNow` (expired) $\rightarrow$ Returns `401 Unauthorized`.
4. **Valid Token Rotation:**
   - Mark old refresh token `is_revoked = true`.
   - Generate new access token (15 min) and new refresh token (7 days).
   - Insert new row into `refresh_tokens`.
   - Return `200 OK` with new `AuthResponse`.
   - Client updates stored tokens and retries the original request.
5. **Refresh Failure:** If refresh returns `401`, client purges stored tokens and redirects user to login.

#### UML Activity Diagram: Token Refresh Process

```mermaid
flowchart TB
    subgraph ClientLane["Client (HTTP Client & Interceptor)"]
        R_Start((Start Request)) --> R_SendReq["Send API request with Bearer access token"]
        R_SendReq --> R_CheckStatus{"Response status code?"}
        R_CheckStatus -->|200 OK| R_ProcessData["Process response data"] --> R_EndReq((End))
        
        R_CheckStatus -->|401 Token Expired| R_Intercept["Interceptor traps 401<br/>calls POST /api/auth/refresh"]
        R_Intercept --> R_SendRefresh["Send POST /api/auth/refresh {refreshToken}"]
        
        R_RefreshResult{"Refresh response?"}
        R_SendRefresh --> R_RefreshResult
        
        R_RefreshResult -->|401 Unauthorized| R_Purge["Purge local tokens and user session"]
        R_Purge --> R_RedirectLogin["Redirect to Login screen"] --> R_EndReAuth((Re-authenticate))

        R_RefreshResult -->|200 OK| R_UpdateTokens["Update stored access & refresh tokens"]
        R_UpdateTokens --> R_RetryReq["Retry original API request with new token"]
        R_RetryReq --> R_ProcessData
    end

    subgraph ControllerLane["AuthController"]
        R_SendRefresh --> C_CheckRefreshBody{"refreshToken provided?"}
        C_CheckRefreshBody -->|No| C_Ref400["Return 400 Bad Request"]
        C_Ref400 --> R_RefreshResult
        C_CheckRefreshBody -->|Yes| C_CallRefreshService["Call AuthService.RefreshAsync(refreshToken)"]
    end

    subgraph ServiceLane["AuthService & TokenService"]
        C_CallRefreshService --> S_LookupToken["Find row in refresh_tokens table"]
        S_LookupToken --> S_EvalToken{"Token exists and valid?<br/>(not expired, is_revoked == false)"}
        
        S_EvalToken -->|No| S_RefErr["Token unknown, expired or revoked"]
        S_RefErr --> C_Ref401["Return 401 Unauthorized"]
        C_Ref401 --> R_RefreshResult

        S_EvalToken -->|Yes| S_Rotate["Set old refresh token is_revoked = true"]
        S_Rotate --> S_CreateNewPair["Create new access token (15m)<br/>and new refresh token (7d)"]
        S_CreateNewPair --> S_InsertNewToken["Insert new row into refresh_tokens"]
        S_InsertNewToken --> S_ReturnAuth["Return new AuthResponse"]
        S_ReturnAuth --> C_Ref200["Return 200 OK"]
        C_Ref200 --> R_RefreshResult
    end
```

---

### Process 3: User Logout Process

#### Specification
- **Endpoint:** `POST /api/auth/logout`
- **Use Case:** `UC-01`
- **Preconditions:** User is signed in (`Authorization: Bearer <accessToken>`).
- **Postconditions:** Refresh token marked `is_revoked = true` in database, audit log entry written with action `LOGOUT`, response `204 No Content`, client clears tokens and session state.

#### UML Activity Diagram: Logout Process

```mermaid
flowchart TB
    subgraph ClientLane["Client (Web / Mobile App)"]
        LO_Start((Start)) --> LO_Click["User clicks 'Log Out'"]
        LO_Click --> LO_SendReq["Send POST /api/auth/logout {refreshToken}<br/>Headers: Authorization: Bearer accessToken"]
        LO_AwaitResp["Receive response (204 No Content)"]
        LO_SendReq --> LO_AwaitResp
        LO_AwaitResp --> LO_Clear["Clear tokens and cached user data from storage"]
        LO_Clear --> LO_Redirect["Redirect to Login screen"] --> LO_End((End))
    end

    subgraph ControllerLane["AuthController"]
        LO_SendReq --> C_AuthValid{"Bearer token valid?"}
        C_AuthValid -->|No| C_LO401["Return 401 Unauthorized"]
        C_LO401 --> LO_Clear
        C_AuthValid -->|Yes| C_CallLogout["Call AuthService.LogoutAsync(userId, refreshToken)"]
        C_Return204["Return 204 No Content"]
        C_Return204 --> LO_AwaitResp
    end

    subgraph ServiceLane["AuthService"]
        C_CallLogout --> S_Revoke["Find refresh_tokens row and set is_revoked = true<br/>(if unknown or already revoked, proceed gracefully)"]
        S_Revoke --> S_WriteAudit["Insert row into audit_logs (LOGOUT, userId, timestamp)"]
        S_WriteAudit --> C_Return204
    end
```

---

### Process 4: Profile Management & Password Change

#### Specification
- **Endpoints:**
  - `GET /api/users/me` (View current profile)
  - `PUT /api/users/me` (Update `fullName`, `phoneNumber`)
  - `PUT /api/users/me/password` (Change password with current password verification)
  - `POST /api/users/me/avatar` (Upload avatar image $\le 5\text{MB}$ via MinIO)
- **Use Case:** `UC-02`
- **Preconditions:** User is signed in with a valid access token.
- **Postconditions:** Profile updated; if password changed, all user refresh tokens revoked and audit log entry created.

#### Rules from Contract:
1. `PUT /api/users/me`: Only `fullName` and `phoneNumber` can be updated. Email and role cannot be changed. If phone number is already used by another account $\rightarrow$ `409 Conflict`.
2. `PUT /api/users/me/password`:
   - Checks `currentPassword` against stored hash: if wrong $\rightarrow$ `401 Unauthorized`.
   - Checks `newPassword` complexity (8–100 chars, letter + digit): if invalid $\rightarrow$ `400 Bad Request`.
   - Hashes new password with BCrypt and updates database.
   - **Revokes all refresh tokens** of the user (`is_revoked = true`) so other sessions must re-authenticate.
   - Writes row in `audit_logs` (action: `CHANGE_PASSWORD`).
   - Returns `204 No Content`.
3. `POST /api/users/me/avatar`: Accepts `multipart/form-data` with field `file` (JPEG/PNG, max 5 MB). Uploads to MinIO and returns `200 OK` with `avatarUrl`. If wrong type or $> 5\text{MB}$ $\rightarrow$ `400 Bad Request`.

#### UML Activity Diagram: Profile Update & Password Change

```mermaid
flowchart TB
    subgraph ClientLane["Client (Web / Mobile App)"]
        P_Start((Start)) --> P_Open["Open Profile screen"]
        P_Open --> P_Get["Send GET /api/users/me"]
        P_Get --> P_Display["Display profile (fullName, phoneNumber, email, avatarUrl, role)"]
        
        P_Display --> P_ActionChoice{"Choose update action"}
        
        %% Update details
        P_ActionChoice -->|Edit info| P_EditDetails["Edit fullName or phoneNumber"]
        P_EditDetails --> P_SubmitDetails["Send PUT /api/users/me {fullName, phoneNumber}"]
        P_SubmitDetails --> P_RespDetails{"Response?"}
        P_RespDetails -->|400 or 409| P_ShowErr["Display validation or phone conflict error"] --> P_Display
        P_RespDetails -->|200 OK| P_UpdateUI["Update local user profile state"] --> P_End((End))

        %% Upload avatar
        P_ActionChoice -->|Upload avatar| P_PickImg["Pick image file (JPEG/PNG, <= 5MB)"]
        P_PickImg --> P_UploadAvatar["Send POST /api/users/me/avatar (multipart/form-data)"]
        P_UploadAvatar --> P_RespAvatar{"Response?"}
        P_RespAvatar -->|400| P_ShowAvatarErr["Show invalid format or file size error"] --> P_Display
        P_RespAvatar -->|200 OK| P_ShowNewAvatar["Update avatarUrl on screen"] --> P_End

        %% Change password
        P_ActionChoice -->|Change password| P_InputPwd["Enter currentPassword and newPassword"]
        P_InputPwd --> P_SubmitPwd["Send PUT /api/users/me/password {currentPassword, newPassword}"]
        P_SubmitPwd --> P_RespPwd{"Response?"}
        P_RespPwd -->|400 or 401| P_ShowPwdErr["Show wrong password or complexity error"] --> P_InputPwd
        P_RespPwd -->|204 No Content| P_ShowPwdSuccess["Show 'Password changed successfully' notice"] --> P_End
    end

    subgraph ControllerLane["UsersController"]
        %% PUT /api/users/me
        P_SubmitDetails --> C_ValProfile{"fullName and phoneNumber valid?"}
        C_ValProfile -->|No| C_Prof400["Return 400 Bad Request"] --> P_RespDetails
        C_ValProfile -->|Yes| C_CallProfService["Call UserService.UpdateProfileAsync(userId, req)"]

        %% POST /api/users/me/avatar
        P_UploadAvatar --> C_ValAvatar{"File valid? (JPEG/PNG, <= 5MB)"}
        C_ValAvatar -->|No| C_Av400["Return 400 Bad Request"] --> P_RespAvatar
        C_ValAvatar -->|Yes| C_CallAvService["Upload file stream to MinIO"]

        %% PUT /api/users/me/password
        P_SubmitPwd --> C_ValNewPwd{"newPassword meets rule?<br/>(8-100 chars, letter + digit)"}
        C_ValNewPwd -->|No| C_Pwd400["Return 400 Bad Request"] --> P_RespPwd
        C_ValNewPwd -->|Yes| C_CallPwdService["Call UserService.ChangePasswordAsync(userId, req)"]
    end

    subgraph ServiceLane["UserService & AppDbContext"]
        %% Profile Service
        C_CallProfService --> S_CheckPhoneUnique{"phoneNumber already used<br/>by another user?"}
        S_CheckPhoneUnique -->|Yes| S_Prof409["Return 409 Conflict<br/>(Phone number already used)"] --> P_RespDetails
        S_CheckPhoneUnique -->|No| S_SaveUser["Update fullName, phoneNumber in users table"]
        S_SaveUser --> S_ReturnUser["Return updated User object (200 OK)"] --> P_RespDetails

        %% Avatar Service
        C_CallAvService --> S_MinIO["Save file into MinIO bucket 'avatars'"]
        S_MinIO --> S_UpdateAvatarUrl["Update users.avatar_url in PostgreSQL"]
        S_UpdateAvatarUrl --> S_ReturnAvatar["Return 200 OK {avatarUrl}"] --> P_RespAvatar

        %% Password Service
        C_CallPwdService --> S_VerifyCurrent["Verify currentPassword with BCrypt against password_hash"]
        S_VerifyCurrent --> S_CurPwdMatch{"Password correct?"}
        S_CurPwdMatch -->|No| S_Pwd401["Return 401 Unauthorized<br/>(Current password is wrong)"] --> P_RespPwd
        S_CurPwdMatch -->|Yes| S_HashNew["Hash newPassword with BCrypt"]
        S_HashNew --> S_UpdatePwdDB["Update users.password_hash"]
        S_UpdatePwdDB --> S_RevokeAllTokens["Set is_revoked = true for all user refresh_tokens"]
        S_RevokeAllTokens --> S_AuditPwd["Insert row into audit_logs (CHANGE_PASSWORD)"]
        S_AuditPwd --> S_Return204["Return 204 No Content"] --> P_RespPwd
    end
```

---

## 4. Contract Status & Error Mapping Matrix

| Endpoint | Condition | Status Code | Response / Detail |
|---|---|---|---|
| `POST /api/auth/login` | Missing email or password | `400 Bad Request` | Problem details with field errors |
| `POST /api/auth/login` | Email not found or wrong password | `401 Unauthorized` | `"Invalid email or password."` |
| `POST /api/auth/login` | Status is `INACTIVE` or `SUSPENDED` | `403 Forbidden` | `"This account is not active."` |
| `POST /api/auth/login` | Valid credentials, status `ACTIVE` | `200 OK` | `AuthResponse` (`accessToken`, `refreshToken`, `expiresIn`, `user`) |
| `POST /api/auth/refresh` | Missing refresh token | `400 Bad Request` | Missing token error |
| `POST /api/auth/refresh` | Unknown, expired, or revoked token | `401 Unauthorized` | Invalid refresh token |
| `POST /api/auth/refresh` | Valid refresh token | `200 OK` | New `AuthResponse` with rotated tokens |
| `POST /api/auth/logout` | Valid access token and refresh token | `204 No Content` | Refresh token revoked in DB, audit log written |
| `GET /api/users/me` | Valid access token | `200 OK` | `User` profile object |
| `PUT /api/users/me` | Validation failed | `400 Bad Request` | Field validation errors |
| `PUT /api/users/me` | Phone number already used | `409 Conflict` | `"Phone number is already registered."` |
| `PUT /api/users/me` | Valid update | `200 OK` | Updated `User` object |
| `PUT /api/users/me/password`| New password < 8 chars or no digit | `400 Bad Request` | Password complexity failure |
| `PUT /api/users/me/password`| Current password incorrect | `401 Unauthorized` | `"The current password is wrong."` |
| `PUT /api/users/me/password`| Successful change | `204 No Content` | All refresh tokens revoked, audit log written |
| `POST /api/users/me/avatar` | Invalid file type or size > 5 MB | `400 Bad Request` | Wrong file type or file too large |
| `POST /api/users/me/avatar` | Valid upload | `200 OK` | `{ "avatarUrl": "..." }` |
