## ADDED Requirements

### Requirement: Safe Linux packaging
The API SHALL run as a non-root user in a version-pinned Linux runtime image built from an allowlisted source context. Provider credentials and local developer state SHALL be excluded from image layers. API exposure SHALL bind to configurable loopback ports; Qdrant SHALL have no default published host port.

#### Scenario: Container safety verification
- **GIVEN** a clean checkout and Docker engine
- **WHEN** the automated packaging checks build and inspect the runtime
- **THEN** the effective API UID is nonzero, excluded secret canaries are absent and Qdrant has no published host ports

### Requirement: Observable dependency readiness
Compose SHALL check API process liveness and Qdrant availability. A separate API readiness endpoint SHALL verify authenticated Qdrant connectivity through service DNS with bounded timeout and generic output. Dependency failure SHALL return HTTP 503 without changing process liveness or exposing credentials.

#### Scenario: Healthy service networking
- **GIVEN** the local services have valid external credentials
- **WHEN** Compose starts and readiness is requested
- **THEN** both services become healthy and API readiness returns HTTP 200 after contacting Qdrant by service DNS

#### Scenario: Qdrant interruption and recovery
- **GIVEN** healthy local services
- **WHEN** Qdrant stops and then restarts
- **THEN** API readiness changes to HTTP 503 and then HTTP 200 while process liveness remains HTTP 200

### Requirement: Explicit local credentials
The container environment SHALL require an externally supplied Qdrant API key. Missing credentials SHALL prevent Compose startup with the setting name and corrective guidance. Invalid credentials SHALL fail readiness without exposing their value. Native foundation development SHALL remain runnable without provider credentials.

#### Scenario: Missing key
- **GIVEN** no Qdrant key in the shell or local environment file
- **WHEN** Compose configuration is evaluated
- **THEN** evaluation fails naming FAULTCODE_QDRANT_API_KEY without creating any service

#### Scenario: Invalid key
- **GIVEN** API and Qdrant use different keys
- **WHEN** readiness probes authenticated Qdrant access
- **THEN** readiness returns HTTP 503 with generic status and no credential value

### Requirement: Verified local data lifecycle
Local Qdrant data SHALL survive service restarts and ordinary Compose teardown/recreation. Documentation SHALL distinguish retained volumes from explicitly destructive volume deletion. Automated Linux smoke tests SHALL verify persistence with demo data and isolate their project and volumes from developer resources.

#### Scenario: Retained demo data
- **GIVEN** a demo test collection in an isolated smoke project
- **WHEN** services restart and are torn down without volume deletion and recreated
- **THEN** the collection and demo point remain readable

#### Scenario: Explicit destructive reset
- **GIVEN** a disposable smoke project
- **WHEN** its explicitly destructive volume reset runs
- **THEN** the recreated Qdrant instance has no prior demo collection and other projects are untouched

### Requirement: Local hostname proxy
The environment SHALL provide a loopback reverse proxy for faultcode.test with process health checks. Documentation SHALL distinguish tested proxy routing from the separate host resolver entry needed for port-free browser access.

#### Scenario: Proxy request routing
- **GIVEN** healthy containers and the proxy port is available
- **WHEN** a request with Host faultcode.test reaches the proxy
- **THEN** the API liveness response is returned without publishing Qdrant
