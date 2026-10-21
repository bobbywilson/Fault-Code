## ADDED Requirements

### Requirement: Versioned notification operations
The API SHALL expose conditional notification enqueue/detail/list/history/dispatch/cancellation and driver local preference reads/updates with explicit DTOs, existing exact policy conjunctions, tenant/original owner filtering before page/count, strong ETags/scoped replay, safe400/401/403/404/409/503 errors and generated compatible OpenAPI. External production dispatch SHALL remain unavailable pending approved adapters.

#### Scenario: Protected notification contracts
- **WHEN** permitted consumers manage synthetic preferences and notification history or attempt malformed/unauthorized mutations
- **THEN** return bounded approved DTOs and honest acknowledgement states or correlated errors, preserve prior REST/MCP schemas and never expose tenant/owner/contact plumbing or vehicle installation operations
