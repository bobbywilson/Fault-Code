## ADDED Requirements

### Requirement: Isolated investigation assessment
Investigation model execution SHALL receive only bounded curated synthetic context and untrusted evidence under a fixed exact schema. Trusted code SHALL enforce classification, citation identity, rules, approval and next action. Model tools SHALL remain disabled; no identity tokens, provider secrets, filesystem, shell, generic network, environment inventory or mutation capability SHALL reach the model.

#### Scenario: Assessment validation and execution bounds
- **WHEN** investigation assessment returns unsupported route/citations, unknown/duplicate/missing fields, tool calls, truncation, invalid usage, refusal or wrong model
- **THEN** fail explicitly without persisted results; timeout, aggregate token reservations, bounded transient retries and cancellation retain existing runtime protections

#### Scenario: Separate MCP and curated context
- **WHEN** trusted code composes an investigation under original caller permissions
- **THEN** it reuses the application reads behind MCP without attaching MCP to the model; immutable approved evidence is data only and cannot authorize execution; existing advisory API contract stays compatible
