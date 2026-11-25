# LA-365: Flutter assistant availability and recovery

Validated source: `97b8113276670be2f7ef7378304e09ac7c8e28b4`, based on develop `8466a47`.
Executed by TBA Review / Codex on 2026-11-19 (Asia/Ho_Chi_Minh).

## Behavior

- Chat defaults off. Without `--dart-define=AI_CHAT_ENABLED=true`, input, Send and suggestion chips are disabled; direct controller sends are also rejected. The screen explains availability and links to the selected delivery's tracking, or the customer's orders when no delivery is selected. Existing owned conversation history remains readable.
- The build flag is a presentation capability, never permission to process data. It does not enable the backend, configure a model, approve retention or consent, or bypass ownership/authentication checks. LA-348 live AI acceptance remains separate.
- A definite HTTP 503 retains the draft and permits a deliberate retry without requiring history recovery. Network/transport failures, HTTP 504, other potentially ambiguous server errors and malformed success responses retain the existing uncertain-send recovery. No automatic POST retry was added.
- Sign-out, account change, or loss of the active customer role clears protected state and invalidates outstanding session, message and send responses.
- Runtime inspection found the production theme gave Send an infinite minimum width inside a Row, collapsing the composer. A local 52x52 minimum overrides it, with an accessible Send label. A regression uses the actual production theme.

## Verification

| Check | Actual result | Evidence |
| --- | --- | --- |
| Focused assistant suite | 17 passed | Included in full suite below |
| Full Flutter suite | 381 passed, exit 0 | [Raw test output](../evidence/la365/flutter-test.txt) |
| `flutter analyze --no-pub --no-fatal-infos` | Exit 0; 30 existing info-level findings, no errors/warnings | [Analyzer output](../evidence/la365/flutter-analyze.txt) |
| Customer release web, default flag | Build exit 0 | [Build output](../evidence/la365/build-disabled.txt) |
| Customer release web, enabled UI flag | Build exit 0 | [Build output](../evidence/la365/build-enabled.txt) |
| Default-off runtime | Notice visible, sending disabled, zero assistant POSTs, selected tracking route reached | [Runtime events](../evidence/la365/disabled-events.json), [screenshot](../evidence/la365/disabled-assistant.png) |
| Definite 503 runtime | Draft retained, manual retry enabled, exactly one initial POST, no uncertain recovery requirement | [Runtime events](../evidence/la365/enabled-events.json), [screenshot](../evidence/la365/enabled-503.png) |
| Deliberate retry | Second POST returns supported transient HELP outcome; reply rendered | [Screenshot](../evidence/la365/enabled-retry-success.png) |
| Transport timeout | Third POST fails; Send disabled until Check sessions succeeds; recovery makes only a GET, no additional POST | [Screenshot](../evidence/la365/enabled-timeout-recovery.png) |

The new 503, default-off, sign-out and production-theme tests were observed failing against the missing behaviors before the fixes. Existing tests additionally exercise enabled ANSWERED/REFUSED behavior, true timeout, HTTP 504, malformed 2xx, stale responses after session/new-chat/access changes, history deduplication and safe body logging.

Runtime: Flutter 3.47.8 / Dart 3.13.7, Chrome 158.0.8127.36 headless on Windows, 390x844 viewport. Both builds use the actual `lib/main_customer.dart` entry point, production router/theme/storage and assistant UI. HTTP auth/history/chat responses are synthetic fixtures intercepted in an isolated browser context. Screenshots were visually inspected. Browser 503/timeout console resource errors are expected injected failures; uncaught page-error lists are empty. This establishes supported Flutter web runtime behavior, not Android/iOS device acceptance, live provider quality, deployment or cloud-policy approval. The operator entry point has no assistant screen and was not runtime-tested for this focused change.

## Reproduction

Run the full suite and analyzer from `frontend/mobile` using the commands above. The repository does not track a web runner. For the browser check, generate a disposable web wrapper in an isolated checkout (`flutter create . --platforms web --project-name smart_drone_delivery --no-pub`), preserve the tracked lockfile/metadata and remove the generated sample `lib/main.dart` and `test/widget_test.dart`. Keep runner/build files out of the PR.

Build the customer entry point twice:

```text
flutter build web --no-pub --target lib/main_customer.dart --dart-define=API_BASE_URL=http://127.0.0.1:5055/api
flutter build web --no-pub --target lib/main_customer.dart --dart-define=API_BASE_URL=http://127.0.0.1:5055/api --dart-define=AI_CHAT_ENABLED=true
```

Copy each resulting `build/web` directory to `artifacts/la365-runtime/web-disabled` and `artifacts/la365-runtime/web-enabled`, respectively. [Recorded build arguments and source SHA](../evidence/la365/build-metadata.json) identify the measured builds. With Python Playwright and local Chrome available, run from the repository root:

```text
python scripts/tests/mobile-assistant-runtime.py disabled
python scripts/tests/mobile-assistant-runtime.py enabled
```

An optional second argument selects the artifact root. The script serves each build on loopback port 18651/18652, uses synthetic HTTP fixtures without contacting a live AI service, asserts the interaction outcomes, and writes screenshots/events. Review the reply screenshot as well: Flutter SelectableText's disabled semantics textbox does not expose reply content in its DOM value. The server and isolated browser stop when each successful run ends.

CI and merge evidence belong to the PR/Jira integration record. LA-344 customer/operator subtasks and its overall acceptance remain separate from this focused fix.
