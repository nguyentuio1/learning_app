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

| Role | Email | Password | How |
|---|---|---|---|
| System administrator | `admin@smartdronedelivery.com` | `Admin@123456` | created by the seed on first start (development only: change it for any shared environment) |
| Customer | any | any | register through the API (below) |
| Dispatcher, Logistics Manager, Administrator | any | any | created by the administrator in the web portal (Users, New user) |

There is **no sign-up screen** in the web portal or in the mobile apps yet, and the administrator's "New user" form only creates staff roles. A customer account is created through the API, for example in Swagger (<http://localhost:5055/swagger>, `POST /api/auth/register`) or with curl:

```bash
curl -X POST http://localhost:5055/api/auth/register -H "Content-Type: application/json" \
  -d '{"email":"customer@example.com","password":"Passw0rd123","fullName":"Demo Customer","phoneNumber":"0900000001"}'
```

Request and response details are in [`docs/api/auth-api.md`](docs/api/auth-api.md). Then sign in with that email and password.

**Stations.** A fresh database has no landing station, and there is no API to create one yet (planned in Sprint 4), so a customer cannot create an order until stations exist. For local testing insert two operational stations:

```bash
docker exec -i drone_postgres psql -U postgres -d drone_delivery_db <<'SQL'
INSERT INTO landing_stations ("Id","StationCode","Name","Latitude","Longitude","Address","TotalSlots","Status") VALUES
  (gen_random_uuid(),'HCM-01','District 1 Hub',10.7769,106.7009,'1 Le Loi, District 1',2,'OPERATIONAL'),
  (gen_random_uuid(),'HCM-02','Thu Duc Hub',10.8506,106.7719,'2 Vo Van Ngan, Thu Duc',2,'OPERATIONAL');
SQL
```

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

# Web
cd frontend/web && npm test && npm run lint && npm run build

# Mobile
cd frontend/mobile && flutter test && flutter analyze
```

`frontend/web/tests/accountFlow.test.tsx` can time out when the whole web suite runs under load; it passes when run alone.

## Documentation

- API contracts: [`docs/api/auth-api.md`](docs/api/auth-api.md), [`docs/api/customer-order-api.md`](docs/api/customer-order-api.md), [`docs/api/station-api.md`](docs/api/station-api.md), [`docs/api/drone-api.md`](docs/api/drone-api.md) (the order, station and drone contracts are still marked proposed)
- Architecture and database: [`docs/architecture/`](docs/architecture/)
- Requirements and business rules: [`docs/requirements/`](docs/requirements/) (start with `SRS.md` and `use-cases.md`)
- Design diagrams: [`docs/design/`](docs/design/)

## Contributing

- Branch from `develop` with a name like `feature/LA-xx-short-description` and open a pull request to `develop`; one pull request per Jira task.
- Before asking for review, merge the latest `develop` into your branch and run the tests of the part you changed (see above).
- Follow the API contract in `docs/api/`; if something is unclear, ask before adding endpoints or fields.
- Mermaid diagrams: do not use `;` inside message text (it breaks rendering).
