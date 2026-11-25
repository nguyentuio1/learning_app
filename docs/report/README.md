# Project report chapter sources

## Six-part contents

The consolidated report follows the existing six-chapter course structure. **Current correction, 25 November 2026:** LA-351 corrections and the final application candidate are integrated through [PR #288](https://github.com/nguyentuio1/learning_app/pull/288), `develop` at `24c03ab`, and released through PR #289. The owner explicitly accepted LA-204 consolidation/export on 19 November (Jira comment 10272); LA-204 remains Done. Instructor evaluation is a separate academic activity. Earlier chapter baselines, publication versions and review-packet hashes below remain historical records.

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

- **Integrated source:** LA-45 performance/test evidence (PR #252, merge `8466a47`), LA-48 technical documentation, LA-203 installation/user guides (PR #280), LA-366 atomic tracking numbers (PR #281), Customer journey (PR #277), Operator journey (PR #278), assistant availability (PR #282), station scope (PR #283) and LA-351 report corrections are in `develop`. Final candidate PR #288 also includes the verified Flutter MinIO work and synthetic Gemini evidence.
- **Executed verification:** [Application CI 44414947666](https://github.com/nguyentuio1/learning_app/actions/runs/44414947666) passed backend, web and mobile; the tested tree equals actual application merge `10c77d0`. Full combined Flutter suite: **425 passed**; analyzer: no warnings/errors, 30 existing information findings. [Documentation CI 44417197181](https://github.com/nguyentuio1/learning_app/actions/runs/44417197181) passed before PR #284 merged as `a90668c`. [Integration review](../testing/Sprint8IntegrationReview.md) records exact source scope.
- **Scoped performance:** [PerformanceTestReport section 5.3](../testing/PerformanceTestReport.md#53-recorded-reference-workstation-and-postgresql16-docker-validation) records unchanged source `f7502b7`, application `10700d2`: three barrier repeats each 15/15 PASS; PostgreSQL16 Docker mixed k6 all 15 thresholds PASS and 0/17111 HTTP failures. BCrypt cost11 and the original p95 <3000 ms criterion were unchanged. Earlier FAIL outputs remain historical; no authentication fix or every-machine guarantee is inferred.
- **Actual client runtime:** Supported API-backed browser [Customer](../testing/LA363-customer-completion.md) and [Operator](../testing/LA364-operator-completion.md) evidence includes disclosed ARRIVED fixtures. The loopback preview's HTTP snapshot/fallback passed; it does not forward SignalR WebSockets. Native Android/iOS installation, physical flight and a whole-team rehearsal are separate scopes.
- **Later executed verification:** [Flutter MinIO evidence](../testing/LA346-flutter-photo-completion.md) records actual upload/display and 432 combined Flutter tests. [Candidate CI 44423852669](https://github.com/nguyentuio1/learning_app/actions/runs/44423852669) passed backend/web/mobile before PR #288; Docker smoke passed separately. These supersede earlier counts only for that recorded candidate, not as a new test run.
- **Remaining acceptance:** LA-348 actual enabled-AI policy/quality/concurrency/latency; LA-49/LA-188 actual team rehearsal; LA-370 native runtime/WebSocket evidence. Production hosting and mobile release signing remain separate release decisions. AI defaults remain disabled. LA-204 is owner-accepted; no instructor decision or named peer sign-off is inferred.

## Ownership, publication ledger, and blockers

- **LA-252** maintains [Chapter I](01-project-introduction.md) (`14188546`) and [Chapter II](02-project-management-plan.md) (`11927566`).
- **LA-253** maintains [SRS](../requirements/SRS.md) (`11993093`).
- **LA-254** maintains [Chapter IV](04-software-design-description.md) (`11993109`).
- **LA-255 / LA-362** maintain this reconciled index and [proofreading-and-formatting-checklist.md](proofreading-and-formatting-checklist.md) (including the Confluence page/version ledger and human proofreader gate).
- **LA-204 (TBA Review)** retains final synthesis, Confluence `98403` publication, and report approval.

## Historical review packet and actual publication

Existing Confluence pages were updated in place and read back on 19 November 2026 (Asia/Ho_Chi_Minh). Historical text and diagrams were retained and marked as superseded by the dated current correction. Returned published versions: Overview **98403 v8**, I **14188546 v3**, III **11993093 v14**, IV **11993109 v14**, V **11927583 v16**, VI **14024801 v9**, NFR **11960371 v9**. II **11927566 v6** records the actual Sprint 8 closure. [The publication ledger](proofreading-and-formatting-checklist.md#42-confluence-pageversion-ledger--remaining-blockers) records the review targets.

At this earlier review-packet stage, two member confirmations were unrecorded. The later owner acceptance in Jira comment 10272 closed LA-204 as the compilation/export deliverable; it did not invent either confirmation. Historical checklist entries are retained for traceability and do not reopen the accepted task. These page versions establish the recorded publication only, not any later Confluence synchronization.

No academic identifiers, meeting attendance, deployment results, page versions, reviewer decisions or model accuracy have been invented.

The original 71-page PDF/DOCX review copies and [export manifest](final-review-manifest.json) preserve that export's hashes and chapter/page versions. They are superseded as current handoff files by the revised [v1.0.0 release assets](https://github.com/nguyentuio1/learning_app/releases/tag/v1.0.0): PDF (108 pages), DOCX and `SmartDroneDelivery-LA204-review-manifest.json`. GitHub asset digests checked on 25 November: PDF `6c765026384dfe170f22d3e8c50401344f3c5bdec84ea85faa108481535ce9aa`, DOCX `d56886471b5dbfe9e6d9ca2a982056d0f8f83e9818e6703906c59ab4037c663c`, manifest `08e7e1b2d62b328518f81c6eb43ec1417ccffca3b5ed66c8cf81354c8ae82213`. These are the reported asset digests, not a claim that the files were downloaded and rehashed in this correction. Collapsed historical appendices remain on the linked Confluence pages.

Template alignment: cover, detailed numbered contents, acknowledgements/acronyms and ten substantive child specifications were included in review export 2. Recorded published versions: Overview v8, System Design `14090250 v3`, Database Design `14188589 v5` and Acknowledgement `14024759 v2`. Later owner-supplied four member names/student IDs and evaluator Nguyễn Văn Chiến were incorporated in release assets per Jira comments 10272/10274. Unconfirmed academic metadata remains a later document correction; no evaluation result/signature is inferred.

Institution correction: the cover uses the user-provided text **UTH University**. Template institution branding was removed completely; no official logo or formal name expansion is inferred.

The actual user-provided `Logo_UTH.png` has been visually inspected and embedded in both review covers; its SHA-256 is recorded in the export manifest.
