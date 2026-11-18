# Project report chapter sources

## Six-part contents

The consolidated report follows the existing six-chapter course structure. LA-351 reconciliation on **19 November 2026** uses integrated documentation baseline [`a90668c`](https://github.com/nguyentuio1/learning_app/commit/a90668c) and combined application baseline [`10c77d0`](https://github.com/nguyentuio1/learning_app/commit/10c77d0). Earlier dated chapter sources remain historical snapshots. The current published pages are a review copy; final two-member proofreading and the approved export remain open under LA-204.

| Part | Repository source | Existing Confluence chapter |
|---|---|---|
| I. Project Introduction | [Chapter I](01-project-introduction.md) | [14188546](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14188546) |
| II. Project Management Plan | [Chapter II](02-project-management-plan.md) | [11927566](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927566) |
| III. Software Requirement Specification | [SRS](../requirements/SRS.md) | [11993093](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993093) |
| IV. Software Design Description | [Chapter IV](04-software-design-description.md) | [11993109](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993109) |
| V. Software Testing Documentation | [Chapter V](05-software-testing-documentation.md), [test plan](../testing/test-plan.md), [execution report](../testing/test-report.md) | [11927583](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927583) |
| VI. Release Package & User Guides | [Chapter VI](06-release-package-and-user-guides.md) | [14024801](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14024801) |

The existing [Confluence Overview (98403)](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=98403) is the single chapter-level contents page; do **not** create duplicate chapter pages.

## Implementation, integration, deployment, and acceptance status

- **Integrated source:** LA-45 performance/test evidence (PR #252, merge `8466a47`), LA-48 technical documentation, LA-203 installation/user guides (PR #280), LA-366 atomic tracking numbers (PR #281), Customer journey (PR #277), Operator journey (PR #278), assistant availability (PR #282) and station scope (PR #283) are in `develop`. LA-351 itself still requires its story PR review, current CI and integration.
- **Executed verification:** [Application CI 44414947666](https://github.com/nguyentuio1/learning_app/actions/runs/44414947666) passed backend, web and mobile; the tested tree equals actual application merge `10c77d0`. Full combined Flutter suite: **425 passed**; analyzer: no warnings/errors, 30 existing information findings. [Documentation CI 44417197181](https://github.com/nguyentuio1/learning_app/actions/runs/44417197181) passed before PR #284 merged as `a90668c`. [Integration review](../testing/Sprint8IntegrationReview.md) records exact source scope.
- **Scoped performance:** [PerformanceTestReport section 5.3](../testing/PerformanceTestReport.md#53-recorded-reference-workstation-and-postgresql16-docker-validation) records unchanged source `f7502b7`, application `10700d2`: three barrier repeats each 15/15 PASS; PostgreSQL16 Docker mixed k6 all 15 thresholds PASS and 0/17111 HTTP failures. BCrypt cost11 and the original p95 <3000 ms criterion were unchanged. Earlier FAIL outputs remain historical; no authentication fix or every-machine guarantee is inferred.
- **Actual client runtime:** Supported API-backed browser [Customer](../testing/LA363-customer-completion.md) and [Operator](../testing/LA364-operator-completion.md) evidence includes disclosed ARRIVED fixtures. The loopback preview's HTTP snapshot/fallback passed; it does not forward SignalR WebSockets. Native Android/iOS installation, physical flight and a whole-team rehearsal are separate scopes.
- **Remaining acceptance:** LA-346 Flutter MinIO upload/display; LA-348 actual enabled-AI policy/quality/concurrency/latency; LA-49/LA-188 actual team rehearsal; LA-204 two real proofreaders and final approved export. Production hosting and mobile release signing remain separate release decisions. AI defaults remain disabled. Do not merge the held LA-49 branch indirectly.

## Ownership, publication ledger, and blockers

- **LA-252** maintains [Chapter I](01-project-introduction.md) (`14188546`) and [Chapter II](02-project-management-plan.md) (`11927566`).
- **LA-253** maintains [SRS](../requirements/SRS.md) (`11993093`).
- **LA-254** maintains [Chapter IV](04-software-design-description.md) (`11993109`).
- **LA-255 / LA-362** maintain this reconciled index and [proofreading-and-formatting-checklist.md](proofreading-and-formatting-checklist.md) (including the Confluence page/version ledger and human proofreader gate).
- **LA-204 (TBA Review)** retains final synthesis, Confluence `98403` publication, and report approval.

## Review packet and actual publication

Existing Confluence pages were updated in place and read back on 19 November 2026 (Asia/Ho_Chi_Minh). Historical text and diagrams were retained and marked as superseded by the dated current correction. Returned published versions: Overview **98403 v8**, I **14188546 v3**, III **11993093 v14**, IV **11993109 v14**, V **11927583 v16**, VI **14024801 v9**, NFR **11960371 v9**. II **11927566 v6** records the actual Sprint 8 closure. [The publication ledger](proofreading-and-formatting-checklist.md#42-confluence-pageversion-ledger--remaining-blockers) records the review targets.

Two distinct team members must review these exact versions (chapters I-VI plus Overview/NFR), check chapter consistency, terminology, evidence scope, links and retained diagrams, then record their real name, date, decision and findings in the existing Overview proofreading record. Both confirmations remain **Open**; authorship, CI and agent review are not human sign-off. Resolve findings and identify the final approved export artifact/version under LA-204. This index and its linked chapters are the concrete review packet; publication alone does not approve the report.

No academic identifiers, meeting attendance, deployment results, page versions, reviewer decisions or model accuracy have been invented.

The generated 71-page PDF and DOCX review copies are available in local `artifacts/la204-20261119/SmartDroneDelivery-LA204-review.*`. [The export manifest](final-review-manifest.json) records their exact SHA-256 hashes, chapter/page versions and validation. Collapsed historical appendices remain on the linked Confluence pages.

Template alignment: cover, detailed numbered contents, acknowledgements/acronyms and ten substantive child specifications are included in review export 2. Existing Confluence Overview v8 explains the nested report structure; System Design `14090250 v3`, Database Design `14188589 v5` and Acknowledgement `14024759 v2` now contain substantive current text. Student IDs, formal TBA Review name, supervisor/external-supervisor names and academic project code remain explicit confirmation placeholders.

Institution correction: the cover uses the user-provided text **UTH University**. Template institution branding was removed completely; no official logo or formal name expansion is inferred.

The actual user-provided `Logo_UTH.png` has been visually inspected and embedded in both review covers; its SHA-256 is recorded in the export manifest.
