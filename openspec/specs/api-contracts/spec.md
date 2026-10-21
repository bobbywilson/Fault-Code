# api-contracts Specification

## Purpose
Define observable Fault Code api contracts requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Exclusive versioned API boundary
All application capabilities including every Angular administration operation SHALL be available through versioned REST API contracts with consistent DTOs, validation, pagination and concurrency behavior.

#### Scenario: Exclusive versioned API boundary acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a user performs an administrative action
- **THEN** Angular calls an authorized ASP.NET Core API operation; no UI operation accesses storage or AI directly

### Requirement: Standard errors and documentation contracts
The API SHALL generate OpenAPI and return standardized Problem Details with correlation IDs for invalid input, missing identity, forbidden access, absent resources and conflicts.

#### Scenario: Standard errors and documentation contracts acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** requests contain invalid input, no token, insufficient privilege or a version conflict
- **THEN** responses use the specified 400/401/403/404/409 semantics without internal secrets; contract tests validate the generated OpenAPI

### Requirement: Version one foundation operations
The API SHALL expose public GET /api/v1/health and read-only synthetic examples at /api/v1/examples with camelCase DTOs, stable IDs and deterministic ordering. Unknown versions and resources SHALL return 404 without exposing business data.

#### Scenario: Public foundation responses
- **GIVEN** a running API without provider credentials
- **WHEN** a client requests version one health or examples
- **THEN** health returns 200 and examples return only synthetic steering fixtures in stable ID order

### Requirement: Bounded collection queries
Collections SHALL validate one-based page and pageSize from 1 through 100, defaulting to page 1 and size 20; examples SHALL support an optional exact system filter of EPS, steer-by-wire or steering-ADAS. Invalid queries SHALL return 400; empty pages SHALL return an empty items array and accurate totalCount.

#### Scenario: Query boundaries
- **GIVEN** the synthetic examples collection
- **WHEN** a client filters, requests an empty page or supplies invalid pagination
- **THEN** valid results include page, pageSize and totalCount while invalid queries return correlated validation Problem Details

### Requirement: Correlated safe errors
HTTP errors SHALL use RFC 9457 application/problem+json with status, title, type, instance and server-generated correlationId also sent in X-Correlation-ID. Validation, identity, permission, resource, conflict, throttling and unexpected failures SHALL retain 400, 401, 403, 404, 409, 429 and 500 semantics without exception details or supplied secrets.

#### Scenario: Failure contract
- **GIVEN** an invalid request, denied request, missing resource, conflict, throttling or unhandled exception
- **WHEN** the API responds
- **THEN** the correct status and correlated Problem Details are returned without internal exception text

### Requirement: Generated compatible contracts
Builds SHALL generate OpenAPI describing operation IDs, schemas, examples, error responses and security conventions without Swagger UI. CI SHALL validate the document and reject breaking changes against the versioned baseline, including removed operations, narrowed inputs and weakened security. Public foundation operations SHALL declare no authentication requirement.

#### Scenario: Contract validation
- **GIVEN** a generated document and baseline
- **WHEN** CI validates a compatible document or a deliberately breaking fixture
- **THEN** the compatible document passes and the breaking fixture fails

### Requirement: Mutation conventions
Future mutations SHALL document strong ETag and If-Match concurrency with 409 for version conflicts and an Idempotency-Key for retryable creation, scoped to actor, operation and payload with durable replay. Reusing a key for a different payload SHALL return 409. Consumers SHALL use API contracts exclusively.

#### Scenario: Future mutation design
- **GIVEN** a ticket adding a persistent mutation
- **WHEN** its API contract is designed
- **THEN** it specifies concurrency, durable replay and authorization without direct UI storage access

### Requirement: Protected caller permission contract
GET /api/v1/authorization/me SHALL return only the authenticated caller kind and sorted granted permission names under authorization.read (Reader, Operator, Engineer or Administrator) or Authorization.Read application role. OpenAPI SHALL declare EntraBearer and exact delegated scope, allowed human roles and application role for protected operations, preserving anonymous fixture contracts and safe correlated 401/403 errors.

#### Scenario: Caller permissions
- **GIVEN** a signed synthetic delegated or application access token
- **WHEN** the caller has the exact authorization read permission
- **THEN** return its caller kind and granted permission names without token or resource data; missing identity returns 401 and insufficient permission returns 403

### Requirement: Vehicle and driver mutation contracts
Versioned vehicle, driver and association APIs SHALL document schemas, examples, exact security, bounded pagination and safe errors including unavailable storage. PUT/DELETE and association changes SHALL require a strong If-Match; creates SHALL require Idempotency-Key scoped to tenant, actor, client, operation and normalized payload. The store contract SHALL atomically persist state, association history, replay result and actor/client/correlation audit.
#### Scenario: Concurrent and retried mutations
- **WHEN** a create repeats with the same key/payload or a stale update races
- **THEN** replay returns the original result without duplicate audit, changed payload conflicts and stale versions return 409 without partial changes
#### Scenario: Invalid concurrency input
- **WHEN** mutation headers are missing, weak or malformed
- **THEN** return correlated 400 before mutation

### Requirement: Fault management API contracts
Versioned fault management, history, publication and approved lookup operations SHALL expose exact bearer policies, bounded validation, pagination, operation-specific examples, strong ETags and safe correlated Problem Details, including503 for unavailable production adapters.
#### Scenario: Documented secured operations
- **GIVEN** generated OpenAPI
- **WHEN** a consumer inspects each fault operation
- **THEN** its actual read/write/publication permission and request/response/precondition contracts are documented and baseline compatibility checks pass

### Requirement: Incident API contracts
Versioned incident intake/list/detail/history/status/failure/retry operations SHALL use exact policies, correlated safe Problem Details, bounded pagination, strong ETags and source/key replay protection. Creation SHALL return201 with Location and original replay ID/version. Missing/malformed preconditions SHALL return400 and conflicts409.
#### Scenario: Incident contract acceptance
- **WHEN** authorized consumers use production incident controllers
- **THEN** generated examples and security metadata match actual policies; invalid input400, invalid identity401, insufficient permission403, missing/other-owner404, conflicts409 and unavailable persistence503 remain distinct

### Requirement: Ontology API contracts
The versioned API SHALL expose draft creation/replacement/retrieval, history, publication, exact approved retrieval and stored evaluation operations. Management SHALL require knowledge.publish; approved reads and evaluation SHALL require knowledge.read. Contracts SHALL declare preconditions, replay keys, operation examples and safe correlated errors; tenant and approval fields SHALL be server-derived.
#### Scenario: Additive protected ontology API
- **WHEN** generated contracts and production routes are inspected
- **THEN** every ontology operation declares exact policies and examples, existing contracts remain compatible, malformed inputs reject400, unknown404, stale/replay conflict409 and unconfigured storage503

### Requirement: Document lifecycle and retrieval API
Versioned document create/draft/update/publication/delete/index and tenant reindex APIs SHALL require knowledge.publish; search SHALL require knowledge.read. Explicit DTOs SHALL reject unknown input fields and omit tenant data. Mutations SHALL use strong If-Match and scoped retry keys where specified. OpenAPI SHALL document schemas, examples, policies, correlated errors, ETags and creation Location.
#### Scenario: Authorized document API and failures
- **WHEN** synthetic delegated or machine callers invoke document operations
- **THEN** exact scope/role policies enforce401/403, inaccessible IDs404, bad bounds/fields/preconditions400, stale/replay conflicts409 and unconfigured/provider failures503; search has bounded results and explicit evidence status
