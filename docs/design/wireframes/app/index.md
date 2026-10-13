# Mobile App Wireframes

Low-fidelity mobile wireframe previews and user flow documentation for Customer and Station Operator applications.

LA-76 under LA-14 · Mobile only · 43 screens for 2 actors (27 Customer, 16 Station Operator). Built for Flutter mobile platform. This index covers mobile wireframe PNG previews only and does not represent final application implementation.

Design system reference: [Design System](../../design-system.md)

Wireframe conventions: [Mobile Wireframe Conventions](./mobile-wireframe-conventions.md)

---

## 1. Overview

| Actor | Number of Screens | Directory |
| :--- | :---: | :--- |
| Customer | 27 | [`customer/`](./customer/) |
| Station Operator | 16 | [`station-operator/`](./station-operator/) |
| **Total** | **43** | |

---

## 2. Customer App Wireframes

| ID | Screen | Description | Wireframe |
| :--- | :--- | :--- | :--- |
| **C01** | Login | Account authentication via email & password (UC-01) | [C01-Login.png](./customer/C01-Login.png) |
| **C02** | Register | New customer account creation and phone verification (UC-01) | [C02-Register.png](./customer/C02-Register.png) |
| **C03** | Home — Active Deliveries | Dashboard displaying active orders, real-time status summary, and quick actions (UC-03, UC-07) | [C03-Home.png](./customer/C03-Home.png) |
| **C04** | Home — Empty State | Initial home view when customer has no active deliveries (UC-03) | [C04-Home-Empty.png](./customer/C04-Home-Empty.png) |
| **C05** | Orders List & Filters | Tabbed view for active, pending, and completed orders with search & status filters (UC-06, UC-07) | [C05-Orders.png](./customer/C05-Orders.png) |
| **C06** | In-Transit Order Detail | Real-time tracking view showing live map trajectory, ETA, speed, battery, and AI summary (UC-07, UC-20) | [C06-OrderDetails-InTransit.png](./customer/C06-OrderDetails-InTransit.png) |
| **C07** | Pending Order Detail | Details of order awaiting dispatcher approval with Edit and Cancel options (UC-06) | [C07-OrderDetails-Pending.png](./customer/C07-OrderDetails-Pending.png) |
| **C08** | Arrived Order & Pickup Code | Arrival state showing assigned station compartment and secure OTP/QR code (UC-08) | [C08-OrderDetails-Arrived.png](./customer/C08-OrderDetails-Arrived.png) |
| **C09** | Cancel Order Sheet | Reason selection modal for cancelling pending or approved orders (UC-06) | [C09-CancelOrder.png](./customer/C09-CancelOrder.png) |
| **C10** | Order History & Audit Log | Comprehensive lifecycle timeline and audit trail of order events (UC-07, UC-10) | [C10-OrderHistory.png](./customer/C10-OrderHistory.png) |
| **C11** | Confirm Delivery — Enter OTP | Keypad interface for entering 6-digit compartment pickup code (UC-08) | [C11-ConfirmDelivery.png](./customer/C11-ConfirmDelivery.png) |
| **C12** | Confirm Delivery — Incorrect OTP | Error state handling invalid secure code entry with retry feedback (UC-08) | [C12-ConfirmDelivery-Wrong.png](./customer/C12-ConfirmDelivery-Wrong.png) |
| **C13** | Delivery Completed | Successful pickup confirmation screen with delivery summary and rating option (UC-08) | [C13-Delivered.png](./customer/C13-Delivered.png) |
| **C14** | Create Delivery | Multi-step form selecting route, package, and station with AI ETA/fee estimation (UC-05, UC-20) | [C14-CreateDelivery.png](./customer/C14-CreateDelivery.png) |
| **C15** | Create Delivery — Validation Errors | Form validation error state highlighting missing fields or over-weight packages (UC-05) | [C15-CreateDelivery-Errors.png](./customer/C15-CreateDelivery-Errors.png) |
| **C16** | Review Delivery | Pre-submission confirmation screen reviewing route, price breakdown, and instructions (UC-05) | [C16-ReviewDelivery.png](./customer/C16-ReviewDelivery.png) |
| **C17** | Order Submitted | Success screen displaying generated tracking code (DD-YYYYMM-NNNN) (UC-05) | [C17-OrderSubmitted.png](./customer/C17-OrderSubmitted.png) |
| **C18** | Packages List | Saved package templates list with weight, dimensions, and fragile indicators (UC-04) | [C18-Packages.png](./customer/C18-Packages.png) |
| **C19** | Add Package | Form to save new package profile with fragile flag and photo attachment (UC-04) | [C19-AddPackage.png](./customer/C19-AddPackage.png) |
| **C20** | Saved Addresses List | Address book managing pickup and dropoff locations (UC-03) | [C20-Addresses.png](./customer/C20-Addresses.png) |
| **C21** | Add / Edit Address | Address entry form with campus hub selection and location tagging (UC-03) | [C21-AddAddress.png](./customer/C21-AddAddress.png) |
| **C22** | Notifications | Center displaying status updates, arrivals, and "Mark all as read" feature (UC-10) | [C22-Notifications.png](./customer/C22-Notifications.png) |
| **C23** | AI Assistant | Conversational interface providing quick delivery query suggestions and support (UC-09) | [C23-Assistant.png](./customer/C23-Assistant.png) |
| **C24** | Profile & Settings | Main user profile overview, station assignment info, and app preferences (UC-02) | [C24-Profile.png](./customer/C24-Profile.png) |
| **C25** | Edit Profile | User contact information update form (UC-02) | [C25-EditProfile.png](./customer/C25-EditProfile.png) |
| **C26** | Change Password | Security form to update account authentication password (UC-01, UC-02) | [C26-ChangePassword.png](./customer/C26-ChangePassword.png) |
| **C27** | Theme Settings | Appearance preferences selection (Light, Dark, or System Default) (UC-02) | [C27-Theme.png](./customer/C27-Theme.png) |

---

## 3. Station Operator App Wireframes

| ID | Screen | Description | Wireframe |
| :--- | :--- | :--- | :--- |
| **O01** | Station Sign In | Operator login interface restricted to admin-created work accounts (UC-01) | [O01-Login.png](./station-operator/O01-Login.png) |
| **O02** | Operator Home | Hub operational status, slot availability summary, and pending task queue (UC-16, UC-17) | [O02-Home.png](./station-operator/O02-Home.png) |
| **O03** | Tasks (Pickups) | List of customer-deposited packages waiting for physical verification and pickup confirmation (UC-18) | [O03-Tasks.png](./station-operator/O03-Tasks.png) |
| **O04** | Tasks (Arrivals) | Inbound drone queue showing incoming flights and reserved landing slots (UC-17, UC-18) | [O04-Tasks-Arrivals.png](./station-operator/O04-Tasks-Arrivals.png) |
| **O05** | Scan Order | Barcode/QR camera scanner and manual tracking number lookup tool (UC-18) | [O05-Scan.png](./station-operator/O05-Scan.png) |
| **O06** | Scan Not Found | Validation error screen for orders not assigned to the operator's station (UC-18) | [O06-Scan-NotFound.png](./station-operator/O06-Scan-NotFound.png) |
| **O07** | Confirm Pickup Checklist | Required 3-point QA safety checklist (matching photo, weight \(\leq 3\text{ kg}\), damage-free) before loading (UC-18) | [O07-Pickup.png](./station-operator/O07-Pickup.png) |
| **O08** | Pickup Completed | State confirming package received, drone loaded at slot, and status changed to Ready for Takeoff (UC-18) | [O08-Pickup-Done.png](./station-operator/O08-Pickup-Done.png) |
| **O09** | Confirm Arrival | Inbound telemetry display and station hardware slot sensor validation before landing confirmation (UC-17, UC-18) | [O09-Arrival.png](./station-operator/O09-Arrival.png) |
| **O10** | Arrival Completed | Final arrival state notifying customer and generating secure OTP for compartment release (UC-08, UC-18) | [O10-Arrival-Done.png](./station-operator/O10-Arrival-Done.png) |
| **O11** | Slot Management | Overview of landing slots, charging states, and station mode toggles (Operational / Maintenance / Offline) (UC-16, UC-17) | [O11-Slots.png](./station-operator/O11-Slots.png) |
| **O12** | Set Slot Status | Modal sheet for manual override of slot sensor state (Empty / Error) with dispatcher notes (UC-16) | [O12-Slot-Status.png](./station-operator/O12-Slot-Status.png) |
| **O13** | Station Maintenance | Maintenance mode registration form temporarily pausing new customer deliveries to the hub (UC-16) | [O13-Station-Maintenance.png](./station-operator/O13-Station-Maintenance.png) |
| **O14** | Report a Problem | Exception reporting form sending package/hardware issues directly to Dispatcher with photo (UC-18) | [O14-ReportProblem.png](./station-operator/O14-ReportProblem.png) |
| **O15** | Notifications | Station alert center for incoming drones, scheduled pickups, and dispatcher decisions (UC-10) | [O15-Notifications.png](./station-operator/O15-Notifications.png) |
| **O16** | Profile & Station | Operator profile info, security settings, and assigned hub location details (UC-02, UC-16) | [O16-Profile.png](./station-operator/O16-Profile.png) |

---

## 4. Scope Notes & Validation Remarks

1. **Asset Type:** All 43 visual assets are static PNG preview images (`C01`–`C27`, `O01`–`O16`). Source vector files (SVG) and design project files (Draw.io / Figma) are managed in design asset repositories.
2. **Asset Format:** All 43 wireframe screens are provided in `.png` format.
3. **Use Case Mapping:** All screens map directly to Use Cases `UC-01` through `UC-20` specified in system documentation.

---
