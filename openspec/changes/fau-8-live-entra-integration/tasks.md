# Tasks

## 1. Provider and specification evidence

- [x] 1.1 Inspect existing API, Angular and authorization implementation and identify test doubles from source.
- [x] 1.2 Confirm separate tenant, Free licensing and administrator account in Microsoft portal; record the failed Azure offer separately.
- [x] 1.3 Set directory display name Fault Code and verify Microsoft's success notification.
- [x] 1.4 Validate scoped specification artifacts with official OpenSpec strict validation (passed 2026-10-21).

## 2. Real registrations and configuration

- [ ] 2.1 Register API with v2 access tokens, exact delegated scopes and human/application roles; verify portal configuration against docs/authorization.md.
- [ ] 2.2 Register Angular SPA with actual local callback/logout URLs and delegated API permissions; verify public configuration and absence of SPA secrets.
- [ ] 2.3 Register machine client with only Authorization.Read, approved credential and consent; verify no unrelated API permissions.
- [ ] 2.4 Wire confirmed tenant/client IDs into existing API and Angular environment configuration; run focused configuration/authentication/authorization tests.

## 3. Live verification and reporting

- [ ] 3.1 Complete real Angular sign-in, token acquisition, permitted API call and logout; record sanitized results and blockers.
- [ ] 3.2 Complete real client-credentials token acquisition and allowed/forbidden API calls; record sanitized results and blockers.
- [ ] 3.3 Exercise unauthenticated, invalid/expired, issuer/audience and unauthorized-user denial; distinguish deterministic and live cases.
- [ ] 3.4 Audit production bundles and changed Git content for test auth and credentials; verify focused builds/tests.
- [ ] 3.5 Update existing FAU-8, FAU-9 and FAU-22 tickets with prior implementation, mocks, changes, tests, results and remaining work; keep incomplete issues open.
- [ ] 3.6 Review diff, validate all OpenSpec, commit and persist approved artifacts/configuration in existing GitHub repository; verify remote commit.

## Workflow follow-up

- Archive officially only when required implementation and applicable live checks are complete; otherwise keep this change active and report blockers.
- Provide the user implemented/verified/blocked/not implemented inventory with tenant/app IDs and evidence.
