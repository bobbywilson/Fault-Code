# Proposal
## Why
Part of FAU-22. The protected backend needs an accessible administration client and explicit user/integration API boundaries. Linux Docker is the sole delivery platform per the user's October 21 direction.
## What Changes
- Add Angular/TypeScript, Spartan UI/Tailwind responsive administration with MSAL PKCE and exclusive versioned API clients.
- Reuse existing fleet, knowledge, incident, investigation, engineering and notification operations; add least-privilege user/role and integration contracts with unavailable production adapters pending approved persistence/identity integration decisions.
- Display only recorded activity; provide loading, empty, forbidden, expired-session, conflict and unavailable states.
- Add isolated mocked-browser tests, server permission tests, Linux frontend packaging and Linux-only backend verification.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `administration`: responsive role-aware API workflows and honest provider availability.
- `identity`: maintained MSAL browser lifecycle with explicit isolated configuration.
- `api-contracts`: explicit protected administration API contracts.
- `authorization`: user/integration least privilege and audited context.
- `development-infrastructure`: Linux/Docker delivery verification and frontend packaging.
## Impact
New frontend, additive backend administration adapter contracts, tests, Docker/CI and documentation. ADR-001/002/003/008 apply. DEC-004 Angular portion resolves to registry-verified Angular22.2.3/TypeScript6/Node22.22.3, Spartan1.8.0/Tailwind4.3.3 and MSAL Angular6.2.3/browser5.25.1, within published peers. No database, Graph access grant, hosting, model, credential, account or live tenant provisioning.
