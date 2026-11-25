# LA-173 integration CI follow-up

> Historical investigation at the source revisions recorded below. These failures and focused results are retained as executed evidence; old pending-integration wording is not the current task status. Use the [testing index](../README.md) and [deliverable inventory](../../deliverables.md) for the current handoff.

The LA-158 producer's CI at ef5b9ed failed only `TrackingHubTests.Owner_can_connect_subscribe_and_unsubscribe_over_real_signalr_protocol` while 974 other tests passed. The same real-time handshake failure occurred on unrelated earlier heads. A complete serial local PostgreSQL run passed 975/975. Parallel unrelated API/Bcrypt fixtures can exhaust the test process during the real SignalR handshake; these protocol tests are functional checks, not a load benchmark.

The six TrackingHubTests now share a nonparallel xUnit collection. Assertions, protocol behavior and production/test timeout values are unchanged. The full local default-parallel run then passed all six SignalR tests; two unrelated PostgreSQL 18 timing tests still timed out:

- SchedulingPostgresTests.Intake_station_lock_prevents_prechecked_empty_slot_deletion_after_intake_commits
- FlightTelemetryPostgresTests.Ingestion_waits_for_a_concurrent_landing_and_then_refuses_the_completed_mission

That run was 973 passed / two failed / zero skipped and is not claimed green. Raw evidence: artifacts/test-results/LA-173-signalr-isolation-full.trx in the primary workspace. The final focused set (ETA reads/producer/model adapter/PostgreSQL plus all six SignalR protocol tests) passed 53/53 with no skips, including the new mission-history guard.

The CI workflow now includes `story/**` alongside develop/main, so a subtask PR can get backend/web/mobile checks on its current head without being pointed at develop. Workflow YAML parsed successfully. This does not change branch rules or authorize bypass on teammates' PRs.

Current-head Linux/PostgreSQL 16 CI must pass before the backend checkpoint is merged into LA-40. Fixing this CI blocker does not complete LA-173's remaining full-flow defects or LA-172 end-to-end acceptance.
# LA-173 web ETA acceptance supplement

The LA-40 integration candidate incorporates `develop` at `894ceab` through a merge commit. Its backend, mobile, web and QA subtasks were reviewed and merged individually. A final review identified missing functional evidence for the web's status-change acceptance path.

`deliveryEtaStatusRefresh.test.tsx` renders the real ETA page, API projection, polling hook and live-update hook. It mocks only the HTTP/SignalR transports, verifies subscription while APPROVED, dispatches the actual registered `StatusChanged` callback, observes another HTTP read and corrected IN_TRANSIT ETA, then verifies ARRIVED clears the countdown. The new case plus the six supplied QA UI cases passed **7/7** locally. This is functional integration evidence, not a live transport/provider/model-quality claim.

The ETA runbook also clarifies that `ModelRevision` is configured metadata; the adapter does not verify the running model digest. Artifact pinning/verification and real-model quality remain activation gates. No model is enabled by this supplement.

Full backend/web/mobile CI on the story PR's current integration head remains required before its `develop` merge. LA-172's complete web/mobile/API flow and outstanding client integration findings remain separate from these verified fixes; LA-173 is not marked fully Done on that basis.
