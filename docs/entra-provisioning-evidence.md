# Live Entra provisioning evidence

Part of FAU-8, FAU-9 and FAU-22. Observed 2026-10-21. This is an incomplete acceptance record, not a deployment or completed authentication claim.

## Verified provider inventory

| Field | Observed value |
|---|---|
| Tenant display name | Fault Code; Microsoft reported successful tenant-property update |
| Tenant ID | c0ff26e9-dd24-4e59-b395-1e9b48354aad |
| Primary domain | emailbobbywilsongmail.onmicrosoft.com |
| License | Microsoft Entra ID Free, as shown in directory overview |
| Administrator | User's Gmail-linked Microsoft account; Global Administrator |
| Administrator creation time | October 21, 2026, 3:05 PM America/New_York, as shown in user profile |
| Security defaults | Portal says organization is protected by security defaults; no policy change made |
| API client ID | ce48cbc2-5349-4228-b5ea-365c9df1e2af |
| Angular client ID | c09a3b35-fa74-4288-9706-db1287bf5634 |
| Machine client ID | 4e8e146f-ff62-475c-9803-80c3cd2d8521 |

The tenant GUID differs from LALAI's observed directory GUID. No LALAI resource was modified. No premium trial, paid plan or Azure subscription activation is confirmed. The portal still offers Azure signup; this does not negate the separately visible Entra directory.

## Partial provisioning correction

The Azure free-offer signup reported ineligibility. On a later authenticated portal inspection, a dedicated directory, one member administrator and Microsoft Entra ID Free licensing were present. The administrator creation timestamp and audit activity coincide with the earlier signup attempt. This supports partial directory provisioning during that attempt; the exact Azure-side failure reason remains undisclosed. Earlier statements that no tenant existed were too broad. No subsequent paid enrollment was needed to observe or name this directory.

The university route did not establish academic eligibility and is not the chosen identity source. The user's final instruction is a separate Free tenant using the Gmail-linked Microsoft account. Temporary consideration of LALAI and premium trials was withdrawn.

## Implementation and test boundaries

Existing API bearer validation and authorization policies use real Microsoft middleware. Angular uses MSAL PKCE. Local browser configuration now uses the confirmed tenant, API and SPA public identifiers. Automated integration tests construct local RSA tokens and in-memory discovery; Angular tests mock MSAL or replace authentication only in test builds. Those checks are not evidence of real Entra-issued token acquisition.

Microsoft confirmed creation of all three single-tenant app registrations. The API has 15 delegated scopes, v2 access tokens and 17 persisted roles (four human roles and thirteen application roles). Microsoft rejected five original machine values as scope duplicates. The user approved `.Application` suffixes on 2026-10-21; provider manifest, API policies, tests, documentation and OpenAPI now use the approved values.

Angular has the SPA callback `http://localhost:4200/auth/callback`, logout URL `http://localhost:4200/`, no client secret, and administrator consent for the 15 API delegated permissions. Default Microsoft Graph permissions were removed from the API and SPA registrations. No production redirect URI or deployment has been configured.

Live local verification: Angular launched the tenant-specific Microsoft authorization-code flow with S256 PKCE. Selecting the Gmail-linked Microsoft identity returned to the registered callback and displayed an authenticated workspace. ASP.NET Core then evaluated the real caller and returned access denied because no application role was assigned. API log correlation `f367ae6f5a0e4569a0c12ff4ea654fb9` records `permission=authorization.read kind=Either allowed=False` for the confirmed user and SPA client IDs. This verifies live user authentication and enforcement of missing-role denial, not authorized-user acceptance. No bearer token was saved in this record.

After direct Reader assignment, Microsoft confirmed one user assigned and displayed Bobby Wilson / User / Reader. Signing in again acquired a fresh token; the API logged `authorization.read allowed=True`, correlation `e30d3884963e486d8c0cf666b8fc5624`, and Angular displayed authorized workspace action controls. An independent unauthenticated request to `/api/v1/authorization/me` returned HTTP 401. Certificate client credentials and consent are verified: real Microsoft token has only Authorization.Read.Application, correct tenant/issuer/audience and idtyp=app. API permission inspection returned 200 with only authorization.read; vehicle POST returned 403; no token and tampered signature returned 401. Initial token immediately after consent lacked roles and was rejected 401; a later fresh token carried the consented role and passed. Certificate is valid for seven days, with private key restricted outside the repository. Production credential custody and rotation remain unconfigured. Actual expired-provider-token and independent wrong-issuer/audience live cases remain unexecuted; deterministic tests cover them. Provider logout reached Microsoft logoutsession, but final callback observation timed out; full logout/renewal and production redirects remain unverified. Changed-file credential-pattern scan found no private key, nonempty clientSecret or JWT; production bundles contain no test-auth fixture markers (three JavaScript files inspected).

Additional verification: complete backend integration suite 387 passed, 0 failed; focused authorization suite after adding retired-role rejection 8 passed; Angular production build passed; strict OpenSpec 21 passed. Existing FAU-8, FAU-9, FAU-22 received live evidence and outstanding-gap comments and remain open.

Focused verification on 2026-10-21: `dotnet test tests/FaultCode.IntegrationTests/FaultCode.IntegrationTests.csproj --filter 'FullyQualifiedName~EntraAuthenticationTests|FullyQualifiedName~EntraConfigurationTests|FullyQualifiedName~AuthorizationTests' --no-restore --verbosity minimal` passed 43 tests, failed 0, skipped 0. The bundled .NET 10 toolchain must be both DOTNET_ROOT and first on PATH; the initial run failed in OpenAPI generation because its child process resolved the system runtime, then the corrected environment passed. Official OpenSpec strict validation of the active change passed.

## Completion gate

Follow active openspec/changes/fau-8-live-entra-integration/tasks.md. Keep relevant Linear issues open and this change active until real registrations, local environment configuration and applicable live checks are verified. Do not commit credentials, private keys, sign-in codes, financial verification details or bearer tokens. Entra Free is not a premium trial; tenant availability remains subject to Microsoft's service and inactivity lifecycle policies.
