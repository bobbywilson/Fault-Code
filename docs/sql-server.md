# SQL Server fleet persistence

Part of FAU-10/FAU-26. SQL Server was approved October21. Use SQL Server2025 Express (free), EF Core SQL Server10.0.13, versioned migrations and normalized tenant-bound fleet tables. This does not complete the incident, knowledge, notification, administration, document, investigation, escalation or ontology adapters. Those still fail closed rather than falling back to memory.

## Implementation

- `FleetDbContext`: Drivers, Vehicles, DriverVehicleAssociations, FleetAudit, FleetReplays.
- Composite tenant/resource primary keys and foreign keys; unique driver/vehicle identifiers and driver-owner mapping per tenant.
- Filtered unique active driver/vehicle pair permits many-to-many associations and preserves ended history.
- `SqlServerFleetStore`: detached reads, serializable SQL transactions and transaction-owned shared/exclusive `sp_getapplock` per tenant. Fleet mutations, audit and replay commit together. Versions detect stale callers. Constraint conflicts return409, unavailable persistence503; raw connection errors stay out of API responses.
- A nonempty `ConnectionStrings__FaultCode` selects SQL Server. Missing configuration keeps `UnavailableFleetStore`. No provider/secret is embedded in the browser.
- `DemoFleetFactory`/`DemoFleetSeeder`: four fictional drivers, two Silverado and two Sierra vehicles, four active links, seven retained association intervals. example.com emails and notifications off. Synthetic owner GUIDs do not create real users or imply consent.

## Configuration and operator commands

Use secret/environment injection, never paste real connection strings into this document or Git. Configure `ConnectionStrings__FaultCode` with the application login, encrypted transport and a trusted server certificate. Local self-signed certificate trust may be explicitly enabled for isolated testing only. Application login requires DML on fleet tables and the database application-lock procedure; schema creation/migration is reserved for the migration login. Do not use sa for the application.

The SQL overlay `compose.sqlserver.yaml` is explicit and adds a dedicated database volume/network membership. It has no published database port, uses Express, a verified Microsoft image digest, CPU1 and3GiB memory cap. It is NOT part of `deploy/demo/compose.yaml`. Set the reviewed EULA acceptance and a strong bootstrap password using ignored local secrets. Do not run it on the current2GiB local VM without approved capacity.

Run the API assembly in explicit command mode, with relevant environment variables injected securely:

```text
Database__Command=migrate
ConnectionStrings__FaultCode=<application secret>
ConnectionStrings__FaultCodeMigration=<migration secret>
```

This applies committed EF migrations and exits without starting the web server. Use a precreated dedicated Fault Code database and authorized migration login; do not grant database-creation privileges to the application login.

Then invoke separately:

```text
Database__Command=seed-demo-fleet
Entra__TenantId=<dedicated Fault Code tenant>
Database__SeedActorId=<authorized operator object ID>
Database__SeedClientId=<authorized administrative client ID>
ConnectionStrings__FaultCode=<application secret>
```

These commands are available only to the trusted OS/container operator, not as public API endpoints. Authorization to run the command must be enforced by deployment/secret access; fields are not accepted from untrusted HTTP callers. Unset `Database__Command` for ordinary API serving. Seed replay is stable for the same actor/client. Do not change the seed payload under the same version/key or force-overwrite existing records.

## Verification

Offline tests inspect the generated SQL migration, tenant foreign keys and filtered uniqueness. Seed tests exercise existing validation/audit/replay in an isolated test store; they do not prove database durability.

Real SQL test `Real_sql_server_seed_replay_reconnect_tenant_isolation_and_rollback` requires `FAULTCODE_SQL_TEST_CONNECTION` targeting an explicitly created disposable database whose name starts `FaultCode_Test_`. It applies migrations, seeds/replays, reconnects with a new context/store, verifies counts and tenant isolation, and tests rollback/unique-pair protection. It never drops the database or persistent data. Without configuration this test reports SKIPPED, not success. A separate SQL container restart test and live Angular list verification are still required after capacity is available.

Current local VM total RAM1966MiB is insufficient to safely add SQL Server alongside existing services. Live SQL tests/loading are BLOCKED pending approved capacity. Shared deployment remains paused.

## Operations still required

Before hosted installation: separately approve shared-server changes, provision scoped credentials/certificate trust, run migration with schema permissions, load seeds, verify application and database restarts, add tested backup/restore and retention procedures, and verify LALAI/Simone health. Teardown may remove only Fault Code resources; persistent data deletion always requires explicit approval. No database volume has been created or deleted by this implementation.

Microsoft references: [EF Core SQL Server](https://learn.microsoft.com/ef/core/providers/sql-server/), [SQL Server containers](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker), [EF Core seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).


## Actual local platform finding

The user approved the local VM resize/restart. It now has3910MiB total RAM, and nginx/mysql/php8.3-fpm returned active after restart. A subsequent architecture check established `aarch64` for both VM and Docker engine. The official SQL Server x64 image failed with `exec format error`; the dedicated failed container was stopped and automatic restart disabled. Its volume was preserved, not deleted. No unsupported emulation or hosted-server installation was attempted. Local RAM approval does not resolve this architecture blocker. The SQL integration workflow instead uses an isolated GitHub Actions x64 runner, a random per-run password and real SQL Server Express. It tests migration, seed/replay, reconnection, tenant isolation, constraints, rollback and SQL process restart. CI fixture data is not the local application's loaded data. Actual workflow results must be recorded before marking those checks verified.


## Verified local takeover (latest result)

The user supplied the existing Mac Docker Desktop SQL container and explicitly authorized taking it over. The separate VM container was left stopped with its empty dedicated volume preserved; the extra Mac image pull was cancelled before another database container was created. Existing `sql2022` was started and limited to3GiB RAM/2CPUs. It reports **SQL Server2022 Developer Edition16.0.4225.2**. Developer is free for this local development/test use, not for the publicly hosted evaluation. The planned hosted SQL Server2025 Express free edition remains separate and has not been installed. Local Docker Desktop handles the x64 image on the ARM Mac; no native ARM SQL Server support is claimed.

Created dedicated `FaultCode` and `FaultCode_Test_local` databases, `FaultCodeApp` application login and a database-scoped migration login. Applied InitialFleet; seeded twice under the real dedicated Fault Code tenant and operator identity. Main database counts: **4drivers,4vehicles,6active associations,14audit events**. The application login has no db_owner membership and no CREATE TABLE permission; only selected table DML grants. Reconnected and restarted only `sql2022`; the same main counts survived. The real Entra-authenticated Angular Vehicles and Drivers pages show these SQL-backed records. Synthetic owner placeholders/emails are not real directory accounts or authorized recipients.

All436 .NET tests passed (39domain,3application,394integration), with **no skipped tests** and actual SQL tests enabled. Twelve isolated browser tests passed, and the real signed-in views were also inspected separately. No credentials were printed or committed. The application uses its scoped login, not sa. Existing `sql2022` stores data in its container writable layer, with no mounted volume; restart durability is verified, but container-removal durability is not. Preserve this container until a reviewed volume migration/backup is completed; do not treat its layout as the hosted deployment design.

This replaces earlier local-capacity-blocked status for fleet loading. The local VM remains at its user-approved4GiB with nginx/mysql/php8.3-fpm active. Hosted LALAI/Simone resources and application deployment were not changed. Current Entra user is Reader: vehicle dispatch correctly stays disabled until an authorized operational role is assigned. Incident/knowledge/delivery adapters still need implementation, so neither the seed nor these reads establish a working fault-to-email workflow.


### Current seed revision
The latest idempotent v3 seed upgrades names/emails and ends superseded demo assignments while retaining history: four vehicles, four drivers, four active links, seven retained association intervals and22 audit events. Earlier counts above describe the earlier verification snapshot, not the current seed.

### Durable dispatch and catalog update (2026-10-21)
The local database now has the PersistWorkflow migration and a shared SQL transaction adapter for fleet, incidents, knowledge, ontology, documents, investigations, engineering and notifications. Each workflow aggregate or journal entry is a schema-versioned typed JSON row; tenant keys, fleet references, source-event uniqueness, notification-to-incident foreign keys and the shared tenant SQL lock protect atomic writes. This is a small demo persistence layout, not a scale benchmark or a claim of fully normalized nested histories. Application access is table DML only, including WorkflowRecords; schema migrations remain separate.

Run `Database:Command=seed-demo-faults` explicitly with `Entra:TenantId`, `Database:SeedActorId`, and `Database:SeedClientId`. The embedded manifest is validated then all nine definitions and their audit entries are committed together. Repeating the command adds zero records and preserves existing edits. The API catalog reads these SQL records. No approved patches, publication, Qdrant indexing, email delivery or external telemetry is fabricated by seeding.

Verified locally: nine fault records and nine seed audits persisted; replay added zero. The real signed-in vehicle dispatch created one incident and one linked notification in SQL. Separate real SQL tests verify seed reconnection, tenant isolation, atomic dispatch replay and callback-failure rollback. Accepted notification state is AwaitingAgent: actual agent/MCP email processing remains unfinished.
