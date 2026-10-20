# FAU-5 verification

2026-10-20. Part of FAU-5. Native Apple Silicon macOS verification using SDK 10.0.402 and runtime 10.0.13; Microsoft SDK SHA512 verified before installation. No database, identity or model credentials used.

## Observed results

- `dotnet restore FaultCode.slnx --locked-mode`: passed for all seven projects.
- `dotnet build FaultCode.slnx --configuration Release --no-restore`: passed, zero warnings/errors.
- `dotnet test FaultCode.slnx --configuration Release --no-build --logger trx`: 16 passed, zero failed/skipped (Domain.Tests 4; Application.Tests 3; IntegrationTests 9).
- `dotnet format FaultCode.slnx --verify-no-changes --no-restore`: passed.
- `dotnet run --project src/FaultCode.Api --configuration Release --no-build`: host started on localhost:5080. Live GET /health/live returned 200/Healthy; /api/v1/vehicles returned 404.
- `git diff --check`: passed. Reviewed added configuration/source: no credentials, real driver data, database packages or external provider registration. Only non-secret application/logging/local URL defaults are committed.
- Live FAU-5 title/scope and cleared FAU-24/FAU-26 dependencies read through authenticated Chrome. PR #1 confirmed merged as e13f2e9 using GitHub CLI.

Commands above use the SDK on PATH as described in local-development.md. The verified local SDK is at `/Users/bobbywilson/Documents/Codex/.toolchains/dotnet/dotnet`.

## Acceptance traceability

| Requirement/scenario | Evidence |
|---|---|
| Component separation | Domain.Tests ArchitectureTests enforce each project's inward references and forbid provider packages/framework references in inner layers |
| Adapter replacement without credentials | Application.Tests CompositionTests and real-host Host_composition_accepts_a_replacement_adapter |
| Missing/blank configuration | Three real-process startup failure cases plus actionable key/environment-variable guidance assertions |
| Invalid values omit supplied contents | Real-process overlength option rejection and captured-output redaction assertion |
| Environment precedence and DI resolution | Real environment variable overrides appsettings and reaches resolved application service |
| Runnable host and liveness | WebApplicationFactory smoke test plus live Kestrel HTTP check |
| No unsecured business data | HTTP 404 for unimplemented vehicle route |
| Persistence decision remains open | Typed cancellable contracts, signature test and real-host assertions that no persistence provider is registered |
| Three business workflows remain specified | Existing fault-investigation spec covers A/B; engineering-escalation spec covers C. No claim of implementing those workflows in FAU-5 |

The Domain test project currently checks architecture only: domain fault behavior belongs to later tickets. Persistence contracts have no production implementation. Liveness does not test downstream readiness. Windows/Linux execution is provided by the CI matrix and must be reported from actual CI results, not inferred from macOS tests. No deployment, tenant/cloud provisioning or Linear closure is performed.

## CI portability correction

The first remote macOS run exposed a WebApplicationFactory failure-path race: a disposed deferred service provider could hide the original startup validation exception. Invalid-startup tests now execute the actual API process with isolated configuration and assert its exit code and actionable, redacted diagnostics; WebApplicationFactory remains for successful HTTP/DI tests. This strengthens the observable startup test without accepting the disposal error as success.
