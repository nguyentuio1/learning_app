# Sprint 7 API handoff and first backend slice

Goal: give the team a reviewed AI API contract, then begin LA-158 without changing flight scheduling through an unsafe ETA producer.

Architecture: existing ASP.NET Core/EF Core/PostgreSQL backend, persisted account/ownership checks, shared typed prediction projection for detail and dedicated read endpoint. Keep model generation outside transactions. Preserve the existing full-route scheduling consumer.

Tech stack: .NET 8, PostgreSQL, existing backend test project, web/Flutter consumers and GitHub CI.

Spec: [working contract](../../api/sprint-7-api-contract.md), existing LA-201 design and actual Jira acceptance criteria once access is restored.

Global constraints: correct hungndemail site; real task keys in commits; no external provider activation without a settled provider/privacy policy; no rebase/force-push; preserve unrelated root files; incomplete LA-40 stays out of develop.

Review focus: authorization parity, nullable DTO compatibility, no invented AI freshness, no accidental remaining-duration writes to scheduling inputs, genuine implementation availability.

## 1. Reconcile Jira and documentation ownership

Read active sprint using board/sprint operations and its issue list. Read LA-158/162/165 plus existing documentation issues and current Confluence pages. Reuse an equivalent contract issue, otherwise create a Task with contract/schema/consumer-review/publication acceptance and the verified backend owner in the active sprint. Add only documentation gaps not already covered. Rename the detached drafting worktree branch using the actual contract issue key.

Reconciled 2026-11-16: correct-site access works with cloud ID 5f215ebf-b081-43df-b580-4f68cd116d34. Sprint 7 is active (ID 7, board 2). LA-158/162/165 belong to TBA Review. Created LA-342 after checking all contract-labelled work; LA-183 is the distinct Sprint 8 final API guide. Updated existing LA-204/253/254/255 with module inputs and TBA Review final approval. Confluence index/design pages remain dated Sprint 5 snapshots; no automatic sync established. Branch: docs/LA-342-sprint-7-api-contract.

Verification 2026-11-16: openapi-spec-validator 0.9.0 validates OpenAPI 3.0.3; all local refs/links and five Markdown JSON examples pass schema validation. Fresh independent review found undefined staff scope, resolved as explicit project-wide redacted ETA/summary reads for active persisted DISPATCHER/MANAGER; customer detail remains customer-only. Aligned chat schema with post-trim validation. Confluence handoff page 18579461 published explicitly as proposed and awaiting consumer approval. No feature acceptance or deployment claimed.

## 2. Contract first

Review the working contract against backend DTOs and mobile PR #207. Settle production policy/provider questions separately from safe reads; make no deployment claims. Produce matching OpenAPI JSON, validate references/schema examples and route/role parity. Review independently, correct findings, commit with the real contract task key and open a reviewable PR. Carry CI and authorized integration through before handing off implemented availability.

## 3. LA-158 first slice: authorized ETA reads

After verifying task acceptance/owner, create the task branch from the appropriate LA-40 story base containing the contract. Add meaningful tests first: owner succeeds, foreign/missing are indistinguishable, persisted inactive/changed-role denial, allowed operational roles, null prediction, stored prediction projection and existing detail route parity. Show the new route tests fail before implementation.

Implement a typed prediction DTO and share only its projection. Preserve the existing customer-only detail boundary; the dedicated ETA route has its own scoped staff access without recipient contact fields. GET must make no provider calls and no writes. Do not infer arrival from duration. Omit invalid prediction values consistently. Run targeted tests, then backend regression tests with the existing PostgreSQL fixture. Inspect diff for privacy and scheduling coupling. Review, commit and open the task PR; record the partial slice honestly if prediction production remains outstanding.

## 4. Complete estimation only after production policy is settled

Verify full LA-158 Jira acceptance against approved model/input/anchor/weather/reservation policy. Test safe full-route generation, complete input-stamp invalidation, competing recalculation, failed provider, atomic writes and unchanged reservation safety. Implement only the settled policy; do not silently substitute heuristics for an accepted AI model. Review and run the relevant integration suite. Reconcile task completion only when its full acceptance passes.

## 5. Story integration

Recheck mobile #207 findings and all LA-40 subtasks/acceptance. Integrate completed story into current develop in dependency order with merge commits and all three CI jobs green. Confirm remote ancestry, then update Jira. Continue the next verified assigned task while an unfinished teammate task is pending.
