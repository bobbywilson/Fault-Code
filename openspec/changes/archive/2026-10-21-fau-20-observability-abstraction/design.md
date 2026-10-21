# Design

## Context
See proposal.md. Existing API generates a fresh correlation; application has no vendor packages. FAU-21 owns exporter integration.

## Goals / Non-Goals
Typed replaceable contracts and standard local diagnostics. No live exporter, automatic SDK capture, credentials, durable audit, model tools or network changes.

## Decisions
- Closed enums instead of arbitrary dictionaries or string redaction: unknown data cannot bypass a blacklist. Only canonical nonzero server correlation GUIDs pass; IDs never become metric labels. No raw Exception parameter.
- Application owns ITelemetry/operation contracts. Infrastructure owns the failure-isolating facade, selected adapter and ActivitySource/Meter/ILogger projection; DI registers one adapter. No-op default. Local uses framework-only diagnostics; Datadog OpenTelemetry exporter and Sentry are future adapter choices, not advertised as implemented.
- AsyncLocal context managed by trusted facade with nested restore; ActivitySource scopes use framework parentage. Start/stop/events are synchronous in-process bounded notifications; adapters contractually must not block or perform I/O. Throw isolation cannot preempt malicious blocking adapter code.
- Fixed operation/outcome/error categories, bounded usage (nullable) and monotonic elapsed time. No actor/tenant/incident ID, body/path/query or exception content. Metrics have operation/outcome dimensions only; correlation belongs only in records/logs/traces.
- API root wraps existing error handler so safe final HTTP outcome is observed; application helper scopes instrument incident reads, retrieval, investigation and model execution; MCP tool dispatch uses a fixed operation. No schema changes or model context changes.

## Risks / Trade-offs
Missing exporter means no remote ingestion proof. Local listener behavior is synchronous; all adapter calls are caught, but blocking third-party code is not sandboxed. Operational telemetry is best-effort and never durable audit. Existing framework/auth logs remain separate; do not attach blanket auto-capture exporters to them.

## Migration Plan
Default no-op preserves operation. Set Telemetry:Provider=Local for local diagnostics; revert to None to disable. No migration or deployment performed.
