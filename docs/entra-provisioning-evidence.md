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
| API client ID | Not registered yet |
| Angular client ID | Not registered yet |
| Machine client ID | Not registered yet |

The tenant GUID differs from LALAI's observed directory GUID. No LALAI resource was modified. No premium trial, paid plan or Azure subscription activation is confirmed. The portal still offers Azure signup; this does not negate the separately visible Entra directory.

## Partial provisioning correction

The Azure free-offer signup reported ineligibility. On a later authenticated portal inspection, a dedicated directory, one member administrator and Microsoft Entra ID Free licensing were present. The administrator creation timestamp and audit activity coincide with the earlier signup attempt. This supports partial directory provisioning during that attempt; the exact Azure-side failure reason remains undisclosed. Earlier statements that no tenant existed were too broad. No subsequent paid enrollment was needed to observe or name this directory.

The university route did not establish academic eligibility and is not the chosen identity source. The user's final instruction is a separate Free tenant using the Gmail-linked Microsoft account. Temporary consideration of LALAI and premium trials was withdrawn.

## Implementation and test boundaries

Existing API bearer validation and authorization policies use real Microsoft middleware. Angular uses MSAL PKCE. Current committed browser identifiers remain empty until registrations are confirmed. Automated integration tests construct local RSA tokens and in-memory discovery; Angular tests mock MSAL or replace authentication only in test builds. Those checks are not evidence of real Entra-issued token acquisition.

No real Angular login to Fault Code, API token acceptance, user-role acceptance, client-credentials token acquisition or live negative-token matrix has passed yet. Registration terms and scoped security grants require action-time approval in the browser workflow. The prepared API registration is single-tenant; it has not been submitted.

Focused verification on 2026-10-21: `dotnet test tests/FaultCode.IntegrationTests/FaultCode.IntegrationTests.csproj --filter 'FullyQualifiedName~EntraAuthenticationTests|FullyQualifiedName~EntraConfigurationTests|FullyQualifiedName~AuthorizationTests' --no-restore --verbosity minimal` passed 43 tests, failed 0, skipped 0. The bundled .NET 10 toolchain must be both DOTNET_ROOT and first on PATH; the initial run failed in OpenAPI generation because its child process resolved the system runtime, then the corrected environment passed. Official OpenSpec strict validation of the active change passed.

## Completion gate

Follow active openspec/changes/fau-8-live-entra-integration/tasks.md. Keep relevant Linear issues open and this change active until real registrations, local environment configuration and applicable live checks are verified. Do not commit credentials, private keys, sign-in codes, financial verification details or bearer tokens. Entra Free is not a premium trial; tenant availability remains subject to Microsoft's service and inactivity lifecycle policies.
