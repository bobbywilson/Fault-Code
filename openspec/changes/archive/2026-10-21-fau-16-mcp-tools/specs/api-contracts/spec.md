## ADDED Requirements

### Requirement: Lifecycle escalation and MCP adapter contracts
POST /api/v1/incidents/{id}/escalations SHALL require escalations.write and strong If-Match plus scoped Idempotency-Key, reusing the incident Escalated transition and atomic history/audit/replay. MCP SHALL expose the same application capabilities with protocol discovery schemas while existing REST schemas/security remain compatible.
#### Scenario: Escalation boundary
- **WHEN** an Engineer or permitted machine requests a legal lifecycle escalation
- **THEN** return MutationResponse/ETag; invalid inputs400, unauthorized401/403, missing404, stale/replay conflict409 and unavailable503 remain distinct; full case management remains FAU-18
