## MODIFIED Requirements

### Requirement: Fault-code ontology browser
The authenticated Ontology route SHALL open a readable fault-code-centered relationship table including incidents, vehicles, drivers and notifications. Schema references SHALL be labeled as model references and not fabricated live data. The Ontology page SHALL be non-editable and SHALL expose no creation, editing or publication controls. The page SHALL NOT create or mutate on entry; unauthorized users SHALL receive no editing controls.

#### Scenario: Read-only visitor
- **WHEN** a user lacks knowledge.publish or is signed out
- **THEN** editing is hidden or denied without granting permissions or using fake records

#### Scenario: Browse the fault-code model
- **WHEN** an authenticated user opens Ontology
- **THEN** the default model table displays fault-code/domain relationships without requiring creation or version input

#### Scenario: Search and open related records
- **WHEN** the user filters relationships or follows a record link
- **THEN** the table filters reference relationships and operational navigation uses existing protected API-backed routes without fabricated instances

#### Scenario: No ontology mutation on navigation
- **WHEN** a signed-in user enters the default Ontology view
- **THEN** no draft/create/update/publish request or mutation control executes automatically; editing controls remain absent

## ADDED Requirements

### Requirement: Complete evaluation administration
Administration SHALL provide the later approved focused workspace through authenticated APIs. Backend knowledge, investigation, delivery and telemetry services SHALL remain available independently of removed UI pages. Invitations and real email SHALL require explicit recipient authorization and consent. Unconfigured external providers SHALL remain visible blockers.

#### Scenario: External administration acceptance
- **WHEN** invitations, role grants, email or provider configuration are reported complete
- **THEN** actual authorized external actions and server-side secret custody are evidenced; local metadata/test sinks alone never count as completion

### Requirement: Focused vehicle fault workspace
The UI SHALL omit the five removed workspaces. Vehicles SHALL list associated drivers and support authenticated idempotent fault ingestion. Drivers SHALL list recorded emails. All lists SHALL show loading, pagination, empty and unavailable states without fabricated data. Backend processing services SHALL remain intact. Accepted ingestion SHALL NOT imply email delivery.

#### Scenario: Dispatch from a vehicle
- **WHEN** an authorized user selects Dispatch fault code for an active vehicle
- **THEN** the form identifies that vehicle, offers its associated drivers, validates event fields and submits through the authenticated API after confirmation

#### Scenario: Unavailable data
- **WHEN** fleet persistence or association access fails
- **THEN** the UI displays an error or unavailable association state without fabricated vehicles, drivers or dispatch success

#### Scenario: Removed page URL
- **WHEN** a user opens a removed administration page URL
- **THEN** Fault definitions, Documents, Engineering, Agent activity and Operational health URLs redirect to Incidents rather than rendering the removed workspace

### Requirement: Repeatable synthetic fleet seed
An explicit administrative seed operation SHALL prepare four synthetic Silverado/Sierra vehicles, four fictional drivers and four active driver-vehicle links using the existing fleet application service. Repeated execution SHALL preserve identifiers and avoid duplicate records or audit events. Demo emails SHALL use example.com addresses with notifications disabled. No fake production store or implicit user consent is permitted.

#### Scenario: Resume or repeat seed
- **WHEN** the same trusted administrator reruns the versioned fleet seed
- **THEN** service validation and stable replay keys preserve four vehicles, four drivers and four active associations without duplicate mutations or overwriting unrelated data

#### Scenario: Production persistence unavailable
- **WHEN** the fleet SQL connection or migrated store is unavailable
- **THEN** seed execution fails visibly and prepared/test-seeded records are not reported as real persisted application data

### Requirement: Seed-only fleet presentation
Vehicles and Drivers SHALL be read-only demo lists without create/edit/delete or management controls. Each seeded demo vehicle SHALL have one active assigned driver and one name/email pair per row. Plain driver names and readable Silverado/Sierra labels SHALL replace internal synthetic prefixes in presentation. Email SHALL occupy a separate column using example.com addresses, and fleet tables SHALL use the same centered content width as the other administration pages.

#### Scenario: Seed-only demo assignment upgrade
- **WHEN** the approved seed is applied to previously loaded demo records
- **THEN** one active driver remains for each vehicle, ended association history remains auditable, replay creates no duplicates and unrelated tenant/user data is not overwritten

#### Scenario: View fleet
- **WHEN** an authorized reader opens Vehicles or Drivers
- **THEN** read-only test data appears without management forms; vehicle dispatch still requires its actual API permissions and remains a separate operation

### Requirement: Linked incident and notification tables
Incidents SHALL list vehicle-originated investigations with vehicle, driver and notification-history links. Drivers SHALL link to their notifications. Notifications SHALL list authorized messages and delivery states with driver/incident filters before pagination. Unavailable storage SHALL remain an error. Ontology SHALL read its non-editable relationship reference from the API. Navigation SHALL end with Notifications, Users & roles, Ontology and omit the Protected workspace badge.

#### Scenario: Follow an incident relationship
- **WHEN** an authorized user selects a vehicle, driver or notifications link in an incident row
- **THEN** the destination loads the exact authorized resource or filtered history rather than an unrelated page of records

#### Scenario: Notification scope enforcement
- **WHEN** a caller lists notifications or selects driver/incident filters
- **THEN** only permitted tenant and ownership records are included in both page items and totals; invalid filters fail validation and inaccessible filters return404

### Requirement: Required backend integrations without management navigation
The UI SHALL omit Integrations and redirect its old URL to Incidents. Datadog telemetry and Mailpit capture SHALL be required backend integrations with server-held credentials and verified external ingestion/delivery. Local capture tests SHALL NOT count as Linux deployment or end-to-end incident workflow verification. Missing provider configuration SHALL remain an explicit acceptance blocker.

#### Scenario: Required providers unavailable
- **WHEN** required Datadog or Mailpit integration lacks credentials, permissions or real receipt evidence
- **THEN** completion remains blocked and metadata/configuration screens are not presented as a working integration

### Requirement: Users and assigned roles table
Users and roles SHALL show a paginated read-only table of authorized managed-user records with display name and assigned roles from the backend API. Driver seed records SHALL NOT be treated as directory users. Storage and permission errors SHALL remain explicit.

#### Scenario: Browse users
- **WHEN** an authorized administrator opens Users and roles
- **THEN** the UI automatically reads the users API and shows names and roles without a generic action selector or mutation form

### Requirement: Profile avatar account control
The signed-in header SHALL use Spartan Avatar and Dropdown Menu for the account image, name and Sign out. Supplied profile images SHALL map to the exact account identity, with initials fallback. The authentication badge and standalone sign-out control SHALL be absent. The Incidents table SHALL omit unavailable/load-error text and retain API failure semantics without fabricated rows or successful empty counts.

#### Scenario: Avatar sign out
- **WHEN** a signed-in user opens their avatar menu
- **THEN** their account name and Sign out appear, and selecting Sign out follows the real MSAL logout flow

### Requirement: Read-only fleet fault-code catalog
The workspace SHALL place Fault codes immediately below Ontology and expose a read-only API-backed table of the steering demonstration codes associated with the Silverado and Sierra test fleet. The catalog SHALL identify demonstration scope without claiming verified manufacturer diagnostic codes or approved vehicle compatibility.

#### Scenario: Browse the fault-code catalog
- **WHEN** an authenticated caller with knowledge.read opens Fault codes
- **THEN** the API returns the persisted tenant definitions seeded from the demonstration manifest with code, system, meaning and test vehicle names
- **AND** the workspace renders those records in a Spartan table without creation or editing controls

### Requirement: Inline vehicle dispatch with durable fault seed
The Vehicles table SHALL provide a fault-code select immediately after Status and a Dispatch button. Codes SHALL come from the tenant SQL catalog and match the row system. Dispatch SHALL remain disabled until a code is selected and SHALL call the two-field dispatch endpoint without a secondary form. Retries of an unconfirmed identical submission SHALL reuse the idempotency key.

#### Scenario: Dispatch from the vehicle row
- **WHEN** the user selects a catalog code and clicks Dispatch
- **THEN** the client sends only vehicleId and code with an idempotency key
- **AND** an accepted response identifies both incident and notification without claiming delivery
- **AND** the row selection remains unchanged and a Spartan toast confirms dispatch

#### Scenario: Seed fault definitions explicitly
- **WHEN** the operator runs seed-demo-faults against the dedicated SQL database
- **THEN** all nine demonstration definitions persist with audit records and no approved remedies
- **AND** repeating the command preserves existing definitions without duplicates
- **AND** the catalog API reads those persisted tenant records rather than a static fallback
