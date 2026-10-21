# FAU-14 verification

Part of FAU-14. Eight authorized APIs, canonical document-store abstraction, immutable engineering approvals, deterministic page chunks, configurable OpenAI HTTP embeddings and rebuildable Qdrant index are implemented. Production canonical storage remains unavailable503 pending DEC-001/003; no model choice/live credential/provider provision or paid call is made. Linear stays open for personal review.

## Local evidence — 2026-10-20

Pinned SDK10.0.402/.NET10.0.13; both DOTNET_ROOT and PATH configured. 211 local tests pass (37 Domain,3 Application,171 integration; zero reported skips). One externally gated acceptance test is a local no-op and is not counted as real Qdrant evidence. Build has zero warnings/errors; format, locked restore, Compose config,74 explicit inputs and baseline/PR-base compatibility plus7 breaking fixtures pass. Locked restore/build, full tests, format, explicit Docker inputs and generated contract checks must pass on final source. Real local Docker acceptance is unavailable because the daemon is not running; the externally gated test returns without real acceptance locally. The Linux retrieval job runs that test explicitly against authenticated pinned disposable Qdrant using mock embeddings.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

## Acceptance and self-review

| Behavior | Evidence |
|---|---|
| Canonical text/chunks | Domain tests verify exact text retention, deterministic IDs/checksums/page offsets, tenant/version separation, Unicode boundary, bounds/duplicates/system/applicability/access rejection |
| Approval isolation | Service/API tests verify create draft absent, publication unindexed503, explicit index, draft edit preserves approval, new approval replaces retrieval, old approval retained |
| Withdrawal/recovery | Tombstone commits before failed purge; search excludes it; exact delete retry cleans points; reindex incomplete markers survive failure and retry rebuilds; source ID remains reserved |
| Permissions and tenant access | Actual signed JWT production controllers cover eight human/machine operations, Reader/Operator/Administrator management denial, spoofed tenant/approval400, exact read; service filters tenant/publisher/applicability and rejects malicious stale/unknown returned IDs |
| Grounding | Page/source/publication/checksums/approval provenance comes from canonical chunk regeneration, low-score/empty explicit missing-evidence; malicious/conflicting text remains labelled untrusted without model generation/authorization |
| Replay/atomicity | Concurrent create/publication/index retries converge, competing drafts conflict, changed retry conflicts, exact publication retry precedes version validation; callback exception/cancel rolls back; detached snapshots cannot mutate committed state; reindex profile keys fail409 on mismatch |
| OpenAI adapter | Mock HTTP asserts fixed URL/model/dimensions/float, response model/count/index/size, malformed/zero/nonfinite vectors, outage/429/disabled and cancellation; no live paid request |
| Qdrant adapter | HTTP asserts tenant/approval/profile/applicability/revision allowlist and access filters; mismatched size/distance503 without destructive reset; mutation requires completed acknowledgement |
| Actual Qdrant | Separate Linux job tests real pinned image, collection creation, idempotent ingestion, replacement/stale exclusion, publisher filter, delete, rebuild after loss, low score and deliberate dimension mismatch |
| Contracts/build | Generated45 protected operations across33 paths; explicit DTOs and success/error examples, source allowlist74 inputs, baseline and PR-base structural compatibility plus7 breaking fixtures; no packages/locks added |

Actual self-review checked domain/application/infrastructure/controller/DTO/DI against spec scenarios. Review tightened Qdrant successful-write acknowledgements, made reindex replay profile-bound before readiness invalidation, replaced a misleading document-version reindex response with explicit indexedDocuments count, and bounded detail to draft/current approval instead of unbounded historical arrays. Source text is not trusted from Qdrant payloads. Collection names bind model/dimensions/chunk version; immutable canonical approval supplies actual citations. No GitHub approval is fabricated.

## Lifecycle and limits

Strict scoped OpenSpec validation preceded application code. Official archive and main strict validation will follow actual disposable CI acceptance; PR/check/merge evidence is recorded separately. All production originals/history/replay/audit still require a real approved durable IDocumentStore adapter with restart/crash/multiple-connection tests. Tests use memory only; Qdrant remains derived. Configured model/dimensions and separate live smoke remain DEC-004. Existing readiness is only Qdrant reachability, not working canonical/embedding retrieval. No generation/GraphRAG/MCP/agent investigation/notification/provisioning/deployment/vehicle software operation is delivered.
