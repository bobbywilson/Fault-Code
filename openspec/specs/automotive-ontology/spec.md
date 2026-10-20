# automotive-ontology Specification

## Purpose
Define observable Fault Code automotive ontology requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Typed domain concepts
The ontology SHALL define versioned EPS, steer-by-wire and steering ADAS concepts, classifications, relationships and deterministic domain rules, without GraphRAG.

#### Scenario: Typed domain concepts acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an authorized engineer creates relationships and rules
- **THEN** valid types and references are accepted; duplicates, invalid references and prohibited cycles are rejected

### Requirement: Traceable rule evaluation
Investigation rule evaluation SHALL use approved versions and retain rule/evidence provenance without rewriting historical outcomes after taxonomy changes.

#### Scenario: Traceable rule evaluation acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a published rule changes after an incident investigation
- **THEN** new investigations use the new approved version while the historical record retains its original rule version and explanation
