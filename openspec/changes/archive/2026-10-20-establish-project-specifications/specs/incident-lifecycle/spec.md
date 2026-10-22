# incident-lifecycle Specification Delta

## Purpose

Define observable Fault Code incident lifecycle requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Idempotent fault intake
The API SHALL validate incoming events, associate vehicles/drivers, persist an incident and its source event identity, and accept unknown codes as valid investigation inputs.

#### Scenario: Idempotent fault intake acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** the same valid event is submitted twice or concurrently
- **THEN** one incident is returned and downstream investigation/notifications are not duplicated; invalid association is rejected

### Requirement: Durable state and history
Incidents SHALL preserve state transitions and investigation history, distinguishing received, investigating, escalated, resolved and notification states; durable dispatch SHALL not lose committed work.

#### Scenario: Durable state and history acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** dispatch fails after intake or an illegal transition is requested
- **THEN** the committed incident remains retryable/visible and history is preserved; illegal transitions fail without state corruption
