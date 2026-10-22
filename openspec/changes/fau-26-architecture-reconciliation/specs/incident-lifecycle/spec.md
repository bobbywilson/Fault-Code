## ADDED Requirements

### Requirement: Knowledge resolution and physical repair separation
Fault events SHALL retain one-vehicle occurrences and nullable fault-definition links for unknown codes. A/B/C processing SHALL remain deterministic for lookup, persistence and transitions. Knowledge resolution SHALL have distinct state/evidence from physical vehicle repair; publishing a definition SHALL NOT mark a vehicle repaired. Unknown occurrences SHALL support repeated-code correlation, automatic engineering intake and investigation-pending notification without a fabricated diagnosis.

#### Scenario: Unknown and repaired-state boundary
- **WHEN** missing knowledge is approved or an unknown fault repeats
- **THEN** relevant unresolved occurrences are retained/correlated and reevaluation/follow-up work is queued durably while physical repair remains unconfirmed without separate authorized evidence

### Requirement: Minimal vehicle dispatch and atomic notification intake
The dedicated fault-dispatch API SHALL accept vehicle identity and code only, with a stable idempotency key. It SHALL load the active driver association and vehicle metadata server-side and atomically persist an incident and linked notification awaiting processing. Matching retries SHALL return the same two identities. Caller-supplied recipients, diagnoses or delivery states SHALL be rejected.

#### Scenario: Dispatch a seeded vehicle fault
- **WHEN** an authorized consumer dispatches a code for an active associated vehicle
- **THEN** one incident and its notification are persisted atomically and the bounded agent/MCP workflow can process them without additional client fields

#### Scenario: Resolution follow-up
- **WHEN** approved resolution is recorded for an incident
- **THEN** a new linked follow-up notification is queued to its original driver, preserving initial notification and attempt history
