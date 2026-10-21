## ADDED Requirements

### Requirement: Live permission acceptance evidence
Authorization acceptance SHALL demonstrate the existing permission matrix using real delegated and application tokens from the configured tenant. Human access SHALL require the exact delegated scope and an allowed application role. Machine access SHALL require the exact application role. Tenant administration SHALL NOT imply Fault Code application access.

#### Scenario: Authorized user
- **GIVEN** a real user assigned an allowed Fault Code role and the required delegated permission
- **WHEN** the user calls its permitted protected API operation
- **THEN** authentication and permission evaluation succeed, subject to the operation's other resource and storage preconditions

#### Scenario: Unauthorized user
- **GIVEN** a valid tenant token missing the required scope or allowed application role
- **WHEN** the user calls a protected operation
- **THEN** the API returns 403 without granting an administrator bypass

#### Scenario: Least-privilege machine
- **GIVEN** a machine granted only Authorization.Read.Application
- **WHEN** it calls permission inspection and then a protected mutation
- **THEN** permission inspection is authorized and the mutation is forbidden

#### Scenario: Invalid identity
- **WHEN** a protected request has no token, an expired token, invalid signature, unrelated issuer or incorrect audience
- **THEN** the API returns 401 without a production authentication shortcut

### Requirement: Distinct machine permission values
The application permission catalog SHALL use Authorization.Read.Application, Knowledge.Publish.Application, Escalations.Write.Application, Integrations.Manage.Application and Users.Manage.Application in place of their former unsuffixed machine values. Delegated scope values and human role mappings SHALL remain unchanged. The API SHALL reject the retired machine values. The user explicitly approved this provider-required naming adjustment on 2026-10-21.

#### Scenario: Retired application role
- **WHEN** a machine presents only a retired unsuffixed application role
- **THEN** the protected operation returns 403

## MODIFIED Requirements

### Requirement: Versioned permission assignments
The API SHALL implement the exact delegated scope, human role and application role assignments in the following scenario matrix. Human roles SHALL be user-only and granular application roles application-only when provisioned; no role or consent provisioning SHALL be implied by the code catalog.

#### Scenario: Exact permission contract
- **GIVEN** the versioned permission catalog
- **WHEN** an operation permission is evaluated or dedicated registrations are prepared
- **THEN** the exact values and allowed human roles are as follows; unavailable means machine access is denied

| Permission / delegated scope | Allowed human roles | Application role |
|---|---|---|
| authorization.read | Reader, Operator, Engineer, Administrator | Authorization.Read.Application |
| drivers.read.own | Reader | unavailable |
| vehicles.read.own | Reader | unavailable |
| drivers.read | Reader, Operator, Engineer, Administrator | Drivers.Read.All |
| vehicles.read | Reader, Operator, Engineer, Administrator | Vehicles.Read.All |
| drivers.write | Operator | Drivers.Write.All |
| vehicles.write | Operator | Vehicles.Write.All |
| faults.read | Reader, Operator, Engineer, Administrator | Faults.Read.All |
| faults.write | Operator | Faults.Write.All |
| knowledge.read | Reader, Operator, Engineer, Administrator | Knowledge.Read.All |
| knowledge.publish | Engineer | Knowledge.Publish.Application |
| escalations.read | Reader, Operator, Engineer, Administrator | Escalations.Read.All |
| escalations.write | Engineer | Escalations.Write.Application |
| integrations.manage | Administrator | Integrations.Manage.Application |
| users.manage | Administrator | Users.Manage.Application |
