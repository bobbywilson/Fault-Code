# development-lifecycle Specification

## Purpose
Define observable Fault Code development lifecycle requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Specification readiness before implementation
Every authorized task SHALL review existing specs and decisions, identify affected components, create or update proposal/design/delta-spec/task artifacts, document acceptance/tests/dependencies, and validate before implementation.

#### Scenario: Specification readiness before implementation acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a coding agent receives a Linear ticket
- **THEN** it locates the traceability row, reads relevant main specs and ADRs, checks active changes and validates a scoped change; missing decisions block only dependent work

### Requirement: Durable verification and archival
Approved specification changes SHALL remain synchronized with code, reference the Linear ticket and be committed to GitHub; completed changes SHALL use the official archive workflow after verification.

#### Scenario: Durable verification and archival acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an authorized change passes required tests and all tracked tasks are complete
- **THEN** the agent validates, reviews the spec delta, archives with the CLI and commits the resulting main specs and archive; unperformed work is not marked complete
