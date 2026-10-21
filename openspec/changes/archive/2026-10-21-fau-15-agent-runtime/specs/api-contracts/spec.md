## ADDED Requirements

### Requirement: Protected advisory runtime contract
POST /api/v1/incidents/{id}/agent-runs SHALL require faults.read, vehicles.access.read and knowledge.read under existing Entra policies and trusted ownership. It SHALL accept an empty object only and return typed advisory run identity, incident identity, outcome, provenance, uncertainty, action, escalation and execution usage. Failure SHALL use correlated safe Problem Details without domain state changes.
#### Scenario: Runtime contract acceptance
- **WHEN** generated OpenAPI and real authenticated controller requests are validated
- **THEN** the additive operation documents explicit DTOs, required policies, success/errors, 400/401/403/404/422/429/502/503/504 semantics, and previous operation/schema contracts remain compatible
