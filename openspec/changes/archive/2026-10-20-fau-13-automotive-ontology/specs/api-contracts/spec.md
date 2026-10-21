## ADDED Requirements
### Requirement: Ontology API contracts
The versioned API SHALL expose draft creation/replacement/retrieval, history, publication, exact approved retrieval and stored evaluation operations. Management SHALL require knowledge.publish; approved reads and evaluation SHALL require knowledge.read. Contracts SHALL declare preconditions, replay keys, operation examples and safe correlated errors; tenant and approval fields SHALL be server-derived.
#### Scenario: Additive protected ontology API
- **WHEN** generated contracts and production routes are inspected
- **THEN** every ontology operation declares exact policies and examples, existing contracts remain compatible, malformed inputs reject400, unknown404, stale/replay conflict409 and unconfigured storage503
