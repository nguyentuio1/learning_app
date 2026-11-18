# LA-249: Installation Guide — SmartDroneDelivery

Step-by-step installation instructions for **Docker Compose** (PostgreSQL, MinIO, Backend API, Web Portal) and **Android Mobile Apps** (`customer` and `operator` flavors).

---

## 1. System Requirements

### 1.1 Software Prerequisites
- **Docker Engine / Docker Desktop** with **Docker Compose v2** (`docker compose version`)
- **.NET 8 SDK** (for local API builds or running tests outside Docker)
- **Node.js 24** & **npm** (for local Web development/builds)
- **Flutter SDK** (Dart `^3.13.5`) + **Android Studio / Android SDK (Java 17)** (for Android builds)

### 1.2 Hardware Recommendations
- **Workstation:** 8 GB RAM minimum (16 GB recommended when running Docker Compose + Android Emulator simultaneously), 15 GB free disk space for Docker images, PostgreSQL/MinIO volumes, and Android build cache.

---

## 2. Backend, Database & Web Portal via Docker Compose

### Step 1: Clone Repository & Configure `.env`

```bash
git clone --branch develop https://github.com/nguyentuio1/learning_app.git
cd learning_app
cp .env.example .env
```

These instructions target the integration branch `develop`; the older release branch `main` does not contain the full current stack. For an existing clone, switch to `develop` and update it before following these steps.

For PowerShell, use `Copy-Item .env.example .env`. Open `.env` and fill in every required value:
- `POSTGRES_PASSWORD`: strong password (no spaces or `;`).
- `MINIO_ROOT_USER`: retain the non-empty example value or choose your own.
- `MINIO_ROOT_PASSWORD`: at least 8 characters.
- `JWT_KEY`: random 32+ byte secret (e.g. `openssl rand -base64 48`).
- `ADMIN_PASSWORD`: initial Administrator password (8–100 chars, letter + digit).

For a disposable demonstration with seeded accounts and stations, set `ASPNETCORE_ENVIRONMENT=Development` in `.env`. Production is the default and creates only the configured initial administrator. Development data is gated by `Seed:DevelopmentData` (enabled in the Development settings). Verify the current accounts and station assignments in the [demo prerequisites](../demo/demo-script.md); do not enable demo data on a production database.

Set `MINIO_PUBLIC_URL` to an address reachable by every browser/emulator/phone that will view photos, for example `http://<YOUR_HOST_LAN_IP>:9000`. The API uses `minio:9000` internally, but that container hostname is not a client photo URL. Changing Flutter's API address alone does not rewrite absolute photo URLs. See [MinIO photo verification](../testing/minio-photo-verification.md).

### Step 2: Build and Start All Containers

```bash
docker compose up -d --build --wait
```

This starts:
- `drone_postgres` (`postgres:16-alpine`) on host port `5433`
- `drone_minio` (`MinIO`) on host ports `9000` (API) and `9001` (Console)
- `drone_api` (ASP.NET Core 8) on host port `5055` (applies EF Core migrations automatically)
- `drone_web` (React + Nginx) on host port `8080`

### Step 3: Verify Running Services

Probe the running endpoints manually:

```bash
curl -sf http://localhost:5055/health/live
curl -sf http://localhost:5055/health/ready
```

Or open in your browser:
- **Web Portal:** `http://localhost:8080`
- **API Health:** `http://localhost:5055/health/ready` (returns `200 OK`)
- **MinIO Console:** `http://localhost:9001`

> **Disposable Smoke Test Note:** [`scripts/docker-smoke-test.sh`](../../scripts/docker-smoke-test.sh) is an isolated end-to-end verification script that brings up containers and tears down volumes (`docker compose down -v`) on exit. Run it only in a clean/disposable environment, not alongside an active stack holding data you want to keep.

---

## 3. Running Web Portal in Local Development Mode

If you want hot-reload (`Vite`) while running `postgres` and `minio` in Docker:

```bash
docker compose up -d postgres minio

# Configure API user-secrets once (matching .env passwords, since no secrets are committed)
cd backend/SmartDroneDelivery.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=drone_delivery_db;Username=postgres;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set "Minio:AccessKey" "<MINIO_ROOT_USER>"
dotnet user-secrets set "Minio:SecretKey" "<MINIO_ROOT_PASSWORD>"
dotnet user-secrets set "Jwt:Key" "<at-least-32-random-bytes>"
dotnet user-secrets set "Seed:AdminPassword" "<your-admin-password>"
cd ../..

dotnet user-secrets set --project backend/SmartDroneDelivery.Api "Minio:PublicBaseUrl" "http://<YOUR_HOST_LAN_IP>:9000"
dotnet run --project backend/SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj --launch-profile http
```

Keep that API terminal running. In a second terminal at the repository root:

```bash
cp frontend/web/.env.example frontend/web/.env
npm --prefix frontend/web ci
npm --prefix frontend/web run dev
```

Access the Vite dev server at `http://localhost:5173`. The frontend `.env` points `VITE_API_BASE_URL` to the standalone API; API user-secrets are separate from Compose's `.env`.

For a simulated delivery lifecycle, explicitly enable `DroneSimulator:Enabled` for the API process (or `DroneSimulator__Enabled=true` in Compose `.env`). See the [demo script](../demo/demo-script.md) for scheduling, required station data and the optional accelerated demo speed. AI generation remains off unless explicitly configured; [Gemini configuration](../deployment/gemini-ai.md) records the separate activation/retention gates.

---

## 4. Building & Installing the Android Applications

SmartDroneDelivery uses one Flutter codebase ([`frontend/mobile`](../../frontend/mobile)) with two Android product flavors: `customer` and `operator`.

### Step 1: Install Flutter Dependencies

```bash
cd frontend/mobile
flutter pub get
```

### Step 2: Run on Android Emulator

The default `API_BASE_URL` for Android emulator is `http://10.0.2.2:5055/api` (reaching host port `5055`).

```bash
# Customer App
flutter run -t lib/main_customer.dart --flavor customer

# Station Operator App
flutter run -t lib/main_operator.dart --flavor operator
```

### Step 3: Build Installable APKs for Physical Android Devices

Compose publishes the API host port for LAN clients. If using the standalone API from Section 3, its HTTP launch profile binds localhost; stop that API process and restart it to accept connections from your test phone:

```bash
dotnet run --project backend/SmartDroneDelivery.Api/SmartDroneDelivery.Api.csproj --launch-profile http --urls http://0.0.0.0:5055
```

When installing on a physical Android phone on the same Wi-Fi/LAN, pass your workstation's reachable LAN IP via `--dart-define=API_BASE_URL`:

```bash
# Build Customer APK
flutter build apk --debug -t lib/main_customer.dart --flavor customer \
  --dart-define=API_BASE_URL=http://<YOUR_HOST_LAN_IP>:5055/api

# Build Station Operator APK
flutter build apk --debug -t lib/main_operator.dart --flavor operator \
  --dart-define=API_BASE_URL=http://<YOUR_HOST_LAN_IP>:5055/api
```

These commands produce debug demonstration packages. Native emulator/device acceptance and production signing remain separate checks. Allow the API and MinIO host ports through the workstation firewall for the test LAN. Install onto a connected device via ADB:

```bash
adb install -r build/app/outputs/flutter-apk/app-customer-debug.apk
adb install -r build/app/outputs/flutter-apk/app-operator-debug.apk
```

> **Signing Note:** In `frontend/mobile/android/app/build.gradle.kts`, the `release` build type currently references the debug signing configuration. Configure a production keystore before building signed release APKs/AABs for store distribution.
