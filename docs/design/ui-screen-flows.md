# 3.1 UI/UX & Implemented Screen Flows (SmartDroneDelivery)

**Parent story:** [LA-351](https://hungndemail.atlassian.net/browse/LA-351) · **Task:** [LA-361](https://hungndemail.atlassian.net/browse/LA-361)

**Source baseline:** [`ef7ddec`](https://github.com/nguyentuio1/learning_app/commit/ef7ddecf4875b6504ce2352468a2f3e8fda6a90c) on `develop`

**Confluence target:** [3.1 UI/UX & Screen Flows, page 11894809](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11894809) · v4 at the technical review · v5 planned, publication and readback pending.

## 1. Design references and implementation

The [web wireframes](wireframes/web/index.md) and [Flutter wireframes](wireframes/app/index.md) describe the design intent. The route maps below describe the repository baseline. Web uses React 19.3, TypeScript, Ant Design 6 and SignalR. The Flutter customer and operator apps use GoRouter and Riverpod. The ASP.NET Core 8 API uses PostgreSQL and MinIO. Health probes are `GET /health/live` and `GET /health/ready`.

Role codes are `CUSTOMER`, `DISPATCHER`, `STATION_OPERATOR`, `MANAGER` and `ADMIN`. Routes define web navigation access. API role, ownership and station checks also apply to the requested records. [Web router](../../frontend/web/src/routes/AppRouter.tsx) · [Flutter route constants](../../frontend/mobile/lib/routes/route_paths.dart) · [Health endpoints](../../backend/SmartDroneDelivery.Api/Health/HealthEndpoints.cs)

## 2. Role journeys at the source baseline

### 2.1 Customer

Web: Sign in → My Orders → Create delivery → Acknowledge pickup code → Order details and available insights.

**Web.** Sign-in opens My Orders (`/orders`). Use `/addresses` and `/packages` to manage saved records. Create an order at `/orders/new`, select a registered package and distinct origin and destination stations, and enter recipient details. Saved delivery address is optional (`deliveryAddressId` defaults to `null`). The package API requires positive values and limits `weightGram` to 3,000 g, `lengthCm` to 30 cm, `widthCm` to 20 cm and `heightCm` to 15 cm. The create flow can return from adding a package with the order draft restored. After submission, acknowledge the one-time pickup code and open the order at `/orders/:id`.

Order details can link to the order map (`/orders/:id/map`), ETA (`/delivery-eta/:id`) and summary (`/delivery-summary/:id`). The map displays available tracking data. `/notifications` and `/delivery-assistant` are separate customer routes. Assistant sending depends on backend activation and retention policy.

**Flutter.** The baseline home at `/customer` has the title “Find a station” and links to saved addresses, packages, orders and notifications. From `/customer/orders`, use `/customer/orders/create`, then the review and submitted routes under `/customer/orders/create`. The customer detail route is `/customer/orders/:orderId`; tracking is `/customer/orders/tracking/:orderId`. At `/customer/orders/:orderId/confirm-delivery`, recipient confirmation uses the pickup code. [Package validation](../../backend/SmartDroneDelivery.Api/Services/PackageService.cs) · [Customer home](../../frontend/mobile/lib/presentation/customer/home/customer_home_page.dart)

### 2.2 Dispatcher web

Workspace → Approvals → Scheduling after approval → Tracking and map after assignment.

The workspace at `/` links to `/dispatcher/approvals`, `/dispatcher/scheduling`, `/delivery-tracking` and `/dispatcher/failures`. A pending request can be approved or rejected with a reason. The approved order opens scheduling at `/dispatcher/scheduling/:id`. Scheduling checks departure timing and eligible drones before assignment; the backend enforces route, payload, battery and slot constraints. Assigned deliveries can open tracking at `/delivery-tracking/:id` and the contextual map at `/delivery-map/:id`. Use `/drones` for fleet management. The failure screen supports the actions available for the selected incident: `RESCHEDULE`, `RETURN_TO_ORIGIN` and `CANCEL`. [Approval page](../../frontend/web/src/features/approvals/ApprovalQueuePage.tsx) · [Scheduling service](../../backend/SmartDroneDelivery.Api/Services/SchedulingService.cs) · [Failure service](../../backend/SmartDroneDelivery.Api/Services/FailureService.cs)

### 2.3 Station Operator

Flutter: Operator home → Assigned station → Intake, pickup or expired-parcel workflow.

**Web.** `/stations` lists stations, `/station-slots` displays the slot grid, and `/station-availability` displays station capacity. Slot states include `EMPTY`, `OCCUPIED`, `RESERVED` and `ERROR`. Manual status changes are limited to `EMPTY` and `ERROR` and are subject to station access and slot state.

**Flutter.** The baseline home at `/operator` shows station context and gates station actions on assignment. The app includes origin intake (`/operator/intake`), pickup (`/operator/pickup`), expired parcels (`/operator/expired-parcels/:stationId`) and failed-order work (`/operator/failed-orders`). Intake and pickup remain subject to the order, mission and assigned-station checks. [Operator home](../../frontend/mobile/lib/presentation/operator/home/operator_home_page.dart) · [Slot routes](../../frontend/web/src/features/slots/operationFeature.ts)

### 2.4 Manager web and staff lookup

Dashboard → Reporting period → Delivery report → Selected order's ETA or summary.

`/manager/analytics` is the `MANAGER` dashboard. It starts with the last 30 days and offers Today, Last 7 days, Last 30 days and custom dates. Dates are inclusive creation dates in `Asia/Ho_Chi_Minh` (UTC+07) with a maximum of 366 days. `/manager/analytics?tab=reports` shows the delivery report and links each order to ETA and summary.

`MANAGER` and `DISPATCHER` use `GET /api/delivery-insights/orders` from the ETA and summary pickers. Search accepts tracking numbers and supports status, creation dates and pagination for current and historical deliveries. Use `/delivery-eta/:id` and `/delivery-summary/:id` for the selected order. The lookup returns a safe order projection. Customer access to those insight pages is limited to owned orders and uses the customer flow. Available context and estimate data can still be reviewed when AI generation is disabled. [Staff lookup contract](../api/delivery-insight-lookup.md) · [Analytics page](../../frontend/web/src/features/analytics/AnalyticsPage.tsx)

### 2.5 Administrator web

Sign in → Admin workspace → Users, roles or audit logs.

`ADMIN` uses `/admin/users` to manage accounts, `/admin/roles` to view roles and `/admin/audit-logs` to inspect recorded actions. Actual user-management action codes include `CREATE_USER`, `USER_UPDATE`, `ROLE_CHANGE` and `USER_DEACTIVATE`. `/profile` is the signed-in account's own profile. [User management controller](../../backend/SmartDroneDelivery.Api/Controllers/UserManagementController.cs) · [Audit log screen](../../frontend/web/src/pages/AuditLogsPage.tsx)

### 2.6 Flutter candidates under LA-344

| Change | Candidate and evidence | Remaining acceptance |
| --- | --- | --- |
| Customer home CTA and active orders, draft-safe package return, post-submit continuation | [PR #277](https://github.com/nguyentuio1/learning_app/pull/277), head `6b9f9e80af21c1561e955e34d4e8b42329fa904c` at the LA-361 review. PR reports Dart analysis and 20 Flutter tests. | Integration and emulator walkthrough pending. |
| Operator assigned-station badge, action queue and post-action refresh | [PR #278](https://github.com/nguyentuio1/learning_app/pull/278), head `3186da0177701abad215905cfbfdd29b55485b78` at the LA-361 review. PR reports Dart analysis and 24 Flutter tests. | Integration and emulator acceptance pending. |

## 3. Web screenshot catalog

The candidate files below are linked for visual review. See Section 4 for the provenance of prior runtime checks.

### 3.1 Authentication & Public Access
| Screen / Flow | Design wireframe reference | Source at baseline | Candidate PNG references | Screen purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Sign In (`/login`)** | `web/dispatcher/01-login.png` | `frontend/web/src/pages/LoginPage.tsx` | Desktop: [auth-login-desktop-1440px.png](screenshots/auth-login-desktop-1440px.png)<br/>Narrow PNG: [auth-login-narrow-390px.png](screenshots/auth-login-narrow-390px.png) | Web sign-in entry. |
| **Register (`/register`)** | `app/customer/C02-Register.png` | `frontend/web/src/pages/RegisterPage.tsx` | Desktop: [auth-register-desktop-1440px.png](screenshots/auth-register-desktop-1440px.png)<br/>Narrow PNG: [auth-register-narrow-390px.png](screenshots/auth-register-narrow-390px.png) | Customer account registration. |

### 3.2 Customer Role Journeys
| Screen / Flow | Design wireframe reference | Source at baseline | Candidate PNG references | Screen purpose |
| :--- | :--- | :--- | :--- | :--- |
| **My Orders (`/orders`)** | Web Wireframes (1280px table only) | `frontend/web/src/pages/CustomerOrdersPage.tsx` (PR #271 / LA-349) | Desktop: [customer-orders-desktop-1440px.png](screenshots/customer-orders-desktop-1440px.png)<br/>Narrow PNG: [customer-orders-narrow-390px.png](screenshots/customer-orders-narrow-390px.png) | Customer order list with filtering, pagination and a narrow-layout card view. |
| **Order Details (`/orders/:id`)** | Wireframe details modal | `frontend/web/src/pages/CustomerOrderDetailPage.tsx` | Desktop: [customer-order-detail-desktop-1440px.png](screenshots/customer-order-detail-desktop-1440px.png)<br/>Narrow PNG: [customer-order-detail-narrow-390px.png](screenshots/customer-order-detail-narrow-390px.png) | Order status and available actions for the selected order. |
| **Create Delivery (`/orders/new`)** | `C14-CreateDelivery.png`, `C15-CreateDelivery-Errors.png` | `frontend/web/src/pages/CreateDeliveryOrderPage.tsx` | Desktop: [customer-create-order-desktop-1440px.png](screenshots/customer-create-order-desktop-1440px.png)<br/>Narrow PNG: [customer-create-order-narrow-390px.png](screenshots/customer-create-order-narrow-390px.png) | Select a package and stations, enter recipient information and optionally attach a saved address. |
| **Customer Tracking Map (`/orders/:id/map`)** | `C07-TrackLive.png` | `frontend/web/src/features/deliveryMap/DeliveryMapPage.tsx` (customer map export, PR #264 / LA-345) | Desktop: [customer-tracking-map-desktop-1440px.png](screenshots/customer-tracking-map-desktop-1440px.png)<br/>Narrow PNG: [customer-tracking-map-narrow-390px.png](screenshots/customer-tracking-map-narrow-390px.png)<br/> Order unavailable example: [desktop-1440px](screenshots/customer-tracking-map-order-unavailable-desktop-1440px.png) · [narrow-390px](screenshots/customer-tracking-map-order-unavailable-narrow-390px.png) | Map for the selected owned order. |
| **Saved Packages (`/packages`)** | `C05-Packages.png` | `frontend/web/src/pages/CustomerPackagesPage.tsx` | Desktop: [customer-packages-desktop-1440px.png](screenshots/customer-packages-desktop-1440px.png)<br/>Narrow PNG: [customer-packages-narrow-390px.png](screenshots/customer-packages-narrow-390px.png) | Manage registered packages and optional package photos. |
| **Address Book (`/addresses`)** | `C06-Addresses.png` | `frontend/web/src/pages/CustomerAddressesPage.tsx` | Desktop: [customer-addresses-desktop-1440px.png](screenshots/customer-addresses-desktop-1440px.png)<br/>Narrow PNG: [customer-addresses-narrow-390px.png](screenshots/customer-addresses-narrow-390px.png) | Manage delivery addresses and identify the default address. |
| **AI Delivery Assistant (`/delivery-assistant`)** | Conceptual chat bot wireframe | `frontend/web/src/features/customerAssistant/CustomerAssistantPage.tsx` | Desktop: [customer-assistant-desktop-1440px.png](screenshots/customer-assistant-desktop-1440px.png)<br/>Narrow PNG: [customer-assistant-narrow-390px.png](screenshots/customer-assistant-narrow-390px.png) | View conversations and send when the backend enables assistant messaging. |
| **Customer Notifications (`/notifications`)** | Conceptual alerts list | `frontend/web/src/pages/CustomerNotificationsPage.tsx` | Desktop: [customer-notifications-desktop-1440px.png](screenshots/customer-notifications-desktop-1440px.png)<br/>Narrow PNG: [customer-notifications-narrow-390px.png](screenshots/customer-notifications-narrow-390px.png) | Review customer notifications. |

### 3.3 Dispatcher Role Journeys
| Screen / Flow | Design wireframe reference | Source at baseline | Candidate PNG references | Screen purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Dispatcher Workspace (`/`)** | `02-dashboard.png` | `frontend/web/src/pages/HomePage.tsx` (PR #262 / LA-343) | Desktop: [dispatcher-workspace-desktop-1440px.png](screenshots/dispatcher-workspace-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-workspace-narrow-390px.png](screenshots/dispatcher-workspace-narrow-390px.png) | Open the dispatch queues and active-delivery actions. |
| **Pending Approvals Queue (`/dispatcher/approvals`)** | `03-pending-orders.png`, `04-order-detail-approval.png` | `frontend/web/src/features/approvals/ApprovalQueuePage.tsx` | Desktop: [dispatcher-approvals-desktop-1440px.png](screenshots/dispatcher-approvals-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-approvals-narrow-390px.png](screenshots/dispatcher-approvals-narrow-390px.png) | Review a pending order and record an approval or rejection. |
| **Flight Scheduling & Drones (`/dispatcher/scheduling`)** | `05-scheduling.png`, `06-drone-assignment.png` | `frontend/web/src/features/scheduling/SchedulingPage.tsx` | Desktop: [dispatcher-scheduling-desktop-1440px.png](screenshots/dispatcher-scheduling-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-scheduling-narrow-390px.png](screenshots/dispatcher-scheduling-narrow-390px.png) | Set the departure and select an eligible drone for an approved order. |
| **Failed Deliveries & Incidents (`/dispatcher/failures`)** | `07-incident-management.png` | `frontend/web/src/features/failures/FailureHandlingPage.tsx` | Desktop: [dispatcher-failures-desktop-1440px.png](screenshots/dispatcher-failures-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-failures-narrow-390px.png](screenshots/dispatcher-failures-narrow-390px.png) | Review a failed delivery and the applicable resolution actions. |
| **Fleet Delivery Tracking (`/delivery-tracking`)** | `08-live-tracking.png` | `frontend/web/src/features/tracking/DeliveryTrackingPage.tsx` | Desktop: [dispatcher-delivery-tracking-desktop-1440px.png](screenshots/dispatcher-delivery-tracking-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-delivery-tracking-narrow-390px.png](screenshots/dispatcher-delivery-tracking-narrow-390px.png) | View active deliveries and the freshness of available telemetry. |
| **Dispatcher Fleet Radar Map (`/delivery-map`)** | `08-live-tracking.png` | `frontend/web/src/features/deliveryMap/DeliveryMapPage.tsx` (PR #264 / LA-345) | Desktop: [dispatcher-delivery-map-desktop-1440px.png](screenshots/dispatcher-delivery-map-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-delivery-map-narrow-390px.png](screenshots/dispatcher-delivery-map-narrow-390px.png)<br/> No delivery selected example: [desktop-1440px](screenshots/dispatcher-delivery-map-unselected-desktop-1440px.png) · [narrow-390px](screenshots/dispatcher-delivery-map-unselected-narrow-390px.png) | View the network or the selected delivery on the map. |
| **Drone Fleet Management (`/drones`)** | `09-fleet-management.png` | `frontend/web/src/features/drones/DronesPage.tsx` | Desktop: [dispatcher-fleet-drones-desktop-1440px.png](screenshots/dispatcher-fleet-drones-desktop-1440px.png)<br/>Narrow PNG: [dispatcher-fleet-drones-narrow-390px.png](screenshots/dispatcher-fleet-drones-narrow-390px.png) | View and manage drone records. |

### 3.4 Station Operator Role Journeys
| Screen / Flow | Design wireframe reference | Source at baseline | Candidate PNG references | Screen purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Landing Stations Overview (`/stations`)** | Station hub wireframe | `frontend/web/src/features/stations/StationsPage.tsx` | Desktop: [operator-stations-desktop-1440px.png](screenshots/operator-stations-desktop-1440px.png)<br/>Narrow PNG: [operator-stations-narrow-390px.png](screenshots/operator-stations-narrow-390px.png) | Review station records permitted for the account. |
| **Station Slot Grid (`/station-slots`)** | Locker grid layout wireframe | `frontend/web/src/features/slots/StationSlotsPage.tsx` | Desktop: [operator-station-slots-desktop-1440px.png](screenshots/operator-station-slots-desktop-1440px.png)<br/>Narrow PNG: [operator-station-slots-narrow-390px.png](screenshots/operator-station-slots-narrow-390px.png)<br/> No assigned station example: [desktop-1440px](screenshots/operator-station-slots-unassigned-desktop-1440px.png) · [narrow-390px](screenshots/operator-station-slots-unassigned-narrow-390px.png) | Review slot state and eligible manual EMPTY/ERROR changes. |
| **Station Availability Matrix (`/station-availability`)** | Station capacity wireframe | `frontend/web/src/features/availability/StationAvailabilityPage.tsx` | Desktop: [operator-station-availability-desktop-1440px.png](screenshots/operator-station-availability-desktop-1440px.png)<br/>Narrow PNG: [operator-station-availability-narrow-390px.png](screenshots/operator-station-availability-narrow-390px.png)<br/> No station selected example: [desktop-1440px](screenshots/operator-station-availability-unselected-desktop-1440px.png) · [narrow-390px](screenshots/operator-station-availability-unselected-narrow-390px.png) | Check station capacity and availability. |

### 3.5 Logistics Manager Role Journeys
| Screen / Flow | Design wireframe reference | Source at baseline | Candidate PNG references | Screen purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Analytics Dashboard (`/manager/analytics`)** | `01-dashboard.png`, `02-statistics-filters.png` | `frontend/web/src/features/analytics/AnalyticsPage.tsx` (PR #276 / LA-350) | Desktop: [manager-analytics-dashboard-desktop-1440px.png](screenshots/manager-analytics-dashboard-desktop-1440px.png)<br/>Narrow PNG: [manager-analytics-dashboard-narrow-390px.png](screenshots/manager-analytics-dashboard-narrow-390px.png)<br/>Presets: [manager-analytics-presets-desktop-1440px.png](screenshots/manager-analytics-presets-desktop-1440px.png) | Review creation-cohort KPIs with preset or custom Vietnam reporting dates. |
| **Deliveries Performance Report (`/manager/analytics?tab=reports`)** | `03-reports-analytics.png` | `frontend/web/src/features/analytics/AnalyticsPage.tsx` (PR #276 / LA-350) | Desktop: [manager-delivery-report-desktop-1440px.png](screenshots/manager-delivery-report-desktop-1440px.png)<br/>Narrow PNG: [manager-delivery-report-narrow-390px.png](screenshots/manager-delivery-report-narrow-390px.png) | Review delivery report rows and open ETA or summary for the selected order. |
| **Delivery ETA Countdown (`/delivery-eta`)** | ETA detail wireframe | `frontend/web/src/features/deliveryEta/DeliveryEtaPage.tsx` | Desktop: [manager-delivery-eta-desktop-1440px.png](screenshots/manager-delivery-eta-desktop-1440px.png)<br/>Narrow PNG: [manager-delivery-eta-narrow-390px.png](screenshots/manager-delivery-eta-narrow-390px.png) | Review the available estimate and prediction information for the selected order. |
| **Delivery Summary & Flight Log (`/delivery-summary`)** | Flight log wireframe | `frontend/web/src/features/deliverySummary/DeliverySummaryPage.tsx` | Desktop: [manager-delivery-summary-desktop-1440px.png](screenshots/manager-delivery-summary-desktop-1440px.png)<br/>Narrow PNG: [manager-delivery-summary-narrow-390px.png](screenshots/manager-delivery-summary-narrow-390px.png) | Review a delivery record and request a summary when generation is available. |

### 3.6 System Administrator Role Journeys
| Screen / Flow | Design wireframe reference | Source at baseline | Candidate PNG references | Screen purpose |
| :--- | :--- | :--- | :--- | :--- |
| **User Directory (`/admin/users`)** | User table wireframe | `frontend/web/src/pages/UsersPage.tsx` | Desktop: [admin-users-desktop-1440px.png](screenshots/admin-users-desktop-1440px.png)<br/>Narrow PNG: [admin-users-narrow-390px.png](screenshots/admin-users-narrow-390px.png) | Search users and use available management actions. |
| **Role Management (`/admin/roles`)** | Roles list wireframe | `frontend/web/src/pages/RolesPage.tsx` | Desktop: [admin-roles-desktop-1440px.png](screenshots/admin-roles-desktop-1440px.png)<br/>Narrow PNG: [admin-roles-narrow-390px.png](screenshots/admin-roles-narrow-390px.png) | Review the role catalog and role membership counts. |
| **Security Audit Logs (`/admin/audit-logs`)** | Audit logs wireframe | `frontend/web/src/pages/AuditLogsPage.tsx` | Desktop: [admin-audit-logs-desktop-1440px.png](screenshots/admin-audit-logs-desktop-1440px.png)<br/>Narrow PNG: [admin-audit-logs-narrow-390px.png](screenshots/admin-audit-logs-narrow-390px.png) | Filter recorded actions, for example CREATE_USER or ROLE_CHANGE. |
| **Admin & Staff Profile (`/profile`)** | User profile wireframe | `frontend/web/src/pages/ProfilePage.tsx` | Desktop: [admin-profile-desktop-1440px.png](screenshots/admin-profile-desktop-1440px.png)<br/>Narrow PNG: [admin-profile-narrow-390px.png](screenshots/admin-profile-narrow-390px.png) | View and update the signed-in account profile. |

---

## 4. Verification and evidence boundaries

The replacement captures use web baseline `ef7ddecf4875b6504ce2352468a2f3e8fda6a90c` and an isolated local ASP.NET Core API on port 5265, backed by PostgreSQL with synthetic demo records. Chrome rendered the Vite app on port 5266 (port 5267 for maps) at CSS viewports 1440 × 1000 and 390 × 844, both DPR 1, with mobile and touch emulation for 390 px and the Asia/Ho_Chi_Minh timezone. [Per-image capture manifest](screenshots/capture-la361.json) records role, route, time, browser version and dataset details.

The tracking and map images use `ORD-DEMO-002` with a simulated position stored in PostgreSQL. The maps use © OpenStreetMap contributors, with tiles served by [OpenStreetMap Germany](https://tile.openstreetmap.de/) through the existing tile URL setting. The report and customer detail use the same departure timestamp for `ORD-DEMO-003`. Each analytics capture checks that the form and displayed result cohort agree. The summary capture shows the disabled-provider state and the ETA comes from the schedule without model confidence. These examples establish UI behavior with demo data. Native Flutter, enabled Gemini quality and real telemetry are outside this evidence.

The earlier order-unavailable, unselected-map, unassigned-operator and unselected-station images are retained as separate examples. The full capture settings for those and other earlier images were not recorded. Visual review of the replacement images is pending. Narrow filter density and broader table/action reachability remain responsive QA follow-ups.

| Scope | Tested revision and runtime | Method and observed result |
| --- | --- | --- |
| Customer web journey | `34a6b3cb76283da844643adc3b21111944bbbf07`; local browser with isolated synthetic DB and API on port 5061. Review reports 360/390/1440 px browser widths; device scale factor not recorded. | Registration, package return, order creation and continuation, keyboard date toggle and retry. The tested address Edit target measured 44 px. Axe 4.10.3 covered eight routes and flagged the Default tag on Addresses at this revision. [Review](https://github.com/nguyentuio1/learning_app/pull/271#pullrequestreview-5956409951) |
| Default tag follow-up | `5c25432cfab7d60b79b12f498ec8f96d4a08f2ba`; Chrome on the local web route. CSS viewport and device scale factor not recorded for the rescan. | Computed colors `#275e36` on `#e6f3df` yield 6.66:1. The reviewer reported a clean settled Addresses scan within that run's scope. [Review](https://github.com/nguyentuio1/learning_app/pull/271#pullrequestreview-5957599309) |
| Staff lookup and analytics | `1edf42d2665ffb9ab798664bfa52d51d6418324e`; isolated local browser/API. Review reports a 390 px narrow layout; device scale factor not recorded. | Historical lookup, report return, inclusive periods, 366-day validation, mocked read failure followed by live retry, and disabled-AI messaging. [Review](https://github.com/nguyentuio1/learning_app/pull/276#pullrequestreview-5957778047) |

The unchanged desktop breadcrumb measured 4.18:1 and remains a [LA-175 QA follow-up](https://hungndemail.atlassian.net/browse/LA-175). These are scoped measurements. Whole-product accessibility, minimum touch-target coverage, and enabled Gemini quality require separate evidence.

## 5. Traceability and publication

Customer web [PR #271](https://github.com/nguyentuio1/learning_app/pull/271) and staff lookup/reporting [PR #276](https://github.com/nguyentuio1/learning_app/pull/276) are in the source baseline. The screenshot catalog and repository document are under review in [PR #279](https://github.com/nguyentuio1/learning_app/pull/279). The image review remains with the user. Confluence page 11894809 was reported at v4 in the technical review; publication of v5 and page-version readback are pending.
