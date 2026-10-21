# Design

## Context

See proposal.md. Existing JwtBearer token validation, operation policies and Angular MSAL PKCE implementation remain in place. Their deterministic tests use isolated test doubles and do not establish live provider acceptance.

Microsoft now confirms tenant c0ff26e9-dd24-4e59-b395-1e9b48354aad, domain emailbobbywilsongmail.onmicrosoft.com, Microsoft Entra ID Free, and a Gmail-linked Global Administrator created October 21, 2026 at 3:05 PM local time. The directory name was changed from Default Directory to Fault Code with a Microsoft success notification. Azure free-offer rejection did not establish absence of the directory. Earlier claims that no tenant existed were incorrect after this partial provisioning. No Azure subscription or premium trial activation is confirmed.

## Goals / Non-Goals

Use this dedicated directory for real user and machine authentication while preserving the existing permission matrix. No deployment, paid services, premium trial, LALAI changes or university-directory use. The user's temporary consideration of LALAI was withdrawn; the final instruction restores complete tenant separation.

## Decisions

- Keep the provider-assigned primary domain: its spelling does not affect display name or isolation. Adding a custom domain is unnecessary infrastructure scope.
- Create three single-tenant registrations. API uses v2 access tokens, explicit delegated scopes, user-only Reader/Operator/Engineer/Administrator roles and Application-only granular roles from docs/authorization.md. Machine smoke initially receives only Authorization.Read. No Microsoft Graph data permissions are needed by Fault Code.
- Use existing same-origin Angular callback /auth/callback and post-logout root. Determine the actual local origin before adding redirect URIs. Do not invent a production hostname or deploy.
- API configuration uses the API client GUID as audience and the fixed tenant issuer. Angular receives only public tenant, SPA and API IDs and scope names. Credentials stay in an approved secret store; certificate is preferred for the machine client. Interactive credential creation/consent follows Microsoft and browser approval requirements.
- Keep security defaults. Do not enable premium group assignment or change tenant-wide policies to pass tests. Assign app roles directly to approved users.
- Record response status and sanitized claims in evidence, never full tokens. An operation returning storage-unavailable after authorization is not functional success; distinguish authentication acceptance from storage behavior.

## Risks / Trade-offs

- Provider eligibility errors can coexist with successful directory provisioning: inspect actual directory state separately.
- Entra Free has no premium-trial expiration, but continued tenant availability remains subject to Microsoft lifecycle policies. Do not promise perpetual service independent of those policies.
- Local interactive browser sign-in can require the user's email code or MFA. Ask in chat because the user is away from the computer.
- Existing tests cover negative token construction deterministically; real expired/wrong-audience test execution must be recorded separately or left blocked.

## Migration Plan

Validate these artifacts, configure registrations, wire verified public identifiers into local environment, and run focused tests plus real login/machine checks. No deployment. Roll back local configuration by disabling Entra while keeping protected routes fail closed; preserve external registration evidence rather than deleting resources without approval.
