## ADDED Requirements

### Requirement: Read-only fault investigation model
The Angular Ontology view SHALL present a non-editable relationship table covering fault definitions, incidents, vehicles, drivers, notifications and steering knowledge. It SHALL distinguish model references from persisted records. No GraphRAG is permitted.

#### Scenario: Engineer opens ontology
- **WHEN** an authenticated engineer with publication permissions opens Ontology
- **THEN** the model table is readable and no create, edit or publish controls are displayed
