## ADDED Requirements

### Requirement: Shared Linux demo isolation
Fault Code SHALL use only the approved existing Linux host with a dedicated deployment directory, Compose project, containers, networks, volumes, credentials, resource limits and restart/health policies. It SHALL NOT reuse application databases or change LALAI/Simone containers, processes, environments or authentication. Preflight SHALL verify host identity, capacity, existing service health, ports and network conflicts before mutation.

#### Scenario: Shared-host conflict
- **WHEN** a proposed port, resource allocation, proxy operation or deployment could interrupt an existing service
- **THEN** stop dependent deployment, report concrete conflict and request approval before shared-impact changes

#### Scenario: Isolated acceptance
- **WHEN** Fault Code starts, restarts or rolls back
- **THEN** its services are bounded and isolated while LALAI and Simone baseline health remains verified
