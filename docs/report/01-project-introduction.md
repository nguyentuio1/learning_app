# I. Project Introduction

> **Current lifecycle correction (19 November 2026):** Jira Sprint 8 (id 8) is CLOSED at `2026-11-18T23:09:56.278Z` (06:09:56 Asia/Ho_Chi_Minh). Older active-sprint statements below are dated historical planning snapshots. Unfinished acceptance remains in backlog/In Progress. See the [current report index](README.md) for application evidence and final review targets.

Owner: [LA-252](https://hungndemail.atlassian.net/browse/LA-252), TBA Review. Evidence checked on 2026-11-16 against `develop` at `8413d84` and the active Jira Sprint 7. Publication target: the existing [Project Introduction](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14188546), not a new chapter. Final report assembly and human approval remain [LA-204](https://hungndemail.atlassian.net/browse/LA-204).

## 1. Overview

### 1.1 Project information

| Item | Reference |
|---|---|
| Product | SmartDroneDelivery — AI-powered Drone Delivery Management Platform |
| Purpose | Manage the business lifecycle of lightweight package delivery between landing stations |
| Repository | [nguyentuio1/learning_app](https://github.com/nguyentuio1/learning_app); `develop` integrates completed stories and `main` carries releases |
| Work tracking | [Jira LA](https://hungndemail.atlassian.net/jira/software/projects/LA/boards/2); Sprint 7 is active, 16–23 November 2026; Sprint 6 is closed |
| Stack | ASP.NET Core 8, PostgreSQL, React/Ant Design, Flutter/Riverpod, SignalR, MinIO and Docker |
| Scope source | [SRS sections 1–2](../requirements/SRS.md), [business processes](../design/business-processes.md) and the [existing Product Overview](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14057518) |

Sprint dates come from Jira; a fixed two-week cadence or an old overall project date range is not assumed. Academic identifiers and supervisor details belong in the final report's verified cover metadata.

### 1.2 Project team

Current responsibilities follow Jira ownership and the documentation handoff recorded in LA-204/253/254/255.

| Jira member | Sprint 7 responsibility |
|---|---|
| TBA Review | Backend/AI implementation, integration review, introduction/PMP and final report consolidation/approval |
| Phan Van Khai | QA and acceptance evidence; requirements/business-rule/test chapter consolidation |
| Trần Nguyễn | Web consumers and web/design chapter consolidation |
| Nhân Lưu Thiện | Customer mobile completion, responsive/error states and customer-mobile documentation |
| Hung Nguyen | Mobile ETA/assistant, station-operator completion and station/mobile-AI documentation/formatting |

This allocation describes project work; it does not invent formal academic roles, student IDs or meeting attendance.

## 2. Product background

A delivery involves several distinct handoffs: a customer requests delivery for a package, a dispatcher reviews and schedules it, a station operator receives it, the delivery executes between stations, and the recipient confirms receipt. These actions need a shared order identity, valid state transitions and a chronological record. Arrival at the destination station and recipient receipt are separate business events.

SmartDroneDelivery gives customers visibility into their own orders and gives operational roles the tools appropriate to their responsibilities. Scheduling must respect drone capacity, mission conflicts and station resources. Tracking and notifications communicate recorded progress; failure handling retains the history of unsuccessful attempts and subsequent recovery. This background follows the implemented business processes and the SRS, rather than an assumed commercial deployment.

The project adds informational AI assistance to those authoritative records: an ETA prediction, a transient delivery summary and a customer assistant. Access rules, source freshness and failure handling apply to AI features as well as ordinary endpoints. A model answer cannot approve, cancel or operate a flight.

## 3. Existing systems

The following comparison uses public primary descriptions checked on 2026-11-16. It is a qualitative design reference, not a measured cost/performance benchmark or an assertion that the team deployed either system.

| System and source | Relevant published capabilities | Lesson for this project's scope |
|---|---|---|
| [Wing technology](https://wing.com/technology) | Describes automatic route selection, package handoff at pickup stations and central oversight of delivery flights. | Coordinate order, package and station handoffs while keeping customer-facing progress understandable. Aircraft automation remains outside SmartDroneDelivery's hardware-control scope. |
| [Matternet software platform](https://www.matternet.com/our-system-software-platform) | Describes request intake, routing/monitoring and separate mission-control, ground-operations and management interfaces. | Separate operational responsibilities and preserve a traceable delivery record. This project does not reproduce its aircraft command/control or claim its regulatory approvals. |

These systems illustrate the relationship between customer requests, ground handling and operational visibility. SmartDroneDelivery's assessed deliverable is a business-management platform with testable web/mobile/API workflows. Vendor speed, payload, availability and certification claims are not transferred to this product.

## 4. Business opportunity

The project addresses a concrete coordination need: maintain one traceable delivery record across customers, dispatchers, station operators and managers. Role-appropriate screens, chronological status logs, resource checks and recipient confirmation help each participant understand which step has actually completed.

The intended benefits are clearer handoff accountability, less manual reconciliation of conflicting status information, and a consistent basis for operational reporting. ETA and summaries can make recorded information easier to consume when their source and limitations are clear. These are product goals; no revenue forecast, measured time saving or live-service performance result is claimed by this chapter.

## 5. Software product vision

SmartDroneDelivery supports delivery request management, package management, delivery execution, delivery tracking and delivery confirmation. Its five modules are Customer and Delivery Order Management; Package and Delivery Workflow Management; Landing Station Management; Delivery Tracking and Confirmation; and Analytics and AI-assisted Services.

The SRS defines the target capabilities. Implemented availability, feature flags, model quality, deployment and end-to-end acceptance are evidenced separately through API runbooks, commit-specific CI and test reports. A merged API contract does not establish an enabled AI service.

## 6. Project scope and limitations

### 6.1 Major features

Customer workflows cover accounts, addresses, packages, owned orders, tracking and confirmation. Operational workflows cover review, scheduling, drone/station resources, telemetry, failures and management reporting. Standardized external integration endpoints have their own authentication and authorization boundary. Detailed requirements and actors remain in SRS sections 2–3 rather than being duplicated here.

### 6.2 Constraints and exclusions

- Packages must satisfy the implemented package-size validation and the assigned drone's payload/range constraints; one drone cannot have overlapping missions. Exact limits belong to the applicable API/business-rule specification.
- The platform manages delivery operations and simulator/integration events. Direct flight-hardware control, airworthiness and aviation approvals are outside this project scope.
- Ownership, persisted role checks and secret handling apply to every client and AI boundary. Real-model/provider activation and chat retention approval are explicit release gates.
- Flutter analysis/tests establish code-level checks. Physical-device coverage, signing and store publication require their own evidence. The earlier chapter's iOS/Mac-availability exclusion is retained as an unverified planning assumption for the owner to confirm in final platform/cover metadata; generated platform folders do not settle that decision.
- The final six-part report, numbering/TOC and two human proofreaders remain LA-204 acceptance work. Updating these two chapters does not assert that the entire report is accepted.
