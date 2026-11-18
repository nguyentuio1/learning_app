# Proofreading, Formatting, and Terminology Checklist (LA-255 / LA-362)

Owners: [LA-255](https://hungndemail.atlassian.net/browse/LA-255) & [LA-362](https://hungndemail.atlassian.net/browse/LA-362), Hung Nguyen. Parent stories: [LA-204](https://hungndemail.atlassian.net/browse/LA-204) / [LA-351](https://hungndemail.atlassian.net/browse/LA-351) (TBA Review retains final report consolidation, export, and approval).

---

## 1. Cross-document structure and numbering

| Report part | Repository source | Confluence target | Formatting, numbering & reconciliation status |
|---|---|---|---|
| **Overview / Contents** | [README.md](README.md) | [Page 98403](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) | Reconciled in repo to `develop` `e3c647c` (18 Nov 2026); manual Confluence `98403` sync pending. |
| **I. Project Introduction** | [01-project-introduction.md](01-project-introduction.md) | [Page 14188546](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14188546) | Numbered `1.`–`6.` with sentence-case headings (`Checked`; dated baseline `8413d84` retained as snapshot). |
| **II. Project Management Plan** | [02-project-management-plan.md](02-project-management-plan.md) | [Page 11927566](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927566) | Numbered `1.`–`8.` with sentence-case headings (`Checked`; dated baseline `8413d84` retained as snapshot). |
| **III. Software Requirements Specification** | [SRS.md](../requirements/SRS.md) | [Page 11993093](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993093) | Cross-referenced from Chapters I, II, and IV (`Checked`). |
| **IV. Software Design Description** | [04-software-design-description.md](04-software-design-description.md) | [Page 11993109](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993109) | Numbered `1.`–`4.` (`4.3 Historical references`), sentence-case headings aligned with Chapters I–II (`Checked`; §4.1 at `894ceab` preserved as historical baseline). |
| **V. Software Testing Documentation** | [05-software-testing-documentation.md](05-software-testing-documentation.md) · [test-plan.md](../testing/test-plan.md) · [test-report.md](../testing/test-report.md) | [Page 11927583](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927583) | Linked to Chapter V ledger (`07f38d1`); final alignment with `LA-175` / `LA-188` live results pending execution. |
| **VI. Release Package & User Guides** | [06-release-package-and-user-guides.md](06-release-package-and-user-guides.md) | [Page 14024801](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14024801) | Linked to Chapter VI (`07f38d1`); final export & TOC assembly remain a pending gate under LA-204. |

---

## 2. Mobile and station terminology consistency

All repository report chapters and Sprint 7–8 mobile/station user-flow sources should use the following canonical terms:

| Term / Symbol | Canonical usage | Notes / verified alignment |
|---|---|---|
| **Flutter Customer app** | `Customer app` (`AppFlavor.customer`) | Owns customer orders, tracking, delivery confirmation, ETA, and AI assistant (`LA-160`, `LA-167`, `LA-169`, `LA-171`). |
| **Flutter Station Operator app** | `Station Operator app` (`AppFlavor.operator`) | Owns station availability, slot maintenance, origin intake, destination pickup, expired-parcel clearing, and operator notifications (`LA-170`). |
| **Landing station & slot states** | `Landing station` with `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR` slots | Manual operator toggle is restricted to `EMPTY <-> ERROR`. |
| **Order lifecycle transitions** | `APPROVED -> READY_FOR_TAKEOFF` (intake, requires `PLANNED` mission), `APPROVED -> FAILED` (intake rejection), `IN_TRANSIT -> ARRIVED` (station arrival), `ARRIVED -> DELIVERED` (recipient/pickup confirmation) | `IN_AIR` is a `FlightMission.FlightStatus` (not a `DeliveryOrder.Status`); do not mix order/mission states or use non-backend labels (`IN_ORIGIN_STATION`, `REJECTED`, `COMPLETED`). |

---

## 3. Diagrams, links, and status-distinction check

- **Relative links in `docs/report/*.md`:** Audited against `origin/develop` (`b5e54fa`); 0 broken relative links.
- **Mermaid diagrams in `docs/design/user-flows/*.md` and `docs/report/*.md`:** All `mermaid` code fences are properly closed and syntactically balanced.
- **Implementation vs. integration vs. deployment vs. acceptance:** [README.md](README.md) explicitly separates merged `develop` code (`e3c647c`), Docker smoke packaging (`LA-47`), and open live acceptance/rehearsal gates (`LA-175`, `LA-188`).

---

## 4. Proofreader confirmations & Confluence page/version ledger (LA-362 / LA-204)

> [!IMPORTANT]
> Per **LA-362** and **LA-204**, CI runs, authorship, and automated agent reviews do **not** count as human proofreading confirmations. Leave Proofreader #1 and Proofreader #2 open until two distinct team members record their actual confirmation on the published report/page versions.

### 4.1 Human proofreading confirmations

| Role | Reviewer name | Date | Target page(s) / version(s) | Decision & notes | Status |
|---|---|---|---|---|---|
| Formatting & index reconciliation | Hung Nguyen (`LA-255` / `LA-362`) | 2026-11-18 | Repo `docs/report/*` at `develop` `e3c647c` (prior pass: PR #233 merged `229d1cf`) | Cross-document numbering, terminology, and link pass prepared in repo; does **not** constitute `Overview 98403` publication or final proofreading | Source prepared; human proofreaders pending |
| Proofreader #1 | *Pending distinct member* | *Pending* | *Exact Confluence page IDs/versions* | *Record actual decision/notes* | **Open** |
| Proofreader #2 | *Pending distinct member* | *Pending* | *Exact Confluence page IDs/versions* | *Record actual decision/notes* | **Open** |

### 4.2 Confluence page/version ledger & remaining blockers

Repository index edits do **not** equate to `Overview 98403` publication or final proofreading completion.

| Confluence page | Page ID | Last observed / readback version | Sync status against `develop` `e3c647c` | Remaining blocker |
|---|---|---|---|---|
| Overview (Six-chapter index) | `98403` | *Pending manual publish* | Needs manual sync after sibling chapter updates | Sibling updates + `LA-175`/`LA-188` alignment + 2 proofreaders |
| I. Project Introduction | `14188546` | *Pending readback/publish* | Maintained by `LA-252` | Record published version after sync |
| II. Project Management Plan | `11927566` | *Pending readback/publish* | Maintained by `LA-252` | Record published version after sync |
| III. Software Requirement Specification | `11993093` | `v13` (LA-360 readback, 2026-11-18) | Updated via LA-360 (includes lookup & backup updates) | Human proofreader confirmation on updated version |
| IV. Software Design Description | `11993109` | `v13` (LA-360 readback, 2026-11-18) | Updated via LA-360 (sibling readbacks: Database `1507330` `v8`, AI Handoff `18579461` `v7`) | Human proofreader confirmation on updated version |
| V. Software Testing Documentation | `11927583` | *Pending readback/publish* | Pending `LA-175` / `LA-188` results | Needs actual `LA-175` & `LA-188` evidence |
| VI. Release Package & User Guides | `14024801` | `v4` (18 Nov 2026 audit) | Re-verify after final export | Final export & `LA-204` approval |
