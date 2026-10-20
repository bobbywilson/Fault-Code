# driver-notifications Specification Delta

## Purpose

Define observable Fault Code driver notifications requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Notification triggers and referrals
Notifications SHALL support known-fault guidance, applicable approved-patch dealership referrals and approved engineering resolution, recording recipient, type, incident and evidence version.

#### Scenario: Notification triggers and referrals acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** each synthetic workflow reaches its approved notification trigger
- **THEN** the correct driver gets approved content and dealership guidance where applicable; no notification claims a vehicle patch was installed

### Requirement: Honest delivery state
Notification status SHALL distinguish pending, acknowledged/sent, failed and retry history, respect preferences and use idempotency; external provider choice remains undecided.

#### Scenario: Honest delivery state acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a test delivery sink fails, times out or receives a duplicate event
- **THEN** failure remains visible/retryable and sent is recorded only after acknowledgement; one logical notification is delivered
