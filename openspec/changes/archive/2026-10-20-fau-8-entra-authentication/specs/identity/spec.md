## ADDED Requirements

### Requirement: Dedicated API access-token validation
Protected API requests SHALL accept only signed RS256 Entra v2 access tokens for the configured dedicated tenant and API audience, validating exact issuer, tenant, audience, signature and lifetime using HTTPS discovery keys. Delegated tokens SHALL contain a scope; app-only tokens SHALL identify an application and contain application roles. Token validation SHALL NOT imply operation permission.

#### Scenario: Valid user and application identities
- **GIVEN** configured dedicated tenant and API identifiers with trusted discovery keys
- **WHEN** a correctly signed, unexpired delegated or application access token is presented
- **THEN** authentication succeeds with original claims available for FAU-9 permission policies

#### Scenario: Invalid token rejection
- **GIVEN** a protected request
- **WHEN** a token is missing, malformed, unsigned, expired, not yet valid, from another tenant or issuer, for another audience, signed with an untrusted key, or lacks access-token identity claims
- **THEN** authentication returns a correlated 401 Problem Details without token contents or validation internals

#### Scenario: Metadata keys unavailable
- **GIVEN** no usable cached signing keys
- **WHEN** metadata discovery fails or no key validates the token
- **THEN** the protected request is denied without falling back to unsigned or locally supplied production keys

### Requirement: Explicit configuration and anonymous boundaries
Unconfigured identity SHALL deny protected requests while explicit public docs, synthetic fixtures and health probes remain available. Enabling identity SHALL require canonical nonempty dedicated tenant and API client GUIDs and fail startup safely for invalid settings. Production SHALL expose no test authentication route, token issuer or alternate identity bypass.

#### Scenario: Unconfigured identity
- **GIVEN** no dedicated tenant has been provisioned
- **WHEN** public and protected operations are requested
- **THEN** explicit public operations remain available and protected operations return 401 for any supplied token

#### Scenario: Invalid enabled configuration
- **GIVEN** identity is enabled
- **WHEN** required identifiers are missing or malformed
- **THEN** startup fails with setting names and no supplied values

### Requirement: Isolated registration and live verification evidence
Identity documentation SHALL specify isolated tenant checks, distinct API/browser/machine registrations, PKCE S256 without SPA secrets or implicit flow, machine .default/application-role configuration, secret handling and live smoke tests. Pending provisioning and unimplemented browser flows SHALL be reported explicitly; deterministic API tests SHALL NOT be represented as successful browser or live tenant tests.

#### Scenario: Registration preparation
- **GIVEN** no separately authorized tenant provisioning
- **WHEN** FAU-8 configuration is delivered
- **THEN** the runbook records pending identifiers, exact configuration keys and verification steps without reusing LALAI resources or creating registrations

#### Scenario: Browser and machine live acceptance
- **GIVEN** separately authorized isolated registrations and a browser client implementation
- **WHEN** live acceptance runs
- **THEN** it checks PKCE callback/state/nonce failures, renewal, expired/revoked sessions, logout, delegated access, app-role machine access and unrelated-tenant/audience rejection with redacted evidence
