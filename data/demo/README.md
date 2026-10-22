# Demo steering seed preparation

Part of FAU-11/FAU-12. `steering-faults.json` contains nine clearly demo demo fault drafts and their matching classification concepts, covering the approved EPS, steer-by-wire and steering ADAS scope. Codes are application-owned SYN-* examples, not genuine Nexteer/OEM DTCs. The Nexteer source supports product scope only, not diagnostic mappings or remedies. No vehicle/driver identifiers or private credentials are included.

This manifest has **not been loaded into a database**. SQL Server 2025 Express is now approved; the fault-knowledge adapter and real database loading are still incomplete. Do not wire this file into a fake production in-memory store or represent prepared drafts as approved published knowledge. A future authenticated seed importer must idempotently create/reconcile exact entries, publish only reviewed content, preserve unrelated tenant data and verify durable restart behavior. Remedy candidates remain unapproved. Real manufacturer-code imports require an authoritative, permitted OEM/model/software-specific catalog.


## Fleet factory and seeder

`src/FaultCode.Application/Demo/DemoFleetFactory.cs` supplies a deterministic .NET factory and explicit `DemoFleetSeeder`. It uses the existing `FleetService`, including validation, optimistic versions, tenant boundaries, audit and stable replay keys. No new data-generation package or database provider is required.

| Vehicle | Associated fictional drivers |
|---|---|
| SYN-SILVERADO-001 | Alex Morgan |
| SYN-SILVERADO-002 | Jordan Lee |
| SYN-SIERRA-001 | Casey Taylor |
| SYN-SIERRA-002 | Riley Parker |

Emails are `alex.morgan@example.com`, `jordan.lee@example.com`, `casey.taylor@example.com`, and `riley.parker@example.com`. Notifications are disabled. Owner GUIDs are demo placeholders, not real Entra identities, and grant no directory roles. VINs and real customer data are not fabricated. Vehicle identifiers identify the selected truck examples; this does not assert verified OEM steering configurations or diagnostic mappings.

Invoke `await new DemoFleetSeeder(fleetService).SeedAsync(trustedCaller, cancellationToken)` only from an explicit trusted administrative runner after the SQL Server fleet store is configured. The runner must authorize driver/vehicle writes and construct the caller from validated identity, not request-supplied privilege flags. The seeder is not registered on API startup and exposes no public endpoint. Use the same actor/client for replay/resume. A different actor, changed payload, preexisting conflicting identifier or concurrent version change must be reviewed rather than force-overwritten. Multiple individual transactions can commit before interruption; stable replay keys permit safe continuation.

Fleet records are now **loaded and verified in real local SQL Server**: four vehicles, four drivers, four active links, seven retained association intervals and22 audit events. Repeating the seed produced no duplicates; counts survived SQL container restart and are visible in the real Entra-signed Angular UI. The separate fault-definition manifest remains prepared, not loaded. Hosted deployment remains paused. Real SQL integration tests cover migration, replay, reconnect, tenant isolation, rollback and active-pair uniqueness. See `docs/sql-server.md` for the local instance and remaining persistence gaps. This factory does not create users, invite anyone, send email, publish fault definitions or select a database engine.

.NET comparison: EF Core supports `UseSeeding`/`UseAsyncSeeding` for database-backed initialization. A deterministic application factory and explicit seeder supply Laravel-like organization without choosing EF Core or a provider prematurely. Source: https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding
