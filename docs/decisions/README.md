# AI processing and retention proposals

| Proposal | Scope | Status |
|---|---|---|
| [Cloud assistant processing](ai-cloud-processing.md) (LA-348) | Proposed provider data flow, user notice, retention/backup decisions and rollout evidence | Draft; no customer-cloud activation approval recorded |
| [Assistant retention](assistant-retention-policy.md) (LA-165) | Original local-provider session lifetime, user deletion and backup/restore proposal | Draft; does not authorize sending customer text to Gemini |

These records are approval inputs, not evidence that an owner accepted them. Preserve their exact policy identifiers because the application validates those values. Configuration steps are in the [AI deployment guide](../deployment/gemini-ai.md); executed synthetic checks are in [testing](../testing/acceptance/gemini-synthetic-acceptance.md). Neither source integration nor successful synthetic inference grants activation approval.
