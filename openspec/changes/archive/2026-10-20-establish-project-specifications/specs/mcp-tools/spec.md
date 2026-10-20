# mcp-tools Specification Delta

## Purpose

Define observable Fault Code mcp tools requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Permission-aware tool contracts
MCP SHALL expose typed fault lookup, knowledge retrieval and authorized workflow tools with validated schemas, caller identity and structured errors.

#### Scenario: Permission-aware tool contracts acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a tool caller has insufficient scope or supplies malformed input
- **THEN** the tool denies the operation without privilege escalation or secret disclosure

### Requirement: Safe workflow mutations
Mutation tools SHALL reuse domain policies, require authorized workflow intent, record correlation/audit identity and implement idempotency; arbitrary SQL/files/URLs SHALL NOT be accepted.

#### Scenario: Safe workflow mutations acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a caller retries a workflow mutation or retrieved content requests an extra action
- **THEN** the prior result is reused and untrusted content cannot authorize new operations
