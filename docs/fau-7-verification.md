# FAU-7 verification

Part of FAU-7. Verified 2026-10-20 on macOS arm64 with .NET SDK 10.0.402, runtime 10.0.13 and native ASP.NET OpenAPI 10.0.13.

## Evidence

- `dotnet restore FaultCode.slnx --locked-mode`: passed with updated committed API/integration package locks.
- `dotnet build FaultCode.slnx -c Release --no-restore`: passed, zero warnings/errors; generated `artifacts/openapi/faultcode-v1.json`.
- `dotnet test FaultCode.slnx -c Release --no-build`: 54 passed (4 Domain, 3 Application, 47 integration), zero skipped/failed.
- `dotnet format FaultCode.slnx --verify-no-changes --no-restore`: passed.
- `python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json` in an isolated venv with `scripts/requirements-contracts.txt`: OpenAPI and response examples validate; baseline comparison passes; seven breaking fixtures rejected and additive optional input accepted.
- `python3 scripts/check-build-inputs.py`: 28 explicit inputs checked, no missing allowlist entries or recognized credential patterns.
- `git diff --check`: passed.
- Official OpenSpec 1.14.3 strict change validation passed; official archive synchronized five requirements into api-contracts and archived to `openspec/changes/archive/2026-10-20-fau-7-api-first`.
- Official `validate --all --strict`: 20 main specs passed, zero failed.

Both DOTNET_ROOT and PATH referenced the SDK in Documents/Codex/.toolchains/dotnet. Calling only the absolute SDK without updating PATH caused build-time document generation to find the old system dotnet; using documented exports fixed that environment issue.

## Scenario mapping

| Requirement | Evidence |
|---|---|
| Public versioned foundation | ApiContractTests.Public_versioned_health_and_examples_have_stable_contracts |
| Query bounds/filtering and overflow | Pagination_and_filtering_are_bounded_and_accurate, Invalid_queries_return_safe_validation_problems |
| Not found and unknown versions | Unknown_resources_and_unimplemented_routes_return_problem_404 |
| Correlated/redacted 400/401/403/404/409/429/500 | Invalid_queries_return_safe_validation_problems, Central_pipeline_preserves_failure_status_and_redacts_internals, Correlation_is_generated_per_request_and_does_not_trust_inbound_header |
| Unsupported Accept headers | Status_and_exception_problems_ignore_unsupported_accept_headers |
| Public generated production-only contract | Openapi_is_public_and_contains_only_production_operations; Python spec/schema/example validation |
| Breaking contract detection | Seven negative compatibility fixtures; CI compares generated output to committed and PR-base baselines |
| Mutation conventions | docs/api-conventions.md; no persistent mutation is claimed or exposed |

Failure controllers exist only in the test assembly and are explicitly loaded by test factories. 401/403 tests verify the response contract, not authentication/authorization. Entra/token/policy behavior belongs to FAU-8/9. Idempotency/concurrency are documented requirements for future mutations; selecting a database and implementing durable replay remain outside FAU-7.

Local `docker info` still reports no daemon connection. No Docker UI permission bypass or hostname mapping change was attempted. Linux container smoke and Windows/Linux/macOS CI must be checked on the PR commit separately before merging. No tenant, deployment, purchases, real driver data or vehicle software operations occurred.
