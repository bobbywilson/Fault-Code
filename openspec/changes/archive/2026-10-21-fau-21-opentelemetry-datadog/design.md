# Design

## Context
See proposal.md. FAU-20 owns sanitized ActivitySource/Meter contracts and the API correlation boundary. Live FAU-21 requires actual OTLP, sampling, workflow/HTTP coverage and Datadog configuration. ADR-006 and DEC-003/005 remain authoritative.

## Goals / Non-Goals
Export explicit application telemetry only. Preserve model blindness, five MCP schemas, authorization, transactions and internal Compose network. No automatic instrumentation package that captures URLs, exception text or bodies; use existing ASP.NET middleware and explicitly instrument provider HTTP attempts instead. No account/agent installation or live ingestion.

## Decisions
Use pinned official OpenTelemetry .NET SDK/OTLP HTTP protobuf exporter in Infrastructure; Datadog consumes standard OTLP via an already approved same-host Agent. This avoids vendor coupling and direct Datadog API keys in the API. None/Local/Otlp/Datadog are exact provider selections, each one adapter. Otlp permits explicit loopback HTTP or HTTPS collector; Datadog requires loopback HTTP receiver. No endpoint userinfo/query/fragment, exporter headers, redirects or global OTEL environment-driven resource/endpoint selection. Enabled export safely rejects conflicting OTEL_* environment settings before constructing SDK providers. Service resource is fixed and empty except controlled service/version. All configuration failures name settings only.

Retain standard FaultCode.Operations source and Meter. Dedicated logger factory exports only LocalTelemetryAdapter's sanitized completion record; do not register a global logging provider. Configure batch trace/log processors (bounded capacity), bounded periodic metric aggregation and bounded exporter timeout; no retries or disk retention. Trace ratio configurable 0..1. Metrics/logs represent all recorded work, independently of trace sampling; documentation explains this cost implication. SDK callbacks only enqueue/aggregate on business paths; network work occurs on exporter threads. SafeTelemetry still catches listener/adapter exceptions.

Add closed Agent, Engineering, Notification, Http and Delivery operations, preserving existing enum values. Service scopes wrap existing operations, preserve cancellation/result and avoid transaction changes. Model snapshot enrichment uses validated dated server configuration only, never response input or metric labels. Provider HTTP attempts get safe Http spans without URL/headers. Keep server-created application roots independent from untrusted external W3C headers, as already established by FAU-20.

## Risks / Trade-offs
SDK exporters have bounded shutdown/queue loss; telemetry is best-effort, never durable audit. Sampling applies to traces only; all metrics/logs can still incur ingestion charges. No local disk retention; remote Agent/backend retention requires separately approved settings. An unreachable collector is tested locally; live Datadog validation remains independent. Dedicated manual ASP.NET/HTTP coverage deliberately omits unsafe automatic request metadata.

## Migration Plan
Default None requires no migration. Operator explicitly chooses receiver/provider/sampling only after activation checklist. Restart with None rolls back. No database/schema/network changes.
