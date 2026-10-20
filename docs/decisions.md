# Architectural decisions

Approved 2026-10-20; user requirements are the authority. This file records decisions, not implemented features.

| ID | Decision | Implication |
|---|---|---|
| ADR-001 | ASP.NET Core/C# API-first | All operations, including Angular administration, have versioned API contracts and server authorization. |
| ADR-002 | Angular/TypeScript, Spartan UI, Tailwind CSS | UI consumes API exclusively; no direct storage, vector database or AI access. |
| ADR-003 | Dedicated Microsoft Entra ID Free tenant | Completely separate from LALAI; OAuth 2.0/OIDC; authorization code + PKCE for browser; client credentials/app roles for machines. Do not create tenant during setup. |
| ADR-004 | Microsoft Agent Framework in C# and OpenAI | Bounded agent execution, typed outputs, evidence and permission-aware MCP tools. No alternate orchestrator/provider silently substituted. |
| ADR-005 | Qdrant and traditional RAG | Approved documents, OpenAI embeddings, vector retrieval, citations and rebuildable index. No GraphRAG or graph-database assumption. |
| ADR-006 | Provider-independent observability | Standard OpenTelemetry, logging and metrics; Datadog adapter primary and Sentry replaceable. Vendor SDKs stay outside domain/application logic. |
| ADR-007 | OpenAPI and Mintlify | Generated contracts; public documentation is readable without login; protected Try It requires valid Entra authorization. No Swagger UI frontend. |
| ADR-008 | Docker Compose and Linux | Reproducible local services and Linux images; cloud target unspecified; no infrastructure provisioned now. |
| ADR-009 | Synthetic automotive data only | EPS, steer-by-wire and steering-related ADAS; no proprietary data or other-company branding. |
| ADR-010 | No vehicle software deployment | Approved patch metadata can result in dealership recommendation, never vehicle installation/control. |
| ADR-011 | OpenSpec/Linear/GitHub/Codex responsibilities | GitHub versions authoritative specs; chats and environments are replaceable; Linear references requirements and tracks work. |
| ADR-012 | Explicit engineering approval | Draft/unapproved remedies never become driver guidance; preserve author, approval and evidence versions. |
| ADR-013 | No automatic issue closure | Use Part of FAU-ID references; do not enable closing/merge automation without approval. |
| ADR-014 | Private existing repository | bobbywilson/Fault-Code, preserve history; no duplicate repository, invites or paid subscriptions. |

## Unresolved decisions (approval required)
- DEC-001: relational database/provider, schema/migration tooling and durable outbox implementation. Do not assume PostgreSQL or another engine.
- DEC-002: external notification channel/provider and contact/consent policy. Local test sinks may be specified; no external provisioning.
- DEC-003: deployment target, persistence/object-storage service, retention/backups and operating budget. Linux/Docker does not select hosting.
- DEC-004 (remaining): Angular/agent-framework/model/embedding versions; record compatibility and model dimensions before use. .NET portion resolved below. OpenSpec CLI remains pinned to 1.14.3.
- DEC-005: paid-tier limitations (Mintlify interactive OAuth/hosting, Datadog ingestion, Entra features). Prefer free capabilities; request approval before cost or architecture changes.

Secrets belong in environment/secret stores, not specifications, logs, repository files or prompts. No real driver information is part of this setup.

## FAU-5 framework version decision (2026-10-20)

User requested the latest stable version and authorized installing required tooling. Use .NET 10 LTS, C# 14 and SDK 10.0.402; matching ASP.NET Core runtime is 10.0.13. `global.json` pins the feature band with latestPatch roll-forward and previews disabled. Microsoft support policy lists .NET 10 support through November 2028: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core. SDK/version/checksum source: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json. No Angular, model or agent SDK version is selected by this decision.
