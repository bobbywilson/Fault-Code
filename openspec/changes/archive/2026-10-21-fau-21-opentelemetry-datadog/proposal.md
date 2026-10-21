# Proposal

## Why
Part of FAU-21. FAU-20 provides safe correlated instrumentation but no OTLP delivery. Implement optional OpenTelemetry export usable with a local sink and Datadog Agent without making business processing depend on ingestion.

## What Changes
- Select None, Local, Otlp or Datadog explicitly; default remains None.
- Export only sanitized operation traces, bounded metrics and dedicated correlated operational logs through standard OTLP HTTP/protobuf.
- Add bounded background batching, timeout, sampling and safe configuration; test actual wire delivery and outage locally.
- Cover engineering, notification, agent and outbound provider HTTP operations without exporting payloads, paths, headers or raw exceptions.
- Document local disposable sink and separately authorized Datadog activation, costs and retention.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `observability`: explicit optional OTLP/Datadog export, bounded delivery and expanded operation correlation.

## Impact
Infrastructure SDK packages/composition, API options, Application operation enum and service scopes, integration tests and docs. No REST/MCP schema, model permissions, production persistence, hosting, accounts, credentials, ingestion activation or Compose network changes. Dependencies FAU-15/20 are implemented and merged; Linear remains open for personal review. Live native Linear ticket read October 21, 2026.
