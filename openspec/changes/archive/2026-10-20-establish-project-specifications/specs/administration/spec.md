# administration Specification Delta

## Purpose

Define observable Fault Code administration requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Accessible Angular administration
Angular/TypeScript administration SHALL use Spartan UI and Tailwind, responsive accessible states, Entra PKCE and exclusively protected ASP.NET Core APIs.

#### Scenario: Accessible Angular administration acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** an authorized administrator uses vehicle, driver, fault, knowledge, user/permission or integration management
- **THEN** each operation has an API counterpart; loading, empty, error, forbidden and keyboard/mobile interactions are supported

### Requirement: Recorded operational views
Administration SHALL display actual incident, engineering, notification and agent activity/history subject to permissions; absent metrics SHALL be visibly unavailable.

#### Scenario: Recorded operational views acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** an operator opens an empty or partially recorded activity view
- **THEN** no fake data or zero-valued history is fabricated; API authorization still prevents hidden or cross-driver operations
