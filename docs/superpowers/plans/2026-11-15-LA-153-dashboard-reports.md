# LA-153 dashboard and reports API

Implement the LA-341 handoff for LA-38 independently of unfinished tracking/confirmation stories. Native execution in this worktree; fresh review before integration. Business metric acceptance remains distinct from technical implementation.

1. Add HTTP tests against the existing API: manager authorization/current persisted eligibility, Vietnam-day boundaries, complete status counts, null empty metrics, duration exclusions, throughput deduplication, paginated reports, invalid/unknown/duplicate query parameters. Run RED against the missing routes.
2. Add typed DTOs, strict query parsing and manager-only controller. Add an EF query service using database aggregates (no order-cohort materialization), a repeatable-read transaction for relational providers, projections without protected fields, and paginated stable sorting. Existing orders supply the data; no analytics writes or new package dependencies.
3. Run focused HTTP tests and real PostgreSQL tests for SQL translation and consistent reads under concurrent status changes. Check a representative seeded cohort against the three-second target without claiming a production SLA.
4. Update handoff availability and design references, run full backend verification, get independent review, fix findings, commit/push with LA-153, run backend/web/mobile CI. Merge the subtask into story/LA-38-dashboard-reports only; LA-154/155 remain teammate work. Integrate the story into develop only when all tasks and acceptance checks are complete.

Review focus: inclusive local dates map to half-open UTC; zero denominator is null; only valid DELIVERED durations contribute; origin=destination counts once per station; concurrent commits cannot mix status counts and ratios; integer paging cannot overflow; old role claims cannot authorize a demoted account; no recipient/code/identity fields in responses.

## Execution ledger

- RED: missing routes produced 25 expected HTTP failures and two anonymous-auth passes (27 cases).
- Focused GREEN: 31/31 HTTP/PostgreSQL cases passed, no skips. Real PostgreSQL tests verify concurrent status changes preserve a consistent dashboard snapshot, deduplicated station totals, stable UUID tie sorting, operational data is unchanged, and a 5000-order cohort meets the local <3-second response target.
- Fresh independent review: no blockers. Strengthened report assertions for default empty pagination, null/valid durations and secret exclusion. Remaining optional cases: accepted 366-day boundary and report-specific concurrent insertion.
- Ruling: implement LA-341's explicit analytics proposal as requested, retaining pending business-acceptance labels. Technical completion does not silently approve KPI definitions.
- Local setup: existing cached packages restored with NuGetAudit disabled after remote advisory-index access warnings; no dependency changes. EventLog sink disabled for Windows test execution. Dedicated PostgreSQL on 127.0.0.1:55443, retained unique test databases and sequential local collections; CI retains its standard parallel execution and teardown.
- Full-suite and remote CI results are recorded in the PR/Jira handoff after completion.
