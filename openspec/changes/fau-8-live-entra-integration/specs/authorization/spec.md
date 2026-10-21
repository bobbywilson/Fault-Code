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
- **GIVEN** a machine granted only Authorization.Read
- **WHEN** it calls permission inspection and then a protected mutation
- **THEN** permission inspection is authorized and the mutation is forbidden

#### Scenario: Invalid identity
- **WHEN** a protected request has no token, an expired token, invalid signature, unrelated issuer or incorrect audience
- **THEN** the API returns 401 without a production authentication shortcut
