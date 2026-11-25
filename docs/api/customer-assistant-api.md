# Customer assistant API (LA-165)

Source availability: backend, web and Flutter consumers are integrated in the released application. The configured provider may be Ollama (default) or Gemini; all generation and frontend send flags remain default-off. Current activation decisions and deployed end-to-end/quality/load evidence remain under LA-348. See [provider configuration](../deployment/gemini-ai.md) and [synthetic Gemini results](../deployment/LA-348-synthetic-acceptance-20261119.md).

Implements the [Sprint 7 assistant contract](history/sprint-7-api-contract.md#5-customer-assistant--la-165) with existing `AiChatSession`/`AiChatMessage` tables. No migration or dependency on unfinished ETA/summary stories is required. Implementation and passing tests do not mean an enabled model, deployment or completed LA-42 acceptance.

## HTTP boundary

- `POST /api/ai-assistant/messages`: requires all of `sessionId`, `orderId`, `message`; IDs may be null. Extra members are rejected. Trimmed messages contain 1–2,000 Unicode scalar values. Known-length and chunked bodies are bounded to 32 KiB before JSON binding.
- `GET /api/ai-assistant/sessions?page=1&pageSize=20`: owned sessions only, newest creation time/UUID first. Session titles are always null to avoid exposing legacy prompt-derived titles.
- `GET /api/ai-assistant/sessions/{id}/messages?page=1&pageSize=20`: owned session only, globally ordered by creation time/numeric bigint ID before pagination. All IDs are decimal strings, including values above JavaScript's safe integer range. Page >=1; pageSize1–50. Large valid page offsets safely return empty items.
- `DELETE /api/ai-assistant/sessions/{id}`: LA-165 deletion extension supporting the retention proposal. Removes an owned session and messages atomically, including while generation is disabled. Unknown/foreign sessions have identical404.

Bearer JWT only. Every operation checks the persisted ACTIVE CUSTOMER role; staff roles and stale demoted/inactive customer tokens are denied. An explicit session and explicit order are independently checked for ownership before generation. Controller responses use `Cache-Control: no-store`.

## Grounded generation and atomic storage

Static HELP, CLARIFICATION_REQUIRED and REFUSED responses have null session/message IDs and never create or modify a conversation. Questions without a selected order ask for selection, or return help if the customer has no orders. Secret/contact-bearing requests are conservatively refused without echoing their values. Pattern checks cover common credential, code, phone, email, contact/address labels in English/Vietnamese and URLs; this is a conservative supported-input boundary, not a general personal-data classifier.

When explicitly enabled with the required provider/retention approvals and configured model, the classifier receives only the validated current question and bounded owned history: latest20 messages in chronological order, at most12,000 Unicode scalars after dropping oldest whole messages. Sensitive/oversized legacy history is excluded from model context and hidden on history reads. Prior BOT text is never used as current operational truth.

Both the default Ollama adapter and explicit Gemini transport return one intent from STATUS/ETA/HISTORY/HELP/CLARIFY/REFUSE. Neither can supply prose, facts, links, references or operational tool calls. The server renders status, stored arrival estimate or recent chronological events from the currently authorized database snapshot. ARRIVED means destination-station arrival awaiting pickup; DELIVERED means receipt confirmation. Missing/overdue estimates remain unknown/overdue. No raw recipient/contact data, pickup codes, remarks, unrelated orders or telemetry are read into provider facts. No order facts or IDs are needed in the model request at all.

The initial snapshot is a short repeatable-read transaction, fully released before inference. Inference and finalization have a10-second deadline with cancellation propagation. A shared process-local account gate returns429 with Retry-After5; a multi-instance rollout needs a distributed gate before promising a global cap.

The final short PostgreSQL transaction locks the current user, existing session, selected order and used log rows; rechecks persisted access, source facts and conversation generation; then inserts a new owned session if needed and the successful USER/BOT pair. The pair's inserts are separate inside one transaction so numeric IDs preserve USER-before-BOT order. Source/history changes return409. Provider failure, invalid output, lost access or an insert failure commits no successful exchange/new session. Delivery status, ETA, reservations, notifications and all operational resources remain untouched.

Generation is not automatically retried. A response may be lost after a successful commit; recover through owned session/history reads before an explicit retry. Exactly-once retry/idempotency is not claimed.

## Retention and activation

The concrete [retention proposal](assistant-retention-policy-proposal.md) remains subject to owner approval. `CustomerAssistant:Enabled=false`, empty `ApprovedPolicyVersion` and `BackupRetentionDays=0` are safe defaults. No model is installed/started and no real customer chat is generated or stored by verification.

Activation requires the approved policy marker,30-day session lifetime, explicitly confirmed backup lifetime of1–30 days, provider privacy/restore handling and configured model name/revision. The internal generation URL remains loopback; URL credentials, query, fragments and redirects are rejected. For Gemini, the additional `ModelProvider:GeminiAssistantApprovedPolicyVersion` marker must match the actual approved cloud policy before any assistant text is sent. The local proposal alone does not authorize cloud processing; see the [cloud-policy decision package](../deployment/LA-348-cloud-policy-decision.md). Model output is capped at16 KiB. Configuration failure cannot silently enable storage. Web `VITE_AI_ASSISTANT_SEND_ENABLED` and Flutter `AI_CHAT_ENABLED` are separate build flags; neither grants backend permission. Model quality remains rollout/QA work.

With approved retention configuration, expired sessions stop appearing in active reads/generation. Hourly cleanup deletes expired sessions in batches of100 with database cascades; cleanup continues when new generation is disabled. With no approved policy, cleanup does not delete legacy data. User deletion remains available for existing owned data. Application tests cannot verify backup expiry, restore procedures or provider logging; those remain explicit deployment-owner confirmations before activation.

## Verification

Original LA-165 focused suite:76/76 passed, no skips, with PostgreSQL18 locally. Includes actual HTTP/auth boundaries, Unicode/body limits, bigint pagination, privacy refusal before model/storage, grounded status, fake-provider errors, caller cancellation,10-second timeout, same-account concurrency and history races. Those adapter tests use a fake HTTP handler. Later live synthetic Gemini gateway/API evidence is recorded separately in the linked acceptance document; no production customer-cloud approval is implied.

Three real PostgreSQL tests apply actual migrations and cover no model transaction, changed logs/session ownership, genuine post-BOT-insert failure with rollback of pair/new session, and real deletion/retention cascades. The failure injector explicitly proves it is attached; EF8 options must be replaced rather than re-added when configuring it. Full backend/web/mobile CI on the current PR head is the integration gate.

Command: set `TEST_POSTGRES_CONNECTION` to a test PostgreSQL server and run `dotnet test backend/SmartDroneDelivery.Tests --filter FullyQualifiedName~Assistant`. LA-166/167 consumers and LA-168 QA are already included in the LA-42 source integration; current backend/web/mobile CI remains required for future changes.
