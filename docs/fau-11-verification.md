# FAU-11 verification

Part of FAU-11. Provider-independent fault knowledge lifecycle and authorized production API delivered; production storage/ontology deliberately unavailable. Linear remains open for personal review.

## Observed local checks — 2026-10-20

SDK10.0.402/.NET10.0.13 with DOTNET_ROOT and PATH configured per local-development.md.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

146 tests passed (20 Domain,3 Application,123 integration), zero skips. Release build zero warnings/errors; locked restore, formatting and diff whitespace passed. OpenAPI schema/examples/baseline compatibility passed, seven breaking fixtures rejected and optional additive input accepted. 50 explicit Docker inputs scanned; no package or lockfile changes. Current route inventory verifies22 protected operations across15 OpenAPI paths, including8 new fault operations.

## Acceptance evidence

| Requirement | Observed checks |
|---|---|
| Stable namespaces/CRUD/search/uniqueness | FaultKnowledgeTests validates all3 namespaces and mismatches; KnowledgeTests exercises normalized retry, duplicate and retired reservation409, management create/get/list/edit/retire, invalid content and code400 |
| Engineering-only publication | Signed-token tests invoke actual production controllers; Operator publication403, Engineer exact knowledge.publish succeeds; Administrator write403; machine exact granular roles reach intended unavailable adapter503; FAU-9 scope/role-negative matrix remains passing |
| Draft isolation and provenance | Publish then edit retains first approved guidance; republish retains two immutable publication snapshots and original actor/time/content; paginated history includes draft/publication/retirement revisions |
| Approved applicable patches | Known lookup returns only approved remedy with exact model/software; unapproved and other-model remedies excluded; nonmatching software returns known definition with empty remedies; retired/unpublished/unknown404 distinct from unavailable503 |
| Ontology/invalid combinations | Test-only taxonomy validates exact synthetic classification version/system; unknown/wrong-system/wrong-version links400; duplicate remedy IDs, guidance+patch, inconsistent patch software, missing symptoms/references and empty descriptions400; unavailable taxonomy503 prevents publication |
| Atomic concurrency and replay | 12 simultaneous creates converge to one result; 12 simultaneous publications converge to one result;8 competing updates yield1 success+7 conflicts; failure-before-commit retains state/history/audit/replay; thrown callback and cancellation before commit roll back; reads detached and tenants isolated |
| Request safety | Missing/invalid identity401; exact insufficient permission403; missing/weak/list/zero/noncanonical ETags400; stale409; malformed replay keys400; unknown approval input fields400; correlated safe Problem Details; management DTO omits tenant |
| Public contract | Route inventory checks exact security policies per verb; generated request/response examples validate, permission summary example is not reused; all new source files explicitly allowed into Docker build |

Self-review checked immutable nested collections, tenant filters before pagination, actor/client provenance from validated tokens, publication snapshot/version safety, replay before concurrent version checks/dependency revalidation, no accidental Operator approval, safe retirement, absence of production fake persistence, ontology fail-closed adapter, actual endpoint metadata, image inputs and no external provider/dependency changes. Same-account approval was not fabricated.

## Delivery and limitations

OpenSpec proposal/scenarios/design/tasks strictly validated before implementation. Official archive synchronizes6 added requirements across existing fault-knowledge, automotive-ontology and api-contracts capabilities; all main specs require strict validation. GitHub exact-source backend and Linux container acceptance are checked after publishing. Local Docker backend remains unavailable; no local container success or deployment is claimed.

Test-only transactional adapter proves one-process semantics only. DEC-001 relational engine/provider/schema/migrations, multi-process isolation, restart/crash durability, audit/replay retention and durable outbox remain unresolved. Production UnavailableKnowledgeStore always503; no database or cloud service was selected. FAU-13 must implement the approved versioned taxonomy adapter; production UnavailableFaultTaxonomy always503, never accepting fabricated classifications. No live isolated Entra registration/consent/browser/machine credentials, RAG or incident processing exists from this ticket. See fault-code-management.md for exact API and downstream provenance contracts.
