# Proposal

## Why
Part of FAU-17. Incident intake, approved knowledge, rules, retrieval and bounded advisory execution exist independently. A traceable investigation must combine them without giving the model environment access or authority.

## What Changes
- Add original-caller authorized investigation creation and paginated history APIs with conditional version and scoped replay.
- Deterministically route KnownFault, KnownFaultWithApprovedPatch, UnknownFault and InsufficientEvidence using current approved definitions, pinned rules and canonical RAG citations.
- Persist immutable structured evidence, rule outcomes, bounded model assessment and proposed notification/referral/escalation intents atomically. Production storage remains unavailable pending DEC-001/003.
- Keep model tools disabled. Trusted application code reuses the read services behind MCP; MCP remains a separate authenticated consumer interface.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `fault-investigation`: grounded routing, evidence, history, replay and evaluation thresholds.
- `agent-runtime`: isolated bounded structured investigation assessment alongside existing advisory contract.
- `api-contracts`: additive investigation endpoints and exact policies.

## Impact
Application orchestration/storage contracts, Infrastructure unavailable adapter and existing Agent Framework adapter, API DTOs/controllers/OpenAPI, demo integration/SDK evaluations and documentation. No provider selection, account, outbound activation, durable adapter, notification delivery, engineering case or vehicle operation.
