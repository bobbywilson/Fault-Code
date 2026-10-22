# FAU-18 verification

Part of FAU-18. October21,2026: locked restore, zero-warning Release build, format verification and345 local tests passed (37Domain,3Application,305Integration), no reported skips.27 new engineering cases include actual production JWT middleware and a real InvestigationService unknown-fault->case->proposal->approval->approved KnowledgeService lookup flow with deterministic model provider. Ordinary opt-in live provider entries remain no-ops locally; no paid OpenAI call or live identity provisioning is claimed.

## Observed acceptance

| Requirement | Evidence |
|---|---|
| Unknown/ambiguous cases | Concurrent6 identical creates yield one case/audit/history; distinct keys still deduplicate by incident. Known routes, no persisted investigation, newly approved definition and unavailable storage cannot fabricate unknown. |
| Lifecycle and history | Assignment, immutable notes, legal transitions, proposal withdrawal, separate approval, reopen and second approval preserve prior proposals/publications/events. Direct Resolved transition is rejected; notification-state reopen conflicts until FAU-19 cancellation coordination. |
| Explicit authority | Real signed delegated/machine tests omit each of5 required approval grants independently, then succeed with all. Reader/Operator/Administrator write/publication denial, write/read separation, original owner and tenant filtering before counts/replay, strict fields/headers, safe correlated errors and production503. No model/MCP changes or new grants. |
| Publication and evidence | Proposal stores bounded authored content and exact expected fault/incident versions plus accessible current canonical document metadata. Approval repeats exact approved ontology/applicability/source access checks. Missing/inapplicable/unapproved/other-tenant/deleted/publisher-only/changed document evidence cannot publish. Same source version with a new publication/checksum requires a new proposal. |
| Atomic state/retry |6 concurrent approvals yield one immutable publication, case approval, resolved incident and Pending event. Full combined state is unchanged after failed commit and cancellation; retry succeeds. Changed knowledge/incident/ontology/document state prevents partial publication. Stable event IDs survive repeat reads/approval retry after failed dependencies; no delivery claim. |
| Model isolation | Actual existing Agent Framework strict-schema/no-tools tests pass unchanged. Engineering requests explicitly authorize engineering actions; historical investigation remains immutable Pending evidence. No model authority, tokens, secrets, shell/filesystem, network inventory or added tools. |
| Limits | Production engineering store503; combined test adapter is one-process copy-on-write, not durable. Existing stores must join one authoritative cross-process transaction, never independently copy/commit. DEC-001/003 restart/crash/retention/concurrent-connection acceptance remains pending. |

## Contracts and source review

59 protected operations,45 paths. Generated REST schemas/security/examples pass against both committed baseline and the actual FAU-17 Ubuntu CI artifact from backend run39608835938 (openapi-ubuntu-latest/openapi/faultcode-v1.json). All seven breaking fixtures reject; all five actual SDK MCP input/output schemas and success/error examples validate unchanged.95 explicit Docker build inputs pass credential-pattern scan. Compose configuration passes with a disposable demo validation key; local Docker daemon is unavailable, so real Linux container/Qdrant acceptance requires exact-source CI.

Actual self-review inspected new service/store/domain/DTO/controller/example code and modified DI/source allowlist, route/security inventory, immutable snapshots, scoped replay/resource checks, current unknown versus historical evidence, canonical source access/applicability, multi-store approval/rollback, reopen/event supersession and complete generated contract compatibility. Review found and fixed source evidence drift between proposal and approval by pinning exact document IDs/publications/checksums and rejecting changed evidence, with regression coverage. Each case resolves its own incident and records its driver identity; separate incidents require their own cases and authorization. Assignment is a work label, not a directory grant. No unreported bulk notification or external dispatch is implemented.

FAU-19 must revalidate current case/incident/approval, publication/applicability and consent, reject superseded events after reopen, and deduplicate stable event IDs. Pending means historical intent, never delivered or current authority. Test event reads prove retention/retry only. Existing lifecycle-only REST/MCP escalation and all existing FAU-17 proposal/model behavior remain compatible.

## Reproducible checks

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json PATH_TO_FAU17_CI/openapi/faultcode-v1.json
python scripts/check-mcp-contract.py artifacts/mcp-contracts.json
```

Official scoped strict validation preceded implementation. Official archive/main strict validation, exact-source three-OS backend/Linux container/real Qdrant CI and actual COMMENTED self-review are required for merge. Inspect signed-in author approval controls without inventing APPROVED. Linear remains open for Bobby's review. No account/provider/tenant provisioning, database selection, paid calls, deployment, notification delivery or network isolation changes.
