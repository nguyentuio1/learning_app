# LA-213: Activity Diagram for Authentication and Profile
**Sprint:** Sprint 1-2  
**Parent Ticket:** [LA-194](https://hungndemail.atlassian.net/browse/LA-194)  
**Ticket:** [LA-213](https://hungndemail.atlassian.net/browse/LA-213) - Activity diagram for authentication and profile  
**Task Description:** Model login, token refresh, logout and profile update with the decisions for invalid credentials and expired tokens.  
**Use Cases Covered:** [UC-01, UC-02](../requirements/use-cases.md)  
**Target Architecture:** ASP.NET Core Web API, PostgreSQL, SignalR, MinIO, ReactJS / Flutter Client  

---

## 1. Overview & Architectural Scope

This specification defines the complete identity lifecycle and profile management workflows for the **SmartDroneDelivery** platform. 

It explicitly models all conditional decision branches, edge cases, and security mitigations:
- **Login:** Rate-limiting, hash verification, account lockout policies, and role-based claim issuance.
- **Token Refresh:** Handling expired access tokens, expired refresh tokens, rotated token verification, and refresh token reuse detection (theft mitigation).
- **Logout:** Server-side refresh token revocation, access token blacklisting, SignalR disconnect, and client cache teardown.
- **Profile Update:** Avatar handling via MinIO, email uniqueness checks, current password re-verification, and multi-session revocation on credential modification.

---

## 2. Actors & System Swimlanes

| Swimlane | Component / Actor | Responsibilities |
| :--- | :--- | :--- |
| **Client Lane** | Mobile App (Flutter) / Web Portal (ReactJS) | Captures user inputs, intercepts 401 HTTP statuses, stores tokens, and renders UI state. |
| **API Gateway & Auth Controller** | ASP.NET Core Web API (`/api/v1/auth`, `/api/v1/users`) | Request validation, rate-limiting, JWT parsing, and business logic execution. |
| **Database & Token Store** | PostgreSQL & In-Memory / Distributed Cache | Credential persistence, token family tracking, lockout timestamps, and audit logging. |
| **Auxiliary Services** | MinIO Object Store / Notification Service | Avatar binary persistence and verification email dispatch. |

---

## 3. Detailed Specifications & UML Activity Diagrams

---

### Process 1: User Login (with Invalid Credentials & Lockout Decisions)

#### Specification
- **Endpoint:** `POST /api/v1/auth/login`
- **Primary Actors:** User (Client), Auth API, PostgreSQL Database
- **Preconditions:** User is registered in the system.
- **Postconditions:** User authenticated, issued JWT Access Token (15-min TTL) and Refresh Token (7-day TTL), redirect to role-specific dashboard.

#### Decision Logic & Edge Cases:
1. **Rate Limit Exceeded:** If IP/Client sends > 10 requests/minute $\rightarrow$ Returns `429 Too Many Requests`.
2. **Account Not Found:** Query returns null $\rightarrow$ Returns `401 Unauthorized` with generic error ("Invalid email or password" to prevent user enumeration).
3. **Account Locked:** If `lockout_until > UtcNow` $\rightarrow$ Returns `423 Locked` with remaining lockout minutes.
4. **Invalid Password:** BCrypt verification fails:
   - Increment `failed_login_attempts`.
   - If `failed_login_attempts >= 5` $\rightarrow$ Set `lockout_until = UtcNow + 15 minutes`.
   - Return `401 Unauthorized` ("Invalid email or password").
5. **Account Inactive / Suspended:** `status != Active` $\rightarrow$ Returns `403 Forbidden` ("Account suspended or pending approval").
6. **Success:** Reset `failed_login_attempts = 0`, issue signed JWT Access Token and persistent Refresh Token family.

#### UML Activity Diagram: Login Process

```mermaid
flowchart TD
    subgraph ClientLane["Client (Mobile / Web App)"]
        L_Start([Start]) --> L_Input["Enter Email & Password"]
        L_Input --> L_Submit["Send POST /api/v1/auth/login"]
        L_HandleError["Display Error Message"] --> L_CheckLockout{"Is Account Locked?"}
        L_CheckLockout -->|Yes| L_WaitLockout["Disable Login Button with Countdown"] --> L_EndFail([End - Failed])
        L_CheckLockout -->|No| L_Input
        L_StoreTokens["Store Access Token & Refresh Token"]
        L_StoreTokens --> L_ConnectSignalR["Initialize SignalR Notification Hub"]
        L_ConnectSignalR --> L_NavDashboard["Redirect to Role Dashboard"] --> L_EndSuccess([End - Logged In])
    end

    subgraph AuthAPILane["ASP.NET Core Web API (Auth Service)"]
        L_Submit --> S_RateLimit{"Rate Limit Exceeded?"}
        S_RateLimit -->|Yes| S_Resp429["Return 429 Too Many Requests"]
        S_Resp429 --> L_HandleError

        S_RateLimit -->|No| S_FetchUser["Query User by Email in DB"]
        S_CheckUserExists{"User Record Exists?"}
        S_FetchUser --> S_CheckUserExists

        S_CheckUserExists -->|No| S_DummyHash["Execute Dummy Hash (Timing-Attack Defense)"]
        S_DummyHash --> S_Resp401Invalid["Return 401 Invalid Credentials"]
        S_Resp401Invalid --> L_HandleError

        S_CheckUserExists -->|Yes| S_CheckLockoutState{"Is Account Currently Locked?"}
        S_CheckLockoutState -->|Yes| S_Resp423["Return 423 Account Locked (Retry in X mins)"]
        S_Resp423 --> L_HandleError

        S_CheckLockoutState -->|No| S_VerifyHash["Verify Password Hash via BCrypt"]
        S_VerifyHash --> S_PasswordValid{"Password Hash Matches?"}

        S_PasswordValid -->|No| S_IncFailedAttempts["Increment failed_login_attempts in DB"]
        S_IncFailedAttempts --> S_CheckMaxAttempts{"Attempts >= 5?"}
        S_CheckMaxAttempts -->|Yes| S_LockAccount["Set lockout_until = UtcNow + 15m in DB"]
        S_CheckMaxAttempts -->|No| S_Resp401Password["Return 401 Invalid Credentials"]
        S_LockAccount --> S_Resp401Password
        S_Resp401Password --> L_HandleError

        S_PasswordValid -->|Yes| S_CheckStatus{"Account Status == 'Active'?"}
        S_CheckStatus -->|No| S_Resp403["Return 403 Account Suspended/Inactive"]
        S_Resp403 --> L_HandleError

        S_CheckStatus -->|Yes| S_ResetAttempts["Reset failed_login_attempts = 0 in DB"]
        S_ResetAttempts --> S_GenTokens["Generate JWT Access Token & Refresh Token"]
        S_GenTokens --> S_PersistRefresh["Save Refresh Token Record in DB"]
        S_PersistRefresh --> S_Resp200["Return 200 OK (Tokens, Expiry & User Profile)"]
        S_Resp200 --> L_StoreTokens
    end

    subgraph DBLane["PostgreSQL Database"]
        S_FetchUser -.-> DB_Users[(User Table)]
        S_IncFailedAttempts -.-> DB_Users
        S_LockAccount -.-> DB_Users
        S_ResetAttempts -.-> DB_Users
        S_PersistRefresh -.-> DB_RefreshTokens[(Refresh Tokens Table)]
    end
```

---

### Process 2: Token Refresh & Expired Token Decisions

#### Specification
- **Endpoint:** `POST /api/v1/auth/refresh`
- **Primary Actors:** Client HTTP Interceptor, Auth API, Database Token Store
- **Preconditions:** Client has an expired Access Token and holds an active Refresh Token.
- **Postconditions:** Old refresh token rotated/invalidated, new Access Token + new Refresh Token returned to client.

#### Decision Logic & Edge Cases:
1. **Access Token Expired During API Call:** Client HTTP client intercepts `401 Unauthorized` with `WWW-Authenticate: Bearer error="invalid_token", error_description="The token expired"`.
2. **Refresh Token Format Invalid:** Request body or cookie missing or malformed $\rightarrow$ Returns `400 Bad Request`.
3. **Refresh Token Not in Database:** Token string not recognized $\rightarrow$ Returns `401 Unauthorized` (Session terminated, redirect to login).
4. **Refresh Token Expired (`expires_at < UtcNow`):** 
   - Token is past its 7-day validity window.
   - Delete/mark expired in DB.
   - Return `401 Unauthorized` ("Refresh token expired. Please re-authenticate").
   - Client clears tokens and redirects to Login.
5. **Token Reuse Detected (Theft Detection):** 
   - A refresh token with `is_revoked == true` or `is_used == true` is presented.
   - **Critical Security Event:** Indicates the token was intercepted and already consumed by an attacker.
   - **Mitigation:** Revoke the **entire token family** (all active sessions of the user account).
   - Log security anomaly alarm in audit log.
   - Return `401 Unauthorized` ("Security Alert: Session revoked due to reuse detection").
6. **Valid Active Refresh Token:**
   - Mark current refresh token as `is_used = true`.
   - Issue new Access Token (15 min) and new Refresh Token (7 days, same family ID).
   - Persist new refresh token in DB.
   - Return `200 OK`.
   - Client replays the original queued request seamlessly.

#### UML Activity Diagram: Token Refresh & Expiration Process

```mermaid
flowchart TD
    subgraph ClientLane["Client (HTTP Client & Interceptor)"]
        T_Start([Start API Request]) --> T_SendReq["Send API Request with Bearer Access Token"]
        T_SendReq --> T_ReceiveStatus{"Response Status Code?"}
        
        T_ReceiveStatus -->|200 OK| T_ConsumeData["Process Data Normal Flow"] --> T_EndSuccess([End - Completed])
        
        T_ReceiveStatus -->|401 Token Expired| T_Intercept["HTTP Interceptor Traps 401 Expired Token"]
        T_Intercept --> T_QueueReqs["Queue Pending Outgoing Requests"]
        T_QueueReqs --> T_SendRefresh["Send POST /api/v1/auth/refresh with Refresh Token"]
        
        T_ReceiveRefresh{"Refresh Response?"}
        T_SendRefresh --> T_ReceiveRefresh

        T_ReceiveRefresh -->|401 Expired / Invalid / Reused| T_PurgeSession["Purge Stored Tokens & Client State"]
        T_PurgeSession --> T_RedirectLogin["Force Redirect to Login Screen"] --> T_EndReAuth([End - Re-Authenticate])

        T_ReceiveRefresh -->|200 OK| T_UpdateTokens["Update Stored Access & Refresh Tokens"]
        T_UpdateTokens --> T_ReplayQueue["Replay Queued API Requests with New Token"]
        T_ReplayQueue --> T_ConsumeData
    end

    subgraph AuthAPILane["ASP.NET Core Web API (Token Service)"]
        T_SendRefresh --> S_ParseToken{"Refresh Token Present & Formatted?"}
        S_ParseToken -->|No| S_400Bad["Return 400 Bad Request"]
        S_400Bad --> T_ReceiveRefresh

        S_ParseToken -->|Yes| S_LookupToken["Query Refresh Token Record in DB"]
        S_LookupToken --> S_TokenFound{"Token Record Found?"}
        
        S_TokenFound -->|No| S_401NotFound["Return 401 Invalid Refresh Token"]
        S_401NotFound --> T_ReceiveRefresh

        S_TokenFound -->|Yes| S_CheckReused{"Is Token Already Used or Revoked?"}
        
        S_CheckReused -->|Yes - Breach Suspected!| S_RevokeFamily["Revoke ALL Active Tokens in Token Family"]
        S_RevokeFamily --> S_LogBreach["Log Security Incident: Token Reuse Detected"]
        S_LogBreach --> S_401Breach["Return 401 Session Terminated (Security Breach)"]
        S_401Breach --> T_ReceiveRefresh

        S_CheckReused -->|No| S_CheckExpired{"Token Expiration < UtcNow?"}
        S_CheckExpired -->|Yes - Expired| S_MarkExpired["Delete / Mark Token Expired in DB"]
        S_MarkExpired --> S_401Expired["Return 401 Refresh Token Expired"]
        S_401Expired --> T_ReceiveRefresh

        S_CheckExpired -->|No - Valid| S_CheckUserActive{"Is User Account Still Active?"}
        S_CheckUserActive -->|No| S_403Inactive["Return 403 User Account Inactive"]
        S_403Inactive --> T_ReceiveRefresh

        S_CheckUserActive -->|Yes| S_RotateToken["Mark Current Token 'is_used = true'"]
        S_RotateToken --> S_GenNewPair["Generate New Access Token & New Refresh Token"]
        S_GenNewPair --> S_SaveNewToken["Insert New Refresh Token into DB (Same Family)"]
        S_SaveNewToken --> S_200Refresh["Return 200 OK with New Token Pair"]
        S_200Refresh --> T_ReceiveRefresh
    end

    subgraph DBLane["PostgreSQL Database"]
        S_LookupToken -.-> DB_Tokens[(Refresh Tokens Table)]
        S_RevokeFamily -.-> DB_Tokens
        S_MarkExpired -.-> DB_Tokens
        S_RotateToken -.-> DB_Tokens
        S_SaveNewToken -.-> DB_Tokens
    end
```

---

### Process 3: User Logout Process

#### Specification
- **Endpoint:** `POST /api/v1/auth/logout`
- **Primary Actors:** User (Client), Auth API, Database / Cache Store, SignalR Hub
- **Preconditions:** User is authenticated.
- **Postconditions:** Active refresh token revoked, access token JTI blacklisted until expiration, SignalR connection closed, client tokens deleted.

#### Decision Logic & Edge Cases:
1. **Invalid or Already Expired Token:** If client presents an expired access token during logout, API extracts JTI without throwing authentication failure, ensuring the refresh token is still revoked from database.
2. **Scope of Logout:**
   - *Current Device Logout (Standard):* Revokes only the specific refresh token passed in request.
   - *All Devices Logout (Global):* Revokes all refresh tokens belonging to the user ID.
3. **SignalR Disconnect:** Disconnects real-time hub session to stop receiving flight/telemetry notifications.
4. **Client Teardown:** Client purges in-memory token store, secure storage, and local state cache before redirecting to login.

#### UML Activity Diagram: Logout Process

```mermaid
flowchart TD
    subgraph ClientLane["Client (Mobile / Web App)"]
        LO_Start([Start]) --> LO_ClickLogout["User Clicks 'Log Out'"]
        LO_ClickLogout --> LO_Option{"Logout Scope?"}
        LO_Option -->|Current Device| LO_SendCurrent["Send POST /api/v1/auth/logout (Current Refresh Token)"]
        LO_Option -->|All Devices| LO_SendAll["Send POST /api/v1/auth/logout-all (User Sessions)"]
        
        LO_SendCurrent --> LO_AwaitResp
        LO_SendAll --> LO_AwaitResp
        
        LO_AwaitResp["Receive 200 OK Confirmation"] --> LO_ClearStorage["Clear Access Token, Refresh Token & User State"]
        LO_ClearStorage --> LO_DisconnectWS["Terminate SignalR WebSocket Connection"]
        LO_DisconnectWS --> LO_Redirect["Navigate to Login Screen"] --> LO_End([End - Logged Out])
    end

    subgraph AuthAPILane["ASP.NET Core Web API"]
        LO_SendCurrent --> S_ValidateReq["Extract Access Token Claims & Refresh Token"]
        LO_SendAll --> S_ValidateReq
        
        S_ValidateReq --> S_CheckScope{"Scope?"}
        
        S_CheckScope -->|Current| S_RevokeSingle["Mark Specified Refresh Token 'is_revoked = true' in DB"]
        S_CheckScope -->|All Devices| S_RevokeAll["Mark ALL User Refresh Tokens 'is_revoked = true' in DB"]

        S_RevokeSingle --> S_BlacklistJTI["Add Access Token JTI to Distributed Cache Blacklist"]
        S_RevokeAll --> S_BlacklistJTI

        S_BlacklistJTI --> S_AbortSignalR["Abort Active SignalR Connection Contexts for Session"]
        S_AbortSignalR --> S_WriteAudit["Write 'LOGOUT' Entry to Audit Log"]
        S_WriteAudit --> S_Return200["Return 200 OK"]
        S_Return200 --> LO_AwaitResp
    end

    subgraph StorageLane["Database & Cache Store"]
        S_RevokeSingle -.-> DB_Refresh[(PostgreSQL Refresh Tokens)]
        S_RevokeAll -.-> DB_Refresh
        S_BlacklistJTI -.-> Cache_Blacklist[(Redis / MemoryCache JTI Blacklist)]
    end
```

---

### Process 4: Profile Update (with Image Upload & Password Change Decisions)

#### Specification
- **Endpoints:** 
  - `GET /api/v1/users/profile` (Retrieve Current Profile)
  - `PUT /api/v1/users/profile` (Update Profile Details & Avatar)
  - `PUT /api/v1/users/change-password` (Update Password)
- **Primary Actors:** User, Users API Controller, PostgreSQL, MinIO Storage, Notification Service
- **Preconditions:** User is authenticated with a valid access token.
- **Postconditions:** User profile updated, avatar persisted in MinIO, password re-hashed if changed, other active sessions revoked upon password change.

#### Decision Logic & Edge Cases:
1. **Avatar Image Validation:**
   - Allowed formats: `image/jpeg`, `image/png`, `image/webp`. Max size: `2MB`.
   - If invalid format or size $> 2\text{MB}$ $\rightarrow$ Return `400 Bad Request`.
   - If valid $\rightarrow$ Upload stream to MinIO bucket `user-avatars`, generate permanent/presigned URL.
2. **Email Change & Uniqueness:**
   - If email is updated $\rightarrow$ Check if new email already exists in DB.
   - If already taken by another user $\rightarrow$ Return `409 Conflict` ("Email is already registered").
   - If unique $\rightarrow$ Set `email_verified = false`, dispatch email verification link via Notification Service.
3. **Password Change Verification:**
   - Verify `current_password` hash via BCrypt.
   - If incorrect $\rightarrow$ Return `400 Bad Request` ("Current password does not match").
   - Validate new password against complexity policy (minimum 8 chars, uppercase, lowercase, numeric, special char).
   - If weak $\rightarrow$ Return `422 Unprocessable Entity` ("Password complexity criteria not met").
   - If valid $\rightarrow$ Hash new password, update DB, and **revoke all active refresh tokens except the current one** to secure against compromised sessions.

#### UML Activity Diagram: Profile Update Process

```mermaid
flowchart TD
    subgraph ClientLane["Client (Mobile / Web App)"]
        P_Start([Start]) --> P_Nav["Open Profile Settings Screen"]
        P_Nav --> P_Fetch["GET /api/v1/users/profile"]
        P_Fetch --> P_Display["Render Profile Form (Name, Phone, Email, Avatar)"]
        
        P_Display --> P_EditChoice{"Select Update Action"}
        
        P_EditChoice -->|Update Details / Avatar| P_InputDetails["Modify Name, Phone, New Email or Pick Avatar File"]
        P_EditChoice -->|Change Password| P_InputPassword["Enter Current Password & New Password"]

        P_InputDetails --> P_SubmitDetails["Send PUT /api/v1/users/profile"]
        P_InputPassword --> P_SubmitPassword["Send PUT /api/v1/users/change-password"]

        P_ViewError["Display Specific Validation Error"]
        P_SubmitDetails --> P_AwaitProfileResp{"Response?"}
        P_SubmitPassword --> P_AwaitPwdResp{"Response?"}

        P_AwaitProfileResp -->|Error| P_ViewError
        P_AwaitPwdResp -->|Error| P_ViewError
        P_ViewError --> P_Display

        P_AwaitProfileResp -->|200 OK| P_SuccessProfile["Update Local User State & Avatar View"] --> P_End([End - Updated])
        P_AwaitPwdResp -->|200 OK| P_SuccessPwd["Show 'Password Changed Successfully' Notice"] --> P_End
    end

    subgraph APIControllerLane["ASP.NET Core Web API (User Profile Service)"]
        P_SubmitDetails --> S_ValFields{"Are Basic Fields Valid?"}
        S_ValFields -->|No| S_Err400["Return 400 Bad Request"]
        S_Err400 --> P_AwaitProfileResp

        S_ValFields -->|Yes| S_CheckAvatar{"Avatar File Attached?"}
        
        S_CheckAvatar -->|Yes| S_ValAvatar{"MIME Valid & Size <= 2MB?"}
        S_ValAvatar -->|No| S_AvatarErr["Return 400 Invalid Image Format/Size"]
        S_AvatarErr --> P_AwaitProfileResp
        S_ValAvatar -->|Yes| S_UploadMinIO["Upload Image Binary to MinIO Bucket"]
        S_UploadMinIO --> S_CheckEmailChange
        S_CheckAvatar -->|No| S_CheckEmailChange{"Email Changed?"}

        S_CheckEmailChange -->|Yes| S_CheckEmailUnique{"Is Email Already in DB?"}
        S_CheckEmailUnique -->|Taken| S_EmailTaken["Return 409 Email Already in Use"]
        S_EmailTaken --> P_AwaitProfileResp
        S_CheckEmailUnique -->|Unique| S_PrepVerification["Generate Verification Token & Queue Email"]
        S_PrepVerification --> S_SaveProfileDB["Save Updated User Entity in DB"]
        S_CheckEmailChange -->|No| S_SaveProfileDB

        S_SaveProfileDB --> S_ReturnProfile200["Return 200 OK with Updated Profile"]
        S_ReturnProfile200 --> P_AwaitProfileResp

        %% Password Change Flow
        P_SubmitPassword --> S_VerifyOldPwd["Verify Current Password Hash via BCrypt"]
        S_VerifyOldPwd --> S_OldPwdValid{"Current Password Matches?"}
        S_OldPwdValid -->|No| S_OldPwdErr["Return 400 Current Password Incorrect"]
        S_OldPwdErr --> P_AwaitPwdResp

        S_OldPwdValid -->|Yes| S_CheckPwdComplexity{"New Password Meets Complexity Rules?"}
        S_CheckPwdComplexity -->|No| S_PwdComplexityErr["Return 422 Weak Password"]
        S_PwdComplexityErr --> P_AwaitPwdResp

        S_CheckPwdComplexity -->|Yes| S_HashNewPwd["Hash New Password with BCrypt"]
        S_HashNewPwd --> S_UpdatePwdDB["Update Password Hash in DB"]
        S_UpdatePwdDB --> S_RevokeOtherSessions["Revoke All Other User Refresh Tokens in DB"]
        S_RevokeOtherSessions --> S_ReturnPwd200["Return 200 OK Password Changed"]
        S_ReturnPwd200 --> P_AwaitPwdResp
    end

    subgraph MinIOLane["MinIO Storage & Email"]
        S_UploadMinIO -.-> M_Bucket[(MinIO: user-avatars)]
        M_Bucket -.-> S_CheckEmailChange
        S_PrepVerification -.-> N_EmailService["Notification Service (Verification Email)"]
    end

    subgraph DBLane2["PostgreSQL Database"]
        S_CheckEmailUnique -.-> DB_UserQuery[(Users Table)]
        S_SaveProfileDB -.-> DB_UserQuery
        S_UpdatePwdDB -.-> DB_UserQuery
        S_RevokeOtherSessions -.-> DB_TokensQuery[(Refresh Tokens Table)]
    end
```

---

## 4. Error Code & Decision Matrix

| Action | Condition / Decision | HTTP Status | Code / Response Message | System Action |
| :--- | :--- | :--- | :--- | :--- |
| **Login** | Rate limit threshold exceeded | `429 Too Many Requests` | `AUTH_RATE_LIMIT_EXCEEDED` | Client throttled for 60 seconds. |
| **Login** | Unknown user or incorrect password | `401 Unauthorized` | `INVALID_CREDENTIALS` | Generic message; increments failed counter. |
| **Login** | Failed attempts $\ge 5$ | `423 Locked` | `ACCOUNT_TEMPORARILY_LOCKED` | Account locked for 15 minutes; timer returned. |
| **Login** | Account marked inactive/suspended | `403 Forbidden` | `ACCOUNT_INACTIVE` | Login rejected; contact admin notice. |
| **Refresh** | Token missing or malformed | `400 Bad Request` | `INVALID_TOKEN_FORMAT` | Immediate client redirect to login. |
| **Refresh** | Refresh token not found in database | `401 Unauthorized` | `REFRESH_TOKEN_NOT_FOUND` | Session invalid; force re-login. |
| **Refresh** | Token expired (`expires_at < UtcNow`) | `401 Unauthorized` | `REFRESH_TOKEN_EXPIRED` | Expired record purged; force re-login. |
| **Refresh** | Token already revoked or reused | `401 Unauthorized` | `TOKEN_REUSE_DETECTED` | **All tokens in family revoked**; security alert logged. |
| **Logout** | Normal single session logout | `200 OK` | `LOGOUT_SUCCESSFUL` | Target token revoked; access JTI blacklisted. |
| **Logout** | All devices session revocation | `200 OK` | `GLOBAL_LOGOUT_SUCCESSFUL` | All active tokens for user revoked. |
| **Profile** | Avatar file $> 2\text{MB}$ or non-image MIME | `400 Bad Request` | `INVALID_AVATAR_FORMAT` | Upload aborted. |
| **Profile** | New email already registered by another | `409 Conflict` | `EMAIL_ALREADY_EXISTS` | Update aborted; current email retained. |
| **Profile** | Current password incorrect on change | `400 Bad Request` | `CURRENT_PASSWORD_INCORRECT` | Password change rejected. |
| **Profile** | New password fails complexity regex | `422 Unprocessable` | `PASSWORD_COMPLEXITY_FAILED` | Requires upper, lower, number, special char. |
| **Profile** | Password successfully changed | `200 OK` | `PASSWORD_CHANGED_SUCCESS` | New hash saved; **all other sessions revoked**. |

---

## 5. Implementation Notes for Sprint 1-2

1. **JWT & Refresh Token Architecture:**
   - Access token lifetime: **15 minutes** (`expires_in: 900`).
   - Refresh token lifetime: **7 days** with automatic token rotation.
   - Database table `refresh_tokens`: `id`, `user_id`, `token_hash`, `family_id`, `is_used`, `is_revoked`, `expires_at`, `created_at`, `replaced_by_token_id`.
2. **Timing-Attack Countermeasure:**
   - When an email address is not found in database, execute a dummy BCrypt verification before returning `401 Unauthorized` so that response duration is indistinguishable from an invalid password attempt.
3. **Blacklist for Revoked Access Tokens:**
   - For immediate logout effect prior to 15-minute access token expiration, store the revoked token's `jti` in a distributed Redis or in-memory sliding cache with TTL equal to the token's remaining lifespan.
