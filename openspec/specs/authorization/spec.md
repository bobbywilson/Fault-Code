# authorization Specification

## Purpose
Define observable Fault Code authorization requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Deny by default
Protected operations SHALL validate Entra access tokens and enforce least-privilege roles, delegated scopes or application roles plus resource ownership; only explicitly public operations bypass authentication.

#### Scenario: Deny by default acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a caller lacks a valid token, scope, role or driver ownership
- **THEN** 401 distinguishes invalid identity from 403 insufficient privilege; cross-driver access and unapproved external integrations are denied

### Requirement: Engineering and administration boundaries
Knowledge publication, approved remedies, integration management and user/role administration SHALL require explicit server-side policies and audited actor/context.

#### Scenario: Engineering and administration boundaries acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a reader or agent attempts an engineering publication or integration mutation
- **THEN** the server denies it; hiding a UI control does not replace authorization and MCP does not elevate caller permissions

### Requirement: Exact caller permission matrix
Protected operations SHALL require a named permission. Delegated callers SHALL present its exact space-delimited scope and an allowed Reader, Operator, Engineer or Administrator role; machines SHALL present its exact application role without delegated scopes. Comparisons SHALL be case-sensitive; roles SHALL NOT imply unrelated privileges. Caller-kind-specific policies SHALL reject the other kind.

#### Scenario: Permission separation
- **GIVEN** a validated synthetic identity
- **WHEN** scope, role, caller kind or permission differs from the operation matrix
- **THEN** return 403; correct delegated scope plus role or correct application role succeeds

### Requirement: Trusted driver ownership
Driver-owned resource access SHALL use server-loaded tenant and owner identifiers and require either an exact own-read delegated permission with matching oid or an explicit all-resource permission. Machines SHALL NOT use own-read permission. Unowned, malformed and cross-tenant resources SHALL fail closed; client-supplied ownership SHALL NOT confer access.

#### Scenario: Cross-driver denial
- **GIVEN** a trusted synthetic resource owned by one driver
- **WHEN** another driver with only own-read permission accesses it
- **THEN** deny with 403; the matching owner succeeds and explicit all-resource permission is separately required for staff or machines

### Requirement: Policy coverage and safe audit context
Every mapped protected operation SHALL declare a named permission policy; unannotated or bare-authenticated operations SHALL deny even valid identities. Public operations SHALL remain explicitly anonymous. Permission decisions SHALL provide actor, client, policy, outcome and correlation context without token contents or resource payloads; future MCP callers SHALL reuse these policies without elevation.

#### Scenario: Route coverage
- **GIVEN** the production route inventory
- **WHEN** routes and generated contracts are inspected
- **THEN** each route is explicitly public or has a registered permission policy; unannotated test routes deny and unknown routes remain 404

#### Scenario: Sensitive operation denial
- **GIVEN** a reader or machine lacking exact elevated permission
- **WHEN** engineering publication, integration management or user administration is authorized
- **THEN** deny and emit safe decision context; Administrator alone does not grant Engineer publication

### Requirement: Versioned permission assignments
The API SHALL implement the exact delegated scope, human role and application role assignments in the following scenario matrix. Human roles SHALL be user-only and granular application roles application-only when provisioned; no role or consent provisioning SHALL be implied by the code catalog.

#### Scenario: Exact permission contract
- **GIVEN** the versioned permission catalog
- **WHEN** an operation permission is evaluated or dedicated registrations are prepared
- **THEN** the exact values and allowed human roles are as follows; unavailable means machine access is denied

| Permission / delegated scope | Allowed human roles | Application role |
|---|---|---|
| authorization.read | Reader, Operator, Engineer, Administrator | Authorization.Read |
| drivers.read.own | Reader | unavailable |
| vehicles.read.own | Reader | unavailable |
| drivers.read | Reader, Operator, Engineer, Administrator | Drivers.Read.All |
| vehicles.read | Reader, Operator, Engineer, Administrator | Vehicles.Read.All |
| drivers.write | Operator | Drivers.Write.All |
| vehicles.write | Operator | Vehicles.Write.All |
| faults.read | Reader, Operator, Engineer, Administrator | Faults.Read.All |
| faults.write | Operator | Faults.Write.All |
| knowledge.read | Reader, Operator, Engineer, Administrator | Knowledge.Read.All |
| knowledge.publish | Engineer | Knowledge.Publish |
| escalations.read | Reader, Operator, Engineer, Administrator | Escalations.Read.All |
| escalations.write | Engineer | Escalations.Write |
| integrations.manage | Administrator | Integrations.Manage |
| users.manage | Administrator | Users.Manage |

### Requirement: Tenant filtered lifecycle operations
Lists SHALL apply validated tenant and own/all permission restrictions before counting and pagination. Reads SHALL authorize trusted stored owner mapping, excluding unowned vehicles. Writes SHALL require exact operation permission and same-tenant trusted records; association mutations SHALL require both drivers.write and vehicles.write. Ownership mappings SHALL be immutable through general updates.
#### Scenario: No list or mutation escalation
- **WHEN** another owner, cross-tenant caller, own-only reader or Administrator without an exact write grant attempts lifecycle access
- **THEN** private records are excluded, writes deny and client-supplied owner query fields cannot expand access
