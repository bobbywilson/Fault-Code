# Design

## Context
FAU-9 has exact policies; FAU-11/12/14 supply application services. FAU-15 deliberately forbids model tool calls. Production stores fail503 pending DEC-001/003. See proposal.md.

## Goals / Non-Goals
Implement authenticated, typed MCP discovery/invocation with existing domain policies. No new persistence provider, paid calls, model loop, full investigation/engineering case workflow, notifications or vehicle software actions.

## Decisions
1. Official ModelContextProtocol.AspNetCore2.3.0, stateless Streamable HTTP, SDK protocol2026-07-28 and compatible legacy handshake. SDK handles wire protocol; custom list/call handlers keep exact allowlist/schema/error control. No stdio, SSE sessions, resources, prompts, sampling, roots, elicitation or Tasks. All requests authenticate through existing Entra middleware; named mcp.access transport boundary accepts valid identity only, each discovered/invoked tool enforces existing policy conjunctions using IAuthorizationService. No authorization.read grant needed just to discover tools.
2. Five tools: fault_lookup(knowledge.read), knowledge_search(knowledge.read), incident_get(faults.read+vehicles.access.read+knowledge.read), incident_status(faults.write), incident_escalate(escalations.write). Typed JSON rejects wrong case, extra/missing/null/duplicate fields, invalid IDs/bounds. Lookup/retrieval preserve approved provenance and label source text untrusted. All service calls retain original tenant/actor/client and server correlation. Stateless per-request authorization avoids sticky identity/cross-client state.
3. Mutation arguments id/expectedVersion/idempotencyKey plus status for incident_status. X-FaultCode-Workflow-Intent must equal toolName/id/expectedVersion/status/idempotencyKey, with status fixed Escalated for incident_escalate. Header comes from authenticated consumer independently of tool arguments; no intent from _meta/arguments/model/retrieval. It is explicit consumer intent, not a cryptographic human-approval assertion. Callers allowed to construct headers remain responsible for their own authorization of automation. No tool/model can mint headers inside FAU-15 (tools stay disabled). Reuse IncidentService transaction/replay; server authorization separately selects exact mutation policy. Escalate records only existing lifecycle transition to Escalated, exposed as POST /api/v1/incidents/{id}/escalations with escalations.write; FAU-18 owns cases. Avoid duplicating mutation storage/approvals.
4. MCP result envelope includes status, correlationId, failureCode, data; isError for failures. Fixed safe codes400/403/404/409/500/503/504. Request abort propagates; timeout configurable1..120(default30); argument size bounded16KiB, HTTP body64KiB, max depth16. No provider exception text or logging of arguments/results; audit safe tool/outcome/actor/client/correlation plus domain mutation audit. Unknown tools fail closed. No automatic retries.
5. REST schemas/security remain structurally compatible. Protocol POST excluded from generated REST OpenAPI (SDK discovery supplies MCP schemas); specialized route tests enforce its named boundary. Additive escalation endpoint uses existing MutationResponse/preconditions/errors and shared service. No new Entra catalog values.

## Risks / Trade-offs
- Consumer-provided intent is not proof of human review -> document exact trust boundary, require operation binding and permission, keep model invocation disabled.
- Test memory cannot prove durability -> production stores remain unavailable, reuse existing durable-adapter requirements.
- SDK schemas are descriptive -> explicit server validation before services, actual SDK client/raw protocol negative tests.
- Engineering escalation status is not a case -> document FAU-18 dependency, no dispatcher or false completion.

## Migration Plan
Additive endpoint and SDK package locks; default identity-disabled host returns401. No deployment/config activation. Rollback source commit without changing storage or grants.

## Sources
Official SDK2.3.0 NuGet index and versioned source docs/concepts/transports/transports.md checked October21,2026: https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.3.0 . SDK/client compatibility verified locally, no remote Entra session claimed.
