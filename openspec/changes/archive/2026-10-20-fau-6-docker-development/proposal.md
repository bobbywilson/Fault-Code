# Proposal
## Why
Part of FAU-6. The merged FAU-5 backend needs reproducible Linux packaging and local Qdrant infrastructure before retrieval work can begin.
## What Changes
- Add pinned multi-stage Linux API and Qdrant images, health/readiness checks, private networking and named persistence volumes.
- Supply external local credentials, bounded loopback API/proxy exposure, startup/reset guidance and automated container smoke tests.
- Prepare the requested faultcode.test reverse proxy without changing other projects or requiring cloud services.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `development-infrastructure`: specify observable packaging, dependency readiness, credential failures and lifecycle verification.
## Impact
Docker/Compose, API health composition, integration tests, Linux CI and local-development documentation. ADR-008 applies; FAU-5 is merged. No relational database, cloud, identity or RAG implementation. Angular profile follows FAU-22 when frontend source exists.
