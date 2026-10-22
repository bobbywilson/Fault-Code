## MODIFIED Requirements

### Requirement: Effective association integrity
Associations SHALL use server UTC effective intervals, require active same-tenant vehicle and driver, support many active drivers per vehicle and many vehicles per driver, retain ended history and reject stale writes. Only duplicate active vehicle-driver pairs SHALL conflict; deactivated entities SHALL reject new associations. A durable adapter SHALL enforce pair uniqueness atomically across concurrent connections.

#### Scenario: Many-to-many association changes
- **WHEN** a second driver is associated with a vehicle or one driver joins a second vehicle
- **THEN** valid distinct pairs succeed while duplicate active pairs, stale writes, inactive/cross-tenant records fail and ending one pair preserves other pairs and all incident snapshots

#### Scenario: Association changes
- **WHEN** a pair is ended or a driver/vehicle is deactivated
- **THEN** preserve prior IDs/history and unaffected active pairs, reject new intake for the closed/inactive pair, and leave historical incident context unchanged
