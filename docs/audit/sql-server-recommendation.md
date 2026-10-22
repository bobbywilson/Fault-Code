# SQL Server recommendation — approved engine, execution pending

The October21 architecture prompt prefers SQL Server but explicitly requires approval before selection/provisioning. No SQL Server edition, database, migrations or relational adapter has been installed, created or selected by this audit. Qdrant remains a derived retrieval index.

## Observed implementation and host

The canonical services bind unavailable stores in `src/FaultCode.Api/Program.cs`; no relational driver/DbContext/migrations or durable application records were found. Compose defines API, Angular, Qdrant and proxy only. The live DigitalOcean root console identified `lalai-staging-runner` /134.209.215.92, Ubuntu24.04.5 x86_64,4CPUs,7941MiB RAM with7272MiB available and150GiB free. Docker has no containers, and no accepted Fault Code release manifest exists. The LALAI runner is active, PID186131, zero restarts. This is headroom evidence, not approval to change shared infrastructure.

## Recommendation

Use **SQL Server2025 Express** for this short-lived public evaluation, with EF Core SQL Server migrations and an atomic relational outbox. Express is free for small production web/server applications. Developer editions are licensed for development/test, so I do not recommend assuming this public demo qualifies. Evaluation edition expires and is unnecessary.

Proposed isolated service: project `faultcode-demo`, service `database`, its own named data volume, internal data network only, no host port1433, CPU cap1, container memory cap3GiB and explicit instance memory limit2048MiB (validate actual process headroom). SQL Server on Linux needs at least2GiB to start. Express2025 limits include50GB per relational database and1410MB buffer pool; the buffer-pool limit is not the whole process/container limit. Use default relational features only; no paid Azure/Arc integration.

Current four-service caps total2CPU/2304MiB. With the proposed database, configured ceilings become3CPU/5376MiB, leaving roughly2.5GiB of host RAM for OS/LALAI/other processes before measured baseline use. Actual load and application p95 latency must be observed before hosted activation. Do not reserve or install anything on the shared server without approval.

Store a generated database administrator credential only in the server secret store. The API receives a separate least-privilege application login; use a separate migration identity, explicit schema/version checks, encrypted connections with a validated server certificate, parameterized queries and tenant-filtered access. Mount backups/volumes only inside the Fault Code namespace. Do not share LALAI MySQL databases, users or volumes. Application Entra authentication remains separate from SQL Server container authentication.

Implement normalized users/role mappings, consent/invitations, vehicles/drivers and effective association pairs, fault/ontology/document revisions/publications, incidents/history/work, engineering cases/approvals, notifications/outbox/attempts and integration/audit records. Apply unique tenant/source-event and replay keys plus transactional outbox leases/fencing. Verify concurrent connections, crash/restart, exact seed replay, duplicate delivery suppression, migrations and rollback/retention before claiming durable production workflows. Backups and persistent data deletion need an explicit retention/removal decision; no destructive migration is implicit.

## Trade-offs

Express keeps licensing cost zero but lacks SQL Server Agent; use the application's bounded background outbox worker and dedicated maintenance jobs instead. Memory/CPU/database-size limits suit a small30-day demo POC, not an unlimited production service. .NET's EF Core SQL Server integration fits the chosen backend; PostgreSQL can use less baseline memory but would be a separate database decision. No provider choice is implied by this recommendation.

## Primary sources checked during audit

- https://www.microsoft.com/en-us/sql-server/sql-server-downloads — Express production eligibility; Developer development/test restriction.
- https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-editions-and-components-2025?view=sql-server-ver17 — Linux/container editions and Express limits.
- https://learn.microsoft.com/sql/linux/install-upgrade/setup — Linux minimum memory and platform prerequisites.

Approval requested: accept this engine/edition and isolated **local implementation/testing** plan. Shared-host database installation or deployment remains a separate approval step after local evidence and resource review.

## Subsequent decision

The user approved SQL Server on October21. SQL Server2025 Express is selected; fleet EF Core tables/adapter/migration and explicit seed commands are implemented. Live SQL validation is blocked by current local VM capacity. Shared-server installation remains unapproved and deployment remains paused. See `docs/sql-server.md`.
