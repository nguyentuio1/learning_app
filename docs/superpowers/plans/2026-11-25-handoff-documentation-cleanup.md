# LA-371 handoff documentation cleanup plan

> **For agentic workers:** Use superpowers:executing-plans to implement this documentation correction inline, with a final independent source review.

**Goal:** Give a reviewer a clear starting point for deployment, testing and the deliverable inventory.

**Architecture:** Consolidate deployment/backup guides under `docs/deployment`; put policy proposals under `docs/decisions`. Keep core test plans at `docs/testing` and separate reports, acceptance records, history and unchanged raw evidence. Use service names in filenames, retaining Jira keys in content. All document moves update incoming and outgoing relative links.

**Tech Stack:** Markdown, Git, Python link/hash checks, existing OpenAPI validators and GitHub CI.

**Spec:** User-approved cleanup described in the preceding conversation: organize deployment/testing, add indexes, rename DeliverableList.md to deliverables.md and correct obsolete inventory content.

## Global constraints

- Keep runtime code/configuration, HTTP contracts and all default-off AI gates unchanged.
- Preserve all raw evidence bytes and the original historical report export manifest.
- Keep test dates, source SHAs, measured results and history; label superseded status statements.
- LA-369 is integrated; LA-370 PR #291 remains under review and must not enter this branch indirectly. AI/native/team-rehearsal acceptance is separate.
- All eight sprints are closed; LA-371 is post-release work without a new sprint.
- Require source review and current backend/web/mobile CI before develop integration. Admin bypass requires explicit authorization for this PR.

## Review focus

- Changed relative paths and heading anchors still resolve after moves.
- Evidence/manifests retain byte-identical contents and their recorded provenance.
- Indexes distinguish maintained instructions, historical results and unapproved proposals.
- The inventory links actual integrated scripts/source and never substitutes CI for runtime acceptance.
- Current navigation avoids duplicate deployment homes and stale pending-PR claims.

## Steps

- [x] Inspect current source/Jira and create the scoped LA-371 task; use an isolated worktree from develop 9651736.
- [x] Record a relocation map; move/rename documents and rewrite tracked references, preserving raw evidence.
- [x] Add docs/deployment/decisions/testing navigation and refresh deliverables.md with current source/evidence boundaries.
- [x] Validate local targets, changed anchors, naming, schema validity and preserved evidence hashes; obtain independent review.
- [ ] Commit/push, open a develop PR, complete CI and integrate with required approval or report the exact blocker.

## Verification notes

Twenty documents were relocated. One existing station API section anchor was corrected. Local checks resolve 986 file/image/heading links with no missing targets or unresolved anchors; all three OpenAPI schemas validate. All 134 protected original evidence/screenshot/manifest files retain their SHA-256 hashes. Naming and whitespace checks pass; no runtime/configuration files are changed. An independent reviewer reran the validation and found no important/blocking findings. These are documentation checks, not a new runtime test result. Current PR CI and repository approval remain the integration gates.
