# Proofreading, formatting and publication checklist (LA-204 / LA-351)

> **Historical checklist; current correction 25 November 2026:** This file records the earlier 71-page review packet and publication ledger. Its pending-proofreader and integration labels below are historical. LA-351 is integrated through PR #288. The owner explicitly accepted LA-204 consolidation/export in Jira comment 10272; LA-204 remains Done. Later report readability/member-metadata corrections are recorded in comment 10274 and the [v1.0.0 release assets](https://github.com/nguyentuio1/learning_app/releases/tag/v1.0.0). The [report index](README.md) records current asset digests. This correction does not invent peer confirmation, an instructor outcome or new Confluence versions. Flutter MinIO acceptance is also complete within the [recorded runtime scope](../testing/LA346-flutter-photo-completion.md); AI, device/WebSocket and actual team rehearsal remain separate.

Review packet: 19 November 2026, documentation baseline `a90668c`, combined application `10c77d0`. Earlier source baselines are historical. The existing six-chapter structure and chapter numbering are retained; current corrections distinguish implementation, integration, runtime, deployment and acceptance.

## 1. Structure and terminology

The [report index](README.md) links Chapters I-VI and the existing Confluence pages. Customer app and Station Operator app remain distinct flavors. Canonical slot states are `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR`; manual transitions are `EMPTY <-> ERROR`. Order `ARRIVED` differs from `DELIVERED`; mission `IN_AIR` is not an order status. Diagrams/history remain on the original pages, with dated current corrections above earlier snapshots.

## 2. Verification scope

Application CI `44414947666` passed backend/web/mobile and its tested tree equals `10c77d0`; documentation CI `44417197181` passed before PR #284 merged as `a90668c`. Flutter: 425 passed, no analyzer warnings/errors (30 information findings). [PerformanceTestReport section 5.3](../testing/PerformanceTestReport.md#53-recorded-reference-workstation-and-postgresql16-docker-validation) records scoped reference passes while preserving old FAIL outputs. Browser API evidence does not establish native installation or SignalR delivery through the loopback preview. Flutter MinIO, actual enabled AI and actual team rehearsal remain outstanding acceptance.

Jira Sprint 8 (id 8) CLOSED at `2026-11-18T23:09:56.278Z` (06:09:56 on 19 November, Asia/Ho_Chi_Minh). Unfinished acceptance remains open in backlog; closure does not prove acceptance.

## 3. Links and publication checks

Published pages were read back in full HTML on 19 November 2026. Existing page IDs/history were preserved, including the original diagrams and links. Current source links are pinned to `a90668c`; historical sources remain pinned to their earlier commits. Repository relative-link and fence validation is recorded with this change. Full readbacks and generated DOCX/PDF review exports are in local `artifacts/la204-20261119`; exported copies are not signed/approved submissions.

## 4. Proofreader confirmations and page/version ledger

### 4.1 Human proofreading confirmations

| Role | Actual member | Date | Exact review target | Decision | Status |
|---|---|---|---|---|---|
| Proofreader 1 | Pending distinct real member | Pending | Versions below and generated review artifact hash | Pending | **Open** |
| Proofreader 2 | Pending distinct real member | Pending | Versions below and generated review artifact hash | Pending | **Open** |

CI, authorship, Jira Done subtasks and automated agent review do not supply either human confirmation. Each reviewer must inspect the six parts, contents, numbering, links, diagrams and scope, then record actual findings/decision in the existing Overview record. LA-204 final approval must identify the final exported artifact and exact hash after findings are resolved. Preserve credit for completed contributor work.

### 4.2 Confluence page/version ledger & remaining blockers

| Page | Existing ID | Actual published/readback version | Result |
|---|---|---|---|
| Overview | [98403](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) | **v8** | Current baseline/evidence and actual Sprint 8 closure; two confirmations remain open |
| I. Introduction | [14188546](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14188546) | **v3** | Current pointer; old Sprint 7 plan explicitly historical |
| II. Management plan | [11927566](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927566) | **v6** | Actual Sprint 8 closure; old active labels historical |
| III. Requirements | [11993093](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993093) | **v14** | Current integration/runtime/performance scope; diagrams/history retained |
| IV. Design | [11993109](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993109) | **v14** | Atomic allocator and current flows; diagrams/history retained |
| V. Testing | [11927583](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927583) | **v16** | Actual PR #252 merge/reference passes; old FAIL evidence retained |
| VI. Release/guides | [14024801](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14024801) | **v9** | LA-48/203 integrated; actual runtime and remaining acceptance |
| NFR | [11960371](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11960371) | **v9** | Original performance criterion unchanged; unsupported fixed test counts/scaling claims corrected |

Remaining report acceptance: two actual member confirmations and approved final export. LA-351 source PR still needs current review/CI/integration before reporting that story integrated. Publishing pages is a separate completed action.

Template review export 2 includes ten detailed child specifications and two real runtime figures. Cover metadata still requires actual student IDs, formal TBA Review name, supervisor/external-supervisor and academic project code. Expanded published targets: Acknowledgement `14024759 v2`, System Design `14090250 v3`, Database Design `14188589 v5`; Definitions `14024779 v1` retained.
