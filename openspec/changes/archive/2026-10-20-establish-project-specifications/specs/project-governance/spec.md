# project-governance Specification Delta

## Purpose

Define observable Fault Code project governance requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Independent project boundaries
Fault Code SHALL use an independent private Linear workspace and the existing private bobbywilson/Fault-Code repository; it SHALL NOT modify unrelated resources, invite users, buy subscriptions or provision infrastructure during setup.

#### Scenario: Independent project boundaries acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** the setup environment is audited
- **THEN** one human Linear member, disabled invite links, no auto-join domains and Free plan are verified; only Fault Code resources changed

### Requirement: Linking without automatic closure
Issue references SHALL identify the stable FAU identifier in branches, commits and PRs; linking SHALL NOT automatically close issues or change completion state without approval.

#### Scenario: Linking without automatic closure acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a specification PR references Part of FAU-26
- **THEN** the association is reviewable while the issue remains open; permission grants and actual event verification are recorded separately
