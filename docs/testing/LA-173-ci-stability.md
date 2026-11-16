# LA-173 integration CI follow-up

The LA-158 producer's CI at ef5b9ed failed only `TrackingHubTests.Owner_can_connect_subscribe_and_unsubscribe_over_real_signalr_protocol` while 974 other tests passed. The same real-time handshake failure occurred on unrelated earlier heads. A complete serial local PostgreSQL run passed 975/975. Parallel unrelated API/Bcrypt fixtures can exhaust the test process during the real SignalR handshake; these protocol tests are functional checks, not a load benchmark.

The six TrackingHubTests now share a nonparallel xUnit collection. Assertions, protocol behavior and production/test timeout values are unchanged. The full local default-parallel run then passed all six SignalR tests; two unrelated PostgreSQL 18 timing tests still timed out:

- SchedulingPostgresTests.Intake_station_lock_prevents_prechecked_empty_slot_deletion_after_intake_commits
- FlightTelemetryPostgresTests.Ingestion_waits_for_a_concurrent_landing_and_then_refuses_the_completed_mission

That run was 973 passed / two failed / zero skipped and is not claimed green. Raw evidence: artifacts/test-results/LA-173-signalr-isolation-full.trx in the primary workspace. The final focused set (ETA reads/producer/model adapter/PostgreSQL plus all six SignalR protocol tests) passed 53/53 with no skips, including the new mission-history guard.

The CI workflow now includes `story/**` alongside develop/main, so a subtask PR can get backend/web/mobile checks on its current head without being pointed at develop. Workflow YAML parsed successfully. This does not change branch rules or authorize bypass on teammates' PRs.

Current-head Linux/PostgreSQL 16 CI must pass before the backend checkpoint is merged into LA-40. Fixing this CI blocker does not complete LA-173's remaining full-flow defects or LA-172 end-to-end acceptance.
