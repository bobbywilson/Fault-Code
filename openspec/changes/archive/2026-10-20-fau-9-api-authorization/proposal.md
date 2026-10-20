# Proposal
## Why
Part of FAU-9. Authentication currently accepts arbitrary nonempty scopes/roles without operation permissions. Domain tickets need a reusable least-privilege boundary before adding private data.
## What Changes
- Exact scope plus human role policies, separate granular machine roles, ownership authorization and fail-closed route defaults.
- Read-only caller permission endpoint with generated security metadata; no placeholder business mutations.
- Matrix and negative signed-token/API coverage, safe denial audit context and registration documentation.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `authorization`: exact permissions, ownership and protected route coverage.
- `api-contracts`: protected caller permission contract and security metadata.
## Impact
API authorization/composition/OpenAPI, integration tests and docs. FAU-7/8 merged dependencies. No database, tenant, browser, MCP server or cloud provisioning. ADR-001/003/012 preserved.
