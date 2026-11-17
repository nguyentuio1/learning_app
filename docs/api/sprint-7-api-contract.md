# Sprint 7 AI API contract — working draft

Task: [LA-342](https://hungndemail.atlassian.net/browse/LA-342). Status: **proposed implementation and mocking baseline**, based on `develop` at `321733b198834a25db6a8f04fbc4f9f1a4492ef0`. No endpoint below is advertised as implemented or deployed. Jira checked on 2026-11-16: Sprint 7 (ID 7, board 2) is active, 16–23 November. Sprint 6 is closed. Consumer approval is still pending; production gates below remain open.

Sources: [AI class specification](../design/class-specification-ai-services.md), [ETA sequence](../design/sequence-ai-eta-estimation.md), [summary sequence](../design/sequence-ai-delivery-summarization.md), [assistant sequence](../design/sequence-ai-customer-assistant.md), [existing HTTP conventions](sprint-6-api-contract.md).

Machine-readable draft: [sprint-7-openapi.json](sprint-7-openapi.json). It reserves the proposed operations below; it does not advertise deployed functionality.

Explicit Confluence handoff: [Sprint 7 AI API Contract and Documentation Handoff](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=18579461). Consumer approval is tracked through LA-342; publication does not imply implementation acceptance.

## 1. Delivery and ownership

| Area | Existing implementation reference | Handoff |
|---|---|---|
| ETA, story LA-40 | Backend LA-158 (TBA Review); web LA-159 (Trần Nguyễn); mobile LA-160 (Hung Nguyen); tests LA-161 (Phan Van Khai) | Implement the read boundary first; prediction production has the separate gates in section 3. |
| Summary, story LA-41 | Backend LA-162 (TBA Review); web LA-163 (Trần Nguyễn); tests LA-164 (Phan Van Khai) | Transient, grounded generation; no summary storage exists. |
| Assistant, story LA-42 | Backend LA-165 (TBA Review); web LA-166 (Trần Nguyễn); mobile LA-167 (Hung Nguyen); tests LA-168 (Phan Van Khai) | Owned conversations, bounded context and atomic successful exchanges. |

These assignments were verified on Jira. LA-43 mobile completion belongs to Nhân Lưu Thiện (LA-169/171) and Hung Nguyen (LA-170); LA-44 integration belongs to Phan Van Khai, with defects assigned to TBA Review (LA-173). LA-204 report consolidation belongs to TBA Review. All were To Do at inspection. Mobile PR [#207](https://github.com/nguyentuio1/learning_app/pull/207), head `ad5e1769c125e8d4979e58c16302833d428391d3`, is open against `story/LA-40-eta-estimation` with changes requested; it does not establish completed ETA acceptance.

Confluence publication is explicit work: no repository-to-Confluence synchronization has been established. Module owners prepare their API/design/testing changes and evidence; the documentation owner reviews and publishes the consolidated pages. Reuse existing documentation issues where their acceptance criteria already cover the work. Add narrowly scoped missing publication tasks only after checking the actual pages and Jira assignments.

Documentation responsibilities are recorded on existing LA-204/253/254/255: Phan Van Khai consolidates SRS, business rules and test evidence; Trần Nguyễn owns web flows and design-chapter consolidation; Nhân Lưu Thiện supplies customer-mobile/customer-order flows; Hung Nguyen supplies station/mobile-AI flows and formatting consistency. TBA Review supplies backend/AI design and performs final synthesis/approval. Each module owner supplies source commit/PR and affected Confluence page/version links. LA-183 remains the Sprint 8 final implemented API guide; LA-342 is the earlier proposed contract handoff, not a duplicate of that acceptance work.

Confluence audit on 2026-11-16: Overview (98403), Software Design Description (11993109) and Detailed Design (14090271) still cite Sprint 5 implementation snapshots, last updated 2026-11-14. Treat those as dated evidence, not current Sprint 7 availability. This audit does not claim those pages have been reconciled with every Sprint 6 merge.

## 2. Common rules

- All paths include `/api`. Use first-party bearer authentication, JSON camelCase, UUID strings and ISO 8601 UTC timestamps. All roles below require the current persisted ACTIVE account and role, not only JWT claims. Integration API keys are not accepted.
- ETA and summary: owning `CUSTOMER`, or active persisted `DISPATCHER`/`MANAGER`. For these redacted resources, staff scope is explicitly project-wide (any order), consistent with those roles' existing tracking read predicate. The current model has no organization/station partition for these roles; do not invent one or reuse station-operator assignment as authorization. This new-route policy does not grant staff access to customer order detail/contact data. `ADMIN` and `STATION_OPERATOR` are denied. Assistant: active `CUSTOMER` only.
- `401` invalid authentication; `403` inactive/disallowed role; `404` identical for an unknown or foreign order/session; `400` invalid input. Check role, ownership and validation before any model call. Recheck current access after generation before returning protected content.
- Use the existing problem body (`type`, `title`, `status`, `detail`, optional validation `errors`); clients branch on status/fields rather than English wording. `409` means source facts changed during generation; `503` means the generator is unconfigured/unavailable; `504` means its deadline expired. Cancellation does not fabricate a success.
- Return `Cache-Control: no-store` on protected AI responses. Render generated content as text; no raw HTML, executable Markdown or model-authored URLs. Never expose pickup codes, credentials, contact details or raw telemetry in AI payloads, prompts or logs.
- The proposed generation deadline is 10 seconds including cancellation propagation. Summary/chat reject concurrent generation for the same authenticated account with `429` and `Retry-After: 5`. This is a proposed deployment requirement, not an existing rate limiter; multi-instance deployments need a shared limiter before promising an account-wide cap.

## 3. ETA — LA-158

### GET /api/delivery-orders/{id}/eta

Read-only: never invoke a model or write predictions on a GET. Return the authorized current order snapshot even when no prediction exists:

```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "status": "IN_TRANSIT",
  "estimatedDeliveryTime": "2026-11-16T08:24:00Z",
  "actualDeliveryTime": null,
  "aiEtaPrediction": null
}
```

All five fields are required; the timestamps and prediction are nullable. The estimate is the stored `DeliveryOrder.EstimatedDeliveryTime`, including an ordinary scheduling estimate when there is no AI prediction. Do not derive it from prediction duration or label a scheduler estimate as AI output. `actualDeliveryTime` means the existing order field, not an inferred recipient-pickup timestamp.

When a stored prediction is available, `aiEtaPrediction` has exactly:

```json
{
  "calculatedDistanceKm": 12.35,
  "predictedDurationMinutes": 18,
  "weatherCondition": null,
  "confidenceScore": null,
  "createdAt": "2026-11-16T07:55:00Z"
}
```

All five properties are required; weather and confidence are nullable. Distance is full-route km, duration is the full flight in integer minutes, weather is at most 50 characters, confidence when known is a decimal in [0,1]. `createdAt` is the stored calculation time, not response time. Do not invent weather or confidence. The existing schema does not prove prediction freshness against changed inputs; clients must not infer “fresh” from this DTO. Invalid legacy predictions must be omitted rather than used to synthesize an arrival time.

Compatibility requirement: `GET /api/delivery-orders/{id}` retains its existing customer-only authorization and fields, and gains the same typed nullable `aiEtaPrediction` shape. Mobile LA-160 currently reads the order-detail route, so adding only the dedicated route does not unblock it. Share only the prediction projection, not the entire detail DTO or its access boundary: staff authorization on the dedicated ETA route must not grant staff access to customer detail/contact data. Both routes require ownership when accessed by a customer.

Consumer behavior:

1. Read on page entry, after reconnect and after every order status notification. An `APPROVED` screen must continue observing transitions to `IN_TRANSIT`; do not subscribe only when already in transit.
2. While the page is visible, refresh every 30 seconds with at most one request in flight. A corrected ETA can change without a status change. Ignore obsolete responses after changing orders or leaving the page.
3. Recompute a displayed countdown from the absolute timestamp and current clock, rather than freezing the original duration. Clear countdowns for `ARRIVED`, `DELIVERED`, `CANCELLED`, `FAILED`; arrival and recipient pickup remain different events. For absent/overdue estimates, show unavailable/delayed, not a fabricated future arrival. A future timestamp alone does not justify a “Live estimate” or fresh-AI label; model-input freshness is not recorded by the current schema.
4. On `401`/`403`/`404`, clear protected state. On transient failure, label previously loaded values as cached; do not replace failure with a successful empty prediction.

### Prediction production is a separate acceptance gate

There is no public recalculate endpoint in this draft. Trusted server-side triggers must be implemented and tested separately. The existing `DeliveryRouteEstimate` feeds prediction distance/duration into drone range checks, scheduling and reservation windows. **Remaining-flight predictions must never overwrite the full-route row.**

Before enabling a producer, settle: model/provider and hosting, prediction anchor, eligible states/triggers, package-weight/weather inputs, rounding and duration bounds, input-generation persistence, and reservation-safe handling of any revised full-flight duration. No external provider or customer-data transfer is authorized by this document.

Production must snapshot all relevant inputs, call the model outside a database transaction, then serialize per order and recheck the complete input generation. Commit the prediction and any permitted order ETA update atomically; reject superseded inputs. An order `UpdatedAt` check alone does not cover package/station/weather changes. Do not refresh `createdAt` after a failed calculation. If reservation safety cannot be proven, update neither the shared prediction nor order ETA.

Completing the read endpoint is a useful first slice, but does not complete LA-158's estimator or LA-40's acceptance criteria.

## 4. Delivery summary — LA-162

### POST /api/delivery-orders/{id}/ai-summary

No request body. Return an informational, transient summary of the authorized frozen timeline:

```json
{
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "outcome": "GENERATED",
  "text": "The order arrived at the destination station and is awaiting pickup.",
  "generatedAt": "2026-11-16T08:25:00Z"
}
```

All fields required. `outcome` is `GENERATED` or `NO_HISTORY`; for `NO_HISTORY`, `text` and `generatedAt` are null and no provider call occurs. Generated text is at most 4,000 Unicode scalar values. `generatedAt` is generation completion, not proof of timeline freshness; compare the frozen source generation before responding and return `409` if it changed. No summary/history row is written.

Order facts are chronological by log `createdAt` then ID. Preserve failures and reschedules. `ARRIVED` must never become recipient delivery. Only allowlisted factual reason categories may enter the context; raw free-form remarks, recipient identity/contact, codes and unrelated orders are excluded. Provider failure returns a problem response; the authoritative timeline remains usable.

## 5. Customer assistant — LA-165

### POST /api/ai-assistant/messages

```json
{
  "sessionId": null,
  "orderId": "e8a3d1f4-5b62-4c79-8a0e-2f6b9c1d7e33",
  "message": "What is the current delivery status?"
}
```

All three fields required; session/order IDs nullable. Trim the message; require 1–2,000 Unicode scalar values after trimming and bound the raw request size to 32 KiB. Clients cannot supply a user ID, sender type, system prompt or model options. An explicit foreign/missing resource yields the same `404`; verify each referenced order independently of session ownership. Reject unsupported secret-bearing requests without echoing or persisting their sensitive contents.

Proposed successful exchange:

```json
{
  "outcome": "ANSWERED",
  "sessionId": "83ec4120-6388-426f-80a7-c8b627998d32",
  "userMessageId": "1201",
  "reply": { "id": "1202", "senderType": "BOT", "messageText": "The order is in transit.", "createdAt": "2026-11-16T08:15:00Z" }
}
```

Message IDs are decimal strings to preserve PostgreSQL bigint precision in JavaScript. `reply.messageText` is at most 4,000 Unicode scalar values. Additional outcomes `CLARIFICATION_REQUIRED`, `HELP`, `REFUSED` have null session/message IDs and a nonpersisted `reply` with null `id`, `senderType: BOT`, safe explanatory text and response time. They never create or mutate a session, even if the request names an existing one. No model-provided actions or links are accepted in this initial contract.

For `ANSWERED`, read at most the 20 newest owned messages in chronological order, bounded to 12,000 Unicode scalar values total; omit oldest whole messages until within budget. Re-authorize all current order facts. Earlier BOT text is untrusted history, not current operational truth. Call the provider without a database transaction, then recheck access/source facts and atomically persist the successful USER/BOT pair plus a new owned session if needed. Provider failure, lost authorization or changed source commits no pair/session. No operational tool or order mutation is exposed.

Assistant persistence is disabled until a conversation retention/deletion policy is approved and implemented. That policy must state the retention period, user deletion behavior, cleanup cadence, backup handling and provider retention/training settings. Bounded model context does not bound database retention. The existing cascade on user/session deletion is not a complete retention policy; do not enable writes or deploy chat generation on that basis alone.

No idempotency key exists in the current schema: clients must not automatically retry this POST after an uncertain response. Read owned history before an explicit user retry; a lost response for a newly created session also requires the session-list endpoint below for recovery.

### GET /api/ai-assistant/sessions?page=1&pageSize=20

Owned sessions only; page >= 1, pageSize 1–50. Return `{ "items": [{ "id": "<uuid>", "sessionTitle": null, "createdAt": "<UTC>" }], "page": 1, "pageSize": 20, "totalCount": 1 }`. Order by `createdAt`, then ID descending. Titles are nullable, at most 200 characters and sanitized; do not copy prompts containing sensitive data into titles. A read never creates a session.

### GET /api/ai-assistant/sessions/{id}/messages?page=1&pageSize=20

Owned session only; identical missing/foreign `404`. Same pagination limits. Return `{ "sessionId": "<uuid>", "items": [{ "id": "1201", "senderType": "USER", "messageText": "...", "createdAt": "<UTC>" }], "page": 1, "pageSize": 20, "totalCount": 2 }`. Global order is `createdAt`, then numeric ID ascending before pagination. IDs remain strings on the wire. An empty owned session returns an empty list. Offset pages are not a frozen snapshot while new messages arrive; deduplicate by ID when refreshing.

### DELETE /api/ai-assistant/sessions/{id} — LA-165 deletion extension

Persisted active CUSTOMER and owned session only. Return `204` after atomically deleting the session and its messages. Unknown/foreign sessions have the same `404`. This operation remains available for existing owned data while generation is disabled. The [LA-165 retention proposal](LA-165-retention-policy-proposal.md) describes the approval/backup/cleanup activation gate; [implementation notes](LA-165-customer-assistant.md) record backend availability separately from rollout and story acceptance.

## 6. Release checklist

- Correct-site access and Jira ownership verified; LA-342 created after duplicate search. Existing documentation tasks reused with module responsibilities. Publish an explicit Confluence handoff and retain source/version evidence.
- Review these proposed routes/shapes with the existing mobile/web consumers and create machine-readable OpenAPI schemas from the settled contract. Resolve provider and ETA production policy before enabling generation.
- Add authorization/ownership parity, no-provider-on-denial, cancellation, concurrent-source-change and privacy tests. ETA additionally needs detail-route compatibility and reservation-coupling coverage; chat needs failed/competing-exchange atomicity and bigint pagination tests.
- Publish the reviewed contract first, then implement backend slices under their actual task keys. Do not mark a story Done for a document or partial backend slice.
- Require current-head backend/web/mobile CI for completed story integration into `develop`; confirm remote ancestry before reconciling Jira. Deployment and business/model accuracy acceptance remain separate.
