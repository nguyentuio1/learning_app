# Sequence Diagrams - Authentication

Sequence diagrams of the authentication use cases, following the [API contract](../api/auth-api.md) and the entities `User`, `Role` and `RefreshToken`.

> **Proposed class names.** The participants below (`AuthController`, `AuthService`, `TokenService`, `PasswordHasher`, `AppDbContext`) are the proposed classes of the authentication module. The class diagram of the same module (LA-222) must use the same names.

## Participants

| Participant | Role |
|---|---|
| Client | Web portal (React) or mobile app (Flutter) |
| AuthController | ASP.NET Core controller exposing `/api/auth/*` |
| AuthService | Business logic of login, refresh and logout |
| PasswordHasher | BCrypt hashing and verification |
| TokenService | Creates and validates JWT access tokens and refresh tokens |
| AppDbContext | EF Core access to the tables `users`, `roles`, `refresh_tokens`, `audit_logs` |

## 1. Login (LA-223)

```mermaid
sequenceDiagram
    actor U as User
    participant C as Client
    participant AC as AuthController
    participant AS as AuthService
    participant PH as PasswordHasher
    participant TS as TokenService
    participant DB as AppDbContext

    U->>C: Enter email and password
    C->>AC: POST /api/auth/login {email, password}
    AC->>AC: Validate request body
    alt invalid body
        AC-->>C: 400 Bad Request with errors
    else valid body
        AC->>AS: LoginAsync(email, password)
        AS->>DB: Find user by email, include role
        DB-->>AS: user or null
        alt user not found
            AS-->>AC: invalid credentials
            AC-->>C: 401 Unauthorized
        else user found
            AS->>PH: Verify(password, user.PasswordHash)
            PH-->>AS: true or false
            alt wrong password
                AS-->>AC: invalid credentials
                AC-->>C: 401 Unauthorized
            else password correct
                alt status is INACTIVE or SUSPENDED
                    AS-->>AC: account not active
                    AC-->>C: 403 Forbidden
                else status is ACTIVE
                    AS->>TS: CreateAccessToken(user)
                    TS-->>AS: access token (JWT, 15 minutes)
                    AS->>TS: CreateRefreshToken()
                    TS-->>AS: refresh token (7 days)
                    AS->>DB: Insert refresh_tokens row
                    AS->>DB: Insert audit_logs row (LOGIN)
                    DB-->>AS: saved
                    AS-->>AC: AuthResponse
                    AC-->>C: 200 OK AuthResponse
                    C->>C: Store tokens securely
                    C-->>U: Open the home screen of the role
                end
            end
        end
    end
```

Notes:
- Wrong email and wrong password return the same `401` message so the response does not reveal which accounts exist.
- The web portal keeps the tokens in browser storage chosen in the web design; the Flutter apps use secure storage.

## 2. Token refresh and logout (LA-224)

### 2.1 Refresh an expired access token

```mermaid
sequenceDiagram
    participant C as Client
    participant AC as AuthController
    participant AS as AuthService
    participant TS as TokenService
    participant DB as AppDbContext

    C->>AC: Any API call with expired access token
    AC-->>C: 401 Unauthorized
    C->>AC: POST /api/auth/refresh {refreshToken}
    AC->>AS: RefreshAsync(refreshToken)
    AS->>DB: Find refresh_tokens row by token, include user
    DB-->>AS: row or null
    alt token unknown, expired or revoked
        AS-->>AC: invalid refresh token
        AC-->>C: 401 Unauthorized
        C->>C: Clear tokens and open the login screen
    else token valid
        AS->>DB: Set old row IsRevoked = true
        AS->>TS: CreateAccessToken(user)
        TS-->>AS: new access token
        AS->>TS: CreateRefreshToken()
        TS-->>AS: new refresh token
        AS->>DB: Insert new refresh_tokens row
        DB-->>AS: saved
        AS-->>AC: AuthResponse
        AC-->>C: 200 OK AuthResponse
        C->>AC: Retry the original API call with the new access token
        AC-->>C: Original response
    end
```

Notes:
- The client refreshes once per expiry. If several calls fail with `401` at the same time, they wait for the same refresh and then retry.
- Rotating the refresh token means a stolen old token stops working after the legitimate client refreshes.

### 2.2 Logout

```mermaid
sequenceDiagram
    actor U as User
    participant C as Client
    participant AC as AuthController
    participant AS as AuthService
    participant DB as AppDbContext

    U->>C: Choose Log out
    C->>AC: POST /api/auth/logout {refreshToken} with Bearer access token
    AC->>AS: LogoutAsync(userId, refreshToken)
    AS->>DB: Find refresh_tokens row by token
    DB-->>AS: row or null
    alt row exists and is not revoked
        AS->>DB: Set IsRevoked = true
    end
    AS->>DB: Insert audit_logs row (LOGOUT)
    DB-->>AS: saved
    AS-->>AC: done
    AC-->>C: 204 No Content
    C->>C: Clear stored tokens
    C-->>U: Open the login screen
```

Notes:
- Logout returns `204` even if the token is unknown or already revoked, so retrying is safe.
- The short-lived access token is not stored on the server; it expires by itself within 15 minutes.

## 3. Link to use cases and requirements

| Diagram | Use case | Requirements |
|---|---|---|
| Login | UC-01 | FR-01, FR-02, FR-03 |
| Refresh and logout | UC-01 | FR-02, FR-32 |
