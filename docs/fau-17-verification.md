# FAU-17 verification

Part of FAU-17. October21,2026: locked restore, zero-warning Release build and318 local tests passed (37Domain,3Application,278Integration), zero reported skips. The42 investigation-focused cases use deterministic demo fixtures, real application read services, production JWT middleware and an actual Agent Framework/OpenAI mocked-HTTP request. Ordinary live OpenAI and external Qdrant test entries remain local no-ops without explicit opt-in; they do not establish live provider verification.

Generated REST OpenAPI/examples pass compatibility against both committed contracts/openapi-v1.json and the complete FAU-16 Ubuntu generated artifact from backend run39605770259 (openapi-ubuntu-latest/openapi/faultcode-v1.json). Seven deliberately breaking fixtures are rejected. Inventory:49 protected operations,36 paths. All five unchanged actual MCP SDK schemas and success/error examples also pass validation.88 explicit Docker source inputs pass the credential-pattern scan. Compose config passes with a disposable demo validation key; the local Docker daemon is unavailable, so Linux smoke and actual Qdrant acceptance require exact-source CI.

## Acceptance and actual self-review

| Contract | Observed evidence |
|---|---|
| Grounded routes |15 deterministic routing fixtures A/B/C,0.85 threshold,0.8/0.5 scores, missing/conflicting/injected evidence, unapproved/inapplicable patches, missing rule/publication, wrong source/classification versions. Exact route/action/provenance assertions; zero unsupported selected remedies. Different individually approved remedy text has an additional abstention regression. |
| Isolated model |Fixed purpose-selected schema/instructions, no registered tools/automatic invocation; actual SDK native strict schema/no tools/fresh two-message conversation. Inputs contain only curated demo metadata/rule/citations; no actor/client/tenant/driver/contact/approver/secret fields. Model cannot promote deterministic route or generate guidance; review can downgrade. |
| Runtime bounds |Nine malformed/provenance/injection model-output fixtures; tool/length/refusal/usage/model failures, aggregate token rejection, bounded transient retry, cancellation/deadline rollback. Existing SDK retry/buffer/refusal/cancellation tests still pass. No live call or model selected. |
| Atomic history/replay |Six identical concurrent requests create one result/proposal/audit/replay; committed replay survives disabled model and failed knowledge dependencies. Changed expected version and stale new request409. Failed commit leaves empty state; retry succeeds. Rollback does not claim exactly-once provider billing. |
| Permissions and disclosure |Real signed delegated/machine policy tests, write/read separation, missing scopes, own-read restrictions, hidden resources, strict headers/body, pagination and production503. Cross-tenant/malformed caller/original-owner checks run before model/replay. History filters original actor/client to protect publisher-only evidence before count/page. |
| Explicit limits |Production UnavailableInvestigationStore503; test store is one-process only. Future adapters must coordinate authoritative incident/knowledge/ontology/documents across async transaction/commit/processes and pass crash/restart/retention acceptance. Proposed notification/referral/escalation stays Pending; no dispatcher, incident transition, engineering case or vehicle operation. |

Actual self-review inspected each new source/DTO/store contract, existing MCP/read service reuse, policy conjunction, scoped replay access, current404 versus outage, pinned taxonomy input, citation approval/access, rule/remedy applicability, model projection/schema, aggregate reservations, cancellation/rollback, explicit Docker inputs and generated contract diff. It found and corrected cross-actor historical source leakage by restricting original actor/client, differing individually approved remedy conflicts by requiring all citations to support the same selected text, and a missing explicit historical evidence policy on API output. Regression tests cover these findings. No privileged model capabilities were added.

## Reproducible checks

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json PATH_TO_FAU16_CI/openapi/faultcode-v1.json
python scripts/check-mcp-contract.py artifacts/mcp-contracts.json
```

Official scoped OpenSpec validation preceded implementation. Official archive/main strict validation and exact-source three-OS backend/Linux container/real Qdrant CI are required for merge. Review is an actual self-review comment; signed-in author approval controls must be inspected without fabricating APPROVED. Linear stays open for Bobby's personal review. No provider/account/tenant/credential provisioning, network isolation changes, notifications, deployment or paid model calls.
