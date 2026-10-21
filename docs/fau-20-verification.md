# FAU-20 verification

Part of FAU-20. Regular checkout branched from fetched origin/main dea5f86c3a44183066e4d56c9727f63f1e104cbf; GitHub PR16 merge/source verified live. Live Linear scope read; issue kept In Progress.

OpenSpec fau-20-observability-abstraction proposal/design/full observability scenarios/tasks strictly validated BEFORE application implementation. Existing main observability requirements retained. Official archive and main validation follow completed checks.

## Observed local evidence
- SDK10.0.402 locked restore passed. Release build passed, zero warnings/errors; no new package or lockfile changes.
- Full Release tests passed: 38 Domain,3 Application,360 Integration =401,0failed,0skipped. Twenty added tests:16 TelemetryTests,2 actual investigation collector/commit/replay cases,1 safe startup selection,1 vendor-boundary test.
- Tests cover no-op/local/replacement adapter, nested concurrent parentage, exact trace/log IDs, metric label bounds, nullable usage, malicious correlation/query values, invalid enum/numeric suppression, collector/sampler/start/stop callback exceptions, original errors/cancellation, API200/401 and real approved investigation commit/replay during collector outage. No model input/context/permissions changed.
- REST OpenAPI/examples/compatibility checker passed, seven breaking fixtures rejected. Actual five MCP SDK schemas and examples checker passed. No REST/MCP operation/schema change.
- Build input scan passed106allowlisted inputs; Compose configuration passed with a synthetic validation-only Qdrant key. Docker daemon unavailable locally: no native container or actual Qdrant acceptance claimed here; exact-source Linux CI must supply that evidence.
- Source review checked sanitized contract projection, incoming trace/correlation isolation, AsyncLocal parent restoration, no-op silence, missing usage, listener/collector exception boundaries, transaction/replay invariance, package boundaries and unchanged model/MCP separation.

## Limits
Only None/Local providers exist. Datadog/OpenTelemetry remote export and optional Sentry adapter are FAU-21 extension boundaries. No live ingestion, SDK exporter, provider credentials, deployment, durable storage, network changes or paid commitment. Operational records are not durable audit. Synchronous adapter code must honor bounded nonblocking/no-I/O contract; exceptions are isolated, blocking malicious code is not sandboxed. Existing framework/auth logs are separate and not approved for blanket export. Account walkthrough is tracked independently from code validation.
