# Design and evidence

Use docs/audit/architecture-compliance.csv as the requirement matrix and docs/approved-architecture.md as current authority. Each result states code/test/external evidence and a permitted status. Mocked HTTP/local stores do not prove real delivery, storage, OpenAI/Qdrant/Datadog or deployment. Current server is read-only audited; SQL Server recommendation uses actual headroom and Microsoft licensing, without provisioning.

Bounded corrections retain interfaces: duplicate active association uniqueness becomes (vehicle,driver), not vehicle alone; Driver gets only own-driver/own-vehicle and caller-permission scopes, no administrative or all-fleet rights; ontology retains a model-reference entry view and adds an explicit Engineer-authorized aggregate catalog editor using existing APIs; cause relationships extend existing validation. Re-sync Mintlify from Release output. No claim that these isolated fixes implement consent, driver incident access end-to-end, directory invitations/roles, durable workers or agent MCP invocation.

Record full workflow A/B/C acceptance, consent/directory role assignment, knowledge versus physical repair, automatic publication fan-out, real MCP tool identity/budgets, live RAG/telemetry, thirty-day teardown and existing-host isolation. Latest authoritative requirements supersede earlier read-only ontology, single-active-driver and no-agent-tools policies. Missing features remain tracked/open.

### Driver notification browsing
The driver directory links to the selected driver's notification history. Driver Notifications is a paginated Spartan table of message, driver, email, delivery state, creation time and attempt count, with an all-driver view. GET /api/v1/notifications optionally filters by driverId and applies existing tenant, driver and incident visibility policies. Unavailable notification persistence must show an error rather than a fabricated empty inbox. Mailpit messages alone are not canonical notification records.

### Incident list presentation
Incidents is a read-only, paginated Spartan data table of vehicle-originated investigation records: fault code, vehicle, driver, system, reported time and status. Creation in Angular is available only by dispatching a fault from the Vehicles page. The table remains visible when persistence is unavailable, with the service error displayed and no fabricated records or empty-success claim.
