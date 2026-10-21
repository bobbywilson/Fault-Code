## ADDED Requirements

### Requirement: Explicit administration operation policies
User/role endpoints SHALL require users.manage and integration endpoints SHALL require integrations.manage under the existing exact delegated scope plus Administrator role or granular application role. Server-loaded tenant context SHALL filter before counting/pagination and changes SHALL retain trusted actor/client/correlation. Browser guards SHALL never confer server authority.
#### Scenario: No administrative elevation
- **WHEN** a Reader, Operator or Engineer attempts user/integration mutation or an Administrator lacks the exact scope
- **THEN** server returns403 and performs no write; local role records never alter validated Entra claims
