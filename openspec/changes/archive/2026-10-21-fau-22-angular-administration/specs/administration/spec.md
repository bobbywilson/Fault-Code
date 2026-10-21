## ADDED Requirements

### Requirement: Complete protected administration workspace
Administration SHALL provide responsive keyboard-accessible navigation for users/roles, vehicles/drivers, fault definitions/incidents, ontology/documents, engineering, agent activity, notifications and authorized integrations. Every visible action SHALL map to a documented versioned API operation, enforce input validation and reuse concurrency/replay headers. Client permission checks SHALL be presentation only.
#### Scenario: Authorized workflows
- **WHEN** permitted operators and engineers perform the three synthetic investigation workflows
- **THEN** the client invokes existing investigation, engineering approval and notification operations with exact permissions and preconditions; pending proposals never become approved guidance without server approval
#### Scenario: Permission and error states
- **WHEN** identity expires, permission is missing, a version conflicts, storage is unavailable or a collection is empty
- **THEN** display distinct sign-in, forbidden, reload-before-retry, unavailable or empty states without invented results; mutation retries preserve the original replay key and stale forms never overwrite server state
#### Scenario: Accessible mobile navigation
- **WHEN** a user navigates with keyboard or a narrow viewport
- **THEN** labeled controls, visible focus, a skip link, route focus and scrollable results remain usable
### Requirement: Honest operational activity
Agent and notification views SHALL read recorded incident investigation and notification history only. Missing measurements SHALL show unavailable rather than fabricated zero. Test fixtures SHALL remain outside production bundles and no production authentication bypass SHALL ship.
#### Scenario: No recorded data
- **WHEN** history is missing or its adapter returns503
- **THEN** show unavailable or empty results without synthetic production fallback or fake performance charts
