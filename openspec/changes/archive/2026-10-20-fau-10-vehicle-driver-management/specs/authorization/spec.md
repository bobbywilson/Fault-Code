## ADDED Requirements

### Requirement: Tenant filtered lifecycle operations
Lists SHALL apply validated tenant and own/all permission restrictions before counting and pagination. Reads SHALL authorize trusted stored owner mapping, excluding unowned vehicles. Writes SHALL require exact operation permission and same-tenant trusted records; association mutations SHALL require both drivers.write and vehicles.write. Ownership mappings SHALL be immutable through general updates.
#### Scenario: No list or mutation escalation
- **WHEN** another owner, cross-tenant caller, own-only reader or Administrator without an exact write grant attempts lifecycle access
- **THEN** private records are excluded, writes deny and client-supplied owner query fields cannot expand access
