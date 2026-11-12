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
docker-compose.yml             PostgreSQL 16 and MinIO for local development
```

## Prerequisites

- Docker Desktop (PostgreSQL and MinIO)
- .NET SDK 8
- A current Node.js LTS and npm (the project was built with Node 24)
- Flutter with a Dart SDK matching `frontend/mobile/pubspec.yaml` (only for the mobile apps)

## Run it locally

### 1. Infrastructure

```bash
docker compose up -d
```

| Service | Host port | Credentials |
|---|---|---|
| PostgreSQL 16, database `drone_delivery_db` | 5433 | `postgres` / `Password123!` |
| MinIO (S3 API / console) | 9000 / 9001 | `minioadmin` / `minioadminpassword` |

These are development-only values (also in `backend/SmartDroneDelivery.Api/appsettings.json`).

### 2. Backend

```bash
cd backend/SmartDroneDelivery.Api
dotnet run --launch-profile http
```

The API listens on <http://localhost:5055> and Swagger is at <http://localhost:5055/swagger>. On start it applies the EF Core migrations and seeds the five roles and one administrator account.

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

When the backend runs in the **Development** environment (`dotnet run` does this by default), it creates the accounts and stations below on start. It is safe to start many times: anything that already exists is left as it is, so your own changes are kept. Nothing is created in any other environment, and `Seed:DevelopmentData` in `appsettings.Development.json` switches it off.

| Role | Email | Password |
|---|---|---|
| System administrator | `admin@smartdronedelivery.com` | `Admin@123456` (also created outside Development: change it for any shared environment) |
| Customer | `customer@smartdronedelivery.com` | `Customer@123456` |
| Dispatcher | `dispatcher@smartdronedelivery.com` | `Dispatcher@123456` |
| Station operator | `operator@smartdronedelivery.com` | `Operator@123456` (assigned to `HCM-01`) |

| Station | Name | Status | Slots |
|---|---|---|---|
| `HCM-01` | District 1 Hub | `OPERATIONAL` | 4 |
| `HCM-02` | Thu Duc Hub | `OPERATIONAL` | 4 |
| `HCM-03` | Binh Thanh Hub | `MAINTENANCE` | 2 |

So a customer can sign in and create an order right after `docker compose up` and `dotnet run`, with no SQL. `HCM-03` is there to check that a station in maintenance is refused.

Visitors can create another **Customer** account from **Register** on the web portal login page or **Create a customer account** in the Customer mobile app. Registration validates the email, password, name and phone number; a successful registration signs the customer in automatically. The public API can also be used from Swagger (<http://localhost:5055/swagger>, `POST /api/auth/register`) or with curl:

```bash
curl -X POST http://localhost:5055/api/auth/register -H "Content-Type: application/json"   -d '{"email":"customer2@example.com","password":"Passw0rd123","fullName":"Second Customer","phoneNumber":"0900000010"}'
```

Request and response details are in [`docs/api/auth-api.md`](docs/api/auth-api.md). Other staff accounts (Dispatcher, Logistics Manager, Administrator) are created by the administrator in the web portal (Users, New user).

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
TEST_POSTGRES_CONNECTION="Host=localhost;Port=5433;Username=postgres;Password=Password123!" dotnet test

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

- API contracts: [`docs/api/auth-api.md`](docs/api/auth-api.md), [`docs/api/customer-order-api.md`](docs/api/customer-order-api.md), [`docs/api/station-api.md`](docs/api/station-api.md), [`docs/api/drone-api.md`](docs/api/drone-api.md) (the order, station and drone contracts are still marked proposed)
- Architecture and database: [`docs/architecture/`](docs/architecture/)
- Requirements and business rules: [`docs/requirements/`](docs/requirements/) (start with `SRS.md` and `use-cases.md`)
- Design diagrams: [`docs/design/`](docs/design/)
- Real-time tracking sequence (LA-238): [telemetry intake, SignalR and web/mobile updates](docs/design/sequence-real-time-tracking.md)

## Contributing

- Branch from `develop` with a name like `feature/LA-xx-short-description` and open a pull request to `develop`; one pull request per Jira task.
- Before asking for review, merge the latest `develop` into your branch and run the tests of the part you changed (see above).
- Follow the API contract in `docs/api/`; if something is unclear, ask before adding endpoints or fields.
- Mermaid diagrams: do not use `;` inside message text (it breaks rendering).
