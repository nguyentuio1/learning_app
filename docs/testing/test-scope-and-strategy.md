# LA-245: Test Scope and Strategy — SmartDroneDelivery

## 1. Purpose
This document defines the testing scope, test types, test levels, and supporting tools for the **SmartDroneDelivery** platform (`backend/`, `frontend/web/`, `frontend/mobile/`) in accordance with **LA-202** and the project SRS (`docs/requirements/SRS.md`).

---

## 2. Testing Scope

### 2.1 In-Scope
1. **Authentication, Session & RBAC**
   - Customer registration, login, JWT access/refresh token issuance, rotation, revocation, and logout.
   - Role-based access control across 5 actors: `CUSTOMER`, `ADMIN`, `DISPATCHER`, `STATION_OPERATOR`, and `MANAGER`.
   - Profile retrieval, profile update, avatar upload validation, and password change.
2. **User & Role Management (Admin)**
   - User listing, filtering, role assignment, and account status transitions (`ACTIVE` / `INACTIVE`).
3. **Customer Delivery Prerequisites & Orders**
   - Saved delivery addresses CRUD and default selection.
   - Saved packages CRUD and dimension/weight validation.
   - Delivery order creation, order list/detail queries, order update (`PENDING`-only), and cancellation (`PENDING` or `APPROVED`), including concurrency guards.
4. **Landing Stations, Slots & Drone Fleet Management**
   - Landing station CRUD, status transitions (`OPERATIONAL`, `MAINTENANCE`, `OFFLINE`), and transaction rollback on audit failure.
   - Station slot creation, capacity enforcement, status transitions, and availability monitoring.
   - Drone fleet CRUD, payload/range/battery validation, and maintenance transitions.
5. **Station Operations & Package Pickup**
   - Package intake, rejection, OTP generation, pickup confirmation, wrong-OTP lockout persistence, and pickup timeout job interactions.
6. **Sprint 5 Delivery Workflow & Notifications (In-Flight / Feature Branches)**
   - Delivery request approval/rejection (`LA-31`), order status state machine (`LA-32`), delivery scheduling & drone assignment (`LA-33`), failed delivery handling (`LA-34`), and customer notifications (`LA-326`).

### 2.2 Out-of-Scope & Deferred Baseline Items
- **Out-of-Scope (Project Phase):**
  - Physical drone flight hardware, MAVLink, and real GPS telemetry hardware-in-the-loop testing (simulated via software services where applicable).
  - Production cloud load/stress testing, penetration testing, and third-party payment gateway settlement.
- **Not Yet Tested in Current Baseline (Deferred Until Feature Ready):**
  - `LA-35` real-time delivery tracking (`FR-24`), `LA-36` delivery map (`FR-25`), `LA-37` delivery confirmation (`FR-26`, `FR-27`), `LA-38` dashboard and reports (`FR-28`), and `LA-39` external integration API (`FR-34`) are **not** excluded permanently from project QA; they are currently deferred because their implementation branches have not yet landed on `develop`, and their unit/integration tests (`LA-146`, `LA-149`, `LA-152`, `LA-155`, `LA-157`) will be executed as soon as each feature is ready.

---

## 3. Test Levels and Types

| Level | Scope | Implementation in Repo |
| --- | --- | --- |
| **Unit Testing** | Domain rules, validation helpers, state machine transitions, API client/interceptor logic, Riverpod notifiers | `SmartDroneDelivery.Tests`, `frontend/web/tests/*.test.ts`, `frontend/mobile/test/core/**` & `*_notifier_test.dart` |
| **Integration Testing (InMemory / Mocked HTTP)** | ASP.NET Core `WebApplicationFactory` (`AuthApiFactory`) end-to-end controller → service → EF Core InMemory DB; Web `axios-mock-adapter` + React Router | `AuthApiTests`, `DeliveryOrder*Tests`, `Rbac*Tests`, `accountFlow.test.tsx`, `order*.test.tsx` |
| **Relational Concurrency & Transaction Testing** | Real PostgreSQL 16 concurrency races, row-level locking, unique constraints, and transaction rollback | `*ConcurrencyTests.cs` and `*PostgresTests.cs` using `PostgresApiFactory` (`[PostgresFact]`) |
| **Widget & UI Component Testing** | React Testing Library UI rendering, form validation, Ant Design modals/tables, Flutter `testWidgets` | `frontend/web/tests/*Ui.test.tsx`, `frontend/mobile/test/presentation/**` |
| **Smoke & Regression Testing** | App boot, DI wiring, route guards, and CI pull-request gates | `app_smoke_test.dart`, `di_wiring_test.dart`, `.github/workflows/ci.yml` |

---

## 4. Supporting Tools & Frameworks

| Layer | Tools / Frameworks |
| --- | --- |
| **Backend (.NET 8)** | `xUnit`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`, `Npgsql.EntityFrameworkCore.PostgreSQL` (PostgreSQL 16-alpine in CI) |
| **Frontend Web (React + TypeScript)** | `Vitest`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `jsdom`, `axios-mock-adapter`, `ESLint`, `tsc` |
| **Frontend Mobile (Flutter / Dart)** | `flutter_test`, `flutter_riverpod` (`ProviderScope` overrides), `flutter analyze` |
| **CI/CD Automation** | GitHub Actions (`.github/workflows/ci.yml`) running `Backend tests`, `Web lint, test and build`, and `Mobile analyze and test` on every PR to `develop` or `main` |
