# Proposal

## Why
FAU-5 needs a runnable backend foundation so subsequent API, identity and domain tickets can be implemented and tested independently. The repository currently contains specifications only.

## What Changes
- Establish a pinned, supported ASP.NET Core/C# solution with API, application, domain and infrastructure projects.
- Define vendor-independent repository and unit-of-work contracts without a persistence implementation.
- Add dependency injection composition and validated external configuration with no database vendor selection.
- Add meaningful architecture/unit and HTTP integration tests, a liveness endpoint and reproducible developer commands.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `solution-architecture`: specify runnable host, startup validation and independently replaceable component acceptance behavior.

## Impact
Part of FAU-5. Affects new src/tests, tooling and developer documentation; ADR-001, DEC-004 (.NET only). Docker Compose, API business contracts, identity, database, AI and telemetry integrations remain separate tickets. User explicitly authorized implementation and latest stable .NET on 2026-10-20.
