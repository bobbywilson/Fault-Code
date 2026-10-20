# Spec Delta

## ADDED Requirements

### Requirement: Runnable backend foundation
The backend SHALL build and run on a supported pinned SDK without provider credentials or a selected database. Its liveness endpoint SHALL report process availability only, expose no configuration or secrets, and require no identity. Unimplemented application routes SHALL return not found.

#### Scenario: Clean startup and liveness
- **GIVEN** a clean checkout and valid foundation configuration
- **WHEN** the developer restores, builds, tests and starts the API using documented commands
- **THEN** the solution builds, automated tests pass and GET /health/live returns HTTP 200 with status Healthy without contacting external providers

#### Scenario: No business capabilities exposed
- **GIVEN** the foundation API is running without identity integration
- **WHEN** a caller requests /api/v1/vehicles
- **THEN** the request returns HTTP 404 and no domain data is exposed

### Requirement: Actionable startup configuration validation
Required foundation settings SHALL be validated before the host serves requests. Missing, blank or invalid settings SHALL prevent startup with setting names and corrective guidance without disclosing supplied values. Environment variables SHALL override checked-in non-secret defaults.

#### Scenario: Invalid required configuration
- **GIVEN** the application name setting is missing or blank
- **WHEN** the host starts
- **THEN** startup fails with an actionable FaultCode:ApplicationName error without logging the supplied value

#### Scenario: Environment configuration override
- **GIVEN** a valid FaultCode__ApplicationName environment variable
- **WHEN** the host starts
- **THEN** the composed application receives the environment override instead of the checked-in default

### Requirement: Provider-independent persistence contracts
The solution SHALL define repository lookup/add and unit-of-work save contracts without choosing a relational vendor, ORM or durable persistence implementation. Contracts SHALL accept cancellation and remain usable by tests without credentials.

#### Scenario: Persistence adapters remain a future decision
- **GIVEN** the backend foundation has no selected database
- **WHEN** application code references repository and unit-of-work contracts
- **THEN** it can compile against typed identity, lookup, add and save operations with cancellation, without importing database/provider packages or resolving a production persistence adapter
