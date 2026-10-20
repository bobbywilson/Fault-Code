# api-contracts Specification Delta

## Purpose

Define observable Fault Code api contracts requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

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
