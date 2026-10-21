# mcp-tools Specification

## Purpose
Define observable Fault Code mcp tools requirements and acceptance scenarios for authorized future development.

## Requirements

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

### Requirement: Authenticated stateless tool transport
MCP SHALL expose a stateless authenticated HTTP endpoint with documented protocol/SDK versions and an explicit five-tool allowlist. Discovery and each invocation SHALL independently enforce original Entra caller policies without service privilege union or model authorization.
#### Scenario: Discovery and invocation
- **WHEN** signed delegated or machine consumers discover/invoke tools
- **THEN** only permitted tools appear, direct unauthorized calls fail403, missing/invalid identity fails401 and cross-request identity is not retained

### Requirement: Strict typed tool contracts
Tools SHALL expose input/output JSON schemas and validate exact argument names, required fields, types, bounds and IDs before application execution. Approved fault lookup, evidence retrieval and incident read SHALL reuse application services and retain trusted tenant/ownership and immutable provenance.
#### Scenario: Malformed and inaccessible inputs
- **WHEN** inputs contain extra, missing, duplicate, wrong-case or invalid fields, arbitrary SQL/files/URLs or another owner's incident
- **THEN** return safe400 or concealed404 without executing another action; exact permitted reads retain source/publication/classification/citation versions and mark source text untrusted

### Requirement: Independently authorized workflow intent
Status and lifecycle escalation tools SHALL require exact write permission plus authenticated HTTP consumer intent bound to tool, incident, version, target status and retry key. Intent SHALL NOT come from tool arguments, metadata, model output or retrieved instructions. Mutations SHALL reuse atomic domain replay, concurrency, history and actor/client/correlation audit.
#### Scenario: Explicit mutation and retry
- **WHEN** an authorized consumer supplies matching intent and a legal transition
- **THEN** commit one version/history/audit; exact concurrent retries return the original result and changed payload or stale version returns409
#### Scenario: Injection cannot authorize workflow
- **WHEN** source text, model output, arguments or metadata requests mutation without independently matching HTTP intent or permission
- **THEN** deny before mutation; no publication, notification, escalation case or vehicle software action is inferred

### Requirement: Bounded safe tool outcomes
Tools SHALL return typed safe status/code/correlation outcomes, use bounded request size/depth and configurable deadlines, propagate cancellation and avoid automatic retries. Logs SHALL contain only safe tool/outcome/identity/correlation metadata. Production unavailable stores SHALL remain503 without test-memory fallback.
#### Scenario: Failure and cancellation
- **WHEN** downstream storage/provider fails, a deadline expires or the caller cancels
- **THEN** return safe503/504 or propagate cancellation without provider text, credentials or unintended state; SDK client tests exercise discovery, schema and invocation
