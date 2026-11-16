# LA-150 Customer Delivery Confirmation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow an active owning customer to confirm receipt using the pickup secure code and atomically complete an arrived order.

**Architecture:** Add the customer-scoped endpoint proposed by the existing delivery-confirmation design. Reuse the existing pickup core and OTP audit counter, with persisted customer ownership/role rechecked under the order lock. Keep the operator route and response contract compatible.

**Tech Stack:** ASP.NET Core 8, EF Core/Npgsql, xUnit.

**Spec:** Jira LA-150/LA-37; `docs/design/sequence-delivery-confirmation.md` section 6.1; `docs/api/station-api.md` section 4.4; `docs/api/customer-order-api.md` decisions 5 and 7.

## Global Constraints

- POST `/api/delivery-orders/{id}/confirm-delivery`, owning active CUSTOMER only; unknown/foreign order returns identical 404.
- Body contains only `pickupSecureCode`; never trust station, tracking number, owner or status supplied by a caller.
- Existing ARRIVED state, linked completed mission, occupied current destination-slot ownership rules remain mandatory.
- Share the existing three-failure/ten-minute OTP lockout across customer confirmation, operator pickup and advisory validation.
- Success atomically writes DELIVERED, actualDeliveryTime, one status log/notification/audit and frees the destination slot.
- No secure code in response, audit/status logs or notification content; repeat/conflicting completion returns 409.

## Review Focus

- A stale CUSTOMER JWT after account deactivation or role change must not authorize confirmation.
- Account/ownership changes while waiting on a database lock must be rechecked before consuming attempts or completing.
- Customer and operator requests racing must complete once or share the same lockout budget.
- Audit persistence failure must roll back the status, timestamp, slot, logs and notification together.
- A historical/reused slot must never be freed by an old order confirmation.

### Task 1: Customer API and shared pickup core

**Files:** Create `Controllers/DeliveryConfirmationController.cs`, `Controllers/PickupResponses.cs`, `DTOs/DeliveryConfirmationDtos.cs`, `Services/StationOperationService.DeliveryConfirmation.cs`; modify `StationOperationService.cs` and `StationOperationsController.cs`; create `Tests/DeliveryConfirmationTests.cs` (under the corresponding backend projects).

**Interfaces:** Add `IStationOperationService.ConfirmDeliveryAsync(Guid userId, Guid orderId, ConfirmDeliveryRequest request)` returning existing `PickupVerificationResult`; private pickup core receives a nullable customer order ID to distinguish fixed server-selected authorization scope.

- [ ] Write HTTP tests for success, wrong/locked codes, state/slot conflicts, account/ownership validation and secret exclusion; observe missing-route RED.
- [ ] Extract the existing pickup body behind the unchanged operator signature; add the owning-customer wrapper and recheck caller scope after loading the locked order in both storage paths.
- [ ] Share HTTP response formatting and run focused plus existing station pickup tests.

### Task 2: PostgreSQL concurrency, rollback and documentation

**Files:** Create `Tests/DeliveryConfirmationPostgresTests.cs`; update API/design documents.

- [ ] Verify customer/operator concurrency completes once and shares the wrong-code budget.
- [ ] Verify ownership changes during lock wait cannot complete another customer's order; verify rollback on an injected audit failure.
- [ ] Run full PostgreSQL backend suite and independent review. Open a PR, require backend/web/mobile CI on the current head, then merge the subtask into story LA-37. Keep the story open while LA-151/LA-152 remain incomplete.

## Execution ledger

- LA-150 and LA-37 moved to In Progress; base is develop `30859b4`.
- Ruling: implement customer self-service described in the existing design because LA-37 explicitly requires recipient confirmation and the operator-assisted endpoint already exists. This route requires customer ownership plus the secure code and introduces no anonymous recipient flow.
- Ruling: preserve the existing transaction and OTP lockout implementation rather than create independent counters or a second completion state machine.

- Validation: HTTP RED observed missing-route failures before implementation; 29 customer/legacy pickup tests passed. Final customer/PostgreSQL focused suite: 26/26. Full sequential PostgreSQL backend suite: 802/802, no skips. Independent code review approved with no blockers.
- PostgreSQL tests observe real lock waits, confirm a shared customer/operator guess budget, prevent double completion, recheck ownership/account/role after waiting, and inject audit failure to prove rollback.
- Next priority explicitly requested by the user: LA-341 Sprint 6 API contracts before any other feature story.
