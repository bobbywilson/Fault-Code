## ADDED Requirements

### Requirement: Engineering resolution publication
Engineering resolution approval SHALL publish a validated immutable fault revision with approver/time and exact approved ontology/document references in the same transaction as the case, affected incident link and notification-ready event. Expected fault version0 SHALL mean absent code; retired or concurrently edited definitions SHALL conflict409.

#### Scenario: Conditional approved knowledge update
- **WHEN** authorized engineering approves a proposed new or existing synthetic definition
- **THEN** exact applicable approved remedies are published without altering previous publications; stale draft versions, inaccessible/unapproved document evidence or incompatible ontology fail with no partial resolution
