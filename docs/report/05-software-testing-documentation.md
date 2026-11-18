# V. Software Testing Documentation

> **Current verification alignment (LA-351 - 19 November 2026):** Documentation baseline `a90668c`, combined application baseline `10c77d0`. This correction supersedes the historical open/FAIL-only status below.
>
> - LA-45 system-testing/performance source is integrated; [PR #252](https://github.com/nguyentuio1/learning_app/pull/252) merged as `8466a47`. [PerformanceTestReport section 5.3](../testing/PerformanceTestReport.md#53-recorded-reference-workstation-and-postgresql16-docker-validation) records unchanged source `f7502b7` on application `10700d2`: three reference-workstation barrier repeats each 15/15 PASS, login100 p95 1177.3/1234.4/1202.2 ms; PostgreSQL16 Docker mixed k6 all 15 thresholds PASS, 0/17111 HTTP failures and 17107/17107 checks. BCrypt cost11 and the <3000 ms criterion are unchanged. Earlier 14/15 FAIL, PostgreSQL18 and Linux results remain historical; no authentication-code fix or every-machine acceptance is claimed.
> - [Application CI 44414947666](https://github.com/nguyentuio1/learning_app/actions/runs/44414947666) passed backend/web/mobile and its tested tree equals actual merge `10c77d0`; combined Flutter suite **425 passed**, analyzer no warnings/errors (30 information findings). [Documentation CI 44417197181](https://github.com/nguyentuio1/learning_app/actions/runs/44417197181) passed before PR #284 merged as `a90668c`.
> - Actual supported API-backed browser evidence: [Customer](../testing/LA363-customer-completion.md), [Operator](../testing/LA364-operator-completion.md). Prepared ARRIVED fixtures are disclosed. Loopback preview HTTP snapshot/fallback is verified; SignalR WebSockets are not forwarded. Native installation, physical flights and team rehearsal are unverified.
> - Remaining acceptance is explicitly tracked in the [integration review](../testing/Sprint8IntegrationReview.md): Flutter MinIO (LA-346), actual enabled AI (LA-348), actual team rehearsal (LA-49/LA-188), and two-member report proofreading/final approved export (LA-204). Source merge/CI do not replace those results.

## Historical consolidated ledger (17 November 2026)

The sections below consolidate observed evidence at the earlier integrated source [`07f38d1`](https://github.com/nguyentuio1/learning_app/commit/07f38d1b2630e7414ad114887029c94b3b48acc6), checked on 17 November 2026. Earlier dated test plans and ledgers remain historical sources. A test definition, an executed run, a Jira completion record and a deployed service describe different evidence and are identified separately below.

## 1. Scope and strategy

The scope includes authentication/authorization, customer resources, dispatch workflow, stations/fleet, tracking/confirmation, analytics, external integration and the implemented AI adapters/consumers. Requirements traceability uses `FR-01`–`FR-34`, the module business rules and the API contracts. [Test scope and strategy](../testing/test-scope-and-strategy.md), [test plan](../testing/test-plan.md), [module test cases](../testing/test-cases-per-module.md) and the [dated execution report](../testing/test-report.md) retain the detailed definitions and earlier results.

Backend checks combine API/service tests with relational transaction/concurrency tests. Web and Flutter checks exercise validation, loading/empty/error states, access loss, race handling and navigation. Privacy checks cover customer-only detail, ownership, hidden foreign resources, redacted AI inputs and suppression of assistant message bodies in logs. The flight workflow uses the software simulator; physical drone control is outside scope.

## 2. Test environments and procedures

CI runs on Linux and provides PostgreSQL 16 for backend relational tests. Web verification uses Node.js 24, npm lockfile installation, ESLint, Vitest and a Vite production build. Mobile verification uses Flutter stable, analysis and the Flutter test runner. Local focused runs recorded in reviews use the stated Windows toolchains; their case counts are not combined with unrelated runs or substituted for CI.

For each verification cycle, record the tested source/integration commit, command, environment, passed/failed/skipped result, run URL and any defect. Investigate a failure before rerunning it. Keep previous failed attempts in the execution history. A different PostgreSQL version or a filtered local run is a separate result.

## 3. Integrated verification ledger

All rows below were verified as merged into the stated integration baseline. CI checks are **Backend tests**, **Web lint, test and build**, and **Mobile analyze and test**. Their successful completion is attributed to the exact checked head and run, rather than an invented product-wide test total.

| Scope / integration PR | Checked head | Verified merge | CI evidence and result |
|---|---|---|---|
| ETA, [PR #226](https://github.com/nguyentuio1/learning_app/pull/226) | `31aa0ca` | `99b8a5c` | [Run 43898807001](https://github.com/nguyentuio1/learning_app/actions/runs/43898807001), all three checks passed. |
| Delivery summary, [PR #231](https://github.com/nguyentuio1/learning_app/pull/231) | `08f7f6d` | `d05156a` | [Run 43912459724](https://github.com/nguyentuio1/learning_app/actions/runs/43912459724), all three checks passed. |
| Customer assistant, [PR #234](https://github.com/nguyentuio1/learning_app/pull/234) | `5b93e3c` | `b024f38` | [Run 44018675256](https://github.com/nguyentuio1/learning_app/actions/runs/44018675256), all three checks passed. |
| Completed Flutter apps / route integration, [PR #235](https://github.com/nguyentuio1/learning_app/pull/235) | `1f206a3` | `07f38d1` | [Run 44044790532](https://github.com/nguyentuio1/learning_app/actions/runs/44044790532), all three checks passed; independent six-file Flutter run passed 56/56. |
| Full API delivery flow, [PR #230](https://github.com/nguyentuio1/learning_app/pull/230) | `cbb4508` | `00b9003` | [Run 43925576435](https://github.com/nguyentuio1/learning_app/actions/runs/43925576435), all three checks passed; 3/3 focused lifecycle/analytics/recovery tests passed. |
| Sprint 6 acceptance follow-ups, [PR #232](https://github.com/nguyentuio1/learning_app/pull/232) | `9cfb6c1` | `58b324e` | [Run 43916412530](https://github.com/nguyentuio1/learning_app/actions/runs/43916412530), all three checks passed; 34 backend and 8 map UI focused cases passed. |
| Report formatting, [PR #233](https://github.com/nguyentuio1/learning_app/pull/233) | `0d3afc3` | `229d1cf` | [Run 44028352764](https://github.com/nguyentuio1/learning_app/actions/runs/44028352764), all three checks passed; 56 report-relative links checked. |

Source ancestry and the equality of the tested integration tree and resulting remote merge were checked during integration. Sprint 6 tracking, map, confirmation, analytics and external integration were already integrated before the Sprint 7 baseline; their individual PR/source evidence is retained in the dated execution report. A historical row that says a PR was open is a snapshot of that earlier review, not its current state.

## 4. Integration cases and defect resolution

`FullSystemIntegrationTests` exercises an order through creation, approval, departure scheduling, drone assignment, origin intake, simulated transit, tracking/ETA, arrival, recipient confirmation and analytics. Additional cases cover the analytics creation-date cohort across a date boundary and failed intake/recovery read through the external API. These are HTTP API tests hosted by the test fixture; they are credited as executed API integration tests.

The final Flutter regression uses the actual app router, assistant controller and Dio service with the HTTP transport replaced by a test adapter. It checks the API-backed assistant page, owned history/session requests, POST order/session context, query precedence and navigation-extra fallback, and operator intake/pickup destinations. It is a client integration regression, not a claim that a physical Android/iOS device was driven through a deployed backend.

| Finding | Corrected behavior and evidence |
|---|---|
| Story branches lacked automatic CI; SignalR handshake contended with unrelated tests | [PR #216](https://github.com/nguyentuio1/learning_app/pull/216) adds story-target CI and isolates protocol tests without weakening assertions/deadlines. |
| Lost protected web chat after authorization revocation | [PR #213](https://github.com/nguyentuio1/learning_app/pull/213) removes protected state and aborts pending requests on access loss. |
| Mobile assistant stale POST response, uncertain retry and message logging | [PR #215](https://github.com/nguyentuio1/learning_app/pull/215) adds generation guards, recovery gating and request/response/error-body suppression. |
| Operator notification routed to customer-only screens; intake slot changed between display and submit | [PR #221](https://github.com/nguyentuio1/learning_app/pull/221) adds flavor-safe destinations and reconciled polling selection. |
| Confirmation lockout counted timer callbacks instead of elapsed time | [PR #223](https://github.com/nguyentuio1/learning_app/pull/223) uses an absolute expiry and resume recalculation. |
| Full-flow analytics query lost orders across the creation/completion date boundary | [PR #230](https://github.com/nguyentuio1/learning_app/pull/230) queries the creation-to-completion interval; focused crossing-boundary diagnostics passed. |
| Final branch merge duplicated assistant routes and lost session extras | [PR #235](https://github.com/nguyentuio1/learning_app/pull/235) keeps one API-backed route and preserves order/session context. A failing regression was observed before the correction and passed afterwards. |

## 5. Evidence limits and release conditions

All known reviewed code findings above were resolved and integrated. The preserved execution ledger distinguishes local tests, CI, mocks/adapters, real PostgreSQL tests and any separately recorded client-to-server runs. No global coverage percentage, measured NFR performance result, physical drone result, production deployment or actual device test is inferred from the existence of a test suite or a Jira Done status.

ETA/summary/assistant model options remain default-off. Actual model quality, provider/model configuration, approved retention/deletion policy and backup/restore configuration remain activation work. Publishing the report does not change those flags. Historical cases and comments are retained for traceability, with their original scope.

## 6. Final report review

Chapter sources and their Confluence page versions form the report handoff. LA-204 records final consolidation and the actual two-member proofreading confirmations required for report approval. Automated link/schema checks and agent review are technical validation and are not represented as either human sign-off.
