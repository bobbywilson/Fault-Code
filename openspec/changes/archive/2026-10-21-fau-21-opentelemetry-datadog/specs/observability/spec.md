## MODIFIED Requirements

### Requirement: Bounded interchangeable operational records
Telemetry SHALL accept typed operation, outcome, exception category, duration and optional measured usage through provider-independent contracts. Configuration SHALL select one no-op, local, OTLP or Datadog adapter; unknown selections SHALL fail startup safely. Vendor SDKs SHALL remain outside Domain/Application. Datadog/OpenTelemetry and future optional Sentry adapters SHALL use the same sanitized records without implicit dual reporting.

#### Scenario: Adapter replacement and disabled operation
- **WHEN** configuration switches between no-op and local adapters or a fake collector replaces the adapter
- **THEN** business source and API contracts remain unchanged and no-op emits nothing

#### Scenario: Invalid selection
- **WHEN** an unknown provider is configured
- **THEN** startup fails with a setting-name-only error without echoing its value

## ADDED Requirements

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
