# LA-348 cloud assistant decision package

Status: **DRAFT. No owner approval recorded. No customer cloud chat activation authorized.**
Proposed cloud marker: `LA-348-gemini-retention-v1`. Prepared 2026-11-19.

## Proposed scope and data flow

The authorized free-tier exercise uses invented fixtures only. Do not substitute customer messages or production records. Normal delivery operations remain independent of model availability.

| Service | Server sends to fixed Google HTTPS endpoint | Server retains/returns |
| --- | --- | --- |
| ETA | Distance, weight, fragility, cruise speed, unknown weather; generation schema/instructions | Validated duration, input fingerprint and configured model revision; delivery ETA API remains owner/staff scoped |
| Summary | Current status, status transitions and timestamps; schema/instructions | Grounded text rendered from validated event indexes; no generated operational facts |
| Assistant | Current question and filtered history, plus classification instructions; no order object, contact fields or pickup codes | Intent classification; server renders owned delivery facts and stores successful USER/BOT exchange in an owned session |

Assistant question limit is 2,000 Unicode characters. History considers the most recent 20 messages, excludes detected sensitive text and messages over 4,000 characters, and trims to 12,000 characters. These filters are heuristic and do not guarantee removal of personal information. Current free-form text/history must therefore remain disabled for real customers under the unpaid arrangement.

## Provider terms requiring an owner decision

The official [Gemini terms](https://aistudio.google.com/docs/terms), effective March 23, 2026 and checked November 19, 2026, distinguish unpaid and paid service data handling. Unpaid inputs/outputs may support product improvement and human review; the terms prohibit submitting sensitive, confidential or personal information. Paid requests are not used for product improvement, but limited provider safety logging and international processing remain. The terms also impose age and regional restrictions. The owner must assess eligibility and the applicable data arrangement. Application retention cannot promise deletion of Google's copies. See also [official pricing](https://aistudio.google.com/docs/pricing).

Do not infer the project's billing status from possession of a key. No billing activation, paid upgrade or policy acceptance is authorized by this document. Owner consent does not override provider restrictions.

## Proposed notice for review

**Synthetic demonstration notice (free-tier exercise):**

> This demonstration sends invented delivery questions to Google Gemini to classify the question. Use only the supplied example messages. Do not enter real names, contact details, addresses, pickup codes, account information or other personal information. Google may use submitted content and responses to improve its services and may have human reviewers process it. This demonstration does not enable customer conversations.

**Customer notice candidate, usable only after a suitable provider arrangement is approved:**

> When you send a delivery question, we send its text and a limited selection of recent messages to Google Gemini for classification. The application uses records from your selected delivery to prepare the reply. Do not include pickup codes, passwords or contact details. Conversations expire 30 days after the session begins, even if you keep using it. You can delete a conversation from active storage. Deleted content may remain in restricted backups for up to [owner-approved number, 1–30] days. Google processes submitted content under the approved provider terms, including applicable safety logging. Delivery operations remain available if the assistant is unavailable.

The second notice is a candidate, not deployed copy. The owner must approve the exact provider arrangement and complete the backup number and privacy/contact wording before release. Notice display alone is not proof of any required consent or lawful basis.

## Decisions and evidence required before activation

| Owner decision | Required reviewable evidence | Current status |
| --- | --- | --- |
| Synthetic demonstration only, local model, or suitable paid cloud arrangement for real customer text | Named owner, selected scope/provider/project, applicable terms and date; region/age eligibility review | Missing |
| Which messages may leave the server | Approve current question/history scope, or require structured messages/redaction changes before activation | Missing |
| Application retention | Approve fixed 30-day session lifetime and explicit backup lifetime of 1–30 days | Missing |
| Actual backup expiry and restoration handling | Backup configuration, deletion-record procedure and exercised restore cleanup before serving restored data | Missing |
| User notice and any consent/legal basis | Approved final notice, placement and required consent evidence | Missing |
| Operational rollout limits | Project model/quota evidence, deployment topology, shared limiter for multiple instances, performance acceptance results | Missing |

Only after these decisions are recorded may a deployment set the existing assistant policy controls **and** `ModelProvider:GeminiAssistantApprovedPolicyVersion=LA-348-gemini-retention-v1`. The backend enabled flag and frontend composer build flag are separate. Defaults and markers remain unchanged by this package. A synthetic test fixture's explicit test-only markers never constitute deployment approval.

## Measurable acceptance boundary

Each model service has a 10-second application deadline; a 20-second direct gateway client timeout is not proof of a 3-second requirement. Before claiming that requirement, record the agreed percentile, sample count, concurrent users, model version, quota tier, request shapes and complete API latency distribution. Preserve 503/504/429 outcomes; do not count them as successful generations. Existing deterministic account-slot tests prove local isolation only, not real provider concurrency or a distributed limiter.

For model validation, record tested commit, UTC timestamp, requested model and provider-returned `modelVersion`, service result, latency, and synthetic fixture identifiers. The opt-in `LiveGeminiApiAcceptanceTests` exercises summary HTTP generation, assistant HTTP generation/deletion, real ETA persistence and foreign-order rejection without cloud calls. It uses an isolated in-memory database and is not PostgreSQL, UI or production acceptance.
