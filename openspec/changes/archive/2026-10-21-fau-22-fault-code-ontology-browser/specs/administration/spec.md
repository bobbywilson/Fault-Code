## ADDED Requirements

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
