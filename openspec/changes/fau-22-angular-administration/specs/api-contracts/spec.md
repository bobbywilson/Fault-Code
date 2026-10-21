## ADDED Requirements

### Requirement: User and integration administration contracts
Versioned user, role and integration administration SHALL expose bounded lists, details and conditional replacement under exact users.manage or integrations.manage policies, using explicit secret-free DTOs. Changes SHALL atomically bind record, trusted audit and replay under strong version preconditions. Local records SHALL NOT provision Entra grants or activate providers.
#### Scenario: Validation and failure contract
- **WHEN** unknown fields, invalid grants, malformed preconditions, stale versions or mismatched replay keys are supplied
- **THEN** return400 for invalid inputs,409 for conflicts,404 for inaccessible records and503 for unavailable adapters; filter tenant before counting/pagination
#### Scenario: Authorized administrative mutation
- **WHEN** a permitted administrator replaces same-tenant local user roles or integration metadata with valid version and replay key
- **THEN** the configured adapter atomically records only approved local metadata and audit; unconfigured production returns503 without changing Entra or external services
#### Scenario: Denied or invalid management
- **WHEN** readers, engineers, machines without exact application role, malformed inputs or cross-tenant identifiers attempt management
- **THEN** deny with401/403/400/404 as applicable and expose no credentials or other-tenant records
