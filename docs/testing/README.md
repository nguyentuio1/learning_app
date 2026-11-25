# Testing documentation

Read the plan first, then the recorded results and their source evidence. Test counts and PASS/FAIL outcomes belong to the SHA, date, runtime and workload recorded in each report.

## Plans and test cases

| Document | Purpose |
|---|---|
| [Scope and strategy](test-scope-and-strategy.md) | Requirements covered, test layers and exclusions |
| [Test plan](test-plan.md) | Execution approach, responsibilities and prerequisites |
| [Cases per module](test-cases-per-module.md) | Functional cases and expected outcomes |

## Results and acceptance

| Location | Read it for |
|---|---|
| [Execution report](reports/test-report.md) | Results separated by baseline, PR and environment; older rows retain their recorded dates |
| [Performance report](reports/performance-report.md) | Historical failures, later scoped reference passes and original performance criteria |
| [Acceptance index](acceptance/README.md) | Customer/operator journeys, photo flows, assistant availability and synthetic Gemini results |
| [Evidence index](evidence/README.md) | Original logs, screenshots, JSON and SQL fixtures supporting the reports |
| [History index](history/README.md) | Dated CI investigations and Sprint 8 integration review |

The [demo rehearsal record](../demo/demo-rehearsal.md) belongs with the demo runbook. Component checks and prepared fixtures do not prove an actual whole-team rehearsal.

## Open acceptance boundaries

At the documentation baseline `9651736` on 25 November 2026, native Android/iOS and healthy app WebSocket runtime acceptance remain under LA-370. PR #291's proposed backend/custom Node evidence is still under review and is not integrated here. Default-off UI tests and synthetic Gemini calls do not complete LA-348 cloud-policy/model-quality/concurrency acceptance. LA-49/LA-188 require an actual recorded team rehearsal. No new runtime execution is claimed by reorganizing these documents.
