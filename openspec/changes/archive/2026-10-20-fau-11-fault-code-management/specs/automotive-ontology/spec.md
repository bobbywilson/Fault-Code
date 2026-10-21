## ADDED Requirements
### Requirement: Fault taxonomy validation boundary
Fault writes and publication SHALL validate exact approved classification identity/version/system and remedy kinds through an ontology contract. Missing, unapproved or incompatible links SHALL reject the mutation; unavailable validation SHALL fail503.
#### Scenario: Invalid taxonomy or remedy combination
- **GIVEN** a synthetic fault draft
- **WHEN** a classification is absent, unapproved, belongs to another system, or forbids the remedy kind
- **THEN** the mutation returns400 without changing revisions, audit or replay; unavailable ontology returns503
