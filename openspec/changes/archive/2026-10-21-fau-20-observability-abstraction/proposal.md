# Proposal

## Why
FAU-20 requires vendor-independent operational telemetry before FAU-21 exporter integration. Existing correlation and logs do not define a safe interchangeable adapter contract.

## What Changes
- Add typed bounded operational events, measurements, exception categories and tracing scopes.
- Supply no-op and local standard ActivitySource/Meter/ILogger adapters behind failure isolation and configuration-driven DI.
- Correlate API, MCP, incident, retrieval, investigation and model work without exporting identity, payloads or raw exceptions.
- Document one adapter selection and future Datadog/OpenTelemetry and optional Sentry boundaries; no exporter or account activation.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `observability`: specify adapter contracts, safe bounded payloads, correlation and outage isolation.

## Impact
Application contracts and instrumentation; Infrastructure adapters; API composition/middleware; contract/security tests and documentation. No REST/MCP schema changes, new grants, model tools, durable store, outbound traffic or deployment. ADR-006; depends on completed FAU-5. Account walkthrough separately authorized and cannot accept terms/password/security grants autonomously.
