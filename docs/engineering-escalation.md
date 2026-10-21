# Engineering escalation

Part of FAU-18. Engineering cases, queue/assignment/notes/history, separate resolution proposal/approval and retained notification-ready events are implemented. Production `UnavailableEscalationStore` returns503 pending DEC-001/003. Test memory is one-process only, not durable storage or production acceptance.

## API and authority

All routes start `/api/v1/engineering-escalations`. Every operation requires faults.read AND vehicles.access.read AND knowledge.read plus the exact escalation policy below. Delegated exact scopes AND allowed roles, or exact machine app roles, follow authorization.md. No Administrator bypass, implicit permission union, model tools or new Entra grant. Stored incident tenant and historical owner govern access before list counts/pagination and replay. Only explicit authenticated engineering requests create/mutate cases; historical investigation proposals cannot authorize execution.

| Operation | Additional policy | Inputs |
|---|---|---|
| GET collection, /{id}, /{id}/history, /{id}/events | escalations.read | page/pageSize1–100, collection status filter |
| POST collection | escalations.write | incidentId, investigationId; If-Match of incident version |
| PUT /{id}/assignment | escalations.write | assigneeId canonical object ID or null |
| POST /{id}/notes | escalations.write | text1–2000, no controls |
| PUT /{id}/status | escalations.write | target status |
| POST /{id}/resolutions | escalations.write | full existing FaultContent request plus expectedFaultVersion (0 absent) |
| POST /{id}/approvals | escalations.write AND knowledge.publish | empty object |

Every mutation requires strong quoted positive If-Match and Idempotency-Key1–100 ASCII alnum/hyphen/underscore. Non-create mutations match case version. Replay binds tenant/actor/client/operation/resource/key and exact serialized payload including version; array order is significant. Resource access is checked before replay; committed replay precedes evidence/version revalidation. Exact retries return original MutationResponse/ETag, creation201/Location; payload changes/stale versions409. Separate creation keys still return the one retained case per incident after incident precondition validation. Case IDs are not reused after resolution; reopen instead. Unknown input fields reject400. Correlated safe400/401/403/404/409/503 remain distinct. DTOs omit tenant/owner and original investigation citation text; evidence is version metadata, not authority.

## Lifecycle

Creation accepts persisted UnknownFault or InsufficientEvidence investigations on an Investigating/Escalated incident. UnknownFault also requires current authoritative knowledge absence; a newly approved definition conflicts409. Intake null, missing result or a provider outage cannot fabricate unknown. FAU-17 results remain unchanged, including their original Pending proposals. Existing `/incidents/{id}/escalations` and MCP lifecycle marker are unchanged.

Open->Investigating; Reopened->Investigating; AwaitingApproval->Investigating withdraws the proposal. Investigating->AwaitingApproval occurs only through a resolution proposal. AwaitingApproval->Resolved occurs only through approval. Resolved->Reopened returns the linked Resolved incident to Investigating and retains previous publications/approvals/events/history. Later notification states conflict on reopen until FAU-19 defines coordinated cancellation. Notes are immutable; assignment is a work label, not a directory membership/permission grant. No identity-directory provisioning/existence validation occurs.

Proposal stores bounded authored content, expected fault/incident versions, actor/time and pinned accessible current canonical document metadata. It changes no published knowledge or event. Approval repeats current version, exact approved ontology/classification/remedy-kind, current source version/checksum/publication, publisher access and exact model/software approved-remedy checks. Changed source publication requires a fresh proposal. Engineering approval adjudicates authored findings; document matching is canonical provenance, not automatic semantic proof or a loosened investigation-v1 policy.

Approval creates/updates the normalized incident fault definition, appends immutable publication/revision and approver/time, resolves the linked incident, preserves its original intake snapshot, records an immutable approval linking incident/driver and exact ontology/document/knowledge versions, and retains one stable Pending event. Each case resolves its own incident; other incidents require their own cases and authorization. Draft or concurrent/failed approval never yields a partial resolved state. Reopening requires a new proposal and explicit approval to create another publication/event.

## Persistence and downstream boundary

IEscalationStore supplies one coordinated authoritative transaction across engineering, knowledge, incidents, investigation, ontology and canonical document states. Existing adapters MUST join the same cross-process unit of work, including concurrent authoritative edits; separately copying/committing stores cannot satisfy this. Case/history/publication/incident/event/replay/audit commit or roll back together on any error/cancellation. Retained immutable event IDs support repeat reads/retry, with no delivery/acknowledgement claim.

FAU-19 must revalidate current case/incident approval, publication/applicability and consent before consuming events, reject superseded events after reopen, and deduplicate by event ID. No notification worker/provider/outbound delivery, vehicle action or model call is introduced. Qdrant remains a derived index; published source documents follow their existing independent index readiness. Canonical source revisions remain immutable.

DEC-001/003 must supply approved engine/provider/schema/migrations/durable outbox/retention/backups and restart/crash/multiple-process/cancellation acceptance before activation. No provider or hosting choice, accounts, deployment, credentials or network isolation changes are made here.

## FAU-19 consumer

[Notifications](driver-notifications.md) now consume stable event IDs only through explicit authorized REST enqueue. Current approval/evidence/recipient/preferences are revalidated; events remain historical Pending. Cancel/supersede with no active lease coordinates notification-stage incident back to Resolved before existing reopen. Old events are permanently deduplicated; new approval/event required after reopen. No external delivery activation.
