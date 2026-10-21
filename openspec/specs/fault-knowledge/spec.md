# fault-knowledge Specification

## Purpose
Define observable Fault Code fault knowledge requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Versioned fault definitions
Authorized engineering clients SHALL manage synthetic steering/ADAS fault definitions, classifications, descriptions, technical references and approved remedies through APIs.

#### Scenario: Versioned fault definitions acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** engineering updates a published fault definition
- **THEN** a versioned approval/audit record is preserved and investigations can identify the version used

### Requirement: Approved remedy eligibility
Driver-facing results SHALL include only published, approved and applicable remedies/patch references; a patch reference SHALL never authorize vehicle software installation.

#### Scenario: Approved remedy eligibility acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a known code has draft, unapproved or inapplicable patch metadata
- **THEN** no approved-patch referral is generated from that metadata; approved applicable patches can recommend dealership service only

### Requirement: Tenant fault lifecycle
The API SHALL manage tenant-local normalized synthetic steering codes, bounded drafts and retirement with immutable code/system, reserved uniqueness and retained revision history. Exact faults.write authorizes mutation; faults.read authorizes management reads.
#### Scenario: CRUD and invalid definitions
- **GIVEN** an authorized tenant caller
- **WHEN** drafts are created, edited, searched or retired
- **THEN** current versions and history remain tenant-local; duplicate normalized codes return409; invalid content returns400; other-tenant IDs return404; missing identity/permission return401/403

### Requirement: Engineering publication snapshots
Publication SHALL require exact knowledge.publish, preserve immutable definition and approval actor/time snapshots, and never expose draft edits through approved lookup.
#### Scenario: Publication and edit isolation
- **GIVEN** a valid draft with verified ontology links
- **WHEN** an Engineer publishes then an Operator edits it
- **THEN** lookup retains the approved snapshot and provenance until another approved publication; Operator publication is denied

### Requirement: Applicable approved guidance
Knowledge lookup SHALL require exact knowledge.read and return only current published approved remedies matching the supplied system, synthetic model and software version. Patch metadata SHALL be advisory dealership referral only. Retired, unknown or unpublished codes SHALL return404; service unavailability SHALL return503.
#### Scenario: Patch filtering
- **GIVEN** a publication containing approved and unapproved remedies with distinct applicability
- **WHEN** a consumer looks up an applicable code
- **THEN** only approved matching remedies appear; draft/unapproved/inapplicable patches never become guidance and no installation operation exists

### Requirement: Fault atomicity and concurrency
Mutations SHALL atomically preserve revisions, replay and safe actor audit, require strong versions for updates/retirement/publication, and bind retryable create/publication keys to tenant, actor, client, operation and canonical payload.
#### Scenario: Retry conflict and failure
- **GIVEN** concurrent retries and an adapter failure
- **WHEN** identical creation/publication repeats or a stale edit runs
- **THEN** identical retries replay once, changed payload/stale version returns409, failure/cancellation commits nothing, and unconfigured production storage returns503 without a memory fallback

### Requirement: Engineering resolution publication
Engineering resolution approval SHALL publish a validated immutable fault revision with approver/time and exact approved ontology/document references in the same transaction as the case, affected incident link and notification-ready event. Expected fault version0 SHALL mean absent code; retired or concurrently edited definitions SHALL conflict409.

#### Scenario: Conditional approved knowledge update
- **WHEN** authorized engineering approves a proposed new or existing synthetic definition
- **THEN** exact applicable approved remedies are published without altering previous publications; stale draft versions, inaccessible/unapproved document evidence or incompatible ontology fail with no partial resolution
