# Web wireframe index

Low-fidelity white/grey wireframes: editable sources and PNG previews.

LA-75 under LA-14 · Web only · 20 screens for 3 actors. No Mobile or application implementation.

[Wireframe conventions](design-system.md)

## Dispatcher

[Editable Draw.io source](dispatcher/dispatcher-wireframes.drawio) · [Screen overview](dispatcher/contact-sheet.png)

| # | Screen | Editable SVG | PNG preview |
|---:|---|---|---|
| 1 | 01 · Login | [SVG](dispatcher/01-login.svg) | [PNG](dispatcher/01-login.png) |
| 2 | 02 · Dispatcher dashboard | [SVG](dispatcher/02-dashboard.svg) | [PNG](dispatcher/02-dashboard.png) |
| 3 | 03 · Pending orders | [SVG](dispatcher/03-pending-orders.svg) | [PNG](dispatcher/03-pending-orders.png) |
| 4 | 04 · Order detail and approval | [SVG](dispatcher/04-order-detail-approval.svg) | [PNG](dispatcher/04-order-detail-approval.png) |
| 5 | 05 · Schedule delivery | [SVG](dispatcher/05-schedule-delivery.svg) | [PNG](dispatcher/05-schedule-delivery.png) |
| 6 | 06 · Assign drone | [SVG](dispatcher/06-assign-drone.svg) | [PNG](dispatcher/06-assign-drone.png) |
| 7 | 07 · Active deliveries | [SVG](dispatcher/07-active-deliveries.svg) | [PNG](dispatcher/07-active-deliveries.png) |
| 8 | 08 · Live delivery tracking | [SVG](dispatcher/08-live-tracking.svg) | [PNG](dispatcher/08-live-tracking.png) |
| 9 | 09 · Failed delivery resolution | [SVG](dispatcher/09-failure-handling.svg) | [PNG](dispatcher/09-failure-handling.png) |

## Logistics Manager

[Editable Draw.io source](logistics-manager/logistics-manager-wireframes.drawio) · [Screen overview](logistics-manager/contact-sheet.png)

| # | Screen | Editable SVG | PNG preview |
|---:|---|---|---|
| 1 | 01 · Logistics dashboard | [SVG](logistics-manager/01-dashboard.svg) | [PNG](logistics-manager/01-dashboard.png) |
| 2 | 02 · Statistics and filters | [SVG](logistics-manager/02-statistics-filters.svg) | [PNG](logistics-manager/02-statistics-filters.png) |
| 3 | 03 · Reports and analytics | [SVG](logistics-manager/03-reports-analytics.svg) | [PNG](logistics-manager/03-reports-analytics.png) |
| 4 | 04 · Delivery detail | [SVG](logistics-manager/04-delivery-detail.svg) | [PNG](logistics-manager/04-delivery-detail.png) |

## System Administrator

[Editable Draw.io source](system-administrator/system-administrator-wireframes.drawio) · [Screen overview](system-administrator/contact-sheet.png)

| # | Screen | Editable SVG | PNG preview |
|---:|---|---|---|
| 1 | 01 · Administration dashboard | [SVG](system-administrator/01-dashboard.svg) | [PNG](system-administrator/01-dashboard.png) |
| 2 | 02 · Users | [SVG](system-administrator/02-users.svg) | [PNG](system-administrator/02-users.png) |
| 3 | 03 · Create staff user | [SVG](system-administrator/03-create-user.svg) | [PNG](system-administrator/03-create-user.png) |
| 4 | 04 · User detail | [SVG](system-administrator/04-user-detail.svg) | [PNG](system-administrator/04-user-detail.png) |
| 5 | 05 · Roles | [SVG](system-administrator/05-roles.svg) | [PNG](system-administrator/05-roles.png) |
| 6 | 06 · Audit logs | [SVG](system-administrator/06-audit-logs.svg) | [PNG](system-administrator/06-audit-logs.png) |
| 7 | 07 · System monitoring | [SVG](system-administrator/07-system-monitoring.svg) | [PNG](system-administrator/07-system-monitoring.png) |

## Scope notes

- Jira LA-75's user-provided screenshot also requires Customer web. Customer is not included yet: its flow needs confirmation before design, so the full LA-75 scope is not complete.
- All record values are placeholders. Manager views and audit logs are read-only; no drone flight controls are designed.
- Return-to-origin ending as CANCELLED remains a proposed rule. Backup retention/restore and report export remain unconfirmed.

## References

- User flows: [Dispatcher](../../user-flows/dispatcher.md), [Logistics Manager](../../user-flows/logistics-manager.md), [System Administrator](../../user-flows/system-administrator.md).
- Requirements: [SRS](../../../requirements/SRS.md), [Use cases](../../../requirements/use-cases.md).
- Architecture/database: [Architecture](../../../architecture/architecture.md), [ERD](../../../architecture/erd.md), [DBML](../../../architecture/erd.dbml).
- Interaction rules: [Business processes](../../business-processes.md), [Authentication API](../../../api/auth-api.md).
