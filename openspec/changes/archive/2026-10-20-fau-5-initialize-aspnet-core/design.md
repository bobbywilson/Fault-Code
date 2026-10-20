# Design

## Context
See proposal.md. Existing solution-architecture requirements mandate component separation and provider-free tests. No application source existed. FAU-24 setup dependency is represented by merged PR #1. FAU-5 and its cleared setup dependencies were verified in authenticated Chrome; connector access targets another workspace.

## Goals / Non-Goals
Goals: buildable solution, inward dependencies, a usable HTTP host, validated settings and meaningful tests from the first implementation ticket.
Non-goals: business endpoints, persistence, Angular scaffolding, authentication, OpenAPI rollout, Docker Compose and cloud setup; those have their own tickets.

## Decisions
- Use latest stable .NET 10 LTS / C# 14, SDK 10.0.402, approved by user on 2026-10-20. Official Microsoft release metadata and SHA512 verified. Pin global.json with latestPatch roll-forward and no previews. Framework runtime 10.0.13 accompanies this SDK. Older installed .NET 7 is unsupported; preview .NET 11 is not a stable baseline. Other DEC-004 versions remain unresolved.
- Separate FaultCode.Api, Application, Domain, Infrastructure assemblies. Domain has no project/package dependencies; Application references Domain; Infrastructure references Application; API composes Application/Infrastructure. Framework DI stays in composition/adapter code, domain remains plain C#.
- FoundationStatus application service uses an IRuntimeEnvironment adapter to demonstrate provider-independent replacement without inventing fault workflows. The ticket explicitly requires repository and unit-of-work contracts: a domain IAggregateRoot identity contract, application IRepository<T> lookup/add and IUnitOfWork save contract. No implementation, database, ORM, transaction semantics or migrations are selected.
- Use MVC controllers as the API composition convention with no Razor views; /health/live uses built-in health checks and reports liveness only. All business routes remain absent until their scoped tickets.
- Required ApplicationName uses options validation on startup; appsettings contains a non-secret default. Test absent/whitespace settings in an isolated API process to avoid WebApplicationFactory deferred-host disposal races; test environment override through WebApplicationFactory. No unused provider secrets are demanded.
- Separate xUnit domain architecture and application unit-test projects plus WebApplicationFactory HTTP integration tests. Test assembly and project references to prevent forbidden dependencies, real startup failure, liveness response and DI adapter replacement. Pin NuGet versions and commit lockfiles. CI tests on Linux, Windows and macOS; local verification is native macOS only.

## Risks / Trade-offs
- Liveness is not readiness → document no external dependencies are probed.
- Four projects add initial structure → small interfaces with actual executable tests; no speculative domain models.
- SDK installation needs external download → checksum-verified official Microsoft artifact; install in user tool directory without removing old SDKs.
- Windows/Linux CI results are separate from native macOS verification → report observed results only.

## Migration Plan
No data migration or deployment. Restore/build/test new solution, run locally; revert this commit to remove the foundation. No tenant, service or database is provisioned.
