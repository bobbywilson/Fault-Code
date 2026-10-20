# engineering-escalation Specification Delta

## Purpose

Define observable Fault Code engineering escalation requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Workflow C investigation tracking
Unknown faults SHALL create an idempotent engineering escalation linked to the incident with lifecycle status, engineering notes and complete audit history.

#### Scenario: Workflow C investigation tracking acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a genuinely unknown fault is received and then retried
- **THEN** one escalation tracks the engineering investigation and all state transitions remain queryable

### Requirement: Approved resolution and downstream notification
Authorized engineering SHALL update and approve knowledge, associate findings and evidence versions with affected incidents, and only then trigger driver notification while preserving history.

#### Scenario: Approved resolution and downstream notification acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a draft resolution is proposed, then an authorized engineer approves/publishes it
- **THEN** drafts do not notify drivers; approved findings resolve/link incidents, refresh knowledge and emit a retryable notification event without duplicate delivery
