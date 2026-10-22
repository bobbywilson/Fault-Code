# FAU-12 design
## Context
FAU-10/11 supply immutable fleet/knowledge records and fail-closed production adapters. See proposal.md. DEC-001 is unresolved.
## Goals / Non-Goals
Implement deterministic, testable incident workflow and API; no relational selection, external worker/notification provider, agent investigation, live tenant or vehicle control.
## Decisions
- IIncidentStore owns an atomic transaction with incident state and authoritative same-tenant fleet/knowledge read context under one consistent transaction/lock. Separate preflight reads were rejected because association/publication changes could race intake. The approved adapter must integrate existing fleet/knowledge stores in the same durable unit of work; context unavailability throws503, never empty fabricated records.
- Intake source is validated azp; no client-controlled source identity. Source event token is 1-100 ASCII alnum/hyphen/underscore. Event time UTC between now-7days and now, and within active current association interval. Model/software are exact demo context, client reported (no fleet model inventory exists yet).
- Two atomic replay indexes: tenant/client/event independent of actor and request key, plus tenant/actor/client/operation/key. Hash covers canonical code, IDs, occurrence instant, model/software; replay precedes clock/context validation. Bind create retry keys to canonical payload; changed key cannot duplicate source. Store creation result indefinitely; future retention requires DEC-001.
- Preserve immutable vehicle/driver ID/identifier/version, association snapshot, private owner mapping, original server correlation and approved publication content/classification/reference/approval provenance. Do not store driver contact details. Known approved-only lookup snapshot filters remedies by exact model/software. Drafts/unpublished/retired count as no approved definition, never as guidance.
- Reads require faults.read AND vehicles.access.read AND knowledge.read; application filters using exact trusted all/own flags and original owner; detail also invokes vehicles.resource.read on stored mapping. Writes require faults.write; intake additionally knowledge.read because it exposes approved knowledge context. No new Entra catalog values or privilege union.
- Seven operations: POST/GET incidents, GET id, GET id/history, PUT id/status, POST id/failures, POST id/retries. Mutation keys mandatory; all except intake require strong If-Match. Failure codes limited to dispatch-unavailable, processing-unavailable, dependency-unavailable. No raw exception input.
- Six statuses follow spec graph; PendingWork has stable server ID, kind investigate/notify, Pending/Failed/Completed and attempt count. Created investigation intent awaits FAU-17 worker. Status advances complete current work; notification pending creates intent only. Notified is an authorized recorded acknowledgement, not proof of provider delivery. Failure/retry mutate work and append immutable history; no automatic busy-loop dispatch.
## Risks / Trade-offs
- Test adapter proves one-process semantics only; production remains503. Restart/crash/multi-process/isolation/retention/outbox delivery tests require DEC-001 and approved adapters.
- Operator may report workflow completion; factual evidence-backed results/escalation/delivery integrations remain FAU-17/18/19. No automated claims here.
- Historical owner snapshots are sensitive internal authorization context; API omits owner/tenant and all driver contact data.
## Migration Plan
Additive APIs only, no packages/schema migrations. Unavailable adapter registered in every production environment. Rollback removes new endpoints; no storage deployed.
