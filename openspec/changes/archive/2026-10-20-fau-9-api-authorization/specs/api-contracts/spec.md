# API contracts delta
## ADDED Requirements
### Requirement: Protected caller permission contract
GET /api/v1/authorization/me SHALL return only the authenticated caller kind and sorted granted permission names under authorization.read (Reader, Operator, Engineer or Administrator) or Authorization.Read application role. OpenAPI SHALL declare EntraBearer and exact delegated scope, allowed human roles and application role for protected operations, preserving anonymous fixture contracts and safe correlated 401/403 errors.

#### Scenario: Caller permissions
- **GIVEN** a signed demo delegated or application access token
- **WHEN** the caller has the exact authorization read permission
- **THEN** return its caller kind and granted permission names without token or resource data; missing identity returns 401 and insufficient permission returns 403
