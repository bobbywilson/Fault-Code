## ADDED Requirements
### Requirement: Fault management API contracts
Versioned fault management, history, publication and approved lookup operations SHALL expose exact bearer policies, bounded validation, pagination, operation-specific examples, strong ETags and safe correlated Problem Details, including503 for unavailable production adapters.
#### Scenario: Documented secured operations
- **GIVEN** generated OpenAPI
- **WHEN** a consumer inspects each fault operation
- **THEN** its actual read/write/publication permission and request/response/precondition contracts are documented and baseline compatibility checks pass
