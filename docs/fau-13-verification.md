# FAU-13 verification

Part of FAU-13. Provider-independent ontology management, immutable approved taxonomy and deterministic stored evaluation are implemented. Production storage returns503 pending DEC-001. Linear remains open for personal review.

## Local verification — 2026-10-20

SDK10.0.402/.NET10.0.13, DOTNET_ROOT and PATH per local-development.md:

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

182 tests passed (34 Domain,3 Application,145 integration), zero skips; Release build zero warnings/errors. Locked restore, format and whitespace checks pass. OpenAPI schema/examples/baseline compatibility pass;7 breaking fixtures rejected and optional additive parameter accepted. 64 explicit Docker inputs scanned; no recognized credential patterns. No package or lockfile changes. Route inventory37 protected operations across27 OpenAPI paths. Local Docker daemon unavailable; exact-source Linux container CI is separately required before merge.

## Acceptance and review evidence

| Requirement | Evidence |
|---|---|
| Typed concepts/systems | OntologyRulesTests covers all7 kinds across EPS, steer-by-wire and steering ADAS; bounded/null/duplicate/unknown inputs reject |
| Relationships | Tests exercise declared endpoint-kind pairs, missing endpoints, unknown types, cross-system edges, duplicate IDs/edges, self loops and combined is-a/part-of cycles |
| Deterministic rules | Priority overrides specificity, specificity selects exact applicability, equivalent ties use ordinal ID, explicit no-match; potentially overlapping equal-ranked conflicting outcomes reject even with different symptom sets; disjoint exact applicability accepted |
| Draft/publication isolation | Actual signed-JWT production controller tests manage draft, publish twice, deny knowledge-only draft/history access, reject draft-version retrieval/evaluation and retain immutable old approval actor/time/content |
| Pinned historical evaluation | Stored result/GET/replay remain byte-equivalent after later rule removal/publication; new publication yields explicit no-match; winner/ordered matches/input/publication/approval/evaluation provenance retained |
| Actual FAU-11 adapter | Tests use ApprovedOntologyTaxonomy for all3 systems with real KnowledgeService create/publication; dedicated production fault-controller test confirms DI registration and rejects draft/missing/wrong-version links400; unsupported remedy kinds and wrong-system links fail; old approved classification survives later removal; outage503 never becomes valid/missing |
| Authorization | Exact Engineer+knowledge.publish or Knowledge.Publish.Application manages; Administrator/Operator deny; knowledge.read or Knowledge.Read.All reads approved/evaluates; missing token401; no permission catalog expansion; complete existing permission-negative matrix and production route metadata tests pass |
| Replay/concurrency |10 concurrent creates/publications/evaluations each converge to one committed identity/result;8 competing updates yield1 success and7 conflicts; reordered concept payload replays; changed payload conflicts; publication replay precedes stale-version checks |
| Atomicity/isolation | Callback exception and cancellation before commit roll back; unavailable adapter leaves state unchanged; detached read collection clears cannot alter committed state; other tenant cannot read approved versions/evaluations or validate its classification |
| HTTP and safe contracts | Explicit API DTOs; unknown approval fields400; weak/list/missing/zero/noncanonical If-Match400; bad keys400; stale409; history bounded and huge offsets empty with accurate count; correlated Problem Details, ETag and Location checked; all8 authorized production operations503 when unconfigured |
| Compatibility/build | Existing schemas/security structurally unchanged; new operation examples validate; source allowlist updated explicitly, no new dependencies or provider choices |

Self-review checked specs against domain/application/controller/DTO/DI implementation, stable ID reservations, immutable publication/result nesting, ordered canonical hashes, scoped replay/audit, transaction rollback, exact knowledge policies, tenant isolation, absence of draft guidance and production memory fallback. Review replaced initial direct domain responses with explicit API DTOs to preserve repository API conventions. Route-inventory verification now understands numeric version constraints as well as GUID constraints. Same-account GitHub approval is not fabricated.

## Lifecycle and limitations

Strict OpenSpec proposal validation preceded implementation. Official archive synchronizes5 added requirements across automotive-ontology and api-contracts; main specs must pass strict validation before commit. PR uses Part of FAU-13 and exact-source backend/Linux checks must pass before authorized merge. Native Linear PR linkage and issue remaining open are verified separately.

`IOntologyStore` specifies detached tenant reads and atomic catalog/revision/publication/evaluation/replay/audit with cancellation rollback and cross-process versions. Test-only copy-on-write storage proves one-process behavior only. DEC-001 relational engine/provider/schema/migrations, actual concurrent connections, restart/crash persistence and retention remain unresolved. Immutable old ontology publications remain usable by exact FAU-11 classification references; no withdrawal API exists. There is no production durable persistence, outbox, live tenant provisioning, RAG ingestion, agent investigation, external notification, cloud deployment or vehicle software operation in FAU-13. See automotive-ontology.md for the exact boundary and API contract.
