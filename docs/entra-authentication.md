# Dedicated Entra identity

Part of FAU-8; ADR-003. The API implements bearer access-token validation and deny-by-default endpoint authentication. No tenant, registration, consent, credential, Angular login or live token acquisition has been provisioned or verified. Those external steps require specific authorization. FAU-9 adds operation permissions; FAU-22 supplies the Angular client and browser protocol tests.

## API configuration

| Environment key | Value |
|---|---|
| `Entra__Enabled` | `false` by default; set `true` only after isolation checks |
| `Entra__TenantId` | Dedicated Fault Code directory ID, canonical lowercase nonzero GUID |
| `Entra__ClientId` | Fault Code **API** application/client ID, canonical lowercase nonzero GUID |

The API has no client secret. Configuration uses normal ASP.NET configuration sources, including user secrets and environment variables. Compose accepts the same three variables through `.env`/the shell. Do not commit real credentials. IDs are currently **pending provisioning**, not example GUIDs to deploy. Changing configuration requires a process restart. Invalid enabled settings fail startup with setting names and no supplied values.

The fixed authority and issuer are `https://login.microsoftonline.com/{TenantId}/v2.0`. The v2 audience is the API client GUID, **not** the SPA/machine client ID, Graph audience, or `api://` resource URI. The middleware obtains signing keys from HTTPS OpenID metadata/JWKS, caches/refreshes them, and accepts signed RS256 tokens only. It validates signature, exact issuer/audience (including trailing slash), required expiry, lifetime with 60-second clock skew, exact `tid`, `ver=2.0`, `oid`, and `azp`. Delegated tokens require nonempty `scp`; machine tokens require `idtyp=app`, nonempty `roles`, and no delegated scope. Original claims remain available for permission policies.

Successful authentication is not business permission. A nonempty arbitrary role/scope does not authorize an operation: FAU-9 enforces the [exact permission matrix and ownership policies](authorization.md). Only the protected caller permission summary exists; business endpoints remain future tickets. All mapped endpoints deny unless they have an explicit named permission policy or are explicitly anonymous; unknown routes retain 404. `/api/v1/health`, examples, `/openapi/v1.json` and infrastructure health remain explicitly public. Disabled identity accepts no token and never calls discovery. Protected requests return 401 with a generic Bearer challenge and correlated Problem Details; errors never return token contents or validation internals. No fake authentication route or local issuer ships.

## Registration runbook — execute only after specific authorization

1. Create/identify the approved **dedicated Fault Code Entra ID Free workforce tenant**. Record directory display name, default domain and tenant GUID in the authorized configuration inventory. Independently compare tenant GUID/domain to LALAI's identity inventory; they must differ. Do not read/reuse LALAI secrets, registrations or credentials. A GUID's syntax cannot prove organizational isolation.
2. Create three distinct **single-tenant** registrations named for the API, Angular SPA, and machine client. Record their client IDs separately; verify all belong to the dedicated directory. Do not use common/organizations authorities. No paid Entra feature is required by the designed flow; raise any actual tenant-creation/license restriction before purchasing or choosing another tenant.
3. API registration: set `api.requestedAccessTokenVersion=2`; expose resource URI `api://{api-client-id}`. Define the exact delegated scopes and application-only roles from FAU-9's permission contract before granting access. Add the `idtyp` optional claim to **access tokens** so machine tokens identify `app`. Roles intended for machines must allow `Application`; delegated scopes must not substitute for app roles. Keep the API registration free of browser redirect URIs and client credentials.
4. SPA registration: use the **Single-page application** platform, authorization code + PKCE S256 through supported MSAL Angular/browser libraries (version decision with FAU-22). No SPA secret, implicit grant or resource-owner-password flow. Register exact development callback/logout URLs matched to FAU-22's router, and exact HTTPS URLs for an authorized deployment. The reserved development design is `http://localhost:4200/auth/callback` and post-logout `http://localhost:4200/`; these routes do not exist yet. Do not register wildcards or assume `faultcode.test` HTTP is a deployable secure callback. Verify platform URL rules before registration. Request `openid profile` plus the exact API delegated scope; consent only the approved scope.
5. Machine registration: keep it separate from the API/SPA, assign only approved API application roles, obtain authorized admin consent, and configure assignment requirements as appropriate to the approved callers. Prefer a certificate or approved workload credential over a long-lived secret. If a test secret is specifically authorized, store it only in the secret store with expiry/rotation; never in JSON, shell arguments, logs, PRs or screenshots.
6. Machine token request: tenant-specific `/oauth2/v2.0/token`, `grant_type=client_credentials`, machine `client_id`, certificate assertion or secret, `scope=api://{api-client-id}/.default`. `.default` requests the API's granted application permissions; it is not a delegated scope and does not grant roles itself. Never put machine credentials in Angular.
7. Browser configuration: tenant-specific authority, SPA client ID, exact redirect/logout URLs, and the API delegated scope. Use MSAL redirect handling and cache/session APIs; never implement custom state/nonce or token parsing as trust. Prefer session/in-memory cache according to the client threat model; do not log tokens. Only send **access tokens** to the API. Do not send an ID token as a bearer token.
8. After isolation and registration review, set the three API environment keys, restart and perform the live checks below. Persist only redacted evidence, exact source commit and registration identifiers in the authorized inventory. No tenant value is silently selected by application code.

## Live isolated-tenant smoke — pending, not run

Run after authorized provisioning, FAU-9's protected endpoint and FAU-22's browser implementation are available. Choose a read-only protected operation and its exact documented permission. Keep access tokens/credentials out of command history and shared artifacts.

| Check | Expected result / evidence |
|---|---|
| Tenant isolation | Distinct Fault Code/LALAI tenant GUIDs and domains, three distinct single-tenant client IDs; capture no secrets |
| Browser login | Authorization code flow, S256 challenge, exact redirect, successful access-token call; no secret/implicit grant |
| Callback attacks | Incorrect/replayed state, nonce or callback/code verifier fails; deterministic browser mocks plus actual isolated login |
| Sessions | Renewal before/after expiry; expired/revoked refresh session leads to reauthentication; logout clears local session and provider redirect returns to approved URL |
| Machine | `.default` token has correct aud/tid/issuer and approved app role; protected read succeeds |
| Permission denial | Roleless app rejected; wrong role/scope denied 403; a delegated scope cannot authorize app-only permission |
| Identity denial | No token, malformed, expired, unrelated-tenant, wrong audience and invalid signature return safe correlated 401 |
| Public docs | Anonymous document succeeds; protected Try It requires a correct access token |
| Discovery | Key rollover succeeds via refreshed metadata; outage without cached trusted keys fails closed |

A JWT API checks the token's lifetime, not the current interactive session on every call. Provider session revocation does not instantly invalidate an already issued access token. Do not claim immediate revocation or continuous-access-evaluation enforcement from these tests. Cached trusted keys can remain usable during a metadata outage; unknown keys must never bypass verification.

## Sources

- [Microsoft JWT bearer configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Microsoft access-token claims](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference)
- [Authorization code and PKCE](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
- [Client credentials and .default](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow)

Automated evidence and remaining acceptance: [FAU-8 verification](fau-8-verification.md).
