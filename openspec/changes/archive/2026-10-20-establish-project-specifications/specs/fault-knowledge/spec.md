# fault-knowledge Specification Delta

## Purpose

Define observable Fault Code fault knowledge requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Versioned fault definitions
Authorized engineering clients SHALL manage demo steering/ADAS fault definitions, classifications, descriptions, technical references and approved remedies through APIs.

#### Scenario: Versioned fault definitions acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** engineering updates a published fault definition
- **THEN** a versioned approval/audit record is preserved and investigations can identify the version used

### Requirement: Approved remedy eligibility
Driver-facing results SHALL include only published, approved and applicable remedies/patch references; a patch reference SHALL never authorize vehicle software installation.

#### Scenario: Approved remedy eligibility acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a known code has draft, unapproved or inapplicable patch metadata
- **THEN** no approved-patch referral is generated from that metadata; approved applicable patches can recommend dealership service only
