# FAU-18 engineering escalation

## Why
FAU-17 records unresolved investigation proposals but cannot track engineering resolution. FAU-18 provides an authorized queue and approval transaction without selecting unresolved production storage or sending external notifications.

## What Changes
- Add tenant/incident-scoped unique engineering cases, assignment, notes, lifecycle/history and proposed resolutions.
- Approve versioned knowledge with pinned ontology/document evidence and affected incident links plus one notification-ready event in one transaction.
- Preserve REST/MCP and model isolation boundaries; production storage remains503.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `engineering-escalation`: concrete case lifecycle, approvals, atomic events and retry/security acceptance.
- `fault-knowledge`: engineering resolution publication coordinates knowledge and affected incident/event state.
- `api-contracts`: additive explicit engineering API contracts.

## Impact
Domain/Application/API/Infrastructure, demo tests, generated OpenAPI, Docker source allowlist and docs. Dependencies FAU-11/12/14/17. ADR-009/012; DEC-001/003 remain unresolved. No provider, database, deployment, model tools, account changes or external delivery.
