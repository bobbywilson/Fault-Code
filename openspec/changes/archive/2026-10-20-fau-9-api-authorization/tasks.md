# Tasks
## 1. Permissions
- [x] 1.1 Implement exact human/machine catalog and handler; signed-token matrix tests verify scope/role/kind failures and elevated boundaries; document exact registration values.
- [x] 1.2 Implement trusted tenant/owner checks; demo resource tests verify owner, other driver, staff, machine and malformed resource denial; document future adapter obligations.
## 2. API boundary
- [x] 2.1 Add permission endpoint and catalog-derived OpenAPI; API tests verify 200/401/403, accurate permission list, no secret data, public compatibility.
- [x] 2.2 Enforce fail-closed defaults and safe decision logs; inventory/negative route and log-context tests verify coverage and redaction.
## 3. Integration verification
- [x] 3.1 Run locked restore, Release build/full tests, format, build-input and OpenAPI compatibility checks; record exact evidence and live limitations.
- [x] 3.2 Review diff and scenario traceability; validate change strictly with no unresolved implementation tasks.
## Workflow follow-up
- Officially archive/synchronize and validate all specs; commit/push and attach passing PR.
- Verify exact-source CI, review and merge; keep Linear open and launch FAU-10 with full handoff.
