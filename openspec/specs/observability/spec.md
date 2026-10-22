# observability Specification

## Purpose
Define observable Fault Code observability requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Interchangeable telemetry providers
Instrumentation SHALL expose provider-independent interfaces and standard OpenTelemetry tracing/metrics/structured logging with Datadog primary and an optional replaceable Sentry adapter.

#### Scenario: Interchangeable telemetry providers acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** the configured provider is replaced by a no-op/test adapter or its exporter fails
- **THEN** business processing continues and application/domain code does not require vendor SDK changes

### Requirement: Correlated and redacted operations
API, MCP, retrieval, model and investigation runs SHALL propagate correlation and report outcome, latency and available usage without secrets, driver contacts or prompt/document bodies.

#### Scenario: Correlated and redacted operations acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a demo API request executes an agent/tool chain with a downstream failure
- **THEN** parent-child trace/log correlation identifies the failure; unavailable numeric usage is missing rather than fabricated zero; redaction and bounded cardinality are tested

### Requirement: Bounded interchangeable operational records
Telemetry SHALL accept typed operation, outcome, exception category, duration and optional measured usage through provider-independent contracts. Configuration SHALL select one no-op, local, OTLP or Datadog adapter; unknown selections SHALL fail startup safely. Vendor SDKs SHALL remain outside Domain/Application. Datadog/OpenTelemetry and future optional Sentry adapters SHALL use the same sanitized records without implicit dual reporting.

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

### Requirement: Explicit sanitized OTLP delivery
Optional OTLP and Datadog configurations SHALL export only sanitized application operation traces, bounded metrics and dedicated correlated logs. Default configuration SHALL export nothing. Export SHALL exclude framework/authentication logs, HTTP paths/headers/bodies, exception text, identities, prompts and evidence. Model identifiers SHALL come only from validated explicit server configuration and SHALL NOT be metric labels.

#### Scenario: Disposable collector acceptance
- **GIVEN** an explicitly configured disposable loopback OTLP receiver
- **WHEN** demo nested API, agent, MCP, retrieval and model work completes
- **THEN** real OTLP HTTP/protobuf trace/log/metric messages arrive with matching W3C trace/span IDs, parent links, outcome, latency and only measured usage

#### Scenario: Sensitive request and provider failure
- **GIVEN** a demo request containing malicious query, headers and incoming correlation
- **WHEN** an operation fails or is retried
- **THEN** only server-generated correlation and bounded failure categories are exported; no raw exception, request or provider content is exported and absent usage remains absent

### Requirement: Bounded optional export lifecycle
Export SHALL run off business paths with bounded queues, batches, timeout and trace sampling. Saturation/outage SHALL drop telemetry without blocking business completion or changing retry/commit/replay semantics. Shutdown SHALL release exporters within bounded time. Invalid provider/endpoint/sampling configuration SHALL fail startup without echoing values. Local delivery SHALL NOT silently activate remote ingestion.

#### Scenario: Sampling and outage
- **WHEN** sampling is zero, a collector is unavailable or buffers saturate
- **THEN** sampled-out traces are absent, queue capacity stays bounded, business success/error/cancellation remains unchanged and later requests can proceed

#### Scenario: Disabled and invalid configuration
- **WHEN** None is selected or an invalid OTLP configuration is supplied
- **THEN** None creates no remote pipeline and invalid configuration fails safely before serving requests

### Requirement: Workflow and HTTP coverage
Sanitized operation spans SHALL cover ASP.NET request handling, bounded agent work, MCP, retrieval, model attempts, engineering and notification workflows plus outbound provider HTTP attempts. Application nesting SHALL retain W3C parentage without trusting incoming request context or changing authorization, model capabilities or transaction boundaries.

#### Scenario: Workflow failures and permission rejection
- **WHEN** demo engineering/notification operations fail, an outbound HTTP attempt is retried or an API request is rejected by authorization
- **THEN** correlated bounded operation/outcome/error telemetry identifies the stage while business errors, permissions and transactions remain unchanged

### Requirement: Datadog activation boundary
Datadog SHALL remain optional and target an explicitly configured local Agent OTLP receiver. Documentation SHALL identify free-tier/ingestion uncertainty, sampling, retention and separately authorized connectivity steps. Code verification SHALL NOT be represented as live Datadog ingestion, account setup, paid approval or hosting/network activation.

#### Scenario: Activation checklist
- **WHEN** an operator prepares Datadog connectivity
- **THEN** documentation requires verified account plan/cost/retention and authorized demo ingestion before activation; no credentials or paid ingestion are supplied by default
