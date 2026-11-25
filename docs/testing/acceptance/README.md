# Recorded runtime and service acceptance

| Record | Jira | What it establishes |
|---|---|---|
| [Customer journey](customer-journey.md) | LA-363 | Supported API-backed Flutter browser journey; prepared ARRIVED fixtures are disclosed |
| [Operator journey](operator-journey.md) | LA-364 | Assigned-station intake and pickup in the recorded Flutter browser runtime |
| [Flutter photo upload/display](flutter-photo-upload.md) | LA-346 | Actual Flutter browser package/avatar uploads against API, PostgreSQL and MinIO |
| [MinIO verification procedure](minio-photo-verification.md) | LA-346 | Storage URL setup, regression command and end-to-end verification steps; a procedure alone is not an executed pass |
| [Assistant availability/recovery](assistant-availability.md) | LA-365 | Default-off and 503/uncertain-send behavior using intercepted synthetic HTTP fixtures |
| [Operator station refresh](operator-station-refresh.md) | LA-367 | Station-scoped refresh behavior in the recorded runtime |
| [Synthetic Gemini checks](gemini-synthetic-acceptance.md) | LA-348 | Real provider gateway/API calls with invented data in the stated isolated fixture |
| [SignalR WebSocket & native mobile status](signalr-websocket-and-native-mobile.md) | LA-370 | Live SignalR WebSocket delivery, telemetry, automatic reconnect and HTTP fallback (Node.js test client); native Android/iOS explicitly recorded as untested/blocked |

Read each record's source revision, timestamp, environment and limitations. Browser execution is not native Android/iOS acceptance; a synthetic API fixture is not production customer processing or provider load/quality acceptance. The [current testing index](../README.md) lists remaining boundaries. Original artifacts remain in [evidence](../evidence/README.md).
