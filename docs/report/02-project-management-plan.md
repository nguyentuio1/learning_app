# II. Project Management Plan

> **Current lifecycle correction (19 November 2026):** Jira Sprint 8 (id 8) is CLOSED at `2026-11-18T23:09:56.278Z` (06:09:56 Asia/Ho_Chi_Minh). Older active-sprint statements below are dated historical planning snapshots. Unfinished acceptance remains in backlog/In Progress. See the [current report index](README.md) for application evidence and final review targets.

Owner: [LA-252](https://hungndemail.atlassian.net/browse/LA-252), TBA Review. Evidence checked on 2026-11-16. Publication target: the existing [Project Management Plan](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927566). Scope and quality sources: [SRS](../requirements/SRS.md), [test plan](../testing/test-plan.md), [test report](../testing/reports/test-report.md) and [Sprint 7 handoff](../api/history/sprint-7-api-contract.md).

## 1. Overview and scope

SmartDroneDelivery manages the business delivery lifecycle through backend APIs, web and Flutter clients. The work covers analysis/design, management modules, workflow/tracking, mobile/AI and testing/deployment. Direct drone flight-hardware control is excluded. This plan governs how the team coordinates and verifies the scoped software.

## 2. Management approach

### 2.1 Scrum planning and progress

The backlog, sprint assignment, acceptance criteria and assignee are maintained in Jira LA. Sprint 7 is active from 16–23 November 2026; Sprint 6 is closed. Planning uses the actual Jira dates and capacity rather than assuming all sprints last two weeks. Parent stories express business outcomes; subtasks identify implementation, client and QA responsibilities. A story remains open while required acceptance or integration work is outstanding.

Before implementation, the owner checks the current contract, dependencies and existing tasks to avoid duplicate work. Work uses the real Jira key in branches/commits/PRs. Module owners update progress and concrete blockers on the existing issue. Dependencies are resolved in order, with completed stories integrated into `develop` during the same work cycle.

### 2.2 Quality management

Review checks behavior against the contract, persisted role/ownership boundaries, failure states and relational invariants. Backend concurrency/transaction cases use real PostgreSQL tests. Web and mobile checks cover the applicable state, error and permission transitions. Unit tests, a merge, a deployment and end-to-end business acceptance are recorded as separate evidence.

The actual GitHub Actions workflow runs three checks for PRs targeting `develop`, `main` and `story/**`: **Backend tests**, **Web lint, test and build**, and **Mobile analyze and test**. Backend CI uses PostgreSQL 16 and executes the PostgreSQL test set. All three checks must succeed on the current integration head; review findings must be resolved. Test totals belong to dated commit/run reports, not a fixed “394 tests” project claim.

### 2.3 Team preparation and knowledge transfer

Each module owner supplies the relevant API/UI/design/testing changes and their source commit/PR. Backend owners provide contract/error examples; web/mobile owners check consumer behavior; QA records acceptance evidence. A short handoff or pairing session is planned when an interface changes. Meeting attendance or training completion is not inferred from the existence of this plan.

## 3. Work breakdown structure

| Work package | Deliverables |
|---|---|
| WP1 — Analysis and design | SRS, business processes, architecture, ERD and wireframes |
| WP2 — Management modules | Accounts, roles, addresses, packages, stations, slots and drones |
| WP3 — Workflow and tracking | Review, state transitions, scheduling, failures, telemetry, confirmation and notifications |
| WP4 — Mobile and AI | Customer/station Flutter clients, ETA, delivery summary and customer assistant |
| WP5 — Testing and deployment | Test strategy/cases/reports, PostgreSQL concurrency coverage, CI and verified deployment configuration |

The WBS maps to SRS section 6 and existing Jira work packages. A package's inclusion in scope does not mean all its delivery/activation gates have passed.

## 4. Responsibility assignments

R = responsible for preparing the work; A = accountable for integration/final decision; C = consulted; I = informed. This operating plan follows current Jira ownership and the documentation allocation recorded on LA-204/253/254/255.

| Activity | R | A | C | I / evidence |
|---|---|---|---|---|
| Backend/AI APIs and contracts | TBA Review | TBA Review | Client owners, Phan Van Khai | Team; LA-158/162/165, LA-342 |
| Web consumers and web/design chapter | Trần Nguyễn | TBA Review | Backend, QA, mobile owners | Team; LA-159/163/166, LA-254 |
| Customer mobile completion/error states | Nhân Lưu Thiện | TBA Review | Backend, Hung Nguyen, QA | Team; LA-169/171 |
| Mobile ETA/assistant and station flows | Hung Nguyen | TBA Review | Backend, Nhân Lưu Thiện, QA | Team; LA-160/167/170 |
| Test evidence and requirements/business-rule chapter | Phan Van Khai | TBA Review | All module owners | Team; LA-161/164/168/172, LA-253 |
| Mobile/station documentation and formatting | Hung Nguyen; Nhân Lưu Thiện supplies customer-mobile changes | TBA Review | Trần Nguyễn, Phan Van Khai | Team; LA-255 and module handoffs |
| Introduction/PMP and final report assembly | TBA Review | TBA Review | Module owners and the two human proofreaders to be recorded | Team; LA-252, LA-204 |

Final report acceptance requires actual proofreading evidence from two members. This plan does not invent their names or claim that review has already happened.

## 5. Communications management plan

The table is the operating plan; it is not a record that meetings, staging demos or particular chat channels occurred. Jira, GitHub and Confluence links carry the durable decisions and evidence.

| Activity/channel | Planned cadence | Participants/owner | Output |
|---|---|---|---|
| Daily progress update | Each working day; synchronous standup if the team agrees | Module owners; TBA Review coordinates | Completed work, next work and concrete blockers on Jira |
| Sprint planning | Before starting the Jira sprint | TBA Review and module owners | Scope, assignees, dependencies and acceptance checks |
| Sprint review/demo | End of the actual Jira sprint | Team and relevant reviewers | Demonstration linked to commit/environment evidence; acceptance findings |
| Retrospective | After the sprint review | Whole team | Specific improvement actions in existing/new nonduplicate Jira issues |
| GitHub PR review | On each reviewable change | Author and reviewer | Findings, CI evidence, resolution and integration reference |
| Confluence documentation | On module handoff and final consolidation | Module contributors; TBA Review approves final assembly | Source commit/PR plus published page/version links |
| Optional team chat | When used by the team | Relevant module owners | Coordination only; decisions are copied to Jira. Discord/Slack channel names and actual schedules are not verified by this audit. |

## 6. Configuration and change management

- `develop` is the integration branch; `main` is the release branch. Subtask PRs may target their shared `story/LA-*` branch. Shared branches use merge commits when incorporating dependencies/latest `develop`; no rebase or force-push without explicit authorization.
- Branches and commits use the actual issue key, for example `feature/LA-165-customer-ai-assistant` and `feat(LA-165): add customer assistant`. Documentation reuses LA-252–255; the final implemented API guide remains LA-183 in its planned sprint.
- A completed story is reviewed and merged into `develop` in the same cycle after all required subtasks/acceptance criteria and current-head backend/web/mobile CI succeed. Verify remote ancestry and that the merged tree matches the tested result. Held/unfinished work cannot enter indirectly through another story.
- The inspected `develop` ruleset requires one approving review, resolved review threads and the three named status checks. Consult live rules before every merge. Admin bypass requires explicit user authorization and never waives CI; teammates' PRs follow normal review.
- Requirements/contracts changed by implementation are reconciled with their document owners. Repository commits do not automatically update Confluence; publication is explicit and retains page/version evidence.
- Credentials remain outside committed source and report examples. Runtime configuration, provider/model activation, retention, signing and deployment acceptance are reviewed separately. Do not paste customer codes, contact details or real secrets into prompts, logs or documentation.

## 7. Risk register

| Risk | Owner | Mitigation / evidence | Status on 2026-11-16 |
|---|---|---|---|
| Contract drift between backend, web and mobile | TBA Review; client owners contribute | Contract-first handoff, consumer review and permission/error-state tests; LA-342 and module tasks | Monitored |
| Delayed story integration causes dependency conflicts | TBA Review | Merge completed stories promptly in dependency order; incorporate latest develop, preserve shared history and require current-head CI | Monitored |
| AI provider/model quality is not established | TBA Review; Phan Van Khai verifies acceptance | Default-off local adapters, bounded structured output, source/authorization rechecks and explicit real-model evaluation before activation | Open activation gate |
| Chat retention/provider handling is unapproved | TBA Review | Keep generation/storage disabled until retention, user deletion, cleanup, backup/restore and provider handling are approved/verified | Open activation gate |
| Timing-sensitive CI or environment differences | TBA Review, LA-173 | Inspect failure logs, reproduce the failing boundary, preserve assertions/deadlines and require all three checks; record local PostgreSQL 18 separately from CI PostgreSQL 16 | Monitored; dated failures retained |
| Report/Confluence drifts from repository state | TBA Review; each module owner | Reuse LA-252–255, cite source commits and explicitly publish changed page versions | Active reconciliation |
| Simulator/tests are mistaken for hardware or deployment acceptance | TBA Review and QA | Keep hardware exclusion explicit; require separate environment/full-flow evidence under LA-172 | Open until relevant acceptance |
| Final report lacks metadata, numbering or human proofreading | TBA Review | Assemble six parts under LA-204, verify cover metadata/TOC and record two actual member reviews | Open final-report gate |

## 8. Evidence and maintenance

Sources: the linked repository documents; [CI workflow at the audited develop commit](https://github.com/nguyentuio1/learning_app/blob/8413d84bfcc48894a2025765da277b0e4af63514/.github/workflows/ci.yml); actual Jira Sprint 7/assignees; and the live `develop` ruleset read on 2026-11-16. Chapter updates are maintained in this repository and explicitly published to the existing Confluence pages. LA-204 records final compilation/acceptance; these chapter sources do not assert a completed six-part report.
