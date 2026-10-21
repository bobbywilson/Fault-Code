# Proposal

## Why
FAU-15 needs a bounded C# runtime before permission-aware MCP (FAU-16) and investigation workflows (FAU-17). Provider output must not become a domain mutation or approved engineering guidance.

## What Changes
- Add a protected incident-scoped advisory runtime API and provider-independent application interface.
- Integrate pinned Microsoft Agent Framework/OpenAI with strict structured output, timeout, cancellation, aggregate token/request/tool limits, explicit safe failures and deterministic mocks.
- Document disabled-by-default configuration, compatibility, opt-in live smoke and unresolved model/storage activation.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `agent-runtime`: enforce bounded identity-preserving advisory execution and grounded typed results.
- `api-contracts`: expose an additive protected runtime operation.

## Impact
Application runtime contracts/service; Infrastructure vendor adapter; API/controller/config/contracts; tests/locks/Docker input allowlist/docs. Reuse FAU-8/9 identity and FAU-12 trusted incident reads. No new Entra permission, database, credentials, paid calls, deployment or vehicle action. Part of FAU-15.
