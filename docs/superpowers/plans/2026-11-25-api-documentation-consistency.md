# LA-368 API documentation consistency plan

> **For agentic workers:** Use superpowers:executing-plans for this small documentation correction. Steps track the agreed scope; runtime behavior is unchanged.

**Goal:** Make current API references easy to find and reconcile stale completion claims with remote integration and Jira owner decisions.

**Architecture:** Keep service contracts in `docs/api`, rename implementation notes by service, and move dated sprint handoffs/read verification into `docs/api/history`. Rewrite relative links using each file's old and new location. Preserve historical results and release-export manifests.

**Tech Stack:** Markdown, OpenAPI JSON, Python link checks, GitHub CI and Jira LA.

**Spec:** User-approved steps 1–2 in the conversation: reconcile backlog, standardize API filenames/links and correct stale status/Gemini instructions. New cleanup/runtime work stays in LA-369/370; existing AI/demo work stays in LA-348/49/188.

## Global constraints

- Keep HTTP paths, configuration values and default-off AI gates unchanged.
- Preserve LA-204 owner acceptance recorded in Jira comment 10272; no invented human sign-off or instructor outcome.
- Preserve dated test evidence, export hashes and Confluence publication versions as historical records.
- Require reviewed PR and successful backend/web/mobile CI before integration into develop; no admin bypass authorization is inferred.

## Review focus

- Moved files retain valid links to service references, design documents and schemas.
- No old file path remains in active docs or backend Swagger text.
- Historical OpenAPI documents are clearly partial snapshots, not a complete current API export.
- Current AI notes describe both providers without implying cloud-policy approval.
- LA-346/351 integration and LA-204 acceptance are distinct from pending AI, device/WebSocket and team rehearsal evidence.

## Steps

- [x] Check existing Jira work/comments, release assets and branch rules; create LA-368/369/370 without sprint assignment and clarify existing AI/demo tasks.
- [x] Rename/move API documents, update all tracked relative links/path references and add folder navigation/naming rules.
- [x] Reconcile service-provider notes, report index/checklist and Sprint 8 status while preserving historical records.
- [x] Validate local links, JSON and naming; inspect the diff and obtain a whole-branch review.
- [ ] Commit/push LA-368, open a develop PR and verify required CI. Merge only with the required approving review; otherwise report that exact blocker.

## Execution notes

LA-204 remains Done: later owner acceptance supersedes earlier pending-proofreader labels for the consolidation/export deliverable. The release contains revised report assets; the repository's old export manifest remains historical. Current release code trees on main/develop are equal at the start of this correction.

Verification: 912 local Markdown/image link targets resolved, all three OpenAPI documents validated, API naming rules passed, moved sprint schemas match original content after Git line-ending normalization, historical export manifest untouched, whitespace checks passed. Independent whole-branch review found an outdated Ollama-only deployment paragraph; it was corrected and the reviewer confirmed no remaining blockers. No application test counts in this change represent new local runtime execution; current PR CI is required next.
