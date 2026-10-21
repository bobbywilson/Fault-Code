## ADDED Requirements

### Requirement: Engineering case API contracts
Versioned engineering case creation, queue/detail/history, assignment, notes, status, resolution proposal/approval and event read APIs SHALL use explicit bounded DTOs, exact policies, strong If-Match and scoped Idempotency-Key mutations. Generated examples/security SHALL preserve existing REST and MCP compatibility; errors SHALL retain safe correlated400/401/403/404/409/503 distinctions.

#### Scenario: Authorized engineering API
- **WHEN** signed human or machine callers use engineering case operations
- **THEN** read requires escalations.read plus faults.read/vehicles.access.read/knowledge.read, write requires escalations.write plus the three read policies, and approval additionally requires knowledge.publish; unknown fields reject400 and unconfigured storage503
