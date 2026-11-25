# AI-assisted Services - Class Specification

**Parent:** [LA-201](https://hungndemail.atlassian.net/browse/LA-201). **Task:** [LA-244](https://hungndemail.atlassian.net/browse/LA-244).

**Status:** Proposed service design; existing persistence mapping checked against repository entities and `AppDbContext`. Diagrams are documentation deliverables, not implementation or business/provider approval.

## 1. Scope and sources

Scope: FR-29 ETA, FR-30 delivery-history summaries and FR-31 customer chat. Sources: [SRS](../requirements/SRS.md), [functions F-15–17](../requirements/functions-user-management-and-analytics.md), [business rules BR-UA-15/17/18/21/23/24](../requirements/business-rules.md), [ERD](../architecture/erd.md), [EF mapping](../../backend/SmartDroneDelivery.Api/Data/AppDbContext.cs), and the [class diagram](class-diagram-ai-services.md).

| Deliverable | Sequence / future implementation |
|---|---|
| LA-241 | [ETA](sequence-ai-eta-estimation.md); LA-158 |
| LA-242 | [Delivery summary](sequence-ai-delivery-summarization.md); LA-162 |
| LA-243 | [Customer assistant](sequence-ai-customer-assistant.md); LA-165 |
| LA-244 | This specification and [class diagram](class-diagram-ai-services.md) |

Existing entities/migrations provide storage only; no AI controller/service/provider exists on the current `develop`. Operation names below are logical, not committed routes. The [Sprint 6 API handoff](../api/history/sprint-6-api-contract.md) does not define Sprint 7 AI wire contracts.

## 2. Existing persistence classes

### AiEtaPrediction / ai_eta_predictions

[Source](../../backend/SmartDroneDelivery.Api/Entities/AiEtaPrediction.cs).

| Property | C# / PostgreSQL / nullability | Meaning and boundary |
|---|---|---|
| Id | Guid / uuid, PK | Generated key. |
| OrderId | Guid / uuid, FK, unique | One current prediction per order. Order deletion cascades; no history collection. |
| CalculatedDistanceKm | decimal / numeric(6,2), required | Model distance feature in km, not proof of a flown route; validate finite/nonnegative and representable values before save. |
| PredictedDurationMinutes | int / integer, required | Duration under an agreed time anchor. Validate plausible range/rounding in the service; the entity alone does not enforce it. |
| WeatherCondition | string? / varchar(50), nullable | Optional vetted condition; unknown is null, not invented clear weather. Observation time/provider provenance are not stored by this entity. |
| ConfidenceScore | decimal? / numeric(4,3), nullable | Optional [0,1] confidence; application validation required. It is not a guaranteed probability or accepted accuracy result. |
| CreatedAt | DateTime / timestamptz, required | UTC time of the current calculation. A failed recalculation must not refresh this timestamp. |
| Order | DeliveryOrder navigation | Existing one-to-one mapping. |

Upsert the current row under order-level serialization. Prediction plus any applicable `DeliveryOrder.EstimatedDeliveryTime` update must be atomic and compare the input generation again before commit. The order's `UpdatedAt` is already a concurrency token; the full input stamp remains proposed service metadata. Existing [DeliveryRouteEstimate](../../backend/SmartDroneDelivery.Api/Services/DeliveryRouteEstimate.cs) consumes positive stored prediction distance/duration as authoritative full-route values for [scheduling and range eligibility](../../backend/SmartDroneDelivery.Api/Services/SchedulingService.cs). Therefore remaining-flight or differently anchored predictions must not overwrite this row. Gate both row and timestamp writes on the reservation-safe full-route/full-duration policy in [LA-241](sequence-ai-eta-estimation.md); if unresolved, write neither. Separate informational storage/read paths require a reviewed design/migration before use.

There are no existing ArrivalTimestamp, InputHash, ModelVersion, WeatherObservedAt, FreshnessState or prediction-history columns. Exposing such response metadata requires deriving it reliably or adding reviewed persistence; do not invent entity fields in implementation assumptions.

### AiChatSession / ai_chat_sessions

[Source](../../backend/SmartDroneDelivery.Api/Entities/AiChatSession.cs).

| Property | Type / constraint | Rule |
|---|---|---|
| Id | Guid / uuid PK | Opaque session key; not an authorization token. |
| UserId | Guid FK users, required | Owner; user deletion cascades. Every session/message query is scoped through this relationship. |
| SessionTitle | string? / varchar(200) | Optional safe title; no credentials or sensitive automatic prompt echo. |
| CreatedAt | DateTime / timestamptz | UTC. |
| User / Messages | Navigations | One user; zero or more messages. No direct OrderId association exists. |

No escalation/status/retention/client-request-id fields currently exist. Session possession does not authorize all referenced orders or make historical BOT statements current facts.

### AiChatMessage / ai_chat_messages

[Source](../../backend/SmartDroneDelivery.Api/Entities/AiChatMessage.cs).

| Property | Type / constraint | Rule |
|---|---|---|
| Id | long / bigint PK identity | Stable tie-breaker for message ordering. |
| SessionId | Guid FK session, required | Session deletion cascades. Derive access through owned session, never message ID alone. |
| SenderType | string / varchar(20), required | USER or BOT assigned server-side. This is an application rule; no database enum is claimed. |
| MessageText | string / text, required | Sanitized bounded content; length/redaction/retention policy must be fixed in the API contract. No raw secret input or fabricated successful BOT answer. |
| CreatedAt | DateTime / timestamptz | Order by CreatedAt then Id ascending within an owned session. |
| Session | Navigation | Parent session. |

Proposed chat persistence commits a successful sanitized USER/BOT pair and, if needed, the owned session together after generation/reauthorization. Failure rolls the transaction back. No provider call is made while holding the transaction. Exactly-once replay is not supported by the current schema and requires a separate idempotency design before automatic retries.

There is no existing summary table/column. Summary generation is read-only and transient in this design.

## 3. Proposed service and boundary classes

| Class | Responsibility / operations | Data and failure constraints |
|---|---|---|
| EtaController | ReadEstimate(caller, orderId) | Active CUSTOMER owns the order; active DISPATCHER/MANAGER need operational scope. Derive caller server-side; no model call just to read. |
| SummaryController | Summarize(caller, orderId) | Same permitted reader roles/scope; rejects unknown/foreign customer references before retrieving provider context. |
| CustomerAssistantController | SendMessage(caller, request), ReadHistory(caller, sessionId, page) | Active CUSTOMER only; bounded input/history. External integration API keys do not authenticate these first-party AI operations. |
| AuthorizedAiFactsReader | ReadOrderScope, ReadOwnedSession, ResolveOwnedReference, ReadMinimalSnapshot, Revalidate | Returns narrowly projected facts with immutable input/source stamp. Always constrain customer ownership independently of any concurrent role promotion. Recheck current access after external work; no global unscoped retrieval/RAG. |
| EtaPredictionService | Recalculate(trustedTrigger, orderId), ReadEstimate(scope) | Validate geometry/weight/anchor/conditions, invoke model, discard superseded generation, and gate both shared prediction and timestamp writes on scheduling-compatible full-route/full-duration semantics. Separate informational estimates require a separate path. Does not control flights/slots. |
| DeliverySummaryService | Generate(scope) | Read chronological vetted facts, request grounded text, revalidate scope/source, return labeled text or explicit no-history/error. No operational or summary persistence. |
| CustomerAssistantService | Send(caller, request), ReadHistory(caller, sessionId, page) | Scope session and each order, clarify ambiguity, minimize/redact context, validate answer, reauthorize and persist successful pair atomically. Never grants model tool authority. |
| WeatherObservationSource | ReadVerifiedObservation(features) | Optional observation plus provenance/time or explicit unavailable. Provider/cache/source and maximum age remain open; no new weather entity is assumed. |
| EtaModelGateway | Predict(EtaFeatures) | Receives minimized numerical/condition features, returns candidate duration/confidence or failure; bounded timeout/cancellation. No order credentials, customer identity or database write access. |
| TextGenerationGateway | Summarize(SummaryContext), Answer(AssistantContext) | Untrusted generation boundary. Context text is data, not instructions. No arbitrary URL/SQL tools, state mutations or cross-customer context store. Provider approval is separate. |

Names in the [sequences](sequence-ai-customer-assistant.md) and [diagram](class-diagram-ai-services.md) match this table. `DB` in sequences is PostgreSQL via the existing data layer, not another new service.

## 4. Proposed transient values and privacy

These are conceptual service/response values, not existing database types or exact JSON schemas.

| Value | Allowed content |
|---|---|
| Authorized order scope | Server-resolved actor, fixed customer/operational restriction and selected order correlation. Never derive a broader scope from prompt text. |
| Input/source stamp | Version of every used input, related mission/history and relevant observation times. Detects changes during model calls; not merely a token for Order.UpdatedAt. |
| EtaFeatures | Valid station/current-flight geometry, distance, linked package weight, agreed time anchor and optional verified conditions. Internal IDs stay at the server boundary. |
| Estimate view | Estimate versus actual distinction, calculation/source time, optional confidence and current/stale/unavailable indication. Do not derive a timestamp from duration without an agreed anchor. |
| SummaryContext / view | Vetted chronological order facts and labeled predictions; generated safe text with source/generation time. No raw free-form remarks or identity/contact/credential data. |
| AssistantContext / reply | Sanitized input, bounded owned history, freshly authorized delivery facts, safe text and allowlisted first-party actions. No pickup QR/code retrieval action. |

All generated text is informational and untrusted. Render safely and retain the authoritative timeline/status UI when generation fails. Never rewrite ARRIVED to DELIVERED because a model says the package arrived. Model prompts, outputs, telemetry and debug traces must not contain credentials/secure codes or unrelated customer data.

## 5. Open decisions and implementation acceptance

| Decision | Must be settled for future implementation |
|---|---|
| AI API contract | Exact routes, request/response schemas, limits, errors, rate/cost control, bounded pagination and role scopes. |
| ETA policy | Target/anchor, eligible states, trigger cadence, distance/weight/weather sources, confidence/freshness/rounding bounds, reservation-safe synchronization and input-version persistence. |
| Provider and privacy | Model/provider/hosting, allowed context, retention/training settings, secret redaction and deployment credentials. This documentation does not authorize sending customer data to a provider. |
| Generation quality | Language/length, factual validation and latency/timeout targets. The design claims no measured accuracy or response SLA. |
| Conversation lifecycle | Maximum message/context size, retention/deletion, retry deduplication and uncertain-response recovery. Current schema only supports owned sessions and messages. |
| Optional extras | Prediction history, summary caching, vector stores, human escalation, assistant tools and model-specific confidence gates need separate scope/acceptance; they are not inferred from illustrative activity diagrams. |

Documentation acceptance: each sequence identifies its authorized inputs, generation boundary, missing/failure behavior and persistence; the class diagram reflects existing entities separately from proposed services; ownership is enforced before context retrieval and after generation; no secret or cross-customer retrieval is permitted. Future application tests are listed in each sequence and are not claimed as executed here.
