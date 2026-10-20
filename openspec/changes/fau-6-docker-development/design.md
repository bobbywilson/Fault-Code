# Design
## Context
See proposal.md. FAU-5 pins .NET SDK 10.0.402 and ASP.NET runtime 10.0.13. Docker Desktop is installed but its daemon is unavailable on this Mac. Native API occupies 5080. Existing main specs retain their broader requirements.
## Goals / Non-Goals
Provide Linux API/Qdrant packaging and repeatable isolated verification. Do not select a database, create identity/cloud resources, add a frontend before FAU-22 or implement FAU-14 RAG.
## Decisions
- Multi-stage Ubuntu Noble .NET SDK/runtime images with exact version tags, explicit source allowlist and built-in APP_UID. A small compiled health-probe target avoids adding curl to the runtime.
- Pin Qdrant 1.19.2 (official release API checked 2026-10-20). A derived image adds a Bash TCP health probe using upstream Debian bash/coreutils, bounded by timeout, because upstream does not guarantee curl/wget. Probe requests authenticated /collections; unlike public /readyz this detects invalid keys. Runtime Qdrant retains upstream storage ownership behavior; API runs non-root.
- Internal Compose network plus loopback API port 5081 avoids the existing native 5080 listener. Qdrant is reachable by qdrant:6333 only. Nginx proxy uses port 80 by default, configurable, and forwards to api:8080. Named Qdrant storage/snapshot volumes preserve local data.
- A container-only enabled Qdrant readiness check uses HttpClient with two-second timeout and generic status. Native foundation remains provider-independent; liveness never probes dependencies. No Qdrant SDK or embedding/model decision is needed.
- Local env file is ignored, example contains no key, Compose requires FAULTCODE_QDRANT_API_KEY. No OpenAI/Entra credentials are needed yet. Do not print rendered Compose config with real keys.
- Smoke script uses unique Compose project, ephemeral key and ports, synthetic collection/point. Test restart, stop/start dependency failure, bad credentials, down/up retention, destructive reset, proxy, non-root and build-context secret canary exclusion. CI runs on Ubuntu; existing native matrix remains.
## Risks / Trade-offs
- Daemon unavailable locally -> run static/native checks and Linux CI; keep observed runtime acceptance tasks pending until evidence exists. Do not archive prematurely.
- Exact tags can be republished -> pin verified multi-platform manifest digests; refresh deliberately and never use latest.
- Loopback HTTP is local development only; secrets appear in Docker environment inspection -> no production credentials or remote exposure.
- Port 80/resolver needs local access -> document and report actual activation independently; preserve Homestead mappings.
## Migration Plan
Build/start Compose from checkout with generated local key. Stop without volumes to retain data; explicit down --volumes deletes only this project's data. Rollback stops this Compose project; native development remains usable.
## Verification mapping
Safe packaging -> context scan/build canary/non-root and published-port assertions. Networking/readiness -> health wait, API ready/live, Qdrant stop/start, mismatched-key integration test. Credentials -> missing-key config test and API startup validation tests. Lifecycle -> synthetic point across restart/down/up and absence after reset. Hostname -> proxy Host-header smoke. Existing native tests/format remain mandatory. UI/agent tests are inapplicable because no UI or agent exists.

Image sources: https://github.com/dotnet/dotnet-docker/blob/main/README.aspnet.md and https://github.com/qdrant/qdrant/releases/tag/v1.19.2. Official registry manifests verified for amd64/arm64 on 2026-10-20.
