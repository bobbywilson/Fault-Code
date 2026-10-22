## ADDED Requirements

### Requirement: Document lifecycle and retrieval API
Versioned document create/draft/update/publication/delete/index and tenant reindex APIs SHALL require knowledge.publish; search SHALL require knowledge.read. Explicit DTOs SHALL reject unknown input fields and omit tenant data. Mutations SHALL use strong If-Match and scoped retry keys where specified. OpenAPI SHALL document schemas, examples, policies, correlated errors, ETags and creation Location.
#### Scenario: Authorized document API and failures
- **WHEN** demo delegated or machine callers invoke document operations
- **THEN** exact scope/role policies enforce401/403, inaccessible IDs404, bad bounds/fields/preconditions400, stale/replay conflicts409 and unconfigured/provider failures503; search has bounded results and explicit evidence status
