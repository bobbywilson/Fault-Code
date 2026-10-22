# FAU-10 verification

Part of FAU-10. Implemented provider-independent lifecycle and authorized production API boundary. Linear remains open for personal review. Production business persistence remains unavailable pending DEC-001.

## Observed local checks — 2026-10-20

Pinned SDK10.0.402/.NET10.0.13. Both DOTNET_ROOT and PATH configured as local-development.md.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

123 tests passed (11 Domain,3 Application,109 integration), zero skips. Build zero warnings/errors, locked restore and formatting passed. 43 explicit Docker inputs scanned. OpenAPI responses/examples and baseline compatibility passed; all seven breaking fixtures rejected and additive optional input accepted. No package/lock changes. Test controllers/store never enter the production assembly/image.

## Acceptance mapping

| Requirement | Evidence |
|---|---|
| Identifier validation and association rules | FleetRulesTests checks normalized demo IDs, malformed IDs, inactive/cross-tenant/unowned records, one open association and ended intervals |
| CRUD and validation | FleetTests exercises create/get/update/deactivate, duplicates, missing/deactivated entities, contact/owner validation, active filter and invalid pagination |
| Ownership | Actual signed-token middleware with production controllers; own lists filtered before count/page, spoofed query ignored, another driver cannot read records/vehicles; trusted resource authorization used on single GET; unknown JSON owner update fails400 |
| Authorization | Reader writes and Administrator without exact write grant deny403; all-read Administrator and exact machine roles succeed; association requires both write policies; all FAU-9 catalog negative tests remain passing |
| History and integrity | Explicit end/relink/soft-deactivate retains both intervals; driver deactivation closes associations and increments vehicle versions; inactive targets reject new link; IDs remain retained |
| Concurrency and replay | Same key/normalized payload returns original ID/version and no new audit; changed payload conflicts; parallel Task.Run creates converge to one record; parallel updates yield one success and seven409; stale ETag and malformed/weak/missing preconditions reject |
| Atomic store contract | Test-only locked copy-on-write transaction records state/history/replay/audit together; injected before-commit failure leaves no vehicle/replay/audit; cancellation and detached reads verified; tenant snapshots isolate records |
| Safe errors/unavailable persistence | Actual production unconfigured store returns correlated503 after permission check; missing identity401, missing permission403; invalid input400, absent resource404, conflict409; no request values in problem output |
| API/security documentation | Route inventory covers14 protected operations across10 OpenAPI paths, verifies exact bearer metadata for every verb, both association AND grants and Reader-only own alternatives; operation-specific examples and response schemas validated |

Self-review inspected permission scope/role separation, canonical owner mapping and server-loaded resource checks, tenant/ownership predicates before page/count, association side effects and version increments, immutable general owner update, replay before version checks, transaction rollback obligations, audit payload exclusion, safe middleware error mapping, explicit Docker inputs and absence of fake production storage. No same-account approval was fabricated.

## Delivery and practical limits

OpenSpec proposal was strictly validated before code. Official archive synchronizes vehicle-driver-management, authorization and api-contracts; all main specs must validate strictly. GitHub PR and exact-source backend/container checks are observed after publishing the archived patch. Local Docker backend remains unavailable, so Linux image/lifecycle acceptance requires container CI. No deployment is claimed.

The test store proves one-process contract behavior only. DEC-001 relational engine/provider, schema/migrations, constraints, multi-process transactions, restart/crash durability, audit/replay retention and durable outbox remain unresolved and unimplemented. Production uses UnavailableFleetStore, never in-memory fallback. Auth remains unprovisioned/disabled; no live directory owner-account verification, browser login or live machine token proof. No external provider, tenant or host changes. FAU-12 incident referential persistence is future work; this ticket preserves fleet IDs/history and defines its intake requirements rather than fabricating incidents.
