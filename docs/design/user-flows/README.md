# User Flows and Screen Navigation Index

This directory contains the role-based screen navigation and user flows for the **SmartDroneDelivery** platform across its **5 authorized actors**.

For the consolidated UI/UX screen flows document incorporating recent UI fixes (including responsive narrow-screen cards, draft-safe order creation, staff historical lookup, and action queue refreshing), refer to:
- **Canonical Design Document:** [UI/UX & Implemented Screen Flows](../ui-screen-flows.md)
- **Confluence Mirror:** [Page 11894809 (3.1 UI/UX & Screen Flows)](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11894809)

## Individual Role User Flows

| Actor | Flow Document | Primary Interface | Key Capabilities Covered |
| :--- | :--- | :--- | :--- |
| **Customer** | [customer.md](customer.md) | Flutter Mobile App & React Web Portal | Account access, package & address prerequisites, draft-safe order creation, one-time secure pickup codes, live flight tracking, AI assistant. |
| **Dispatcher** | [dispatcher.md](dispatcher.md) | React Web Portal (`/dispatcher`) | Pending order review/approval/rejection, departure scheduling, drone assignment with battery & payload filters, active flight radar, failure handling. |
| **Station Operator** | [station-operator.md](station-operator.md) | Flutter Mobile App (`/operator`) & Web Portal | Assigned station context, unassigned operator guard, Station Action Queue, origin package intake, destination pickup handover, expired parcel clearing, slot status maintenance (`EMPTY <-> ERROR`). |
| **Logistics Manager** | [logistics-manager.md](logistics-manager.md) | React Web Portal (`/manager`) | KPI dashboard, 30-day/7-day/today reporting presets within Vietnam timezone bounds (`Asia/Ho_Chi_Minh`), authorized staff tracking-number lookup, contextual ETA and Gemini summaries. |
| **System Administrator** | [system-administrator.md](system-administrator.md) | React Web Portal (`/admin`) | User management (lock/unlock), 5-role segregation assignment, security audit log search with PostgreSQL B-tree indexing, external API key management, container health checks (`/healthz`). |
