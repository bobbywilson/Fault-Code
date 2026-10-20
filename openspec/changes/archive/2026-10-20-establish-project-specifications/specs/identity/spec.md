# identity Specification Delta

## Purpose

Define observable Fault Code identity requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Isolated browser identity
Identity SHALL use a dedicated Microsoft Entra ID Free tenant separate from LALAI with OAuth 2.0/OIDC authorization code flow and PKCE for browser clients.

#### Scenario: Isolated browser identity acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an Angular user signs in
- **THEN** state/nonce/PKCE are validated; no SPA secret or implicit flow is used, and tokens from the unrelated tenant are rejected

### Requirement: Machine identity
Authorized machine consumers SHALL authenticate through OAuth client credentials using API application roles; delegated user scopes SHALL NOT substitute for app-only permissions.

#### Scenario: Machine identity acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a machine acquires an API token using its approved confidential registration
- **THEN** the API validates exact issuer, audience, signature and lifetime and enforces app roles; missing role or wrong audience is denied
