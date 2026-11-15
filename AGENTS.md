# Project instructions

## Jira and GitHub

- Use Jira project `LA` at `https://hungndemail.atlassian.net` and GitHub repository `nguyentuio1/learning_app` for this project.
- `develop` is the integration branch; `main` is the release branch.

## Integrate completed stories promptly

- When a story is complete, integrate it into `develop` in the same work cycle. Do not leave completed stories waiting until the end of the sprint.
- Subtask PRs may target their story branch. Merging subtasks into a story branch does not complete story integration: open or update the story PR into `develop` and carry it through review, CI and merge.
- Before merging, verify all required subtasks and acceptance criteria are complete, resolve review findings, and require successful backend, web and mobile CI on the current integration head.
- Merge dependent stories in dependency order. Incorporate the latest `develop`, resolve conflicts while preserving both sides' intended behavior, and verify the merged result matches the tested code.
- Prefer merge commits for shared story branches. Do not rebase or force-push shared history without explicit authorization.
- Respect user instructions to hold an unfinished story. Do not merge a dependent story that would bring the held work into `develop` indirectly.
- Check repository approval requirements. Admin bypass needs explicit user authorization; CI must still pass. If approval blocks integration, report the exact blocker and the reviewable PR.
- Confirm the story commits are in remote `develop` before reporting integration complete or reconciling Jira completion. Keep implementation, integration, deployment and end-to-end acceptance claims distinct.
