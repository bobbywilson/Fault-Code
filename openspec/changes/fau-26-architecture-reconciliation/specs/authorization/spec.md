## ADDED Requirements

### Requirement: Driver least-privilege role
Driver SHALL be a supported human role with authorization.read and own-driver/own-vehicle scopes bound to trusted stored ownership. It SHALL NOT inherit all-fleet reads, engineering publication or administrative management. Existing Reader/Operator/Engineer/Administrator and exact application-role permissions SHALL remain explicit. Directory assignment and local metadata SHALL be distinguished.

#### Scenario: Driver boundary
- **WHEN** a Driver requests own records, another driver vehicle or an administrative mutation
- **THEN** only explicitly scoped owned reads succeed; other-owner resources remain concealed and management is forbidden
