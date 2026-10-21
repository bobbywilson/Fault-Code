# Proposal

## Why
Part of FAU-16. Approved fault, retrieval and incident services exist but remote agents lack a permission-aware MCP interface. Expose controlled tools without granting agent privileges or activating model tool loops.

## What Changes
- Pin official maintained C# MCP SDK2.3.0 and stateless Streamable HTTP at /api/v1/mcp.
- Typed approved fault lookup, evidence search, incident read, status transition and lifecycle escalation tools, with exact original caller policies and schemas.
- Require per-request consumer workflow intent bound to mutation arguments; share atomic incident replay/history/audit and expose lifecycle escalation through REST.
- SDK client contract/security tests, safe bounded failures and documentation; no production storage activation.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `mcp-tools`: concrete discovery, typed invocation, transport, bounds and intent contracts.
- `authorization`: original HTTP identity for each MCP request and independent mutation intent.
- `api-contracts`: additive lifecycle escalation REST contract and MCP adapter boundary.

## Impact
API MCP composition/DTOs, existing Application incident transitions, NuGet locks, Docker source allowlist and tests. Dependencies FAU-9/11/12/14/15. ADR-003/004/009/010/012 preserved. DEC-001/003 production storage remains unavailable503; no new grants, external identity/provider provisioning, model calls or deployment. Full escalation case management remains FAU-18; FAU-16 records only the existing Escalated lifecycle marker.
