# Software Requirements Specification - SmartDroneDelivery

AI-powered Drone Delivery Management Platform (Vietnamese title: Nền tảng quản lý giao hàng bằng drone tích hợp AI).

## 1. Introduction

### 1.1 Purpose
This document specifies the functional and non-functional requirements of SmartDroneDelivery. It is the reference for design, implementation and testing, and it traces every requirement to a use case in [use-cases.md](use-cases.md).

### 1.2 Scope
SmartDroneDelivery is a **business-oriented platform that manages drone delivery operations**. It covers the complete delivery lifecycle from order creation to delivery confirmation. It does **not** control drone flight hardware.

The platform supports five business processes: delivery request management, package management, delivery execution, delivery tracking and delivery confirmation. AI-assisted services provide estimated delivery time, delivery summarization, customer assistance and operational analytics.

### 1.3 Definitions

| Term | Meaning |
|---|---|
| Order | A delivery request for one package between two landing stations |
| Landing station | A physical site with slots where drones land, take off and hand over packages |
| Slot | A single docking position at a landing station |
| Flight mission | The assignment of one drone to one order |
| ETA | Estimated time of arrival |
| Secure code | A code generated per order and entered by the recipient to confirm delivery |
| RBAC | Role-based access control |

## 2. Overall description

### 2.1 Product perspective
The system has four parts: an ASP.NET Core Web API with a PostgreSQL database, a React web portal for logistics management, Flutter mobile apps for customers and station operators, and standardized RESTful APIs for external integration. SignalR provides real-time updates and MinIO stores uploaded files. Everything runs with Docker.

### 2.2 Modules
1. Customer and Delivery Order Management
2. Package and Delivery Workflow Management
3. Landing Station Management
4. Delivery Tracking and Confirmation
5. Analytics and AI-assisted Services

### 2.3 Actors

| Actor | Main concerns | Client |
|---|---|---|
| Customer | Orders, packages, addresses, tracking, AI assistant | Mobile, web |
| Dispatcher | Approval, scheduling, drones, failed deliveries | Web |
| Station Operator | Station slots, package pickup | Mobile, web |
| Logistics Manager | Dashboard, statistics, reports | Web |
| System Administrator | Users, roles, audit logs, system health | Web |

### 2.4 Assumptions and constraints
- Only lightweight packages are delivered; weight and size must stay within the limits of the assigned drone.
- A drone is assigned to at most one mission at a time.
- Authentication uses JWT access tokens with refresh tokens; passwords are stored hashed (BCrypt).
- Technology stack: ASP.NET Core 8, ReactJS with Ant Design, Flutter with Riverpod, PostgreSQL, SignalR, Docker and MinIO.

## 3. Functional requirements

Priority: **M** = must have, **S** = should have.

### 3.1 User management

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-01 | The system shall let users register and log in with email and password | M | UC-01 |
| FR-02 | The system shall issue short-lived access tokens and refresh tokens, and allow tokens to be revoked at logout | M | UC-01 |
| FR-03 | The system shall restrict every API endpoint and screen by role (Customer, Dispatcher, Station Operator, Logistics Manager, System Administrator) | M | UC-01 |
| FR-04 | The system shall let a user view and update their profile, change their password and upload an avatar | M | UC-02 |
| FR-05 | The system shall let an administrator create, deactivate users and assign roles | M | UC-21 |

### 3.2 Customer and delivery management

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-06 | A customer shall create, update, delete and list delivery addresses and mark one as default | M | UC-03 |
| FR-07 | A customer shall create and update packages with name, weight, dimensions, fragile flag and optional photo | M | UC-04 |
| FR-08 | A customer shall create a delivery order for a package with origin station, destination station and recipient name and phone | M | UC-05 |
| FR-09 | The system shall generate a unique tracking number and a secure code for each order and set the initial status to PENDING | M | UC-05 |
| FR-10 | A customer shall edit an order only before it is approved and shall cancel an order with a reason while cancellation is still allowed | M | UC-06 |
| FR-11 | A customer shall list their own orders with filters and view an order with its status history | M | UC-07 |
| FR-12 | The system shall create a notification for the customer when the order status changes | S | UC-10 |

### 3.3 Delivery workflow

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-13 | A dispatcher shall list pending orders and approve or reject each with a reason | M | UC-11 |
| FR-14 | The system shall allow only valid status transitions and shall log every transition with the acting user and time | M | UC-11, UC-12 |
| FR-15 | A dispatcher shall schedule a departure time and assign an available drone, creating a flight mission | M | UC-12 |
| FR-16 | The system shall reject a drone assignment that overlaps another mission of the same drone | M | UC-12 |
| FR-17 | A dispatcher shall see all active deliveries with status and position | M | UC-13 |
| FR-18 | A dispatcher shall mark a delivery as failed with a reason and choose to reschedule, return or cancel | M | UC-14 |
| FR-19 | A dispatcher shall create, update and list drones with model, payload capacity, range and status | M | UC-15 |

### 3.4 Landing station management

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-20 | An administrator or operator shall create, update, deactivate and list landing stations with location and operating status | M | UC-16 |
| FR-21 | The system shall manage the slots of each station and their status | M | UC-16 |
| FR-22 | The system shall show the real-time availability of stations and slots | M | UC-17 |
| FR-23 | A station operator shall confirm that a package was received at the station | M | UC-18 |

### 3.5 Delivery tracking and confirmation

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-24 | The system shall push status and drone position updates to clients in real time | M | UC-07 |
| FR-25 | The system shall show origin, destination and current drone position on a map | S | UC-07 |
| FR-26 | The recipient shall confirm delivery with the secure code; a wrong code is rejected | M | UC-08 |
| FR-27 | The system shall record the actual delivery time when the order is completed | M | UC-08 |

### 3.6 Dashboard and AI services

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-28 | The system shall show delivery statistics (orders by status, success rate, average delivery time, station usage) with date filters | M | UC-19 |
| FR-29 | The system shall estimate the delivery time of an order and update it when conditions change | M | UC-20 |
| FR-30 | The system shall generate a short summary of an order's delivery history | S | UC-20 |
| FR-31 | The system shall provide a chat assistant that answers customer questions using only that customer's own data | M | UC-09 |

### 3.7 Administration and integration

| Id | Requirement | Priority | Use case |
|---|---|---|---|
| FR-32 | The system shall record an audit log entry for sensitive actions (login, role change, order decision) | M | UC-22 |
| FR-33 | An administrator shall search the audit log by user, action and date | S | UC-22 |
| FR-34 | The system shall expose documented, versioned RESTful APIs secured by API keys for external systems | M | UC-05, UC-07 |

## 4. Non-functional requirements

| Id | Category | Requirement |
|---|---|---|
| NFR-01 | Security | Authentication with JWT and role-based authorization on every endpoint |
| NFR-02 | Security | Passwords are hashed; secrets are read from environment variables, never committed |
| NFR-03 | Performance | Common operations respond in under 3 seconds |
| NFR-04 | Scalability | The system supports concurrent users with stable performance |
| NFR-05 | Interoperability | All functions are available through RESTful APIs documented with Swagger/OpenAPI |
| NFR-06 | Deployability | The whole system runs with Docker Compose |
| NFR-07 | Usability | The web portal and the mobile apps are responsive |
| NFR-08 | Operability | System monitoring, audit logging and database backup are provided |
| NFR-09 | Maintainability | Modular architecture; code is checked by build, lint and automated tests before merge |

## 5. Data requirements

The data model has 17 entities (migrations in `backend/SmartDroneDelivery.Api/Migrations`):

| Group | Entities |
|---|---|
| Identity | Role, User, RefreshToken, AuditLog |
| Customer | CustomerAddress, Notification |
| Delivery | Package, DeliveryOrder, DeliveryStatusLog |
| Stations and drones | LandingStation, StationSlot, Drone, FlightMission, FlightTelemetryLog |
| AI | AiEtaPrediction, AiChatSession, AiChatMessage |

## 6. Traceability to the delivery plan

| Work package | Requirements covered |
|---|---|
| WP1 Analysis and design | This document, business process models, architecture, ERD, UI/UX |
| WP2 Management modules | FR-01 to FR-12, FR-19 to FR-23 |
| WP3 Workflow and tracking | FR-13 to FR-18, FR-24 to FR-28, FR-32 to FR-34 |
| WP4 Mobile and AI | FR-29 to FR-31 and the mobile clients of all customer and operator functions |
| WP5 Test and deployment | NFR-03, NFR-06, NFR-08 and verification of all requirements |

## 7. Acceptance
The requirements are accepted when the team has reviewed this document and the leader has approved the pull request that introduces it. Changes after approval go through a new pull request and a Jira issue.
