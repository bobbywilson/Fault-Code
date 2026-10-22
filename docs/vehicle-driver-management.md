# Vehicle and driver lifecycle

Part of FAU-10. SQL Server is the approved transactional database. The configured fleet adapter persists drivers, vehicles, association intervals, audit and replay records. Local SQL Server2022 Developer is for development only; hosted SQL Server2025 Express remains the planned free edition. See [SQL setup and verification](sql-server.md). Dedicated real Entra authentication is configured; authorization still requires the exact API permissions.

The demo UI is seed-only: four Silverado/Sierra vehicles and four fictional drivers, one active driver per demo vehicle. Vehicles and Drivers use the same centered content layout as the other tables. Vehicle labels omit internal prefixes and numeric suffixes. Driver name and example.com email are separate columns. There are no create/edit/delete fleet controls. The authenticated backend lifecycle endpoints remain supported for authorized integrations and the explicit seeder; removing UI management does not remove historical data or authorization.

A driver row links to `/admin/notifications?driverId={id}`. Incident rows link back to `/admin/vehicles?vehicleId={id}`, `/admin/drivers?driverId={id}`, and `/admin/notifications?incidentId={id}`. Filtered fleet links load the authorized individual API resource rather than searching an arbitrary page. The API returns404 for inaccessible resources.

## Operations

| Route | Methods and exact policies |
|---|---|
| `/api/v1/drivers` | GET drivers.access.read; POST drivers.write |
| `/api/v1/drivers/{id}` | GET drivers.access.read; PUT/DELETE drivers.write |
| `/api/v1/vehicles` | GET vehicles.access.read; POST vehicles.write |
| `/api/v1/vehicles/{id}` | GET vehicles.access.read; PUT/DELETE vehicles.write |
| `/api/v1/vehicles/{id}/associations` | GET vehicles.access.read; POST vehicles.write AND drivers.write |
| `/api/v1/vehicles/{id}/associations/{associationId}` | DELETE vehicles.write AND drivers.write |

`*.access.read` accepts exact all-read permission OR Reader with exact corresponding own-read scope. It does not add a permission to the immutable Entra catalog. Machine own-read is unavailable. Owner-restricted lists filter before totalCount/page; all-read is tenant-local. Single reads also invoke the existing trusted resource authorization handler. Vehicle ownership derives from its current association's stored driver mapping. An unassociated vehicle is excluded from reads, including staff all-read; authorized operators can establish its association. Ended associations remain queryable to their former owner or all-read staff, with only the permitted intervals returned. Other-owner/cross-tenant single lookups return 404. No owner oid is returned in DTOs.

Writes use the established FAU-9 Operator+exact scope or exact application role. Administrator alone does not authorize them. Driver general updates cannot change identity, identifier or owner; unknown JSON properties fail 400. Owner mapping creation is an explicit privileged operation, not inferred from a reader-supplied oid. Actual directory-account existence is not verified until dedicated identity provisioning; canonical nonzero GUID syntax is checked. One owner mapping and driver identifier per tenant, retained after deactivation. No hidden role hierarchy or claims from request payloads.

## Input and lifecycle

Demo driver identifiers normalize trim/uppercase to `DRV-...`, vehicles to `SYN-...`, 40 characters maximum, ASCII letters/digits/hyphens only. Vehicle system is exactly EPS, steer-by-wire or steering-ADAS. Display name is 1–100 non-control characters. Optional contact is an `example.com` or legacy `example.invalid` email (maximum 150 characters); email notifications require it. No phone, address or real contact input is supported. Stable GUID record IDs are server-generated. Vehicle updates normalize identifiers and enforce tenant-local uniqueness, including deactivated records.

DELETE soft-deactivates; there is no hard-delete/reactivation API. Driver deactivation closes all current associations and increments affected vehicle versions in the same transaction; vehicle deactivation closes its current association. No incident/driver/vehicle identifiers or ended intervals are deleted. The domain supports many-to-many associations; the demo seed intentionally assigns one active driver to each vehicle. Duplicate active pairs are rejected. New association requires both records active and same-tenant. To replace, explicitly end the prior association then create another using the new vehicle version. Effective intervals are `[from, until)`, UTC server time, with no client backdating or future dating. FAU-12 must store incident association references/snapshots and use active records and the applicable interval at intake; this ticket creates no incident implementation.

GET lists use page=1, pageSize=20, max100, deterministic GUID ordering. Drivers/vehicles accept optional active=true/false; association history only page/pageSize. Empty pages have accurate filtered totalCount. Unknown query keys are ignored and cannot grant ownership.

## Mutation contracts

POST driver/vehicle/association requires Idempotency-Key (1–100 ASCII letters/digits/hyphens/underscores). Scope is tenant+actor+client+operation; association operation includes vehicle ID. Payload SHA-256 covers normalized demo identifiers and normalized driver name/email (and association target/expected version). Same key/payload returns original 201 ID/version with original ETag, even if later state changes; changed payload returns409. Keys/results are retained indefinitely in this contract; production retention/expiry policy requires DEC-001 design. No audit is appended for a replay.

PUT/DELETE and association POST require one strong quoted positive decimal If-Match, for example `"1"`. Missing, weak, wildcard, lists, zero, leading-zero or malformed versions return400. Stale version returns409. Association writes use the vehicle version and increment it. Successful mutations return `{id,version}` and strong ETag; create includes Location. Read DTOs include version; single GET returns ETag. Association create Location identifies its vehicle, matching the returned vehicle ID/version.

Store `TransactAsync` must atomically commit state, history, replay and safe audit (tenant/actor/client/server correlation/operation/resource/time), roll back on failure/cancellation and enforce versions/uniqueness/replay under cross-process concurrency. Read snapshots must be detached and restricted to the requested tenant. The test double verifies these semantics within one process; this is not evidence of database durability or multi-process safety. Operational authorization logs remain distinct from transactional audit.

## Remaining production acceptance

DEC-001 must resolve relational engine/provider, migration tooling, schema/unique constraints, transaction isolation, durable replay/audit retention and durable outbox. An approved adapter must pass the same store/API suite plus restart recovery, actual simultaneous database connections, crash-before/after-commit, migrations and retention tests. No database/outbox/cloud tenant was provisioned. Live Entra registration, browser flows and deployment remain separately pending. API health/live is process liveness and does not prove fleet storage is available.
