## ADDED Requirements

### Requirement: Provider-confirmed identity inventory
The identity acceptance record SHALL identify the actual tenant display name, tenant GUID, primary domain, licensing status and distinct API, SPA and machine client IDs confirmed by Microsoft. It SHALL distinguish failed Azure offer enrollment from directory provisioning and SHALL NOT infer that a directory is absent solely from an offer failure. No credentials or access tokens SHALL appear in the record.

#### Scenario: Partial signup outcome
- **GIVEN** Azure offer enrollment reports an eligibility failure
- **WHEN** an authorized account can subsequently access a dedicated directory
- **THEN** inspect the directory identity, administrator membership and licensing separately before reporting tenant creation or absence

#### Scenario: Unconfirmed registration
- **WHEN** Microsoft has not confirmed a registration or its required settings
- **THEN** mark that registration incomplete and do not substitute fabricated identifiers or test tokens

### Requirement: Evidence for live identity acceptance
Authentication SHALL remain incomplete until a real user obtains a tenant-issued API access token through the configured SPA and an approved machine obtains an application token through client credentials. The API SHALL validate both against tenant metadata and enforce existing token checks. Each unavailable check SHALL carry an explicit blocker; deterministic test tokens SHALL remain labeled as test evidence.

#### Scenario: Real user and machine acceptance
- **GIVEN** confirmed single-tenant registrations and approved least-privilege grants
- **WHEN** the real SPA and machine client request and use API access tokens
- **THEN** record redacted issuer, audience, expiry, scope or app-role evidence and the resulting API status without persisting bearer tokens

#### Scenario: Configuration is unavailable
- **WHEN** tenant or client configuration is missing or invalid
- **THEN** production protected endpoints remain inaccessible and no mock identity is used to complete acceptance
