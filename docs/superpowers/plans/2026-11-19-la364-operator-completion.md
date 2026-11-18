# LA-364 operator completion implementation plan

> For agentic workers: use superpowers:executing-plans to implement and verify each task below.

**Goal:** Finish PR #278's operator journey and integrate it into develop while Nhân completes LA-363 separately.

**Architecture:** Reuse the existing Flutter screens, Riverpod controllers and REST endpoints. Keep the reviewed LA-367 station scope/cache fixes. Add explicit inspection input and a terminal pickup result, with stale-response guards for screen/station changes.

**Tech stack:** Flutter 3.47 / Dart, Riverpod, GoRouter, existing ASP.NET Core API.

**Spec:** LA-364 acceptance, PR278 review5965276029 and the project owner's full takeover instruction. Parent LA-344 remains open until the customer journey and final combined acceptance are complete.

## Constraints

- Preserve backend authorization, station assignment, customer/operator flavor boundaries and safe logging. Never log or persist pickup codes in evidence or URLs.
- No new backend workflow, duplicate story, rebasing or force-pushing shared history. Incorporate current develop by merge.
- Record real runtime outcomes, environment and source SHA. Do not present fixtures as a performed live mission or fabricated sign-off.
- Require current backend/web/mobile CI on the integration tree; confirm tested/merged tree equality and remote develop ancestry.

## Review focus

- Inspection must reset for a new lookup; declared dimensions must not silently become measured values.
- Station changes/access loss must clear old order/forms; late requests cannot restore them.
- Pickup success must clear the entered code and obsolete ARRIVED action without an automatic second POST.
- Failed/locked pickup retains useful recovery and attempt information.
- Narrow production-theme UI must expose primary actions before a long alert list.

## Task 1: Explicit intake inspection

- [x] Add failing UI tests for unchecked acknowledgement, blank actual measurements, valid explicit entry and lookup reset.
- [x] Change PackageIntakePage to separate declared reference values from required actual measurements; disable confirmation until acknowledged and validate finite positive measurements.
- [x] Guard IntakeController against duplicate/ineligible/stale operations without changing backend decisions.
- [x] Run intake regressions and existing operator suites.

## Task 2: Complete pickup and station recovery

- [x] Add failing controller/widget regressions for terminal success, code clearing, duplicate prevention and late responses after reset/new lookup/station change.
- [x] Clear obsolete orderInfo on successful pickup, preserve authoritative success response and reset sensitive input. Preserve attempts/lockout recovery.
- [x] Add generation/disposal guards and station-change reset on touched intake/pickup screens.
- [x] Run all relevant tests, then the complete Flutter suite and analyzer.

## Task 3: Usable home, runtime and integration

- [x] Put Intake/Pickup before the queue/availability and keep the assigned station explicit. Retain expired-parcel actions and loading/empty/error recovery.
- [x] Run the actual operator entry point with the local API. Record real intake/pickup outcomes and scope; record any prepared test data separately from actual executed steps. Capture only code-cleared/masked screens.
- [ ] Independent code/evidence review; address findings before approval.
- [ ] Push normal commits to the existing PR278 branch, target develop for the focused operator integration, pass current CI and merge normally after review.
- [ ] Verify remote integration and update LA-364; keep LA-344 and Sprint completion claims separate.
