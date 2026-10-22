## ADDED Requirements

### Requirement: Bounded interchangeable operational records
Telemetry SHALL accept typed operation, outcome, exception category, duration and optional measured usage through provider-independent contracts. Configuration SHALL select one no-op or local adapter; unknown selections SHALL fail startup safely. Vendor SDKs SHALL remain outside Domain/Application. Future Datadog/OpenTelemetry and optional Sentry adapters SHALL use the same sanitized records without implicit dual reporting.

#### Scenario: Adapter replacement and disabled operation
- **WHEN** configuration switches between no-op and local adapters or a fake collector replaces the adapter
- **THEN** business source and API contracts remain unchanged and no-op emits nothing

#### Scenario: Invalid selection
- **WHEN** an unknown provider is configured
- **THEN** startup fails with a setting-name-only error without echoing its value

### Requirement: Telemetry failure isolation
Telemetry adapter failures during start, event, measurement, exception reporting, completion or disposal SHALL NOT change business results, cancellation, retries or transaction commits. Telemetry SHALL NOT retry exporters, perform synchronous network I/O or retain an unbounded queue on business paths.

#### Scenario: Collector outage
- **WHEN** a collector throws at each adapter lifecycle boundary during successful or failing work
- **THEN** business results and original exceptions remain unchanged and subsequent requests can proceed

### Requirement: Safe correlated records
Telemetry SHALL exclude tokens, secrets, identities, driver contacts, request/query/path values, prompts, document bodies and raw exception messages/stacks. Records SHALL use bounded enumerations and server-generated correlation identifiers; resource IDs SHALL NOT be metric dimensions. Missing usage SHALL remain absent; numeric values SHALL be finite, nonnegative and bounded.

#### Scenario: Sensitive and excessive input
- **WHEN** invalid enums, arbitrary strings, attacker correlation, negative or excessive measurements are supplied
- **THEN** no unsafe record reaches the collector and no exception is exposed to business processing

#### Scenario: Partial usage
- **WHEN** no provider usage was observed or only one measured counter is available
- **THEN** absent counters stay absent and no reservation or fabricated zero is reported as consumption

### Requirement: Nested operation correlation
API, MCP, incident, retrieval, investigation and model operations SHALL share server correlation through nested tracing scopes. Local logs and completed traces SHALL carry matching trace/span context. Telemetry context SHALL remain outside model input and SHALL NOT grant execution authority.

#### Scenario: Concurrent nested requests and downstream failure
- **WHEN** concurrent API requests execute nested demo operations and one fails
- **THEN** each operation retains its request correlation and parent span without cross-request contamination; the failure has a safe category and unchanged business error
