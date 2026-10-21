# FAU-19 verification

Part of FAU-19. October21,2026: locked restore, zero-warning Release build, format verification and381 local tests passed:37Domain,3Application,341Integration, zero reported skips.36 new notification tests exercise real signed JWT middleware and real InvestigationService A/B/C routing (deterministic mock model), C engineering publication, explicit queue, original recipient and local acknowledgement. No external delivery or paid model execution is claimed.

## Acceptance and source review

- A/B use current approved exact applicable remedy and unchanged investigation-v1 canonical support; B renders dealership and exact patch metadata with no installation claim. C runs actual UnknownFault investigation, case/proposal/approval/publication/event then local notification. Original proposals/events remain immutable Pending history.
- Six distinct-key concurrent consumes yield one logical message. Scope binds tenant/actor/client/operation/resource/version/payload; access before replay; completed replay before changed evidence. Cross-tenant/owner/actor denial and page overflow checked.
- Current fault, ontology, document, publisher-only access, case/latest approval/event and incident changes block dispatch. Disabled/missing preferences, closed association, deactivation/reassignment and changed contacts cannot send/redirect. Existing email flag is never consent.
- Real signed human/machine tests omit each of five write grants independently. Operator alone lacks reads; Reader/Engineer/Administrator cannot write without exact role/scope. Preference own-read/all-read, Operator/machine writes, strict input/headers, correlated safe errors and unconfigured503 verified.
- Pending never means Sent. Local acknowledgement alone records Sent/Notified. Failure, invalid acknowledgement, actual5second deadline, caller cancellation, bounded backoff, five-attempt cap, concurrent claims/cancellation, lease expiry and fenced late acknowledgement covered. Failed acknowledgement commit leaves recoverable lease; retry uses stable key and local deduplication without an exactly-once external claim.
- Full combined state remains unchanged after failed enqueue/preference commit and precommit cancellation. Cancel/supersede preserves acknowledgement/history and coordinates incident to Resolved before engineering reopen; old retained event cannot redeliver.
- Actual source review inspected domain/application/store/DI/controller/DTO/examples/route inventory, atomicity and isolation. Found/fixed recovery replay omission, stale preference ETag renewal and public attempt ID sharing internal lease token. API now uses explicit response DTOs; attempt ID and private fencing token are distinct, with regression coverage. No model/MCP schema, execution capability, credential or network-isolation change.

## Contracts and tooling

67 protected operations/51 paths (8 additive operations/6 paths). Complete generated REST is compatible with committed baseline AND actual FAU-18 Ubuntu CI artifact at /Users/bobbywilson/Documents/Codex/2026-10-21/fault-code-fau-18/work/fau18-ci-contracts/openapi/faultcode-v1.json. Seven breaking fixtures reject; actual five SDK MCP schemas and success/error examples validate unchanged.102 explicit Docker inputs pass credential-pattern scan. Compose config passes using a disposable synthetic key. Local Docker daemon unavailable; actual container/Qdrant acceptance requires exact-source CI.

Official strict scoped OpenSpec validation preceded implementation. Official archive and all-main strict validation complete the lifecycle; exact-source CI and COMMENTED self-review/disabled author Approve evidence are checked before authorized normal merge. Keep Linear open for Bobby review.

## Limits and activation

Production notification storage/delivery503 until approved DEC-001/002/003 adapters. One-process combined test store and local sink are not durable storage. Existing adapters must join one authoritative transaction across all contexts and pass restart/crash/concurrent-connection/retention acceptance. External delivery cannot be atomic with database commit; future provider needs documented stable-ID deduplication/reconciliation and approved contact/consent policy. An already claimed call cannot be revoked by later preference changes; no external adapter is enabled. No background worker, account/provider selection, tenant/cloud provisioning, deployment, vehicle operation or live OpenAI call.

## Reproduce

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json PATH_TO_FAU18_CI/openapi/faultcode-v1.json
python scripts/check-mcp-contract.py artifacts/mcp-contracts.json
```
