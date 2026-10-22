# Design

## Context
Part of FAU-17; see proposal.md. FAU-12 incident ownership is historical and immutable. FAU-11 approved lookup filters remedies by approval and exact model/software. FAU-13 publishes immutable ontology versions. FAU-14 revalidates canonical citation identities after ranking. FAU-15 supplies the isolated Agent Framework adapter; FAU-16 MCP is a separate authenticated interface.

## Goals / Non-Goals
Compose a deterministic investigation with bounded model assessment and queryable structured history. No automatic model tools, incident transitions, dispatch, vehicle operations, provider selection, network change or production memory storage. Notification intent is pending only; FAU-18/19 own delivery/case status. Existing advisory contract remains unchanged.

## Decisions
- Trusted orchestrator calls the existing incident/knowledge/ontology/retrieval read services used behind MCP. An HTTP self-call would require extra credentials and duplicate authorization; attaching MCP to model would violate the explicit user isolation boundary. Retain original caller without service grant union.
- New asynchronous IInvestigationStore transaction serializes tenant creation/replay and MUST coordinate authoritative incident, knowledge, ontology and canonical document reads through completion/commit across processes. No independent read then blind commit qualifies as durable acceptance. Production Unavailable adapter returns503; test store proves single-process only.
- POST requires faults.write plus existing three read policies, If-Match and retry key, body{}. No new grants. Atomic result includes original incident version, current definition/publication/classification/approval, complete rule input/outcome, citations, approved remedy, model metadata, one pending proposal and safe audit. It does not update incident status/work. Result ID is server-generated; retry scope includes original tenant/actor/client/incident/key and payload expectedVersion. History paginates per incident after trusted access and original actor/client filtering; broader incident access does not expose another publisher's restricted evidence.
- Use current approved lookup rather than intake absence to confirm unknown. Only its404 establishes UnknownFault. Missing ontology publication/rule or evidence produces InsufficientEvidence; actual dependency failures propagate503. Evaluate approved definition symptoms as taxonomy evidence, not as independently observed sensor symptoms; preserve input explicitly.
- Conservative support policy investigation-v1 requires score>=0.85, approved source/version reference, full chunk ordinal equality with approved applicable remedy description, matching rule classification and allowed remedy kind. Any retrieved chunk not mapped to approved remedy/reference is unadjudicated conflict and suppresses all driver guidance. This deliberately favors abstention; semantic conflict interpretation by model cannot authorize guidance. Exact-match support is sufficient, not a general semantic groundedness metric.
- Model receives demo incident code/system/model/software, candidate route, bounded canonical citation text/IDs and rule identity only; no caller, approver identity, contact/driver metadata or secret. Fixed structured output echoes incident/route/citation IDs plus reviewRequired. Model can downgrade known routes to review but cannot promote deterministic result or generate guidance. Approved guidance comes directly from stored publication only.
- Use a trusted AgentPurpose enum on in-process invocation to select fixed investigation instructions/schema; public input cannot select prompts. Same official adapter, fresh agent, strict native schema, zero tools/retries at SDK level. Application aggregate budget/deadline includes all reads, model and commit; explicit transient retries only within limits. Model input<=24000 UTF8 bytes; output<=16000 chars;5 citations; never truncate citations silently.
- Pending proposals: notification for A, dealership-referral for B, engineering-escalation for C, engineering-review for insufficient evidence. No contact information or dispatcher. History is immutable original evidence, not live approval revalidation for sending guidance.

## Risks / Trade-offs
- Exact-text support reduces recall -> explicit InsufficientEvidence and engineering-review proposal; fixture100 percent routing/provenance and zero unsupported guidance are acceptance thresholds only, not production measured accuracy.
- Network call succeeds before commit fails -> no result/action commits, retry may incur another paid call. Exactly-once external billing is not claimed. Transaction requires bounded execution and coordinated storage adapters after DEC approval.
- Shared source access can change -> coordinated canonical context required on future durable commit; current stores unavailable.

## Migration Plan
Additive routes/DTOs only; no package change or schema migration. Production stays503. Existing contracts compare against complete FAU-16 CI artifact. Archive main specs with official OpenSpec after local acceptance, then exact-source CI before merge.

All returned citations must support the same selected approved remedy text. Different individually approved remedy texts are also unadjudicated conflict and require review. API responses carry an explicit untrusted historical evidence policy.
