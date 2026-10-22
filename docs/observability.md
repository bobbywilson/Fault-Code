# Operational telemetry

Part of FAU-20 and FAU-21; ADR-006. Provider-independent Application contracts accept closed operation/outcome/error enums, elapsed milliseconds and nullable measured input/output token counts. Infrastructure SafeTelemetry owns nested standard ActivitySource scopes and sanitized records. LocalTelemetryAdapter projects records to Meter and structured ILogger; NoOpTelemetryAdapter emits nothing. Default `Telemetry:Provider=None`; set `Telemetry__Provider=Local` and restart for local diagnostics. Unknown/case-mismatched values fail startup without echoing supplied values. Exactly one adapter is registered; there is no implicit dual reporting.

`FaultCode.Operations` is the ActivitySource/Meter name. Operations are Api, Mcp, Incident (single incident reads), Retrieval (evidence search), Investigation, Model, Agent, Engineering, Notification Http (outbound provider attempts) and Delivery (notification I/O attempts). Server-created API correlation propagates through application scopes; incoming X-Correlation-ID and traceparent cannot set the application trace root. Nested operations retain trace/parent/span IDs across async calls. External HTTP tracing remains framework-owned and separate. Logs record matching trace/span/server correlation. Model input remains curated; no telemetry context or execution capability is sent to it.

The contract has no arbitrary event name/property dictionary, raw Exception parameter, identity, resource ID, request path/query, driver contact, secret/token, prompt or document body. Invalid enum/usage records are suppressed. Counters are nullable and bounded 0..1,000,000,000; elapsed time is monotonic, clamped to 0..86,400,000 milliseconds. Metric labels are only operation/outcome (eleven by four values); IDs appear only in correlation records, never metric dimensions. Raw exception messages/stacks are never captured. Safe failure categories are Dependency, InvalidInput, Unexpected and Cancelled. Usage records only validated successful provider counters; failed-attempt billed usage remains unknown and reservations are never consumption. Replay does not emit another Model operation.

Collector exceptions and Activity listener sampling/stop failures are caught; business result, original error, cancellation and transaction commit/replay remain unchanged. None/Local have no exporter worker or queue. FAU-21 remote adapters use the bounded background lifecycle below. Adapter callbacks must be bounded in-process notifications: throwing code is isolated; malicious or blocking adapter code cannot be preempted. Operational records are best-effort and are not durable audit. Existing framework and authorization logs are separate and are not blanket-approved for export.

## OTLP and Datadog configuration

FAU-21 pins official `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1 (including SDK/API) in Infrastructure, verified on net10.0. No Datadog vendor SDK is needed: the Datadog Agent accepts standard OTLP. Domain/Application remain SDK-free. Exactly one of `None`, `Local`, `Otlp`, `Datadog` is selected. `None` is default and creates no remote pipeline. `Local` keeps console/local diagnostics. `Otlp`/`Datadog` use one dedicated trace/metric/log pipeline, without additionally exporting normal host logs.

| Setting | Default / behavior |
|---|---|
| `Telemetry__Provider` | `None`; exact case-sensitive choices above |
| `Telemetry__Endpoint` | empty; explicitly required base URI for Otlp/Datadog |
| `Telemetry__TraceSampleRatio` | 0.1; finite 0..1; parent-based sampling retains the root decision for all children |
| Resource | fixed service.name `fault-code`, version `1.0.0`; no host/process/environment detector or random instance ID |
| Model ID | validated enabled `Agents__Model` dated snapshot; trace-only `gen_ai.request.model`; no default model or metric dimension |

OTLP uses HTTP/protobuf `/v1/traces`, `/v1/metrics`, `/v1/logs`. Otlp allows HTTPS or loopback HTTP; Datadog requires a loopback receiver on the same host. No credentials, query/fragment/userinfo or base-path suffix are accepted. Redirects and proxy use are disabled. Use an approved collector architecture for authenticated remote export; no exporter header/key support is added here. Any `OTEL_*` environment setting conflicts with the explicit pipeline and prevents enabled-export startup safely; this blocks silent SDK retries, disk storage, resource/header or endpoint overrides. None/Local remain unaffected. Clear conflicting settings before opting in; no values are echoed by validation.

Trace/log queues hold at most512 records each, batches128, scheduled every1000ms; a full queue drops new records. Export HTTP timeout and processor timeout are2000ms. Metrics aggregate every5000ms with a64-series limit per stream and only operation/outcome labels. No telemetry retry or disk spool is enabled. Shutdown flushes bounded queued work and releases providers; do not flush per request. Operator/test `ForceFlush` is explicitly blocking and belongs only at test/shutdown boundaries. Sampling affects traces only: logs and aggregated metrics still report recorded operations at ratio0, so trace sampling alone is not a log-cost control. Set Provider=None to stop all remote reporting.

The dedicated logger factory receives only typed sanitized operation completion records. No global OpenTelemetry logging provider or automatic ASP.NET/HttpClient/GenAI instrumentation is registered. Existing ASP.NET request middleware and explicit outbound provider HttpMessageHandler spans supply safe request/attempt duration/outcome/correlation without paths, method, host, headers or bodies. HTTP spans cover agent-runtime, embeddings and document-index clients; exporter HTTP is not instrumented. Framework/auth logs and model SDK internal telemetry sources are excluded. Engineering/notification wrappers preserve existing transaction callbacks/replay; a nested Delivery span reports handled send failures/cancellations even when persisting the failure state succeeds, and agent outer scope retains existing curated model input and zero model tools. MCP is an authenticated external consumer interface; the model never invokes it. A demo nested-chain test exercises correlation topology, while separate real protocol and workflow tests verify those boundaries.

## Disposable local sink

No account, API key or paid call is needed:

```sh
python3 scripts/otlp-dev-sink.py
# Separate shell, with the pinned SDK on PATH:
Telemetry__Provider=Otlp Telemetry__Endpoint=http://127.0.0.1:4318 Telemetry__TraceSampleRatio=1 dotnet run --project src/FaultCode.Api
curl --fail http://localhost:5080/api/v1/health
```

The sink listens only on loopback, accepts capped protobuf bodies, prints signal/byte counts and discards bodies. Ctrl+C stops it; there are no telemetry files or forwarding. This proves transport arrival, not Datadog ingestion. Integration tests run their own disposable Kestrel receiver on random loopback ports and decode actual OTLP protobuf fields/IDs/attributes. They do not depend on this Python sink or an optional environment flag. Normal Compose networking is unchanged; no collector or external egress is added there. Native localhost settings are not a container networking prescription.

## Datadog activation checklist (separately authorized)

1. Verify the actual signed-in account/site, trial expiration, plan, billing/card settings and explicit ingestion allowances before sending anything. A created account or free infrastructure allowance does not establish free APM, logs or custom metrics. DEC-005 remains unresolved; no subscription or paid ingestion is authorized by this code.
2. Confirm permitted telemetry fields, retention, volume limits/budget and shutdown/disable procedure. No local persistence is enabled. Agent/backend retention is a separate operator/account setting; this adapter cannot promise backend deletion or retention duration.
3. On an authorized same-host Datadog Agent, configure OTLP HTTP receiver at `127.0.0.1:4318`. Datadog documents Agent7.32+ for traces/metrics and7.48+ for logs. Logs ingestion is intentionally disabled by default to avoid billing; enabling both `logs_enabled` and `otlp_config.logs.enabled` requires explicit authorized cost/privacy review. Keep Agent API keys in its secret configuration, outside the API/repository.
4. After that authorization, set Provider=Datadog, explicit loopback Endpoint and approved TraceSampleRatio; restart. Generate only a disposable demo health/workflow request. Check the actual Datadog service trace parent links, matching log trace/span IDs, bounded metric labels, missing usage and safe failure categories. Record receiver acceptance separately from a visible backend trace/log/metric.
5. Stop the Agent/disposable test only as authorized; reset Provider=None and verify export stops. Do not weaken Compose isolation, provision hosting, change retention/security settings or make live model calls to complete this checklist.

Primary documentation checked October21,2026: [Datadog Agent OTLP ingestion](https://docs.datadoghq.com/opentelemetry/setup/otlp_ingest_in_the_agent/) (same-host restriction, logs billing opt-in), [OpenTelemetry .NET exporters](https://opentelemetry.io/docs/languages/dotnet/exporters/), [pinned exporter source](https://github.com/open-telemetry/opentelemetry-dotnet/tree/core-1.20.1/src/OpenTelemetry.Exporter.OpenTelemetryProtocol). Live Datadog connectivity/account plan/retention are unverified by FAU-21 local tests. Optional Sentry remains unimplemented and is not an accepted Provider selection.

## Verification

TelemetryTests covers fake collector replacement, no-op/local selection, nested concurrent correlation, bounded/redacted records, partial usage, listener/collector outage, local trace/log/metric agreement and API 200/401 behavior. InvestigationTests executes real demo approved knowledge/rules/RAG/model orchestration with a throwing collector, verifies one committed investigation/audit and replay, and checks no identity/evidence/model-input leak. ApiHostTests verifies safe invalid-provider startup. ArchitectureTests retains exact package boundaries and checks vendor namespaces stay outside Domain/Application. Test stores remain one-process fixtures, not durable production storage.

FAU-20 instrumentation sources checked October21,2026: [Microsoft instrumentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs), [collection boundary](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-collection-walkthroughs), [OpenTelemetry .NET practices](https://opentelemetry.io/docs/languages/dotnet/traces/best-practices/).

## Datadog Incident Management projection

Incident creation and status changes invoke a server-side Incident Management adapter independently of the OpenTelemetry export pipeline. A provider-acknowledged UUID/public number is persisted on the SQL incident projection. The UI opens `https://app.datadoghq.com/incidents/{publicNumber}`, never a log-search fallback. All records use `is_test=true`, `customer_impacted=false`, no notification handles, and a title containing the exact Fault Code incident UUID and code. No driver email or credential is sent as incident content.

Configure `DatadogIncidents__AccessToken` privately on the server, scoped to `incident_read` and `incident_write`. It must never enter browser assets, API docs examples, model context or logs. The adapter sends it only to the fixed HTTPS api.datadoghq.com host, with redirects/proxy use disabled and a15-second deadline. The existing incident CRUD surface remains unchanged. Backfill existing records only via the explicit operator command `Database__Command=sync-datadog-incidents`.

Creation has a durable30-second lease and a separate projection version. Acknowledged records reuse their UUID for PATCH status updates; retries do not create a second remote incident. Ambiguous or malformed create responses become Unknown and need provider reconciliation before any new create. Known rejected requests can be retried after configuration/access is fixed. Failure does not claim remote acceptance or prevent the durable driver email workflow.

Observed October22: Log Management routes to `/logs/activation` and reports free-plan restrictions. Incident Management separately exposes its onboarding and settings without the Logs gate. A restricted three-month token was explicitly approved and created with exactly incident_read and incident_write, expiring January22,2027. Its value is stored only in private local server configuration. Five existing local incidents were accepted by the real provider and their UUIDs/public numbers were persisted in SQL. No subscription was activated.

Primary references: [Datadog create incident](https://docs.datadoghq.com/api/latest/incidents/create-an-incident/), [update incident](https://docs.datadoghq.com/api/latest/incidents/update-an-existing-incident/), [documented incident URL format](https://docs.datadoghq.com/integrations/webhooks/).

Real provider verification: five test incidents were created and their direct URLs persisted. Datadog accepted a resolved state at initial creation for an already-resolved case. Later PATCH of another existing record returned403 with `Seat Required`, including a minimal state-only request. The application preserves its real link and records failed synchronization; local resolution, vector evidence and follow-up email remain independent. No billable seat was assigned and no alternative endpoint was used to bypass that restriction.
