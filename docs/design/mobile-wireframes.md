# SmartDroneDelivery — Mobile Wireframes
**Task:** LA-76 — Design mobile wireframes  
**Parent task:** LA-14 — Design UI/UX for the web portal and mobile apps  
**Scope owner:** Mobile UI/UX — Customer App and Station Operator App  
**Status:** Customer screens documented from the supplied Customer App PDF; Operator screens documented from the agreed screen inventory and must be checked against the source Figma frames.  
**Source reviewed:** `SmartDroneDelivery — Customer App.pdf` (12 pages), plus the Station Operator screen inventory available in project context.

## 1. Objective and scope
LA-76 covers the mobile wireframes for **both**:
- Customer App
- Station Operator App

Web portal wireframes (Customer, Dispatcher, Logistics Manager, Administrator) belong to LA-75 and are outside this document's scope.

This document records the mobile screen inventory, key content, navigation, and main flows. A wireframe/prototype documents intended UI behavior; it does not by itself prove that backend APIs or live tracking are implemented.

## 2. Customer App — screen inventory

| ID | Screen | Purpose / key elements |
|---|---|---|
| C01 | Sign In | Email and password inputs; Sign in action |
| C02 | Home | Greeting, Create Delivery action, delivery summary statuses, recent deliveries empty state, bottom navigation |
| C03 | My Orders | Tracking-number search, status filters (All, Pending, Approved, Scheduled, In Transit, Delivered), order cards |
| C04 | My Packages | Package search, All/Fragile/Non-fragile filters, saved package cards, Add Package action |
| C05 | Add/Edit Package | Package name, weight, dimensions, optional description, fragile toggle, optional photo; save/cancel actions |
| C06 | Create Delivery | Select saved package or add a package, origin and destination stations, recipient name/phone, optional delivery instructions |
| C07 | Review Delivery | Review package, route, recipient and instruction data; edit sections; Submit Delivery Request / Back to Edit |
| C08 | Order Details / Tracking | Tracking ID, status, estimated delivery, timeline, origin/destination stations, package and recipient details, View Order History |
| C09 | Delivery Addresses | Search, saved addresses, default address indicator, Edit/Delete actions, Add New Address |
| C10 | Notifications | All/Unread/Orders filters, Mark all as read, order update/scheduled/completed notifications |
| C11 | AI Assistant | Suggested questions, conversation area, message input; responses depend on available order data |
| C12 | Profile & Settings | Profile details, Edit Profile, personal information, addresses, notifications, language, privacy/security, theme, help/about, Sign Out |

### Customer navigation and flow
- Bottom navigation shown in the design: **Home, Orders, Packages, Profile**.
- Home provides the primary **Create Delivery** action.
- My Packages provides **Add Package**.
- Main request flow: Sign In → Home → Create Delivery → select/add package → choose origin/destination stations → enter recipient details → Review Delivery → Submit Delivery Request.
- Review Delivery allows edits before submission.
- Order Details includes **View Order History**; confirm its destination in the prototype.
- Profile-related destinations should be linked where separate frames exist.

### Customer statuses and data states
- Order statuses shown: Pending, Approved, Scheduled, In Transit, Delivered.
- Order timeline labels: Order Created, Order Approved, Delivery Scheduled, In Transit, Delivered.
- The supplied export contains illustrative/sample order IDs, package names, addresses, notifications, and AI conversation content. Keep sample content distinguishable from live data.
- Preserve explicit unavailable-value labels such as `Time unavailable`, `Date unavailable`, and `Not available`; do not invent values.

## 3. Station Operator App — screen inventory

| ID | Screen | Purpose / key elements |
|---|---|---|
| O01 | Operator Login | Operator authentication and entry to the station workflow |
| O02 | Operator Home | Overview/summary of station work and quick access to operator tasks |
| O03 | Station Details | View the operator's assigned station information |
| O04 | Station Slots | View/manage station slot availability or occupancy as represented in the design |
| O05 | Package Tasks | List and access package-related tasks assigned to the station/operator |
| O06 | Task Details | Inspect a selected task and its package/order information and required next action |
| O07 | Receipt Confirmation | Confirm that a package has arrived at the origin station after the order is scheduled |
| O08 | Profile & Settings | Operator profile and account settings |

**Important business rule:** O07 is **receipt confirmation at the origin station** after the package arrives and the order is scheduled. It is not final delivery confirmation to the recipient. The Station Operator app is for station/package operations, not drone flight control.

### Operator navigation and flow
- Bottom navigation: **Home → O02**, **Tasks → O05**, **Station → O03**, **Profile → O08**.
- Main operational path: O01 Operator Login → O02 Operator Home → O05 Package Tasks → O06 Task Details → O07 Receipt Confirmation when the task requires confirming package arrival.
- O03 Station Details and O04 Station Slots support station-level information/workflows.
- Confirm the precise actions, status transitions, required fields, and success/error feedback against the Operator Figma frames. Do not infer unsupported business rules from screen names alone.

## 4. States and interaction checks

### Customer
- [ ] Home: no deliveries yet and populated state.
- [ ] Orders: empty, search no-results, and each relevant status filter.
- [ ] Packages and addresses: empty states, add/edit forms, validation, delete confirmation if applicable.
- [ ] Create Delivery: required-field validation and unavailable station options.
- [ ] Review Delivery: loading, success, and failure feedback after submission.
- [ ] Notifications: no notifications and unread-filter empty states.
- [ ] AI Assistant: loading/error and unavailable order-data responses.
- [ ] Profile: edit/save/cancel and sign-out behavior as represented in Figma.

### Station Operator
- [ ] Login: invalid credentials, loading, and authentication error.
- [ ] Home and Package Tasks: empty, loading, error, and populated states where applicable.
- [ ] Station Slots: available/occupied/unavailable states only where supported by the actual design.
- [ ] Task Details: status, required action, and unavailable/missing data.
- [ ] Receipt Confirmation: confirm/cancel behavior, success/error feedback, and prevention of accidental duplicate confirmation if required by the design.
- [ ] Bottom navigation routes to O02, O05, O03, and O08 respectively.

These are review checks, not claims that every state already exists in the exported designs.

## 5. Completion checklist
- [ ] All 12 Customer screens (C01–C12) are present and named consistently in Figma.
- [ ] All 8 Station Operator screens (O01–O08) are present and named consistently in Figma.
- [ ] Prototype links cover primary actions and bottom-navigation items for both apps.
- [ ] Customer delivery-request flow is linked end-to-end.
- [ ] Operator package-task and receipt-confirmation flow is linked end-to-end.
- [ ] O07 is described as origin-station receipt confirmation, not final delivery confirmation.
- [ ] Empty, loading, validation, success, and error states are reviewed where applicable.
- [ ] Figma design and prototype URLs are added to the team's task/README if required.
- [ ] Both apps follow the LA-77 design system.
- [ ] No web portal wireframes are included in this LA-76 deliverable.
