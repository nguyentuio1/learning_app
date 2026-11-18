# Project report chapter sources

## Six-part contents

The consolidated report follows the existing six-chapter course structure. **LA-362** (under **LA-351**) reconciles this index against the current integrated `develop` source baseline [`b5e54fa`](https://github.com/nguyentuio1/learning_app/commit/b5e54fa601a666f929ebdb1b626c0c80697a89fc) (18 November 2026), while preserving earlier dated chapter baselines (`8413d84`, `894ceab`, `07f38d1`) as historical snapshots.

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

- **Implementation & integration (`develop` `b5e54fa`):** Sprint 7 stories (`LA-40`–`LA-43`) and Sprint 8 additions (`LA-47` Docker packaging, `LA-177` audit logging, `LA-178` health checks, `LA-355` staff delivery lookup, `LA-186` demo script, `LA-187` demo seed, and `LA-188` rehearsal runbook PR #266) are merged into `develop`. Earlier open-PR labels in dated chapter snapshots (`04-software-design-description.md` §4.1 at `894ceab`) remain historical baseline records.
- **Deployment packaging:** Docker Compose packaging and smoke verification pass in CI; production hosting, Android/iOS release signing, and cloud AI retention-policy approval remain separate release gates.
- **Acceptance & live rehearsal (`LA-175`, `LA-188`):** Repository CI verifies automated tests and builds. Final test execution acceptance (`LA-175`) and live team demo rehearsal evidence (`LA-188`, [LA-188-demo-rehearsal.md](../testing/LA-188-demo-rehearsal.md)) remain open until executed and recorded.

## Ownership, publication ledger, and blockers

- **LA-252** maintains [Chapter I](01-project-introduction.md) (`14188546`) and [Chapter II](02-project-management-plan.md) (`11927566`).
- **LA-253** maintains [SRS](../requirements/SRS.md) (`11993093`).
- **LA-254** maintains [Chapter IV](04-software-design-description.md) (`11993109`).
- **LA-255 / LA-362** maintain this reconciled index and [proofreading-and-formatting-checklist.md](proofreading-and-formatting-checklist.md) (including the Confluence page/version ledger and human proofreader gate).
- **LA-204 (TBA Review)** retains final synthesis, Confluence `98403` publication, and report approval.

Remaining blockers before closing **LA-362** / **LA-204**:
1. Align chapters and Confluence `98403` with final `LA-175` / `LA-188` execution results (live rehearsal itself remains `LA-188`).
2. Obtain two actual distinct member proofreading confirmations (name, date, decision/notes) on the exact updated report/page versions—CI, authorship, and agent review do not count.
3. Publish the updated chapters to the existing Confluence pages above and record each published page version in the ledger.

Publication is manual and explicit. A repository commit does not prove Confluence synchronization. Do not invent academic identifiers, meeting attendance, deployment results, page versions, or model accuracy.
