## ADDED Requirements

### Requirement: API reference and separate request testing
Public Mintlify pages SHALL cover endpoints, request authentication and request/response conventions only. Architecture, CI/CD and release instructions SHALL remain repository documentation. A separate local Swagger UI SHALL use the same-origin generated OpenAPI in Development, without token acquisition, bearer proxies, persistent authorization or relaxed authentication. This approved tester supersedes the earlier no-Swagger requirement.

#### Scenario: Execute a local documented request
- **WHEN** a user opens local Swagger UI and executes a request
- **THEN** the interface displays the actual API status and response
- **AND** protected endpoints reject missing authentication with 401
- **AND** the tester is unavailable by default in Production

### Requirement: Six-resource public API
The public REST API SHALL expose exactly list, get, create, update and delete for incidents, vehicles, drivers, notifications, fault codes and ontology. OpenAPI, Mintlify and Swagger SHALL describe these thirty operations. Other business endpoints SHALL NOT be mapped. Backend processing and MCP MAY remain internal. Health probes and OpenAPI are separate infrastructure routes.

#### Scenario: Exact public surface
- **WHEN** the API generates its contract and maps resource routes
- **THEN** each of the six collections has GET and POST and each ID route has GET, PUT and DELETE
- **AND** all thirty require validated Entra authorization
- **AND** a removed resource operation returns 404

### Requirement: Light reference documentation
Mintlify SHALL use the light theme and contain only API endpoint, authentication, request and response documentation.

#### Scenario: Light documentation
- **WHEN** a reader opens the documentation with a dark operating-system preference
- **THEN** the documentation renders in light mode

### Requirement: Request authentication documentation only
Public API documentation SHALL describe bearer request authentication, required scopes and roles, validation and error responses. It SHALL NOT describe token generation, refresh or reset procedures or expose a token-management endpoint. Entra token refresh remains a Users workspace function outside the resource API.

#### Scenario: Authenticate an API request
- **WHEN** a reader opens authentication guidance or the generated reference
- **THEN** it describes Authorization: Bearer and the required token claims and permissions
- **AND** no token creation, reset or refresh operation or instructions appear
