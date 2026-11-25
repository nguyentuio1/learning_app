# Raw test evidence

Original execution artifacts are retained here with their existing names and bytes. These are supporting records, not generated application source or standalone claims that every test environment passes.

| Group | Related record |
|---|---|
| `la346-flutter/` | [Flutter package/avatar photos](../acceptance/flutter-photo-upload.md) |
| `la363-customer/` | [Customer journey](../acceptance/customer-journey.md) |
| `la364-operator/` | [Operator journey](../acceptance/operator-journey.md); includes disclosed prepared arrival fixtures |
| `la365/` | [Assistant availability and recovery](../acceptance/assistant-availability.md) |
| `la367/` | [Operator station refresh](../acceptance/operator-station-refresh.md) |
| `backend-develop-*`, `web-develop-*` | [Execution report](../reports/test-report.md) at its recorded source baseline |
| `perf-*`, `k6-*`, `performance-reference-environment.json` | [Performance report](../reports/performance-report.md); historical failures and later reference runs both retained |

Some reports identify additional ignored local artifacts or CI links rather than committed files. That distinction remains explicit in the source report. A SQL fixture is preparation data, not evidence of a physical flight; a screenshot alone is not a complete acceptance run. Never add credentials, JWTs or pickup codes to evidence.
