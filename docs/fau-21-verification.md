# FAU-21 verification

Part of FAU-21; ADR-006. Regular clean checkout branched from fetched origin/main `5b8fa9a75612cfb1bed8da73d78a5d8088dec8ed` (merged FAU-20 PR17). Live native Linear FAU-21 scope read, set In Progress and retained for personal review. No worktree/subagent.

OpenSpec fau-21-opentelemetry-datadog proposal/design/full observability deltas/tasks strictly validated BEFORE application implementation. Existing requirements retained; official archive follows completed implementation checks.

## Observed local evidence

- SDK10.0.402 locked restore passed. Release build zero warnings/errors. Full Release tests passed:38 Domain +3 Application +383 Integration =424,0failed,0skipped. Formatting verification passed.
- 23 added telemetry tests cover actual HTTP/protobuf traces/logs/metrics at disposable loopback Kestrel receiver, decoded parent IDs/log IDs/attributes, missing usage/model snapshot, malicious incoming correlation/query/ambient baggage, zero sampling across children, Otlp/Datadog adapter selection, unsafe endpoints/ratio/environment overrides, safe real-process invalid startup, retry/error/cancellation HTTP attempts, saturation/outage and bounded/idempotent shutdown.
- Actual synthetic investigation commit/replay and engineering approval/notification dispatch/replay execute with successful and503 OTLP receivers; one committed result/notification and one model/send call remain unchanged. Handled delivery failure exports a distinct Delivery failure span without contact or raw provider exception. Test stores remain one-process fixtures, not production persistence.
- Actual API health200 -> documented Python dev sink received traces/logs/metrics over OTLP; processes stopped and no bodies retained. Verification driver lives in the chat's work directory, not production source.
- REST OpenAPI/examples/compatibility passed against committed contract AND actual merged FAU-20 Ubuntu CI artifact; seven breaking fixtures rejected. Actual five MCP SDK declarations/input/output schemas exactly equal to FAU-20 CI artifact; separate examples checker passed. No API/MCP schema or permission changes.
- Build-input scanner passed109 explicit allowlisted files. Compose config passed with a synthetic validation-only Qdrant key. Local Docker daemon unavailable; no local native container/real Qdrant acceptance claimed. Exact-source Linux CI supplies those checks before merge.

## Source review

Reviewed exact operation wrappers against main: existing authoritative transaction callbacks, caller checks, notification claim/send/commit boundaries, replay keys and model inputs remain unchanged. Dedicated Delivery instrumentation encloses only already-existing provider I/O; it neither retries nor changes acknowledgement/lease behavior. HTTP instrumentation observes only operation/outcome/duration and preserves response, exceptions and cancellation.

Reviewed Infrastructure source/SDK defaults: explicit empty resource with fixed service/version; source/Meter allowlist; isolated logger factory only for sanitized records; no automatic ASP.NET/HTTP/GenAI or blanket framework/auth log capture. Bounded batch queues512/128, HTTP/processor timeout2000ms,64 metric series with operation/outcome only; explicit no-retry/no-disk-spool environment guard; redirects/proxy disabled. Configuration parses sampling without echoing invalid values. Child contexts retain the actual parent's W3C sampling flag. Exporter failure/saturation drops telemetry and shutdown is bounded; operational records are never durable audit.

## Delivery limits

Default None sends nothing. Explicit Otlp/Datadog receiver configuration is required. Local receiver acceptance is not visible Datadog ingestion. Separate account walkthrough reported Datadog Free, but APM/log/custom-metric allowances, billing/retention and live ingestion are not established by these code tests. No account, Agent, credential, remote ingestion, paid subscription, hosting or network change was made here. Optional Sentry can replace the same ITelemetryAdapter interface; its SDK/account adapter remains unimplemented. No model/key/cost selection or live model call, no production persistence/durable outbox, no vehicle operation.

See observability.md for exact settings, disposable sink, sampling/log-cost distinction and separately authorized Datadog activation checklist. Official OpenSpec archive/main validation and exact-source GitHub checks are recorded with PR delivery.
