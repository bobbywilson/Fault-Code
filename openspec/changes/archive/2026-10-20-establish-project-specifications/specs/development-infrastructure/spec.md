# development-infrastructure Specification Delta

## Purpose

Define observable Fault Code development infrastructure requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Reproducible local environment
The development environment SHALL support Docker Compose with API/supporting services and Qdrant, isolated networking, health checks, persisted local volumes and documented Linux startup/reset procedures.

#### Scenario: Reproducible local environment acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a developer follows setup instructions from a clean checkout
- **THEN** services build, report health and connect by service DNS; a restart preserves expected data and destructive volume reset is explicitly distinguished

### Requirement: Safe service configuration
Container runtime SHALL use external secret configuration and bounded service exposure; no unapproved relational or cloud service SHALL be provisioned.

#### Scenario: Safe service configuration acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** Qdrant is unavailable or credentials are missing
- **THEN** dependent operations fail clearly without exposing secrets; no fallback creates a cloud resource or silently chooses a database
