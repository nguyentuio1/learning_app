# API documentation

Start with the [current endpoint catalog](api-documentation.md). For the configured running API, Swagger is available in Development at `/swagger`.

## Service references

| Area | Reference |
|---|---|
| Authentication and profile | [Authentication API](auth-api.md) |
| Customer addresses, packages and orders | [Customer order API](customer-order-api.md) |
| Notifications | [Notification API](notification-api.md) |
| Stations, slots and station operations | [Station API](station-api.md) |
| Drones | [Drone API](drone-api.md) |
| Dispatch, scheduling and failure recovery | [Delivery workflow API](delivery-workflow-api.md) |
| Staff delivery lookup | [Lookup contract](delivery-insight-lookup.md), [OpenAPI](delivery-insight-lookup.openapi.json) |
| AI ETA | [ETA prediction API](eta-prediction-api.md) |
| AI delivery summary | [Delivery summary API](delivery-summary-api.md) |
| Customer AI assistant | [Customer assistant API](customer-assistant-api.md) |
| Assistant retention proposal | [Local-provider proposal](../decisions/assistant-retention-policy.md), [cloud-policy decision package](../decisions/ai-cloud-processing.md) |

AI generation remains default-off. See [provider configuration and activation](../deployment/gemini-ai.md) and [scoped synthetic Gemini results](../testing/acceptance/gemini-synthetic-acceptance.md). Implementation, activation and acceptance are separate.

## Naming and history

Use lowercase kebab-case names based on the documented service, such as `customer-assistant-api.md`. `README.md` is the conventional GitHub folder entry point. Keep Jira keys in document content rather than service filenames. Use `.openapi.json` for new machine-readable service schemas.

Dated sprint handoffs and partial verification records live in [history](history/README.md). Their existing filenames, results and proposals remain traceable. The sprint JSON files cover selected operations; they are not a complete current API export. Older service contracts also retain explicitly dated design decisions: consult the endpoint catalog and running Swagger for implemented availability. Moving documentation does not rename any HTTP endpoint.
