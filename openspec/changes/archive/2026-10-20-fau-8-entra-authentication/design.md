# FAU-8 design

## Context
See proposal.md. ADR-003 requires isolation; no dedicated tenant/registrations exist. The API's public fixtures and generated build-time contract must work without provider credentials.

## Goals / Non-Goals
Implement production bearer validation and deterministic real-middleware tests. Do not implement a token issuer, browser OIDC protocol, Angular scaffold, production permission policy, external provisioning, or database.

## Decisions
- Use Microsoft.AspNetCore.Authentication.JwtBearer 10.0.13, matching the pinned ASP.NET runtime. It uses Microsoft IdentityModel discovery/JWKS caching and key refresh. Microsoft.Identity.Web adds downstream-acquisition functionality unnecessary for this bearer-only API; handwritten JWT parsing/cryptography is rejected.
- Explicit `Entra:Enabled` defaults false. Disabled middleware returns no identity and challenges with 401. Enabled requires canonical nonzero tenant/client GUIDs. Authority/issuer are derived from the tenant, fixed to login.microsoftonline.com/v2.0; API audience is the API client GUID for v2 tokens. No configurable metadata URL, static key or validation-disable toggle ships.
- Validate RS256, issuer, audience (no trailing slash equivalence), required expiry/signature and lifetime with 60-second clock skew. After cryptographic validation require tid, ver=2.0, oid and azp; delegated scp or app idtyp=app plus nonempty roles distinguish access tokens from ID/roleless tokens. FAU-9 enforces actual scope/role values and resource ownership.
- Default/fallback authorization requires an authenticated identity. Explicit AllowAnonymous marks current public controllers, docs and infrastructure probes. Unknown routes retain 404 by leaving no-endpoint requests out of the fallback policy; future endpoints are protected automatically.
- Keep errors generic with bearer challenge and central Problem Details; disable token storage and diagnostic error details. No token bodies are logged. Tests inject an in-memory discovery HTTP handler into the real JWT middleware, use ephemeral RSA keys and a test-only protected controller. No fake principal scheme.
- Browser login/logout, callback/state/nonce/renewal tests depend on the Angular implementation in FAU-22 and isolated registrations. Document them as pending, never substitute mock API tests for browser proof. This implements the explicitly authorized locally possible subset; original browser/machine requirements remain intact.

## Risks / Trade-offs
- JWT revocation is not immediate → short-lived provider tokens plus client session renewal; do not claim online session-revocation enforcement by a bearer API.
- Wrong configuration could target another tenant → canonical IDs prevent broad common/organizations authorities; runbook requires an independent tenant-isolation check before enabling. Code cannot discover whether an arbitrary GUID belongs to LALAI without external evidence.
- Discovery outage → middleware uses cached keys/refresh; without trusted keys authentication fails closed. Live rollover remains a documented smoke step.
- Public artifacts must build without tenant → bearer security remains HTTP JWT, no fabricated OAuth URLs or IDs.

## Migration Plan
Deploy with identity disabled and public foundation unchanged; protected endpoints fail closed. After separately authorized registration, set Entra__Enabled/TenantId/ClientId and restart. Roll back enabled configuration to deny protected traffic; do not introduce a bypass. No persistence migration.
