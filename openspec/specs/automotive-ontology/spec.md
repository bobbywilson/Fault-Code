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

### Requirement: Fault taxonomy validation boundary
Fault writes and publication SHALL validate exact approved classification identity/version/system and remedy kinds through an ontology contract. Missing, unapproved or incompatible links SHALL reject the mutation; unavailable validation SHALL fail503.
#### Scenario: Invalid taxonomy or remedy combination
- **GIVEN** a synthetic fault draft
- **WHEN** a classification is absent, unapproved, belongs to another system, or forbids the remedy kind
- **THEN** the mutation returns400 without changing revisions, audit or replay; unavailable ontology returns503
