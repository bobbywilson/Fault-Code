## ADDED Requirements
### Requirement: Tenant fault lifecycle
The API SHALL manage tenant-local normalized demo steering codes, bounded drafts and retirement with immutable code/system, reserved uniqueness and retained revision history. Exact faults.write authorizes mutation; faults.read authorizes management reads.
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
Knowledge lookup SHALL require exact knowledge.read and return only current published approved remedies matching the supplied system, demo model and software version. Patch metadata SHALL be advisory dealership referral only. Retired, unknown or unpublished codes SHALL return404; service unavailability SHALL return503.
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
