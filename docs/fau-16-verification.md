# FAU-16 verification

Part of FAU-16. October 21, 2026: locked restore, zero-warning Release build and 276 local tests passed (37 Domain, 3 Application, 236 Integration), zero reported skips. The 23 new MCP-focused cases use the actual SDK/client and production JWT middleware with demo keys. Existing live OpenAI and external Qdrant tests remain ordinary local no-ops without explicit opt-in; those entries do not establish live provider verification.

Formatting, 82 explicit Docker source inputs, Compose configuration, all five actual SDK input/output schemas and success/error examples, generated REST schema/examples and compatibility against both the committed baseline and the complete FAU-15 generated artifact passed. Seven deliberately breaking REST fixtures were rejected. FAU-15 contract source: backend run39599417660, openapi-ubuntu-latest/faultcode-v1.json. FAU-16 adds one REST operation (47 protected, 35 paths); the separate authenticated protocol endpoint is verified through SDK discovery and specialized route coverage.

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build --logger trx
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-mcp-contract.py artifacts/mcp-contracts.json
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

## Acceptance and actual self-review

| Requirement | Observed evidence |
|---|---|
| Maintained typed MCP transport | Official ModelContextProtocol.AspNetCore2.3.0 locks, stateless Streamable HTTP; actual SDK discovers five tools under explicit grants, input/output schemas and hints. An actual SDK test emits all five schemas and success/error examples; Python Draft202012Validator validates them and rejects extra/missing input fixtures. |
| Original caller and permission boundary | Actual signed human/machine middleware tests filter discovery and reauthorize every invocation. Changing token across requests changes available tools. Own-read Reader sees only trusted intake ownership; cross-owner/tenant404, absent/invalid JWT401, wrong permission403. No new Entra catalog grants or privileged service union. |
| Approved fault/retrieval provenance | Actual tools return publication/classification/reference versions and canonical page/checksum/approval citations with untrusted-evidence labels. Malicious source text is returned only as evidence and produces no mutation. Absent knowledge404 differs from provider/storage503. |
| Explicit intent and atomic mutation | Missing/mismatched tool/incident/version/status/key intent403; arguments/metadata cannot supply identity or intent. Six simultaneous identical status calls produce one version/history/audit/replay. Changed payload/stale version/illegal edge409. Failed commit leaves no partial state/audit/replay; retry succeeds once. Machine escalation and REST-to-MCP replay share the exact service operation. |
| Safe bounded outcomes | Duplicate JSON, case/type/null/required/extra/ID/range/oversized input fails before service execution; body depth/size and trailing-slash/case routing tests cover parser guards. Origin header403. Downstream deadline504, cancellation reaches the service with no retries; SDK cancellation can surface499. Fixed safe errors exclude injected secret text and logs exclude arguments/intent/source payload. |
| Compatible application boundary | Additive REST lifecycle escalation has exact escalations.write security, strong ETag/key, safe errors and actual JWT tests. Production unavailable adapters remain503. Route coverage still denies unannotated/bare endpoints. Existing Agent Framework/OpenAI mocked-HTTP tests pass with the new API-process AI.Abstractions10.11.0 dependency. |

Actual self-review inspected all new MCP source/DTO schemas, per-request original HTTP context, exact policy conjunctions, trusted service reads, independent intent binding, mutation replay namespace, rollback, safe logs/errors, body parsing bounds, SDK package locks, Docker allowlist, generated contracts, CI and requirement/scenario traceability. Review caught the trailing-slash parser guard bypass risk and wrong-type numeric validation, then added explicit guards and negative tests. It also verified that cancellation must be asserted at the downstream service because SDK transport499 differs from a local cancellation exception, and added actual schema validation for null error data as well as successful structured output.

No tools are connected to the model. Existing MaxToolCalls0, UseProvidedChatClientAsIs and unsolicited-tool rejection remain unchanged. Full engineering case management remains FAU-18; the escalation adapter records only the existing lifecycle marker. Test-only one-process stores are not durable production storage. Production canonical storage, live Entra setup, model/embedding selection and authorized live activation remain separate dependencies. No paid calls, provider/account/tenant provisioning, credentials, deployments, outbound network changes, notifications or vehicle operations were performed.

Strict scoped OpenSpec validation preceded implementation. Official archive synchronizes four MCP, one authorization and one additive API requirement, followed by all-main strict validation. Exact-source three-OS backend CI plus Linux container/actual authenticated Qdrant checks are required before merge; real Qdrant acceptance uses deterministic mock embeddings. Local Compose configuration is not local Docker runtime acceptance. GitHub self-review is submitted as an actual comment review; signed-in author approval controls are inspected without fabricating an APPROVED state. Linear stays open for personal review.
