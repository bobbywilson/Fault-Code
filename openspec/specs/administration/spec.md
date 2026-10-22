# administration Specification

## Purpose
Define observable Fault Code administration requirements and acceptance scenarios for authorized future development.

## Requirements

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

### Requirement: Complete protected administration workspace
Administration SHALL provide responsive keyboard-accessible navigation for users/roles, vehicles/drivers, fault definitions/incidents, ontology/documents, engineering, agent activity, notifications and authorized integrations. Every visible action SHALL map to a documented versioned API operation, enforce input validation and reuse concurrency/replay headers. Client permission checks SHALL be presentation only.
#### Scenario: Authorized workflows
- **WHEN** permitted operators and engineers perform the three demo investigation workflows
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
- **THEN** show unavailable or empty results without demo production fallback or fake performance charts

### Requirement: Supplied Spartan visual components
All administration views SHALL compose the supplied Spartan Helm styled components for applicable navigation, headers/footers, menus, cards, controls, forms, alerts, records and pagination. Icons SHALL use Lucide. Application-specific data binding SHALL NOT introduce bespoke visual controls or override the library's supplied visual system with a custom component library.

#### Scenario: Consistent visual system across routes
- **WHEN** an operator visits any existing administration route
- **THEN** the shared responsive shell and applicable controls use supplied Spartan styles and Lucide icons, with no emoji or hand-drawn replacement icons

#### Scenario: Accessible mobile and protected behavior
- **WHEN** the viewport narrows or the user navigates by keyboard, lacks permissions or receives unavailable data
- **THEN** supplied mobile navigation, labels, visible focus and honest status states remain usable and existing server authorization/API semantics are preserved

#### Scenario: Reviewable visual evidence
- **WHEN** the redesign is presented for review
- **THEN** screenshots show the actual updated application on desktop and mobile, with demo test contexts distinguished from real Entra authentication and no fabricated production metrics

### Requirement: Fault-code ontology browser
The authenticated Ontology route SHALL open a read-only, fault-code-centered model relationship table including incidents, vehicles, drivers and notifications. It SHALL use supplied Spartan components, label schema relationships as a model reference, support filtering and link to existing operational record views. It SHALL NOT default to creating an ontology or display ontology mutation controls. Existing protected engineering APIs and their permissions SHALL remain unchanged.

#### Scenario: Browse the fault-code model
- **WHEN** an authenticated user opens Ontology
- **THEN** the table shows how fault codes connect to incidents, vehicles, drivers and notifications without requiring a catalog ID, publication version or creation action

#### Scenario: Search and open related records
- **WHEN** the user filters relationships or opens a related record view
- **THEN** matching model relationships are shown and record navigation uses existing protected routes; no record counts or instances are fabricated

#### Scenario: No ontology mutation on navigation
- **WHEN** a signed-in user enters Ontology
- **THEN** no ontology draft or mutation request runs and no Create, Update or Publish ontology controls are displayed
