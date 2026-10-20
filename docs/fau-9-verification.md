# FAU-9 verification

Part of FAU-9. Authorized API permission and resource authorization foundation. Linear stays open for personal review.

## Local evidence — 2026-10-20

Pinned .NET SDK 10.0.402, runtime 10.0.13. Set DOTNET_ROOT and prepend its path as documented in local-development.md.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

97 passing tests (4 Domain, 3 Application, 90 integration), zero skips. Release build zero warnings/errors; locked restore, format and whitespace passed. 33 explicit Docker inputs scanned with no recognized credential patterns. Generated OpenAPI/examples valid and v1 compatible; all seven breaking fixtures rejected, additive optional parameter accepted. Local contract check uses the existing FAU-7 contracts virtual environment; CI installs pinned requirements independently. No new packages or lock changes were needed.

## Acceptance mapping

| Scenario | Evidence |
|---|---|
| Exact permission contract and caller separation | AuthorizationTests iterates every catalog permission through actual Microsoft JWT middleware/discovery using ephemeral RSA, checking independent expected human role assignments, wrong scope/prefix/case, granular app roles, role-only denial and delegated/application-only variants |
| Reader/Operator/Engineer/Administrator boundaries | Reader cannot publish/manage integrations; Administrator has no Engineer/Operator bypass; explicit scopes and correct allowed roles required; machines require exact granular role |
| Driver and vehicle ownership | Both resource areas tested with matching/different oid, own-only Administrator denial, explicit staff/machine all-read success, app own-read denial, different tenant, invalid/missing owner and missing record denial; query ownership spoofing fails |
| Production protected operation | /api/v1/authorization/me checks missing identity 401, insufficient permission 403, exact delegated and app success, sorted least-privilege permission list, no identifiers; expired/wrong issuer/audience/tenant/signing key deny |
| Unprovisioned API | Production permission endpoint denies a signed token with identity disabled; no discovery used |
| Default denial and route coverage | Valid Administrator denied on test-only unannotated/bare Authorize endpoints; unknown route remains 404; every production endpoint explicitly anonymous or registered permission; production OpenAPI has four paths |
| OpenAPI permission security | Production route inventory verifies bearer security and exact scope/human-role/application-role extensions against registered policy; public foundation contracts unchanged |
| Correlated errors and safe decision logs | Signed-token denials assert application/problem+json and matching X-Correlation-ID; logger capture verifies allowed/denied permission, actor/client/correlation context without tokens/Bearer/tenant |
| Existing identity protections | FAU-8 full negative middleware and isolated startup-process suite still passes; exact issuer/audience/signature/lifetime controls unchanged |

Self-review covered privilege escalation, explicit scope/role separation, trusted resource tenant/owner checks, fail-closed fallback/default policies, anonymous route inventory, safe audit context, security metadata, Docker inputs and absence of production test controllers. Domain/Application remain provider-free. Catalog values and provisioning obligations are documented in authorization.md and the authorization OpenSpec delta.

## Practical limits

Only the caller permission summary is a new production endpoint. Future business routes must consume these named policies and trusted resource checks. Synthetic ownership test routes are in the test assembly only; no production vehicle/driver records, CRUD, mutation, persistent audit, MCP server, tenant or registration was fabricated. Durable business audit, engineering approval state, write ownership, list query filtering, idempotency and concurrency belong to future domain implementations after persistence decisions.

Dedicated tenant registration/consent and real browser/machine tokens are unprovisioned. Live key rollover and browser flows remain pending; these API tests do not prove browser behavior or instant JWT revocation. Local Docker Desktop backend remains unavailable; require exact-source Linux container smoke CI before merge. No deployment is claimed.

Existing Lalai cloud host hardening is separate. Its requested reboot and SSH source-IP allowlist remain pending user input; FAU-9 does not change the host.

## Persistence and delivery

Official OpenSpec archive synchronizes authorization and api-contracts deltas, followed by strict validation of all main specs. GitHub PR/check/merge evidence is observed separately on the exact source commit. Keep the issue open; use Part of FAU-9 without auto-close keywords.
