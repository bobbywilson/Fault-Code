# FAU-8 verification

Part of FAU-8. Implements the authorized locally possible API authentication/configuration scope. Live FAU-8 browser/machine acceptance is incomplete until separately authorized tenant registrations and the FAU-22 browser client exist. Linear remains open for personal review.

## Local evidence — 2026-10-20

Pinned SDK 10.0.402, runtime/package 10.0.13. Set DOTNET_ROOT and prepend its directory to PATH as in local-development.md.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

Full suite: 89 tests passed (4 Domain, 3 Application, 82 integration), no skips. Release build: zero warnings/errors. Contract validation: valid OpenAPI/examples, compatible with committed v1 baseline, seven breaking fixtures rejected and additive optional parameter accepted. Build-input check: 30 explicit source inputs, no recognized credential patterns. Format and whitespace checks passed. Python contract command used the existing local contract virtual environment; CI installs pinned requirements independently.

## Scenario mapping

| Requirement / scenario | Evidence |
|---|---|
| Dedicated API access-token validation / valid user and app | EntraAuthenticationTests: ephemeral RSA-signed user/app JWTs, actual Microsoft JWT middleware fetches HTTPS-shaped discovery/JWKS via deterministic in-memory transport; original oid/scp/roles reach test controller |
| Invalid identity | Missing/malformed/unsigned, non-RS256, wrong signature/key, wrong audience/trailing slash, wrong issuer (even when discovery supplies it), tid/ver, expired/future/no-expiry, missing oid/azp, ID-token shape, roleless app and app-with-scope all return generic 401 |
| Metadata keys unavailable | Discovery 503 and unknown signing key fail closed; no static production key or discovery override is exposed |
| Explicit configuration / unconfigured identity | Any bearer denied with identity disabled, no discovery called; public health/examples/docs stay 200; unknown routes stay 404 |
| Invalid enabled settings | Nine isolated startup-process cases verify missing/invalid GUIDs and malformed Enabled produce only setting-name diagnostics |
| Anonymous boundaries | Public endpoints stay anonymous with identity enabled; fallback protects a test endpoint without Authorize; test-only role policy distinguishes missing identity 401 from valid insufficient role 403 |
| Safe error contract | 401/403 correlated application/problem+json independent of HTML Accept; Bearer challenge contains no validation error details, token or tenant |
| No test bypass shipped | Normal host returns 404 for identity-fixture; production OpenAPI still contains only three public fixture operations |
| Registration preparation | entra-authentication.md contains isolation checks, pending IDs, three separate registrations, API v2/idtyp, PKCE S256, exact planned callback/logout URLs, machine .default/roles, credential handling and live test matrix |

The deterministic discovery transport is test-only. Production uses HTTPS metadata and Microsoft IdentityModel key caching/refresh; tests do not contact Entra or issue production identities. Vendor dependencies remain confined to the API; Domain/Application are unchanged and package-free.

## Remaining acceptance

- No tenant, registrations, consent, real credentials or live token acquisition were created/tested. Actual tenant isolation must be checked against the authorized inventory before enabling; syntax validation cannot prove isolation.
- Angular login/logout/callback state/nonce/PKCE/renewal and revoked-session tests are pending FAU-22's client, including deterministic browser mocks and live isolated-tenant smoke. API JWT tests do not prove those browser behaviors.
- FAU-9 must enforce exact operation scope/application role and ownership. A test-only synthetic role policy is not a delivered business permission policy.
- JWT lifetime validation does not guarantee immediate session revocation. Live discovery key rollover/cached-key outage behavior remains in the runbook.
- Local Docker Desktop daemon is unavailable; no local container/live hostname acceptance is claimed. Linux CI container smoke is required on the exact PR commit before merge.

## Review and persistence

Review issuer/audience validation, signature/lifetime controls, disabled identity, metadata failure, public endpoint metadata, redaction, claims separation, explicit build inputs and unchanged business surface. Official OpenSpec archive and all-spec strict validation accompany the code commit. PR CI and merge evidence are separately observable in GitHub.
