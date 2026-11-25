# LA-165 conversation retention proposal

> Local-provider proposal. This document intentionally retains its original local-only scope. It does not authorize Gemini processing; see the separate [cloud-policy decision package](../deployment/LA-348-cloud-policy-decision.md) and [current assistant API](customer-assistant-api.md). Both provider and retention gates remain enforced.

Status: **DRAFT — not approved or enabled**. This proposal makes the activation gate in [LA-342](history/sprint-7-api-contract.md#5-customer-assistant--la-165) concrete. Backend code and tests may be reviewed/merged with storage and model generation disabled.

Proposed version: `LA-165-retention-v1`.

- Lifetime: delete each session and all its messages30 days after session creation. Continued activity does not extend this deadline. Expired sessions are excluded from active reads/generation immediately after cutoff.
- User deletion: `DELETE /api/ai-assistant/sessions/{id}` permanently deletes an owned session and its messages from the active database in one committed operation. Missing/foreign sessions return the same404. Deletion is available for existing owned data even while generation is disabled.
- Cleanup: an hourly bounded cleanup deletes expired sessions; database cascading removes messages. Cleanup remains active whenever the approved retention configuration is present, including when new generation is disabled. No deletion runs merely because defaults exist.
- Backups: retain backups for at most30 days; deleted data may remain only in access-controlled backups until their expiry. Before a restore is served, rerun retention cleanup and reconcile subsequent user-deletion records under the deployment's restore procedure. Backup/deletion-record handling must be confirmed by the deployment owner; application code alone cannot prove it. Do not enable persistence without that confirmation.
- Provider: local loopback Ollama only. No external customer-data transfer, training, prompt/response archival or raw-message application logs. Runtime/proxy/provider configuration must be reviewed to enforce those settings before activation. No model is installed or activated by this change.
- Rollout: owner approves this exact version, confirms backup/restore and local-provider handling, and configures the approved-policy marker and backup lifetime explicitly. Default marker is empty and generation is off. Multi-instance generation also requires a shared account limiter before rollout.

Application/API tests use synthetic local data and explicit test-only policy configuration. They do not establish production policy approval, backup settings or real-model quality.
