# development-infrastructure Specification

## Purpose
Define observable Fault Code development infrastructure requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Reproducible local environment
The development environment SHALL support Docker Compose with API/supporting services and Qdrant, isolated networking, health checks, persisted local volumes and documented Linux startup/reset procedures.

#### Scenario: Reproducible local environment acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a developer follows setup instructions from a clean checkout
- **THEN** services build, report health and connect by service DNS; a restart preserves expected data and destructive volume reset is explicitly distinguished

### Requirement: Safe service configuration
Container runtime SHALL use external secret configuration and bounded service exposure; no unapproved relational or cloud service SHALL be provisioned.

#### Scenario: Safe service configuration acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** Qdrant is unavailable or credentials are missing
- **THEN** dependent operations fail clearly without exposing secrets; no fallback creates a cloud resource or silently chooses a database

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
Local Qdrant data SHALL survive service restarts and ordinary Compose teardown/recreation. Documentation SHALL distinguish retained volumes from explicitly destructive volume deletion. Automated Linux smoke tests SHALL verify persistence with synthetic data and isolate their project and volumes from developer resources.

#### Scenario: Retained synthetic data
- **GIVEN** a synthetic test collection in an isolated smoke project
- **WHEN** services restart and are torn down without volume deletion and recreated
- **THEN** the collection and synthetic point remain readable

#### Scenario: Explicit destructive reset
- **GIVEN** a disposable smoke project
- **WHEN** its explicitly destructive volume reset runs
- **THEN** the recreated Qdrant instance has no prior synthetic collection and other projects are untouched

### Requirement: Local hostname proxy
The environment SHALL provide a loopback reverse proxy for faultcode.test with process health checks. Documentation SHALL distinguish tested proxy routing from the separate host resolver entry needed for port-free browser access.

#### Scenario: Proxy request routing
- **GIVEN** healthy containers and the proxy port is available
- **WHEN** a request with Host faultcode.test reaches the proxy
- **THEN** the API liveness response is returned without publishing Qdrant

### Requirement: Linux Docker delivery platform
Delivery SHALL validate backend builds, tests, contracts and frontend/browser acceptance on Linux and build Linux runtime images. Native Windows/macOS coverage SHALL be optional and SHALL NOT gate the sole developer's Linux Docker workflow, including on Mac. Existing isolation and unavailable-provider boundaries SHALL remain intact.
#### Scenario: Exact source delivery
- **WHEN** a pull request is considered for merge
- **THEN** passing exact-source Linux build and relevant container acceptance provide delivery evidence; cancelled or unrun native jobs are never reported as passed

### Requirement: Isolated administration packaging
The administration frontend SHALL be reproducibly built from locked dependencies and served by a non-root Linux web container with same-origin API proxying, loopback host exposure and existing internal Compose networking. Public docs SHALL remain separately accessible. No test identity or fixture SHALL enter the production bundle.
#### Scenario: Frontend container acceptance
- **WHEN** the Linux frontend image starts without tenant configuration
- **THEN** its administration route loads the honest setup state, protected API remains authenticated and database/vector/model ports remain unavailable to the browser
