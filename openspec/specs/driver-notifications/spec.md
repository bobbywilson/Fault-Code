# driver-notifications Specification

## Purpose
Define observable Fault Code driver notifications requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Notification triggers and referrals
Notifications SHALL support known-fault guidance, applicable approved-patch dealership referrals and approved engineering resolution, recording original recipient, type, incident, template and exact evidence/publication versions. Trusted code SHALL render only current approved applicable content, never model prose. Current canonical evidence SHALL be tenant accessible and unchanged; publisher-only sources SHALL NOT become driver messages.

#### Scenario: Notification triggers and referrals acceptance
- **WHEN** an authorized consumer explicitly enqueues a persisted current supported investigation for the original active associated demo driver
- **THEN** A renders the selected approved guidance; B includes the exact approved applicable patch reference and dealership recommendation, incident reference and no installation claim; no proposal dispatches automatically

#### Scenario: Approved C and supersession
- **WHEN** consuming a stable engineering event
- **THEN** require the current resolved case and latest approval/event, matching resolved incident/publication/driver, approved ontology and canonical evidence; reopened, superseded, draft, retired or changed evidence cannot enqueue or dispatch

### Requirement: Honest delivery state
Notification status SHALL distinguish Pending, Sent, Failed, Cancelled and Superseded; preserve immutable attempt/retry/acknowledgement history and stable source deduplication. Sent SHALL require a bounded provider acknowledgement. Failures and timeouts SHALL retain safe codes and exponential backoff. Delivery SHALL NOT claim exactly-once external effects.

#### Scenario: Honest delivery state acceptance
- **WHEN** a provider fails, times out, returns no acknowledgement or an attempt lease expires
- **THEN** no Sent is recorded without acknowledgement; attempts remain visible, retries respect bounded delay/attempt limits, concurrent dispatch has one current lease and all attempts reuse the notification ID as provider deduplication key

#### Scenario: Cancellation and reopen
- **WHEN** authorized consumers cancel an unsent notification with no active lease or supersede an acknowledged notification
- **THEN** retain content, attempts and acknowledgements, complete pending notification work and return the linked notification-stage incident to Resolved; engineering may then reopen, retained old source IDs cannot create another message

### Requirement: Explicit local test preferences and recipient authority
Local delivery SHALL require explicit enabled demo local preference with a bounded SYN contact label and matching current driver identity/owner, active driver/vehicle and original active association. Existing email flags SHALL NOT constitute consent. Real contacts/channel/consent policy SHALL remain DEC-002.

#### Scenario: Exact preference and notification permissions
- **WHEN** accessing preference or notification APIs
- **THEN** preference reads SHALL require drivers.access.read; preference writes SHALL require drivers.write and drivers.access.read. Notification reads SHALL require faults.read, vehicles.access.read, drivers.access.read and knowledge.read; writes additionally faults.write. Exact existing human scope/role or machine grants SHALL apply with no privilege union.

#### Scenario: Preferences and missing recipient
- **WHEN** preferences are disabled, contact absent, driver deactivated/reassigned, association closed or tenant/owner mismatches
- **THEN** no provider call occurs; invalid local contacts400, inaccessible resources404 and invalid recipient/preferences409; list/count/history filters precede pagination

#### Scenario: Permission and model isolation
- **WHEN** a caller omits a required grant or evidence/model content requests dispatch
- **THEN** deny401/403 or require explicit authorized REST mutation; no model/MCP tools, tokens, contacts, credentials or environment capabilities are added

### Requirement: Coordinated notification outbox and replay
Notification, incident status/work, preference, attempts, lease, stable source uniqueness, scoped replay and audit SHALL share authoritative transactions with fleet/engineering/knowledge/ontology/document/investigation changes.

#### Scenario: Conditional replay contract
- **WHEN** mutating notification state
- **THEN** every mutation SHALL require strong If-Match and scoped Idempotency-Key binding tenant/actor/client/operation/resource/payload/version. Access SHALL precede replay and committed replay SHALL precede dependency revalidation. Enqueue SHALL match incident version; dispatch/cancellation notification version; preference writes driver version. Production SHALL return503 until approved durable adapters exist.

#### Scenario: Atomic enqueue and retry
- **WHEN** simultaneous identical or distinct-key requests consume one source, or commit/cancellation fails
- **THEN** one logical message/outbox/incident transition commits; exact replay returns original result, payload mismatch/stale version409, failure rolls back all state; source uniqueness survives actor/client changes

#### Scenario: Dispatch crash boundary
- **WHEN** a claim commits before provider execution or acknowledgement precedes completion commit
- **THEN** retained lease/attempt supports recovery with stable provider key, no database transaction spans external I/O, uncertain acceptance is explicit and durable activation requires crash/restart/concurrent-connection/retention tests; test memory never counts as durable storage
