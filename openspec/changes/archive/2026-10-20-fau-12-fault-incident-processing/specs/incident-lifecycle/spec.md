## ADDED Requirements

### Requirement: Source bound synthetic intake
Intake SHALL require faults.write and knowledge.read, use authenticated tenant/client as source, normalize synthetic codes and validate event ID, UTC occurrence, model/software and active applicable fleet association. Unknown or unpublished codes SHALL be accepted; unavailable authoritative knowledge SHALL return503 without claiming unknown.
#### Scenario: Valid and invalid events
- **WHEN** known and unknown synthetic events arrive with active same-tenant context
- **THEN** accept both with server IDs and immutable approved-only provenance; reject invalid source/data/timestamps/association400, absent resources404 and missing permissions401/403
#### Scenario: Source spoofing
- **WHEN** input attempts to provide tenant, source client, owner or approval data
- **THEN** reject400; validated identity supplies source and audit fields

### Requirement: Incident replay and atomic pending work
Intake SHALL atomically commit incident, immutable context, history, audit, source replay and one pending investigation work item before any dispatch. Tenant/client/source-event uniqueness SHALL survive actor or retry-key changes. Exact retries SHALL return original ID/version before dependency revalidation; payload changes SHALL conflict409.
#### Scenario: Duplicate and concurrent events
- **WHEN** the same canonical event arrives twice or concurrently with different retry keys
- **THEN** return one stable incident and one work item with no duplicate audit/investigation; changed payload conflicts409
#### Scenario: Persistence and dependency failure
- **WHEN** storage/context lookup fails or cancellation occurs before commit
- **THEN** commit nothing; replay an already committed result without rereading fleet/knowledge; production without approved persistence returns503

### Requirement: Explicit incident transitions
Incident status SHALL follow Received to Investigating, Investigating to Escalated or Resolved, Escalated to Investigating or Resolved, Resolved to NotificationPending, and NotificationPending to Notified. Mutations SHALL require faults.write, strong versions and replay keys, append actor/server-time history, and enqueue unique work only for processing/notification stages.
#### Scenario: Transition matrix
- **WHEN** a caller requests any transition including repeated or terminal transitions
- **THEN** accept only the declared edges; reject illegal or stale transitions409 without state/history/work changes; identical replay returns its original version

### Requirement: Visible processing retry
Processing failures SHALL retain incident state, safe failure code, attempt count, pending work identity and history. Explicit retry SHALL requeue the same work identity without losing the committed incident. A successful stage transition SHALL complete preceding pending work atomically. External dispatch and actual notification delivery SHALL remain future integrations.
#### Scenario: Dispatch failure and retry
- **WHEN** pending work fails with an allowed safe code then retry is requested
- **THEN** record visible retryable failure, retain work/incident, clear current failure on retry, and preserve failure/retry history; retry without failure conflicts409

### Requirement: Historical incident access
Reads SHALL require faults.read, knowledge.read AND vehicle own/all read, filter tenant/immutable intake ownership before count/page, and retain provenance after fleet/knowledge changes. Other-owner or cross-tenant IDs SHALL return404. DTOs SHALL omit tenant/owner mappings; history SHALL be bounded and ordered.
#### Scenario: Reassignment and scoped lists
- **WHEN** fleet ownership or published knowledge changes after intake
- **THEN** original owner retains incident access, new owner gains none through reassignment, approved versions remain immutable, and pagination counts only accessible incidents
