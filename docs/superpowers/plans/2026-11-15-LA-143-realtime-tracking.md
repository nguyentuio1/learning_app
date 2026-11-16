# LA-143 Real-Time Tracking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Provide authorized tracking snapshots and SignalR status/position updates for LA-35 consumers.

**Architecture:** A scoped read service returns a deliberately restricted tracking DTO. The authenticated hub allows one selected order per connection. A one-second hosted broadcaster reads committed database facts and revalidates each subscription before sending changes, avoiding changes to transactional workflow writers and avoiding publication of rolled-back data.

**Tech Stack:** ASP.NET Core 8 SignalR, EF Core/Npgsql, xUnit and TestServer.

**Spec:** `docs/api/delivery-workflow-api.md` sections 5.1-5.4; `docs/design/sequence-real-time-tracking.md`; Jira LA-143/LA-35.

## Global Constraints

- Tracking is available to the owning CUSTOMER or active DISPATCHER, MANAGER and ADMIN; other customers receive 404.
- No pickup secure code, recipient details or drone serial number in tracking snapshots/events.
- Position is null outside IN_TRANSIT/IN_AIR or before a sample; no sample in an in-air mission is stale; stale means strictly over 60 seconds.
- Preserve PositionUpdated(TelemetryPosition) and StatusChanged(OrderDecision) wire shapes; one order per connection, no replay guarantee.
- Close expired authenticated connections; reject deactivated/reassigned subscriptions before further publication.
- Publish committed snapshots only. Transport failure does not change persisted state; clients recover with GET after subscribing/reconnecting.

## Review Focus

- Existing JWT after role/account changes must not retain staff tracking privileges.
- Historical missions and late samples must not overwrite current-flight position.
- No private data leakage from another customer's order or hub invocation errors.
- Switching selected orders, disconnecting and expired tokens must remove subscriptions.
- Failed sends and empty/no-sample/terminal snapshots must not invent telemetry or stop updates to other clients.

### Task 1: Authorized tracking read API

**Files:** Create `DTOs/TrackingDtos.cs`, `Services/TrackingService.cs`, `Controllers/TrackingController.cs` under `backend/SmartDroneDelivery.Api`; create `backend/SmartDroneDelivery.Tests/TrackingApiTests.cs`.

**Interfaces:** `ITrackingService.GetAsync(Guid userId, Guid orderId, CancellationToken)` returns internal TrackingRead containing public TrackingSnapshot plus current mission identity and update timestamp. `AuthorizeViewerAsync(Guid userId, CancellationToken)` validates current persisted account/role.

- [x] Write HTTP tests for ownership, staff access, revoked roles, absent/latest/stale/historical telemetry and excluded secrets; run and observe missing endpoint failure.
- [x] Implement GET `/api/delivery-orders/{id}/tracking` using current database role and a single order/mission/sample projection.
- [x] Run focused tests and commit the passing snapshot API.

### Task 2: Authenticated selected-order subscription

**Files:** Create `Tracking/TrackingHub.cs` and `Tracking/TrackingSubscriptions.cs`; modify `Program.cs`; create `backend/SmartDroneDelivery.Tests/TrackingHubTests.cs`.

**Interfaces:** `/hubs/tracking`, `Subscribe(string orderId)`, `Unsubscribe()`; one immutable subscription entry per connection containing caller ID, selected order and expiry.

- [x] Write real TestServer WebSocket protocol tests for anonymous/foreign-order denial, own-order subscription, switching and disconnect cleanup; verify RED.
- [x] Register SignalR, narrowly scoped query bearer-token handling, expiry closure and database-backed subscription authorization.
- [x] Run focused tests and verify no additional dependency is required.

### Task 3: Committed-state live updates and documentation

**Files:** Create `Tracking/TrackingBroadcaster.cs` and `Tracking/TrackingWorker.cs`; extend hub tests; update the API/design tracking sections.

**Interfaces:** `TrackingBroadcaster.PublishAsync(CancellationToken)` revalidates and sends current status/changed latest position; hosted worker calls it every second.

- [x] Write tests that mutate real persisted state and observe wire events; cover revocation, no duplicate/out-of-order sample, new mission and disconnected/expired entries; verify RED.
- [x] Implement bounded sends, isolated per-subscriber failures and serialized publish passes; no workflow-write hooks or schema changes.
- [x] Run full backend/PostgreSQL suite, review the branch and open LA-143 PR into story LA-35. Keep LA-35 unmerged while web/mobile/test subtasks remain incomplete.

## Execution ledger

- Ruling: implement only assigned LA-143 first, enabling other workstreams; do not take over teammates' UI/test tasks.
- Ruling: use committed-state polling inside the backend (one second) to drive SignalR; this meets few-second delivery without changing transactional writers. Clients still resnapshot on reconnect, and intermediate rapid states are not durably replayed.
- Ruling: authorization and subscription state are process-local; horizontal scale requires a shared connection/backplane design outside this task.
- Initial state: LA-143 In Progress; base `30859b4`; isolated worktree `artifacts/worktrees/LA-143`.

- Validation: HTTP RED confirmed missing endpoint (12 failing cases), then 13/13 GREEN. Hub RED confirmed missing transport, then all focused tests GREEN. Live publication RED confirmed missing events, then GREEN. Final focused PostgreSQL tracking suite: 31/31; full sequential PostgreSQL backend suite: 807/807, no skips. Independent review approved with no blockers.
- Delivery note: commits/PR and story integration remain pending remote CI; no claim that LA-35 web/mobile work is complete.
