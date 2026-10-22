## ADDED Requirements

### Requirement: Real end-to-end observability acceptance
Standard OpenTelemetry and provider-independent interfaces SHALL cover API, incident processing, real database access, RAG, MCP tools, agent runs, notification delivery, errors, latency and health. Datadog SHALL be primary with a future Sentry boundary. Administrative configuration SHALL keep secrets server-side and avoid logging tokens/contacts/prompts. Actual provider trace ingestion SHALL be verified separately from local OTLP sink tests.

#### Scenario: Provider ingestion and database spans
- **WHEN** a demo authenticated end-to-end workflow executes
- **THEN** the real Datadog account receives correlated spans for configured components with bounded safe metadata; unavailable ingestion is an explicit incomplete outcome
