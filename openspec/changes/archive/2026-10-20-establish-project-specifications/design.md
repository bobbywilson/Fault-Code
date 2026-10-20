# Design

## Context
See proposal.md. Existing repository/folder were empty; OpenSpec is initialized with the official 1.14.3 CLI. Linear already has 22 project issues; preserve IDs and repurpose the superseded standalone release ticket as lifecycle ticket 3 while consolidating documentation/testing/deployment in ticket 22.

## Goals / Non-Goals
Goals: durable capability-oriented normative specs, explicit approved decisions, 22-ticket traceability and reproducible official validation/archive tooling.
Non-goals: no application source, tenant/cloud provisioning, database choice, paid service activation or automatic issue closure. Specs describe future behavior, not claims of delivered features.

## Decisions
Use repository-local spec-driven schema because requirements must travel with Git history. An external/temporary specification store was rejected for this project because it would require extra availability/configuration. Keep capabilities cohesive rather than one duplicated spec per ticket; observability tickets share one contract. Use ADR references for mandated technology choices and specs for observable behavior. Pin OpenSpec 1.14.3 in documented commands, rather than silently using changing latest tooling in future runs. Preserve Linear IDs to retain relationships; numbered titles express the revised order. No concrete relational provider is selected; defer only provider-dependent implementation pending DEC-001.

## Risks / Trade-offs
- Specifications are not application evidence: record all application work as unstarted.
- CLI/browser authentication can differ: do not use unrelated account credentials to publish; require correct authorization or browser publishing.
- GitHub integration requests code/workflow access: permission approval and integration-event verification remain separate checks.
- Free-tier documentation/observability limits may change: verify before activation; no purchases.

## Migration Plan
Add specifications and instructions to existing history, validate, synchronize through official archive, and commit without force pushes. Roll back through a normal reviewed revert if required; never delete existing history. No application/database migration occurs.
