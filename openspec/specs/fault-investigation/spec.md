# fault-investigation Specification

## Purpose
Define observable Fault Code fault investigation requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Workflow A known fault
For a known fault the system SHALL identify the stored approved definition, retrieve technical support, investigate with agent/domain rules, generate driver guidance and record results and notification status.

#### Scenario: Workflow A known fault acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an incident matches an approved synthetic definition without an applicable patch
- **THEN** an evidence-backed known-fault result and notification request are persisted with citations, rule/version references and eventual delivery status

### Requirement: Workflow B approved patch
For a known fault with an approved applicable patch the system SHALL assess dealership service and record the recommendation, supporting evidence and driver notification; it SHALL NOT deploy software to vehicles.

#### Scenario: Workflow B approved patch acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** approved patch applicability matches the incident vehicle context
- **THEN** the driver receives the justified dealership recommendation; no tool, API or workflow performs vehicle software installation

### Requirement: Unknown versus unavailable knowledge
Investigation SHALL distinguish absence of an approved definition from provider/retrieval outage and route unsupported or conflicting evidence explicitly.

#### Scenario: Unknown versus unavailable knowledge acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** no approved definition exists, or the lookup dependency is unavailable
- **THEN** an actually unknown code escalates to engineering; outage is a retryable failure and never falsely confirms that no definition exists
