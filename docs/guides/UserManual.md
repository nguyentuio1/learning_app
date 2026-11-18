# LA-250: User Manual per Role — SmartDroneDelivery

This manual covers operational workflows for all **5 user roles** across the **Web Portal** (`http://localhost:8080` or `http://localhost:5173`) and **Flutter Mobile Apps** (`customer` and `operator` flavors).

Use the [installation guide](InstallationGuide.md) first. A fresh Production stack needs staff accounts, operational stations with assigned operators, available slots and eligible drones configured by an Administrator. For disposable synthetic data and a step-by-step cross-role run, follow the [demo prerequisites and script](../demo/demo-script.md). The software simulator must be explicitly enabled for simulated takeoff/arrival; scheduling alone does not fly a drone.

---

## 1. Customer (`CUSTOMER` — Web Portal & Customer Mobile App)

### 1.1 Sign In / Register
1. Open the Web Portal (`/login` or `/register`) or launch the **Customer Mobile App**.
2. Sign in with your Customer email and password (or register a new Customer account).
3. **Expected Result:** Redirected to the Customer workspace (`/orders` on Web; Customer Home on Mobile).

### 1.2 Manage Saved Addresses & Packages
1. Open **Addresses** (`/addresses`) to add/edit/set a default contact address.
2. Open **Packages** (`/packages` -> **New Package**) and enter package name, weight (grams), and dimensions (`L × W × H` cm), optionally uploading a package photo on the web. Values must be positive: at most 3,000 g and 30 ? 20 ? 15 cm. Follow the actual mobile form for its supported image input.
3. **Expected Result:** Package appears in your list and can be selected when creating an order.

### 1.3 Create & Manage Delivery Orders
1. Navigate to **Create Order** (`/orders/new`).
2. Select an owned Package, Origin Station, a different Destination Station, and Recipient Name/Phone (or pick from Saved Addresses).
3. Submit the order.
4. **Expected Result:** Order is created in `PENDING` status and displays a **one-time 6-digit pickup code**. Acknowledge and save it securely; subsequent reads do not disclose it.
5. While `PENDING`, you can edit details (`/orders/:id/edit`). You can cancel (`PENDING` or `APPROVED`) with a mandatory cancellation reason.

### 1.4 Track Delivery, ETA & Confirm Receipt
1. Open **My Orders** (`/orders/:id`), **Tracking**, **Delivery Map**, or **Delivery ETA** (`/delivery-eta/:id`).
2. Observe real-time SignalR/polling updates while `IN_TRANSIT`.
3. Once status reaches `ARRIVED`, use **Confirm Delivery** in the Customer mobile app and enter the 6-digit pickup code. On the web, use the order detail/map to monitor progress and present the code to the destination Station Operator for handover; the current customer web page has no Confirm Delivery form.
4. **Expected Result:** Status transitions `ARRIVED → DELIVERED`, `actualDeliveryTime` is recorded, and the destination slot is released. (3 consecutive wrong codes trigger a 10-minute `423 Locked` cooldown.)

### 1.5 Notifications & AI Assistant
- View unread alerts in **Notifications** (`/notifications`) and mark individual or all items read.
- Open **Delivery Assistant** (`/delivery-assistant`) to view saved sessions (message sending is enabled only when `CustomerAssistant:Enabled` and approved retention policy are active).

---

## 2. Dispatcher (`DISPATCHER` — Web Portal)

### 2.1 Sign In
1. Sign in at `/login` with a `DISPATCHER` account.
2. **Expected Result:** Access to Operations navigation (Approval Queue, Scheduling, Active Deliveries, Failure Handling, Tracking, Map, ETA, Summary).

### 2.2 Review Pending Orders
1. Open **Approval Queue** (`/dispatcher/approvals`).
2. Filter by date/keyword, inspect package & route details, and click **Approve** or **Reject** (with reason).
3. **Expected Result:** Approved orders move to `APPROVED`; rejected orders move to `CANCELLED`.

### 2.3 Schedule Departure & Assign Drone
1. Open **Scheduling** (`/dispatcher/scheduling`).
2. Select an `APPROVED` order, set **Scheduled Departure Time** (must be at least 15 minutes in the future), and click **Save departure**.
3. Review eligible drones (checked against payload, 30% range reserve, and overlapping missions) and click **Assign drone**.
4. **Expected Result:** A `PLANNED` flight mission is created, the drone becomes `ASSIGNED`, and a destination slot is `RESERVED`.

### 2.4 Monitor Active Deliveries & Handle Failures
1. Monitor live flights on **Active Deliveries**, **Delivery Tracking**, and **Delivery Map** (`/delivery-map/:id`).
2. If an incident occurs (`IN_TRANSIT` or `ARRIVED`), open **Failure Handling** (`/dispatcher/failures`), report the failure, and choose a resolution action (`RESCHEDULE`, `CANCEL`, or `RETURN_TO_ORIGIN`) after confirming physical package verification.

---

## 3. Station Operator (`STATION_OPERATOR` — Station Operator Mobile App & Web Station Workspace)

### 3.1 Sign In
1. Launch the **Station Operator Mobile App** (`operator` flavor) or sign in on the Web Portal.
2. **Expected Result:** Intake, pickup and slot administration check your assigned station; using an unassigned station for those actions is rejected. Station list/availability reads are also available to other authenticated roles and do not grant operational permissions.

### 3.2 Station Slot Availability & Maintenance
1. Open **Station Availability / Slots** (`/station-slots` on Web; use the assigned-station actions exposed by the Operator mobile home).
2. View `EMPTY`, `RESERVED`, `OCCUPIED`, and `ERROR` counts. Toggle an unused slot between `EMPTY` and `ERROR` when physical maintenance is required.

### 3.3 Origin Package Intake
1. At the origin station, open **Package Intake** and look up the order by `trackingNumber`.
2. Verify the order is `APPROVED` with a `PLANNED` mission, select an `EMPTY` origin slot, enter measured weight/dimensions, and confirm visual inspection.
3. Submit **Accept Intake** (or **Reject Intake** with a reason if damaged/invalid).
4. **Expected Result:** Accepted intake transitions the order to `READY_FOR_TAKEOFF` and marks the origin slot `OCCUPIED`. Rejected intake marks the order `FAILED`.

### 3.4 Destination Handover & Expired Parcel Clearing
1. When an order is `ARRIVED` at your destination station, open **Pickup Confirmation**, enter `trackingNumber` and the recipient's 6-digit `pickupSecureCode` (or pre-check via **Validate Code**).
2. **Expected Result:** Order becomes `DELIVERED` and the destination slot is released to `EMPTY`.
3. For parcels expired after the 24-hour pickup timeout, open **Expired Parcels** and confirm physical removal to release the slot.

---

## 4. Logistics Manager (`MANAGER` — Web Portal)

### 4.1 Sign In & View KPI Dashboard
1. Sign in at `/login` with a `MANAGER` account and open **Analytics Dashboard** (`/manager/analytics`).
2. Select a date range (`fromDate` to `toDate`, max 366 days, `UTC+07:00` calendar dates).
3. **Expected Result:** Displays order counts by status, delivery success rate (`DELIVERED` vs `FAILED`), average delivery duration, and per-station throughput (`stationUsage`).

### 4.2 Inspect Delivery Reports & Monitoring
1. Use Today / Last 7 days / Last 30 days or custom inclusive Vietnam creation dates. View paginated **Delivery Reports** on `/manager/analytics?tab=reports` showing creation, departure, delivery timestamps, and duration per order.
2. Use a tracking number in the ETA/summary delivery picker to find current or historical orders; a UUID is not required. AI-off screens still show delivery facts or schedule-derived estimates. Managers can also inspect read-only **Tracking**, **Map**, **ETA**, **Delivery Summary**, and **Landing Stations** availability for operational oversight (drone fleet management and station slot administration remain restricted to `DISPATCHER`/`ADMIN`/`STATION_OPERATOR`).

---

## 5. System Administrator (`ADMIN` — Web Portal)

### 5.1 Sign In
1. Sign in at `/login` with an `ADMIN` account (seeded via `ADMIN_EMAIL` / `ADMIN_PASSWORD` in `.env`).

### 5.2 Manage Users & Roles
1. Open **User Management** (`/admin/users`).
2. Create new staff/customer accounts, edit user details, reassign roles (`CUSTOMER`, `DISPATCHER`, `STATION_OPERATOR`, `MANAGER`, `ADMIN`), or deactivate accounts.
3. **Expected Result:** Persisted role/status updates take effect immediately on the user's next API call.

### 5.3 Configure Stations, Slots, Drones & Integration Keys
1. Open **Landing Stations** (`/stations`) to create/edit stations, update station status (`OPERATIONAL` / `MAINTENANCE` / `OFFLINE`), and add/remove slots (up to 20 per station).
2. Open **Drones** (`/drones`) to register drones (unique serial number enforced) and manage maintenance status.
3. Provision/revoke partner `X-API-Key` credentials through the authenticated REST API `/api/admin/integration-keys`, using the [API documentation](../api/API_Documentation.md). This is an API workflow, not a web navigation page. The returned key is disclosed once; keep it server-side.

### 5.4 Review Security Audit Logs
1. Open **Audit Logs** (`/admin/audit-logs`).
2. Filter by user, action code (including failed sign-ins, role changes, station updates, key lifecycle), and date range.
3. **Expected Result:** Read-only paginated audit trail is displayed without exposing plaintext secrets or pickup codes.
