# Tasks

## 1. API identity
- [x] 1.1 Add maintained Microsoft bearer dependency, strict configuration and fail-closed composition; verify locked restore and configuration/anonymous-boundary tests; document settings.
- [x] 1.2 Verify real signed-token/discovery integration tests for valid delegated/app identities, invalid claims/signature/lifetime, metadata failures and generic errors; preserve permission claims for FAU-9.

## 2. Registration preparation
- [x] 2.1 Deliver isolated registration/browser PKCE/machine configuration and live smoke runbook; map unrun browser/session/live acceptance explicitly to pending provisioning and FAU-22.

## 3. Integrated verification
- [x] 3.1 Run full tests, Release build, format, Docker input and compatible contract checks; review code against scenarios and record exact evidence.
- [x] 3.2 Validate change strictly and document remaining external acceptance without claiming tenant/browser delivery.

## Workflow follow-up
- Officially archive, validate all main specs, commit/push, create and attach PR with Part of FAU-8, verify exact-commit CI, review and merge under user authorization.
- Leave Linear open for personal review; carry external tenant/browser limitations and sequential backlog instruction into the FAU-9 chat.
