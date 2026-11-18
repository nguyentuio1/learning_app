# LA-367: operator station scope after refresh

Product source: `9c14be8` (provider fix `a8232c5`), based on develop `8c7c5bc`.
Executed by TBA Review / Codex on 2026-11-19, Asia/Ho_Chi_Minh.

## Fixed behavior

- Initial load and refresh share the operator's assigned-station filter. Refresh no longer replaces the restricted list with every landing station. Direct selection rejects stations outside the current permitted list.
- Reassignment switches to the new station; assignment removal clears the selected station, availability and details and shows the unassigned state. Old station data is cleared before a new station's slow/failed load. Admin/Dispatcher multi-station selection and fallback remain supported.
- Generation checks reject older refresh, select and poll completions. A superseded pending rebuild returns the current state instead of publishing stale or empty state over a newer assignment. Independent review reproduced both late-availability and late-detail rebuild races; their regressions now pass.
- Live login exposed a prerequisite cache bug: `UserModel.toJson` stores `roleCode`, but `fromJson` previously only read `role`, causing stored operators/dispatchers/admins to become Customer in memory. The parser now accepts the cached field while preserving server-role precedence. This repairs role decoding; backend authorization and assignments are unchanged.

## Verification

| Check | Actual result | Evidence |
| --- | --- | --- |
| Station/provider suite | 28 passed, including 11 new scope/race cases | Included in [full output](evidence/la367/flutter-test.txt) |
| Real SecureUserStorage codec tests | 5 passed; Operator/Dispatcher/Admin cases failed before the cache fix | Included in full output |
| Full Flutter suite | 397 passed, exit 0 | [Raw output](evidence/la367/flutter-test.txt) |
| Analyzer | Exit 0, 30 existing info findings; no errors/warnings | [Raw output](evidence/la367/flutter-analyze.txt) |
| Independent technical review | Scope approved after fixing the reproduced rebuild race | PR/Jira review record |
| Live local API walkthrough | Operator login, manual refresh, Intake entry/return, Pickup entry/return; assigned station remained restricted | [Events](evidence/la367/runtime-events.json) |

Runtime used Flutter 3.47.8 / Dart 3.13.7, the real operator entry point and production theme/router/storage in Chrome at 430x932. No HTTP fixtures were used. The existing API ran on port 5055; its deployed commit is not asserted. A loopback-only gateway on port 18731 forwarded UI traffic to Flutter port 18730 and API traffic to port 5055, avoiding a change to the API's CORS/authentication settings.

The **local preview** additionally includes the unmerged LA-364 UI from PR #278, head `3186da0`. This support PR does not include those UI commits. [Preview provenance](evidence/la367/preview-source.json) records the local preview commit and verifies that both changed production files exactly match the tested LA-367 source. Pending LA-364 inspection/pickup findings and its full journey acceptance remain open. These screenshots do not establish approval or integration of PR #278.

Screenshots:

- [Operator home](evidence/la367/01-operator-home.png)
- [After manual refresh](evidence/la367/02-after-refresh.png)
- [Origin intake entry](evidence/la367/03-intake.png)
- [Destination pickup entry](evidence/la367/04-pickup.png)
- [Home after both return paths](evidence/la367/05-home-after-both-actions.png)

The live check only navigated/read station data and authenticated with a documented development account. It did not confirm an intake/pickup, change assignments or create deliveries. Reassignment/removal and concurrency failures were exercised in isolated real-notifier tests. Android/iOS device behavior, deployment and completed LA-344/LA-364 acceptance are not claimed.

## Reproduction

From `frontend/mobile`, run `flutter test --no-pub` and `flutter analyze --no-pub --no-fatal-infos` after restoring dependencies.

For the operator preview, use an isolated checkout with a disposable Flutter web runner. Start the operator entry point with API base `http://127.0.0.1:18731/api` and web server bound to `127.0.0.1:18730`. Start the loopback preview gateway, which forwards to the existing API at port 5055. With Python Playwright and local Chrome available:

```text
python scripts/tests/operator-station-runtime.py artifacts/la367-runtime
```

The script uses only the documented development operator account, records request methods/paths/statuses without tokens/bodies, saves screenshots and asserts scope after manual refresh and both return paths. A Flutter `context.push` may keep the browser URL at the parent route, so screen headings are checked rather than assuming a hash change. Scripts and generated web-runner files are local test infrastructure, not production deployment configuration.

Integration completion requires the PR's current backend/web/mobile CI and verification that its merged tree is in remote develop. Record that separately on Jira/PR; runtime preview alone is not integration.
