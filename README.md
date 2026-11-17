# SmartDroneDelivery

A business-oriented platform that manages drone delivery operations: it covers the delivery lifecycle from order creation to delivery confirmation. It does **not** control drone flight hardware.

The system has an ASP.NET Core 8 Web API with PostgreSQL, a React web portal, Flutter mobile apps (Customer and Station Operator), MinIO for uploaded files and Docker Compose for local infrastructure.

> Student project (Jira project `LA`, 8 sprints). Project documents are in [`docs/`](docs/) and on Confluence (space of the project owner).

## Project status

| Area | State (end of Sprint 3) |
|---|---|
| Authentication, profile, user and role management | Implemented (backend, web admin pages) |
| Customer addresses and packages | Implemented (backend, web, mobile) |
| Customer orders: create, list, details, update, cancel | Implemented (backend, web, mobile) |
| Landing stations | Only a read-only list (`GET /api/landing-stations`); station management is planned (Sprint 4) |
| Dispatcher approval, scheduling, drone assignment | Planned (Sprint 5); design documents exist |
| Station pickup, secure code confirmation, live tracking, dashboard, AI services | Planned (Sprints 6 and 7); design documents exist |

An order stays in `PENDING` after creation. Today the customer can only view, edit (while `PENDING`) or cancel it (`PENDING` or `APPROVED`). The dispatcher screens that approve and schedule it do not exist yet.

## Repository layout

```
backend/
  SmartDroneDelivery.Api/      ASP.NET Core 8 Web API (controllers, services, EF Core, migrations)
  SmartDroneDelivery.Tests/    xUnit tests (EF Core InMemory)
frontend/
  web/                         React 19 + TypeScript + Vite + Ant Design
  mobile/                      Flutter + Riverpod (Customer app and Station Operator app)
docs/
  api/                         API contracts (auth-api.md, customer-order-api.md, station-api.md, drone-api.md)
  architecture/                architecture, ERD (erd.md, erd.dbml)
  design/                      sequence, class and activity diagrams, user flows, wireframes
  requirements/                SRS, use cases, business rules, functions, messages
docker-compose.yml             Full system (PostgreSQL 16, MinIO, API, web portal); see "Run everything with Docker"
```

## Prerequisites

- Docker Desktop (PostgreSQL and MinIO)
- .NET SDK 8
- A current Node.js LTS and npm (the project was built with Node 24)
- Flutter with a Dart SDK matching `frontend/mobile/pubspec.yaml` (only for the mobile apps)

## Run everything with Docker

One command builds and starts PostgreSQL, MinIO, the API and the web portal (LA-47):

```bash
cp .env.example .env        # fill in every empty value; .env is ignored by git
docker compose up -d --build --wait     # returns once the API reports healthy
```

| Service | Address |
|---|---|
| Web portal | <http://localhost:8080> (forwards `/api` and `/hubs` to the API) |
| API (mobile apps, scripts) | <http://localhost:5055> |
| MinIO console | <http://localhost:9001> |

The API runs in the `Production` environment: no Swagger and no demo accounts; the administrator is created from `ADMIN_EMAIL` / `ADMIN_PASSWORD` in `.env`. Set `ASPNETCORE_ENVIRONMENT=Development` in `.env` for Swagger and the demo accounts below. `docker compose down` stops everything and keeps the data; `docker compose down -v` also deletes it. Host ports can be changed in `.env` (`API_HOST_PORT`, `WEB_HOST_PORT`, ...). [`scripts/docker-smoke-test.sh`](scripts/docker-smoke-test.sh) starts the stack from scratch and checks the portal, login, a database read and an avatar upload to MinIO; the `Docker package` workflow runs it on pull requests that touch the package.

### Health and monitoring

| Endpoint | Anonymous | Checks | Status |
|---|---|---|---|
| `GET /health/live` | yes | the API process answers | `200` |
| `GET /health/ready` | yes | PostgreSQL connection and MinIO (`/minio/health/live`) | `200` when all are healthy, otherwise `503` |

Both return JSON such as `{"status":"Healthy","totalDurationMs":4,"checks":[{"name":"database","status":"Healthy","durationMs":3},{"name":"storage","status":"Healthy","durationMs":1}]}` with no host names, connection strings or error text; the reason for a failure is written to the API log. In Docker the `api` service uses `/health/ready` as its health check and `web` starts only after it is healthy: `docker compose ps` shows the state and `docker compose logs api` the log (LA-178).

## Run it locally

### 1. Infrastructure

```bash
cp .env.example .env        # fill in every empty value; .env is ignored by git
docker compose up -d postgres minio
```

| Service | Host port | Credentials |
|---|---|---|
| PostgreSQL 16, database `drone_delivery_db` | 5433 | `postgres` / `POSTGRES_PASSWORD` from `.env` |
| MinIO (S3 API / console) | 9000 / 9001 | `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` from `.env` |

`docker compose` stops with "required variable ... is missing a value" when `.env` is missing a password.

### 2. Backend

The repository holds no secret (LA-328), so give the backend its settings once with `dotnet user-secrets` (stored in your user profile, outside the repository). Use the same passwords as in `.env`:

```bash
cd backend/SmartDroneDelivery.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=drone_delivery_db;Username=postgres;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set "Minio:AccessKey" "<MINIO_ROOT_USER>"
dotnet user-secrets set "Minio:SecretKey" "<MINIO_ROOT_PASSWORD>"
dotnet user-secrets set "Jwt:Key" "<at least 32 random bytes, for example: openssl rand -base64 48>"
dotnet user-secrets set "Seed:AdminPassword" "<your administrator password>"
dotnet run --launch-profile http
```

The API listens on <http://localhost:5055> and Swagger is at <http://localhost:5055/swagger>. On start it applies the EF Core migrations and seeds the five roles and the administrator account (see [Configuration and secrets](#configuration-and-secrets)).

### 3. Web portal

```bash
cd frontend/web
npm install
npm run dev
```

Open <http://localhost:5173>. In development Vite proxies `/api` to the backend (`VITE_API_BASE_URL`, default `http://localhost:5055`; see `.env.example`).

### 4. Mobile apps (optional)

```bash
cd frontend/mobile
flutter pub get
flutter run -t lib/main_customer.dart --flavor customer     # Customer app
flutter run -t lib/main_operator.dart --flavor operator     # Station Operator app
```

The API address defaults to `http://10.0.2.2:5055/api` on the Android emulator and `http://localhost:5055/api` elsewhere. Override it with `--dart-define=API_BASE_URL=http://<host>:5055/api` (for example for a real device).

## Accounts and test data

When the backend runs in the **Development** environment (`dotnet run` does this by default) with `Seed:DevelopmentData` enabled, it creates the demo accounts, stations, addresses, drones and orders below on start. It is safe to start many times: anything that already exists is left as it is, so your own changes are kept. This demo data is not created in any other environment; set `Seed:DevelopmentData` to `false` in `appsettings.Development.json` to switch it off.

| Role | Email | Password |
|---|---|---|
| System administrator | `admin@smartdronedelivery.com` (`Seed:AdminEmail`) | the value of `Seed:AdminPassword`; created in every environment, but only when that setting is present |
| Customer | `customer@smartdronedelivery.com` | `Customer@123456` |
| Dispatcher | `dispatcher@smartdronedelivery.com` | `Dispatcher@123456` |
| Station operator (HCM-01) | `operator@smartdronedelivery.com` | `Operator@123456` |
| Station operator (HCM-02) | `operator2@smartdronedelivery.com` | `Operator@123456` |
| Logistics manager (`MANAGER`) | `manager@smartdronedelivery.com` | `Manager@123456` |

| Station | Name | Status | Slots |
|---|---|---|---|
| `HCM-01` | District 1 Hub | `OPERATIONAL` | 4 |
| `HCM-02` | Thu Duc Hub | `OPERATIONAL` | 4 |
| `HCM-03` | Binh Thanh Hub | `MAINTENANCE` | 2 |

Demo data also includes two customer addresses (`Home - District 1` and `Office - Thu Duc`), four drones, and three sample deliveries:

| Type | Demo records |
|---|---|
| Drones | `DRONE-HCM-001` (`IDLE`, HCM-01, 3000 g), `DRONE-HCM-002` (`IDLE`, HCM-02, 2500 g), `DRONE-HCM-003` (`CHARGING`, HCM-01, 3000 g), `DRONE-HCM-004` (`MAINTENANCE`, HCM-03, 2700 g) |
| Orders | `ORD-DEMO-001` (`PENDING`), `ORD-DEMO-002` (`APPROVED`), `ORD-DEMO-003` (`DELIVERED`, with a completed flight mission) |

So a customer can sign in and create an order right after docker compose up -d postgres minio and dotnet run (or the full Docker stack with ASPNETCORE_ENVIRONMENT=Development), with no SQL. HCM-03 is there to check that a station in maintenance is refused.

`ORD-DEMO-003` uses timestamps relative to `DateTime.UtcNow` at its first seed and they are not rewritten on restart. If you run the demo on another date and want the dashboard's "today" filter to include this order, create a fresh database volume before the demo.

Visitors can create another **Customer** account from **Register** on the web portal login page or **Create a customer account** in the Customer mobile app. Registration validates the email, password, name and phone number; a successful registration signs the customer in automatically. Outside the Development demo seed, staff accounts are created by the administrator in the web portal (Users, New user). The public API can also be used from Swagger (<http://localhost:5055/swagger>, `POST /api/auth/register`) or with curl:

```bash
curl -X POST http://localhost:5055/api/auth/register -H "Content-Type: application/json"   -d '{"email":"customer2@example.com","password":"Passw0rd123","fullName":"Second Customer","phoneNumber":"0900000010"}'
```

Request and response details are in [`docs/api/auth-api.md`](docs/api/auth-api.md).

## Configuration and secrets

No working secret is committed (LA-328). `appsettings.json` keeps the secret settings empty, and the API stops on start with a message naming each missing setting:

| Setting | Environment variable | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt:Key` | `Jwt__Key` | at least 32 bytes; changing it signs every user out |
| `Minio:AccessKey`, `Minio:SecretKey` | `Minio__AccessKey`, `Minio__SecretKey` | MinIO user and password |
| `Seed:AdminPassword` (optional) | `Seed__AdminPassword` | creates the first administrator (`Seed:AdminEmail`) if it does not exist yet; 8 to 100 characters with a letter and a digit. Without it no administrator is created and a warning is logged. Change the password after the first sign-in (Profile, Change password). |

In Development, `dotnet user-secrets` is the simplest place for them (see "Run it locally"); in any other environment use environment variables or the deployment's secret store. Outside Development the API also refuses the values that were committed before LA-328 (the old JWT key, PostgreSQL, MinIO and administrator passwords), because they remain readable in the git history: rotate them on every environment that used them.

A PostgreSQL volume keeps the password it was created with; `POSTGRES_PASSWORD` only applies to a new volume. To rotate a local database either run `ALTER USER postgres PASSWORD '...'` in the container or recreate the volume (`docker compose down -v`, which deletes the local data).

## Using the application today

Sign in as a **customer** (web or mobile):

1. **Addresses** and **Packages**: add a delivery address (optional) and a package (weight up to 3000 g, size up to 30 x 20 x 15 cm).
2. **Create order**: choose the package, an origin and a different destination station (only `OPERATIONAL` stations are listed), and the recipient name and phone. The order is created as `PENDING`. The 6-digit pickup code is shown **once**, on this response: note it down. It is never returned again.
3. **My orders**: list with status filter, search by tracking number, date range and paging; open an order to see its details and status history.
4. **Update**: change recipient, destination station or address while the order is `PENDING`.
5. **Cancel**: with a reason (up to 500 characters) while the order is `PENDING` or `APPROVED`.

Sign in as the **administrator** (web) to manage users and roles. Approval by a dispatcher, scheduling, drone assignment, station pickup and tracking are not implemented yet (see the status table).

## Tests

```bash
# Backend (xUnit, EF Core InMemory; no database needed)
cd backend && dotnet test

# Backend, including the concurrency tests that need a real PostgreSQL (LA-322).
# Start `docker compose up -d postgres` first. Each run creates its own database on the server and drops it again.
TEST_POSTGRES_CONNECTION="Host=localhost;Port=5433;Username=postgres;Password=$POSTGRES_PASSWORD" dotnet test

# Web
cd frontend/web && npm test && npm run lint && npm run build

# Mobile
cd frontend/mobile && flutter test && flutter analyze
```

### Continuous integration

Every pull request into `develop` or `main` runs [`.github/workflows/ci.yml`](.github/workflows/ci.yml) on GitHub Actions. It shows three checks, one for each command block above:

| Check | Runs |
|---|---|
| `Backend tests` | `dotnet test` |
| `Web lint, test and build` | `npm ci`, `npm run lint`, `npm test`, `npm run build` |
| `Mobile analyze and test` | `flutter pub get`, `flutter analyze --no-fatal-infos`, `flutter test` |

A new push to the pull request cancels the run that is still going. A failing check only blocks the merge once the three names are required in the branch ruleset of `develop` (Settings, Rules); until then, look at the checks before you merge.

## Documentation

- Sprint 6 API handoff (LA-341): [`docs/api/sprint-6-api-contract.md`](docs/api/sprint-6-api-contract.md) and [OpenAPI schemas](docs/api/sprint-6-openapi.json). Includes branch availability, tracking/SignalR, confirmation, and explicitly proposed analytics/external contracts for mocks.
- Sprint 7 AI handoff (LA-342): [`docs/api/sprint-7-api-contract.md`](docs/api/sprint-7-api-contract.md) and [OpenAPI schemas](docs/api/sprint-7-openapi.json). Proposed ETA, delivery summary and customer assistant interfaces; implementation, consumer approval and production policy gates are tracked separately.
- API contracts: [`docs/api/auth-api.md`](docs/api/auth-api.md), [`docs/api/customer-order-api.md`](docs/api/customer-order-api.md), [`docs/api/station-api.md`](docs/api/station-api.md), [`docs/api/drone-api.md`](docs/api/drone-api.md) (the order, station and drone contracts are still marked proposed)
- Architecture and database: [`docs/architecture/`](docs/architecture/)
- Requirements and business rules: [`docs/requirements/`](docs/requirements/) (start with `SRS.md` and `use-cases.md`)
- Design diagrams: [`docs/design/`](docs/design/)
- AI service design (LA-201 / LA-241–244): [ETA](docs/design/sequence-ai-eta-estimation.md), [delivery summary](docs/design/sequence-ai-delivery-summarization.md), [customer assistant](docs/design/sequence-ai-customer-assistant.md), [class diagram](docs/design/class-diagram-ai-services.md) and [specification](docs/design/class-specification-ai-services.md). Proposed Sprint 7 implementation; existing entities are distinguished from future services.
- Real-time tracking sequence (LA-238): [telemetry intake, SignalR and web/mobile updates](docs/design/sequence-real-time-tracking.md)
- Dashboard sequence (LA-240): [filtered statistics queries and web results](docs/design/sequence-dashboard.md)

## Contributing

- Branch from `develop` with a name like `feature/LA-xx-short-description` and open a pull request to `develop`; one pull request per Jira task.
- Before asking for review, merge the latest `develop` into your branch and run the tests of the part you changed (see above).
- Follow the API contract in `docs/api/`; if something is unclear, ask before adding endpoints or fields.
- Mermaid diagrams: do not use `;` inside message text (it breaks rendering).
