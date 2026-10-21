# engineering-escalation Specification

## Purpose
Define observable Fault Code engineering escalation requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Workflow C investigation tracking
Unknown or insufficient-evidence investigations SHALL support one authorized tenant-local engineering case per incident with Open, Investigating, AwaitingApproval, Resolved and Reopened lifecycle, assignment, immutable notes and versioned actor/client/correlation audit. Provider outage SHALL remain retryable and SHALL NOT create an unknown case.

#### Scenario: Workflow C investigation tracking acceptance
- **WHEN** an engineer conditionally creates a case from a persisted unresolved investigation and retries concurrently
- **THEN** one case and initial history commit; exact replay returns the original mutation; changed payload or stale version409; known routes and invalid incident state409; inaccessible resources404

#### Scenario: Queue notes and lifecycle
- **WHEN** authorized callers list cases, assign, add notes or transition lifecycle
- **THEN** filter tenant and original incident ownership before count/page; reads require escalations.read plus incident read policies; writes require escalations.write plus incident read policies; illegal transitions409; malformed input400; immutable history preserves each actor/version

### Requirement: Approved resolution and downstream notification
Only exact escalations.write AND knowledge.publish with incident read policies SHALL approve a proposed resolution. Approval SHALL atomically publish applicable approved knowledge, pin ontology/document versions, resolve linked incidents and create a stable notification-ready event. Drafts SHALL NOT publish or emit events. Actual delivery belongs to FAU-19.

#### Scenario: Approved resolution and downstream notification acceptance
- **WHEN** an engineer proposes bounded findings and applicable knowledge, then separately approves against current case/knowledge/incident versions
- **THEN** publish one immutable approval, preserve evidence and affected incident/driver links, resolve the case/incidents and retain one retryable event; replays never duplicate publication or event

#### Scenario: Failure and reopen
- **WHEN** publication/evidence validation or commit fails, cancellation occurs, or a resolved case is reopened
- **THEN** failure commits no partial knowledge/case/incident/event/replay/audit; reopen retains old approval/event/history, returns linked Resolved incidents to Investigating, and requires a new proposal and approval for another event

### Requirement: Coordinated durable engineering transactions
Case, knowledge, incident, evidence, replay, audit and notification-ready events SHALL share one authoritative cross-process transaction. Events SHALL remain available by stable ID for retry without implying delivery. Production storage SHALL fail503 until DEC-001/003 durable adapters pass restart/crash/concurrent-connection/retention acceptance; test memory SHALL NOT count as durability.

#### Scenario: Atomicity and retryable event
- **WHEN** approval is retried after a consumer failure or storage is unavailable
- **THEN** the same committed event remains queryable without duplicate publication; unavailable production storage returns503, and there is no production memory fallback or external dispatch
