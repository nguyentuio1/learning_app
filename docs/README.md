# Project documentation

Use this page to find the current instructions and the evidence behind the handoff. The [deliverable inventory](deliverables.md) maps the submitted source, scripts and documents to repository locations.

| Need | Start here |
|---|---|
| Run the system locally or install a client | [Installation guide](guides/installation-guide.md) |
| Use the system as one of the five roles | [Role-based user manual](guides/user-manual.md) |
| Deploy, configure AI or back up the database | [Deployment index](deployment/README.md) |
| Read requirements and business rules | [SRS](requirements/SRS.md), [use cases](requirements/use-cases.md), [business rules](requirements/business-rules.md) |
| Understand architecture, data and UI flows | [Architecture](architecture/architecture.md), [database](architecture/erd.md), [screen flows](design/ui-screen-flows.md) |
| Consume the HTTP APIs | [API index](api/README.md) |
| Review test plans, results and runtime evidence | [Testing index](testing/README.md) |
| Review AI processing/retention proposals | [Decision records](decisions/README.md) |
| Read the six-part report and release-asset references | [Report index](report/README.md) |
| Prepare or record the team demo | [Demo script](demo/demo-script.md), [rehearsal record](demo/demo-rehearsal.md) |

## Reading status correctly

Instructions describe how to run the software; a test report records what actually ran at its stated SHA, date and environment. Historical snapshots and raw logs retain earlier failures and counts. Source integration, release, deployment and runtime acceptance are separate.

At source baseline `9651736` (25 November 2026), API documentation cleanup (LA-368) and unused Flutter simulation removal (LA-369) are integrated. LA-348 enabled-AI acceptance, LA-370 native/WebSocket verification and LA-49/LA-188 actual team rehearsal remain separate open work. PR #291 is under review and is not included by this documentation cleanup. LA-204 compilation/export is owner-accepted; instructor evaluation is separate.

## Documentation conventions

Use lowercase kebab-case filenames describing a subject; `README.md` is the GitHub folder entry point. Existing standard identifiers such as `SRS.md` and numbered report chapters retain their established names. Keep Jira keys, tested revisions and dates inside documents. New dated execution records must identify their environment and link raw evidence; do not rename historical log/image/JSON files merely for appearance.
