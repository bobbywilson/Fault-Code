# Permission-aware MCP tools

Part of FAU-16. The API hosts the official C# `ModelContextProtocol.AspNetCore` 2.3.0 SDK at `POST /api/v1/mcp`, using stateless Streamable HTTP. The SDK supports protocol 2026-07-28 and compatible legacy initialization; tests also exercise a 2025-11-25 request. NuGet locks retain the exact dependencies. MCP raises the API/test process's Microsoft.Extensions.AI.Abstractions dependency to 10.11.0; the existing actual Microsoft Agent Framework/OpenAI mocked-HTTP tests verify compatibility. Domain/Application remain vendor-free.

## Identity and model isolation

Every HTTP request goes through the existing Entra v2 JWT middleware. An unauthenticated or invalid-token request returns correlated HTTP 401. The `mcp.access` policy is an internal authenticated transport boundary, not a new Entra scope or application role. `tools/list` advertises only tools permitted by the original request's policies. `tools/call` checks those same policies again, even when a client directly names a tool absent from discovery. A valid identity with no applicable grants sees an empty list. No `authorization.read` grant is needed for discovery.

The application takes tenant/actor/client exclusively from validated `tid`/`oid`/`azp`, and generates a fresh correlation ID. It never accepts those values, roles or scopes in tool arguments. Machine consumers use their explicitly assigned application roles; delegated consumers require the existing exact scope plus allowed human role. Resource reads retain the immutable incident intake owner and tenant checks. No service privileges are combined with the requesting consumer's permissions.

This interface is separate from the model. FAU-15 still supplies only selected synthetic incident metadata and approved intake references to a fresh bounded model conversation, has no registered tools and rejects returned tool calls. The model receives no Entra token, provider key, environment inventory, shell, filesystem or general network capability. This ticket does not connect MCP tools to automatic model execution. FAU-17 must explicitly preserve that boundary when composing investigation context.

## Discovery and contracts

| Tool | Required policies | Arguments | Success data |
|---|---|---|---|
| `fault_lookup` | `knowledge.read` | `code`, `system`, `model`, `softwareVersion` | `evidencePolicy`, `fault`: approved applicable fault DTO, immutable publication/classification/reference versions and approval |
| `knowledge_search` | `knowledge.read` | `text`, `system`, `model`, `softwareVersion`, optional `limit` (default 5, 1–20) | Existing evidence DTO: status, evidence policy, canonical citation identities, source/publication/page/offset/checksums/text/score/approval |
| `incident_get` | `faults.read` AND `vehicles.access.read` AND `knowledge.read` | `id` | `evidencePolicy`, `incident`: existing incident DTO with intake snapshot and version; no owner mapping or driver contact data |
| `incident_status` | `faults.write` | `id`, `expectedVersion`, `idempotencyKey`, `status` | Existing `{id, version}` mutation result |
| `incident_escalate` | `escalations.write` | `id`, `expectedVersion`, `idempotencyKey` | Existing `{id, version}` mutation result for lifecycle escalation |

The exact human/app-role mappings remain in authorization.md. For example, fault lookup/search permit an assigned `Knowledge.Read.All` machine role. Status requires `Faults.Write.All`; escalation requires `Escalations.Write`. Human Operator and Engineer roles do not imply each other's permissions.

Each listed tool advertises `inputSchema`, `outputSchema` and read-only/destructive/idempotent/closed-world annotations. Annotations are descriptive hints; server validation and authorization enforce behavior. All inputs are JSON objects with exact case-sensitive property names, required fields and `additionalProperties: false`. Unknown/missing/null/wrong-type fields, noncanonical/empty GUIDs, invalid enums/versions/keys or bounds reject before service execution. Synthetic code/system/model/software validation also uses the existing domain rules. There is no arbitrary SQL, file, URL, command, publication, identity-management or vehicle operation tool.

Approved fault prose, incident prose and retrieved document text are marked untrusted evidence. Source instructions do not authorize another operation, establish an unknown fault, approve a remedy or become executable repair guidance. Missing approved fault returns404; dependency outage503 remains distinct. Retrieval retains canonical approval/applicability/access filters before ranking and authoritative citation revalidation.

## Explicit consumer mutation intent

The authenticated HTTP consumer must independently attach exactly one `X-FaultCode-Workflow-Intent` header to each mutation request. Its exact value binds all mutation arguments:

```text
toolName/id/expectedVersion/targetStatus/idempotencyKey
```

For example, arguments `{"id":"55555555-5555-4555-8555-555555555555","expectedVersion":1,"idempotencyKey":"synthetic-status","status":"Investigating"}` require:

```text
X-FaultCode-Workflow-Intent: incident_status/55555555-5555-4555-8555-555555555555/1/Investigating/synthetic-status
```

For `incident_escalate`, the target status is always `Escalated`. Missing, multiple or mismatched intent yields tool403 before mutation, even for an Engineer or privileged machine. The intent is never read from arguments, protocol `_meta`, source text or model output. The header is explicit consumer intent, not a cryptographic human approval or an additional permission grant. An authorized application capable of constructing headers is responsible for deciding whether its requested automation is authorized. Do not give a model a header-construction or HTTP capability and then treat its text as independent intent.

Both mutation tools reuse the incident service and its atomic state/history/replay/audit transaction. Expected version and key obey existing positive version and 1–100 ASCII alphanumeric/hyphen/underscore rules. Exact concurrent retries return the original result without another history/audit entry. Changed payload or stale version409, illegal lifecycle edge409, failed commit/cancellation roll back by the store contract. Replay is scoped to original tenant/actor/client, incident and operation. Status and dedicated escalation have separate operation namespaces.

`incident_escalate` and additive REST `POST /api/v1/incidents/{id}/escalations` share `escalations.write`, service operation and replay namespace. The REST call requires strong `If-Match` and `Idempotency-Key`, returns MutationResponse/ETag, and uses the existing safe HTTP errors. REST requests are themselves explicit operation intent and do not need the MCP-specific header. This records only the existing legal `Investigating -> Escalated` lifecycle marker. It does not create an engineering case, dispatch work, adjudicate evidence or complete FAU-18. Existing status reporting remains governed by its existing faults.write policy.

## Bounds, failures and audit

The endpoint accepts authenticated non-browser consumers; no browser origins/CORS are enabled. Requests with an Origin header return403. Stateless mode maps no session GET/DELETE, legacy SSE, sampling, roots, elicitation, prompts/resources or Tasks. Existing AllowedHosts remains constrained. No network isolation or outgoing access is changed.

The HTTP JSON body is limited to64KiB (including bodies without Content-Length), depth16, one object and unique property names at every level. Invalid/duplicate/oversized envelopes return correlated HTTP400 Problem Details before the SDK can collapse duplicate keys. Argument JSON is limited to16KiB. Tool execution deadline is `Mcp__TimeoutSeconds`, 1–120, default30; invalid configuration fails startup. Calls have no automatic retries. Cancellation reaches downstream services; the handler propagates cancellation and the SDK HTTP transport can surface aborted execution as499 rather than an OperationCanceledException. Test clients verify downstream cancellation independently of that transport representation.

A completed tool invocation returns structured content, duplicated as JSON text for compatible clients:

```json
{"status":503,"correlationId":"server-generated","failureCode":"tool-dependency-unavailable","data":null}
```

`isError` is true for failures. Successful outcomes have status200, null failureCode and typed data. Codes: `tool-invalid-input`400, `tool-forbidden`403, `tool-intent-required`403, `tool-not-found`404, `tool-conflict`409, `tool-dependency-unavailable`503, `tool-timeout`504 and `tool-failed`500. Protocol/transport errors retain SDK wire semantics; HTTP authentication/body/origin failures use correlated Problem Details. No exception/provider bodies, secrets, arguments or result prose enter errors. X-Correlation-ID matches the original HTTP request context.

Known invocation logs record safe tool name, outcome, validated actor/client and server correlation; existing policy logs record authorization decisions. Protocol SDK content logging is disabled. Domain transactions retain actor/client/correlation/UTC/version/history/audit and scoped replay for committed mutations. Operational logs do not constitute durable business audit.

## Verification and activation limits

`McpToolsTests` uses the actual SDK client and actual production JWT middleware with synthetic signing/discovery keys. It verifies discovery/schemas, invocation, delegated/machine/owner/tenant checks, independent intent, metadata/source injection, concurrent replay/conflicts, rollback, safe failures, request bounds and cancellation/deadline. The suite exports all five actual SDK schemas and success/error examples to ignored `artifacts/mcp-contracts.json`; the Python validator checks those contracts:

```sh
dotnet test tests/FaultCode.IntegrationTests -c Release --filter FullyQualifiedName~McpToolsTests
python scripts/check-mcp-contract.py artifacts/mcp-contracts.json
```

The protocol endpoint is excluded from REST OpenAPI; SDK discovery is its schema contract. The REST lifecycle escalation endpoint is generated and compatibility checked. CI retains both generated REST and MCP artifacts and validates them on all three operating systems.

Production identity remains disabled/unprovisioned, and canonical production stores still return503 pending DEC-001/003. One-process test storage proves neither durability nor restart/crash/multi-connection behavior. There is no production memory fallback, live Entra verification, paid model call, new provider/credential, deployment, notification or vehicle action. Normal Compose retains its internal network.

Sources checked October21,2026: [official SDK2.3.0](https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.3.0), [versioned transport documentation](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.3.0/docs/concepts/transports/transports.md), [NuGet SDK versions](https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.aspnetcore/index.json).
