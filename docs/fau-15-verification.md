# FAU-15 verification

Part of FAU-15. October21,2026: locked restore, zero-warning Release build,253 local tests pass (37Domain,3Application,213Integration), zero reported skips; format verify,80 explicit Docker source inputs, Compose configuration and generated OpenAPI schema/examples/compatibility pass. One new live OpenAI smoke and the inherited external Qdrant acceptance are local/CI no-ops without their respective opt-in environment; these passing test entries are not live-provider verification. No paid request or local Docker daemon acceptance claimed.

Generated contract has46 protected operations across34 paths; baseline and full FAU-14 generated contract (PR11 backend run39530331481 openapi-ubuntu-latest artifact) both pass structural compatibility, with7 breaking fixtures rejected. Existing schemas/security remain unchanged; runtime endpoint/examples/DTOs are additive.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

## Acceptance and self-review

| Requirement | Observed deterministic evidence |
|---|---|
| Caller scoped advisory runs | Signed production JWT controller tests verify401/403, Reader own access, other owner404, exact human/machine scope/role combinations, request identity/instruction spoofing400. Service tests verify other tenant404, unavailable canonical store503, original caller retained and zero provider calls on denied resources. |
| Aggregate execution bounds | Tests verify deadline504, caller cancellation, preflight/aggregate reservation exhaustion, explicit transient retry count, no malformed repair retries, output truncation/refusal and usage/model validation. Actual SDK tests assert a single HTTP request after any unsolicited tool call and rate-limit/outage response. |
| Validated advisory schema | Tests reject malformed/missing/duplicate/unknown/wrong-case fields, foreign incident, invented classification/publication versions, arbitrary action/uncertainty and false escalation. Approved intake provenance exact; absent knowledge remains undetermined/review. No incident/history/work/audit mutation after successful or failed run. |
| Explicit activation and safe failures | Disabled503 and invalid configuration startup tests; SDK transport/status/malformed envelope/refusal tests show safe codes, fixed endpoint and no secret/provider-body leakage; strict native schema/output cap/no tools asserted in HTTP request. Response buffering fails safely above64KiB. |
| Protected advisory runtime contract | OpenAPI tests inventory46 protected operations, exact policy metadata, additive DTOs/examples and unchanged previous complete contracts. Compose validation and explicit Docker source scan80 pass. |

Actual self-review covered all new source, DI/configuration, locks, inward dependency tests, endpoint resource access, errors, OpenAPI examples and spec scenarios. It caught framework automatic function invocation (mock returned41 requests), removed all default decorators with UseProvidedChatClientAsIs, disabled SDK retries, set native OpenAI strict schema explicitly, made JSON property matching exact, added a bounded HTTP response layer and verified13 actual-framework/SDK provider tests.42 agent-focused test cases include the gated live no-op. No GitHub approval is fabricated; review controls are inspected separately in signed-in browser.

Strict scoped OpenSpec validation preceded implementation. Official archive must synchronize four added agent-runtime requirements and one API requirement, then all20 main specs pass strict validation. Exact-source Linux container/actual Qdrant and three-OS backend CI are required before authorized merge; the inherited actual Qdrant test uses mock embeddings. Linear remains open for personal review. Model snapshot/dedicated secret/live smoke and durable incident storage remain activation dependencies. No model/embedding selection, credentials, tenant/cloud provisioning, purchase, deployment, network-isolation change, notification or vehicle action.
