# Sprint 8 integration review

Review date: 2026-11-19, Asia/Ho_Chi_Minh. Application baseline: remote `develop` at `10c77d0781ff38448b07211da07731cf12598e27`. This review distinguishes integrated source, executed checks and remaining acceptance. Jira records the actual sprint lifecycle state.

## Source integration

The initial GitHub audit found no open pull requests. Remote-ref ancestry checks found no commits missing from develop on the completed LA-45, LA-203, LA-344, LA-349, LA-350, LA-46, LA-47, LA-48, LA-328, LA-343, LA-345, LA-346, LA-347 and LA-348 branches. LA-366 is integrated through PR #281. Source integration for LA-346/348 does not establish their remaining runtime/policy acceptance.

Recent integrations include performance/reference evidence [#252](https://github.com/nguyentuio1/learning_app/pull/252), guides [#280](https://github.com/nguyentuio1/learning_app/pull/280), atomic tracking numbers [#281](https://github.com/nguyentuio1/learning_app/pull/281), assistant availability [#282](https://github.com/nguyentuio1/learning_app/pull/282), station scope [#283](https://github.com/nguyentuio1/learning_app/pull/283), operator journey [#278](https://github.com/nguyentuio1/learning_app/pull/278), and customer journey [#277](https://github.com/nguyentuio1/learning_app/pull/277).

Two story refs still contain unmerged commits and must not be treated as completed integration:

| Story | Remote head | Remaining constraint |
|---|---|---|
| LA-351 | `89bbe1c9c9a675f114a8b5b045e8e24734d3afa4` | Report content needs its stale integration ledger corrected; actual two-member proofreading/final export evidence remains pending. |
| LA-49 | `e509d80f858d4ca6bcbefd5fddf0cc0bd9d025ef` | Held final-demo branch. Participants, timestamps, media and executed rehearsal steps are not recorded. Demo prerequisites were already integrated separately; do not merge this branch indirectly. |

## Final application verification

- PR #277's required CI run [44414947666](https://github.com/nguyentuio1/learning_app/actions/runs/44414947666) passed backend, web and mobile. All downloaded job checkout logs identify synthetic merge `2f5c65ee06e77297513359cb1acc4afa3294e002`.
- Tested tree `fbcf32d048a33d1fa0a2cba65d91b81899597a81` equals actual develop merge `10c77d0781ff38448b07211da07731cf12598e27`. The original teammate commits and operator prerequisites are ancestors of remote develop.
- Full combined Flutter suite: **425 passed**. Analyzer: **30 existing information-level findings, no warnings/errors**. See [raw test/analyzer evidence](evidence/la363-customer/).
- Actual API-backed supported browser runtime: [Customer package/order creation, retained draft, detail/tracking and receipt entry](LA363-customer-completion.md); [assigned-station operator intake and pickup confirmations](LA364-operator-completion.md). Prepared ARRIVED fixtures are explicitly identified; these are not executed physical flights or a whole-team rehearsal.
- The loopback preview does not forward SignalR WebSockets. Its HTTP snapshot/fallback was verified; healthy real-time delivery and native Android/iOS installation are not inferred from it.
- [Performance evidence](PerformanceTestReport.md) retains historical failures and later reference-workstation/PostgreSQL k6 passes without lowering BCrypt or the 3,000 ms thresholds. Those passes are scoped to the recorded workload/environment.

The README/documentation PR for this review must itself pass the current required integration checks before merge. Application checks above are the last complete application baseline, not a claim about a future commit.

## Remaining acceptance and sprint accounting

| Existing issue | Remaining work |
|---|---|
| LA-346 | Flutter MinIO photo upload/display and client-accessible storage URLs; web evidence alone is insufficient. |
| LA-348 | Actual enabled-AI policy approval, quality/concurrency and latency acceptance; a default-disabled assistant is not enabled-AI acceptance. |
| LA-49 / LA-188 | Actual team rehearsal with participants, timestamps, executed steps and media. |
| LA-204 / LA-351 | Actual two-member report proofreading, final export and current published report/index evidence. |

The read-only Confluence audit found Overview page `98403` at version 5 with stale Sprint 5/source references and two unrecorded proofreading entries. Chapter V `11927583` v15 preserves the older performance FAIL; Chapter VI `14024801` v8 has substantive guides but stale integration wording. These findings require correction and genuine confirmations, not replacement by fabricated sign-off.

Completed source contributions remain credited even where acceptance is outstanding. Keep unfinished work open and carry it to backlog when administratively closing the sprint; do not mark it Done solely to obtain a clean sprint report. Sprint closure does not imply release to main, deployment, complete native/MinIO/AI acceptance or a performed rehearsal.
