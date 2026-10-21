# Tasks

## 1. Case workflow
- [x] 1.1 Implement case records, coordinated store contract and unavailable adapter; test unknown/ambiguous creation, dedup/replay/conflict/outage and document storage limits.
- [x] 1.2 Implement queue/assignment/notes/lifecycle/history; test permissions, tenant/owner filtering, concurrency and reopen history; document transitions.

## 2. Approval
- [x] 2.1 Implement separate proposals and atomic knowledge/incident/event approval; test exact evidence/applicability, permission separation, publication/commit failure, cancellation and stable event retry; document FAU-19 consumer boundary.

## 3. API and integration
- [x] 3.1 Add explicit API DTOs/controllers/examples and source allowlist; verify real JWT policies, safe errors, route inventory and generated complete REST/MCP compatibility.
- [x] 3.2 Run locked restore/build/tests/format/contracts/build-input/Compose checks, actual diff self-review and scoped strict validation; record exact evidence and limitations.

## Workflow follow-up
- Official archive and main strict validation, commit/push/PR attachment, exact-source CI and actual self-review COMMENTED; inspect signed-in author approval controls and merge passing PR without invented approval.
- Keep Linear open with native PR link, then create FAU-19 chat carrying workflow.
