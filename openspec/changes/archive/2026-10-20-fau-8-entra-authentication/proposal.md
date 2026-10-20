# FAU-8: Dedicated Entra authentication support

## Why
The API has no identity validation. Establish independently testable bearer authentication before FAU-9 adds permission policies, while preserving the explicit boundary around tenant provisioning.

## What Changes
- Validate dedicated-tenant v2 access tokens using Microsoft's maintained JWT bearer middleware, HTTPS discovery/JWKS, exact issuer/audience, signature and lifetime.
- Deny protected requests when identity is unconfigured; require explicit anonymous metadata for public endpoints.
- Add deterministic signed-token and discovery tests, safe error coverage, configuration validation and an isolated-tenant registration/live-test runbook.
- Document browser PKCE and machine client-credentials configuration. Angular implementation belongs to FAU-22; no tenant, registrations, consent or credentials are created by this change.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `identity`: API validation, unconfigured behavior and verifiable registration boundaries.

## Impact
API composition, identity configuration, Microsoft authentication package/lock files, integration tests, Docker input allowlist and development documentation. FAU-5 and FAU-7 are merged. FAU-9 owns operation scopes/app roles and ownership. Existing public API contracts stay compatible. Dedicated tenant and live flows remain pending specific authorization and provisioning.
