# LA-182: Deployment Guide — SmartDroneDelivery

This guide covers environment configuration, Docker Compose full-stack deployment, standalone service startup, health checks, and troubleshooting for **SmartDroneDelivery**.

---

## 1. Technology Stack & Services

| Service | Container Name | Technology | Default Host Port |
| --- | --- | --- | :---: |
| **PostgreSQL** | `drone_postgres` | `postgres:16-alpine` | `5433` (`5432` internal) |
| **MinIO (Object Storage)** | `drone_minio` | `cgr.dev/chainguard/minio:latest` | `9000` (S3 API), `9001` (Console) |
| **Backend API** | `drone_api` | ASP.NET Core 8 (`backend/Dockerfile`) | `5055` (`8080` internal) |
| **Web Portal** | `drone_web` | React 19 + Vite + Nginx (`frontend/web/Dockerfile`) | `8080` (`80` internal) |

---

## 2. Environment Variables (`.env`)

Copy [`.env.example`](../../.env.example) to `.env` at the repository root and fill in every required secret before starting Docker Compose. **Never commit `.env` to Git.**

| Variable | Required | Default | Description |
| --- | :---: | --- | --- |
| `POSTGRES_PASSWORD` | Yes | — | PostgreSQL password for user `postgres` (avoid `;` and spaces). |
| `MINIO_ROOT_USER` | Yes | `drone-minio` | MinIO root access key. |
| `MINIO_ROOT_PASSWORD` | Yes | — | MinIO root secret key (minimum 8 characters). |
| `JWT_KEY` | Yes | — | Signing key for JWT tokens (at least 32 random bytes, e.g., `openssl rand -base64 48`). |
| `ADMIN_EMAIL` | Optional | `admin@smartdronedelivery.com` | Initial Administrator email seeded on first startup. |
| `ADMIN_PASSWORD` | Optional | — | Initial Administrator password (8–100 chars, at least 1 letter & 1 digit). Required on first start if you want the initial admin auto-seeded. |
| `MINIO_PUBLIC_URL` | Optional | `http://localhost:9000` | Public MinIO URL reachable by browsers and mobile clients for avatar/package images. |
| `API_HOST_PORT` | Optional | `5055` | Host port mapped to API container port `8080`. |
| `WEB_HOST_PORT` | Optional | `8080` | Host port mapped to Web Nginx container port `80`. |
| `ASPNETCORE_ENVIRONMENT` | Optional | `Production` | Set to `Development` only in local dev to enable Swagger (`/swagger`) and demo seeding (`Seed:DevelopmentData`). |

---

## 3. Full-Stack Deployment via Docker Compose

### Step 1: Configure `.env`

```bash
cp .env.example .env
# Edit .env and set POSTGRES_PASSWORD, MINIO_ROOT_PASSWORD, JWT_KEY, and ADMIN_PASSWORD
```

### Step 2: Build and Start All Services

```bash
docker compose up -d --build --wait
```

`--wait` waits until both `drone_postgres` (`pg_isready`) and `drone_api` (`/health/ready`) report healthy. On first start, EF Core automatically applies all relational migrations in `backend/SmartDroneDelivery.Api/Migrations/`.

### Step 3: Verify Deployment

Run the automated smoke test script ([scripts/docker-smoke-test.sh](../../scripts/docker-smoke-test.sh)) in a clean/disposable environment (note: it starts its own Compose project and runs `docker compose down -v` during cleanup — do **not** run it against a stack holding persistent data you wish to keep), or probe the running endpoints manually:

```bash
# Non-destructive manual checks against an already-running stack:
curl -sf http://localhost:5055/health/live
curl -sf http://localhost:5055/health/ready
```

Manual checks:
- **Liveness:** `GET http://localhost:5055/health/live` → `200 OK`
- **Readiness (DB + MinIO):** `GET http://localhost:5055/health/ready` → `200 OK`
- **Web Portal:** Open `http://localhost:8080`

---

## 4. Running Backend & Web Standalone (Development)

1. Start only infrastructure containers:
   ```bash
   docker compose up -d postgres minio
   ```
2. Configure `dotnet user-secrets` once (since committed configs hold no secrets per `LA-328`), using the same passwords as `.env`:
   ```bash
   cd backend/SmartDroneDelivery.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=drone_delivery_db;Username=postgres;Password=<POSTGRES_PASSWORD>"
   dotnet user-secrets set "Minio:AccessKey" "<MINIO_ROOT_USER>"
   dotnet user-secrets set "Minio:SecretKey" "<MINIO_ROOT_PASSWORD>"
   dotnet user-secrets set "Jwt:Key" "<at-least-32-random-bytes>"
   dotnet user-secrets set "Seed:AdminPassword" "<your-admin-password>"
   cd ../..
   ```
3. Start Backend API (`Development` profile on port `5055`):
   ```bash
   dotnet run --project backend/SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj --launch-profile http
   ```
   Swagger UI is available at `http://localhost:5055/swagger` when `ASPNETCORE_ENVIRONMENT=Development`.
4. Start Web Portal (`Vite` dev server on port `5173`, proxying `/api` and `/hubs/tracking` to `http://localhost:5055`):
   ```bash
   npm --prefix frontend/web ci
   npm --prefix frontend/web run dev
   ```

---

## 5. AI Feature Flags & Production Gates

AI features (`EtaPrediction:Enabled`, `DeliverySummary:Enabled`, `CustomerAssistant:Enabled`) are **default-off (`false`)** in `appsettings.json`. Explicitly configure either the default local Ollama provider or the Gemini transport with server-side credentials/model settings; the internal BaseUrl remains loopback for both. Stored assistant chat additionally requires the relevant approved retention/privacy/backup controls and, for Gemini, the separate cloud-policy marker. Follow the [current provider configuration and activation guide](../deployment/gemini-ai.md); the [Sprint 7 contract](../api/history/sprint-7-api-contract.md) is a historical proposal.

---

## 6. Common Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| `docker compose up` fails immediately with `set POSTGRES_PASSWORD in .env` | Missing or empty required variable in `.env` | Fill all required keys in `.env` (`POSTGRES_PASSWORD`, `MINIO_ROOT_PASSWORD`, `JWT_KEY`). |
| `GET /health/ready` returns `503 Service Unavailable` | PostgreSQL or MinIO is not reachable or credentials mismatch | Check `docker compose logs postgres minio api`; verify `ConnectionStrings__DefaultConnection` and MinIO credentials. |
| Port `5055` or `8080` already in use | Local `dotnet run` or another service is bound to the port | Stop local `dotnet run` before starting `drone_api`, or override `API_HOST_PORT` / `WEB_HOST_PORT` in `.env`. |
| Avatar/package image URLs fail on mobile/browser | `MINIO_PUBLIC_URL` points to internal Docker hostname `minio:9000` | Set `MINIO_PUBLIC_URL` in `.env` to the host IP/domain reachable by clients. |
