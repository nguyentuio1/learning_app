# IV. Software Design Description

This chapter brings together the system architecture, the data model and the design of each SmartDroneDelivery module. Trần Nguyễn owns the chapter under [LA-254](https://hungndemail.atlassian.net/browse/LA-254). The implementation baseline is [develop at 894ceab](https://github.com/nguyentuio1/learning_app/commit/894ceab0cc70f6c68e543274382580b9d21f40d6), checked on 16 November 2026. Section 4 records Sprint 7 work tracked on story branches. The publication target is the existing [Chapter IV in Confluence](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993109).

## 1. System design

### 1.1 System architecture

The React web portal serves customers, dispatchers, logistics managers and administrators. The Flutter Customer app supports customer workflows, and the Flutter Station Operator app supports station workflows. Both clients use the ASP.NET Core API. Tracking clients also subscribe to authorized delivery updates through SignalR.

The API checks authorization and validates business rules before applying changes through EF Core to PostgreSQL 16. MinIO stores uploaded files such as package images and avatars. Docker Compose provides the local PostgreSQL and MinIO services. The software simulator supplies telemetry for flight and tracking workflows. Physical drone control is outside the project scope. [System design](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14090250) · [Architecture diagram](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=1540097) · [Local infrastructure](../../docker-compose.yml) · [Flutter architecture](../../frontend/mobile/docs/KIEN_TRUC.md)

### 1.2 Access and integration

Web and mobile clients authenticate with bearer tokens. The backend checks the role and, where required, ownership of the requested record. External clients use scoped API keys with the versioned delivery-order integration API. AI features use the operational data permitted for the requester, and their responses are informational. [Authentication API](../api/auth-api.md) · [Tracking and integration API](../api/sprint-6-api-contract.md) · [AI access rules](../api/sprint-7-api-contract.md)

## 2. Database design

### 2.1 Records by module

The Confluence ERD describes the 17-table baseline. The EF Core model also contains records for scoped integration keys, simulator diversions, delivery failure episodes and handling events. Use the migrations when comparing the baseline diagram with the schema of a current checkout. [Database design](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=1507330) · [Baseline ERD](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=950287) · [EF Core model](../../backend/SmartDroneDelivery.Api/Data/AppDbContext.cs) · [Migrations](../../backend/SmartDroneDelivery.Api/Migrations)

| Area | Main records and responsibilities |
| --- | --- |
| Identity and customer | Roles, users, refresh tokens, customer addresses, notifications and audit logs support access and traceability. |
| Orders and packages | A delivery order associates a package with a customer, origin and destination stations. Status logs retain transitions. |
| Stations and fleet | Landing stations have individually numbered slots, and missions associate drones and slots with delivery orders. |
| Tracking and recovery | Telemetry belongs to a mission. Failure episodes and handling events record the incident and subsequent actions. Simulator diversions record a mission's diversion. |
| AI and integration | An order can have one current ETA prediction. Each chat session belongs to a customer and contains chat messages. Delivery summaries are transient. Scoped integration keys authorize external API reads. |

### 2.2 Relationships and workflow constraints

An order can have multiple missions over time, for example after a reschedule. A filtered unique index allows at most one mission in `PLANNED` or `IN_AIR` for an order at a time. Slot numbers are unique within a station. The order timeline and mission telemetry record different events, so station arrival can be shown separately from recipient confirmation. [Current model and indexes](../../backend/SmartDroneDelivery.Api/Data/AppDbContext.cs) · [Workflow classes](../design/class-specification-delivery-workflow.md) · [Station, slot and drone classes](../design/class-specification-stations-slots-drones.md)

## 3. Detailed design

### 3.1 Identity and access

Users register, sign in and manage their profiles. Administrators manage accounts and roles. Authorization is enforced by the API when a client reads a record or changes its state. [Authentication sequence](../design/sequence-auth.md) · [Profile design](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=5537877) · [Authentication API](../api/auth-api.md)

### 3.2 Customer orders and packages

A customer manages addresses and packages before creating a delivery order. The customer can review the order and its status history, and edit or cancel it when the current status allows the action. Notifications communicate recorded changes. [Create order sequence](../design/customer-order-management/sequence-create-order.md) · [Update and cancel sequence](../design/customer-order-management/sequence-update-cancel-order.md) · [Order API](../api/customer-order-api.md) · [Notification API](../api/notification-api.md)

### 3.3 Dispatch and failure handling

A dispatcher reviews the request, selects a departure time and assigns an eligible drone. The workflow checks delivery and fleet constraints when applying changes. When delivery fails, the incident and subsequent handling actions are retained in the delivery record. [Review sequence](../design/sequence-order-review.md) · [Scheduling and assignment sequence](../design/sequence-scheduling-and-drone-assignment.md) · [Failure handling sequence](../design/sequence-failed-delivery-handling.md) · [Delivery workflow API](../api/delivery-workflow-api.md)

### 3.4 Landing stations and fleet

Authorized staff manage station status, slots and the drone fleet. The Station Operator app supports package intake and handover. Station and operator workflow changes in Sprint 7 are tracked in the handoff below. [Station and slot sequence](../design/sequence-station-and-slot-management.md) · [Drone management sequence](../design/sequence-drone-management.md) · [Station API](../api/station-api.md) · [Drone API](../api/drone-api.md)

### 3.5 Tracking, map and confirmation

The tracking and map screens display the last recorded position and delivery status. Authorized clients can receive live notifications, re-fetch an authoritative snapshot and display stale data as such. `ARRIVED` records arrival at the destination station. `DELIVERED` records successful recipient confirmation. [Tracking sequence](../design/sequence-real-time-tracking.md) · [Confirmation sequence](../design/sequence-delivery-confirmation.md) · [Tracking integration](https://github.com/nguyentuio1/learning_app/pull/205) · [Map integration](https://github.com/nguyentuio1/learning_app/pull/206) · [Confirmation integration](https://github.com/nguyentuio1/learning_app/pull/208)

### 3.6 Dashboard, reports and external access

Logistics managers select a reporting period to view delivery metrics and paginated reports. Administrators manage scoped keys for authorized external reads. The API contract describes the access and filtering rules for these workflows. [Dashboard sequence](../design/sequence-dashboard.md) · [Analytics integration](https://github.com/nguyentuio1/learning_app/pull/202) · [Integration contract](../api/sprint-6-api-contract.md) · [Integration PR](https://github.com/nguyentuio1/learning_app/pull/203)

### 3.7 AI-assisted delivery screens

The Sprint 7 API handoff defines the inputs, outputs, role and ownership checks for ETA, summaries and the customer assistant. The table describes the web flows. Section 4 records the code and review status for each client and backend. [Sprint 7 contract](../api/sprint-7-api-contract.md) · [OpenAPI schemas](../api/sprint-7-openapi.json) · [AI class specification](../design/class-specification-ai-services.md)

| Web screen | Flow and behavior | Design and implementation sources |
| --- | --- | --- |
| Arrival estimate | Open a delivery and display its recorded estimate, with available prediction details and a countdown. Refresh the snapshot while the screen is visible and after tracking status events. Mark an overdue or cached estimate and clear the display if access is lost. | [ETA sequence](../design/sequence-ai-eta-estimation.md) · [LA-159 web PR](https://github.com/nguyentuio1/learning_app/pull/211) |
| Delivery summary | Select an order and request a text summary alongside its delivery record. Refresh the record after a successful request and on a source conflict. Handle empty history and wait before retrying a rate-limited request. | [Summary sequence](../design/sequence-ai-delivery-summarization.md) · [LA-163 web PR](https://github.com/nguyentuio1/learning_app/pull/212) |
| Customer assistant | Read saved conversations, select an order if needed, then send when enabled. Keep temporary replies out of stored history. An uncertain send requires checking history before a retry. A denial clears protected data and closes the composer until access is rechecked. | [Assistant sequence](../design/sequence-ai-customer-assistant.md) · [LA-166 web PR](https://github.com/nguyentuio1/learning_app/pull/213) |

AI acceptance still requires evidence against the real backend and the applicable provider and activation settings. The web assistant defaults to read-only until sending is enabled under the conversation retention and deletion policy. [Contract release gates](../api/sprint-7-api-contract.md) · [LA-165 implementation](https://github.com/nguyentuio1/learning_app/pull/224)

Reference drawings for the current design are in the [Detailed Design index](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14090271), the [web wireframes](../design/wireframes/web/index.md) and the [mobile wireframes](../design/wireframes/app/index.md).

## 4. Handoff and review

### 4.1 Sprint 7 implementation evidence

| Module | Status at the chapter baseline | Source |
| --- | --- | --- |
| ETA | Backend, web and mobile changes merged into the LA-40 story branch. | [Backend #210](https://github.com/nguyentuio1/learning_app/pull/210) · [Web #211](https://github.com/nguyentuio1/learning_app/pull/211) · [Mobile #207](https://github.com/nguyentuio1/learning_app/pull/207) |
| Delivery summary | Backend and web changes merged into the LA-41 story branch. | [Backend #219](https://github.com/nguyentuio1/learning_app/pull/219) · [Web #212](https://github.com/nguyentuio1/learning_app/pull/212) |
| Customer assistant | Backend merged into the LA-42 story branch. Web and mobile PRs were open. Web CI passed for `2171b6e`. | [Backend #224](https://github.com/nguyentuio1/learning_app/pull/224) · [Web #213](https://github.com/nguyentuio1/learning_app/pull/213) · [Web CI](https://github.com/nguyentuio1/learning_app/actions/runs/43897305057) · [Mobile #215](https://github.com/nguyentuio1/learning_app/pull/215) |
| Flutter completion | Customer screen, responsive/error-state and Station Operator PRs were open against the LA-43 story branch. | [Customer screens #223](https://github.com/nguyentuio1/learning_app/pull/223) · [Responsive UI #214](https://github.com/nguyentuio1/learning_app/pull/214) · [Station Operator #221](https://github.com/nguyentuio1/learning_app/pull/221) |

### 4.2 Chapter handoff

Trần Nguyễn owns the web flows and chapter consolidation. Nhân Lưu Thiện supplies customer mobile and order flows under LA-169/171. Hung Nguyen supplies station operator and mobile AI flows under LA-170/160/167. TBA Review supplies backend design and approves the consolidated chapter. Phan Van Khai supplies validation evidence. Each contribution should identify the source commit or PR, the affected Confluence page and version, and the verification status. [LA-254](https://hungndemail.atlassian.net/browse/LA-254) · [AI handoff LA-342](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=18579461) · [Test plan](../testing/test-plan.md) · [Chapter V](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927583)

This draft can be reviewed while the remaining mobile flows and acceptance evidence are collected. Publish the reviewed content to the existing Chapter IV page and record the PR, commit, Confluence version and verification date in the Jira handoff. TBA Review coordinates final report assembly and approval under [LA-204](https://hungndemail.atlassian.net/browse/LA-204).

### 4.3 Historical references

The previous layouts and dated implementation snapshots remain available for comparison. [Sprint 5 snapshot](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=18055306) · [Chapter IV version history](https://hungndemail.atlassian.net/wiki/spaces/~712020594ceef270154d55954fe04ed3d2e61b/history/11993109)
