## ADDED Requirements

### Requirement: Authorized agent MCP bridge
The reasoning agent SHALL use relevant allowed MCP operations with original validated caller policies and ownership, server-held credentials, bounded schemas/budgets/deadlines, per-call audit and untrusted-evidence handling. External authenticated MCP consumer support alone SHALL NOT count as agent tool integration. Model-authored text SHALL NOT authorize lifecycle mutations or supply independent workflow intent.

#### Scenario: Actual agent tool execution
- **WHEN** a model requests permitted lookup/retrieval and then an unauthorized operation
- **THEN** actual authorized MCP execution is evidenced and unauthorized execution is rejected before service access, including timeout/cancellation and no credential leakage
