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
- DEC-004 (remaining): model/embedding versions; record compatibility and model dimensions before use. .NET portion resolved below. OpenSpec CLI remains pinned to 1.14.3.
- DEC-005: paid-tier limitations (Mintlify interactive OAuth/hosting, Datadog ingestion, Entra features). Prefer free capabilities; request approval before cost or architecture changes.

Secrets belong in environment/secret stores, not specifications, logs, repository files or prompts. No real driver information is part of this setup.

## FAU-5 framework version decision (2026-10-20)

User requested the latest stable version and authorized installing required tooling. Use .NET 10 LTS, C# 14 and SDK 10.0.402; matching ASP.NET Core runtime is 10.0.13. `global.json` pins the feature band with latestPatch roll-forward and previews disabled. Microsoft support policy lists .NET 10 support through November 2028: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core. SDK/version/checksum source: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json. No Angular, model or agent SDK version is selected by this decision.

## FAU-15 SDK compatibility (2026-10-21)

Pin Microsoft.Agents.AI.OpenAI1.25.0 / OpenAI2.14.0 / System.ClientModel1.15.0 in Infrastructure, with locked transitive versions and actual net10.0 mocked-HTTP framework tests. This implements ADR-004. Model snapshot and embedding model/dimensions remain unselected under DEC-004; runtime defaults disabled and requires an explicit dated model/secret before activation. See agent-runtime.md. No paid call, credential, provisioning or network-isolation change.

## FAU-16 MCP compatibility (2026-10-21)

Official ModelContextProtocol.AspNetCore2.3.0 provides stateless Streamable HTTP at /api/v1/mcp. Its API/test process resolves Microsoft.Extensions.AI.Abstractions10.11.0; actual existing Agent Framework/OpenAI HTTP tests are retained to verify compatibility. Application/Domain remain vendor-free. SDK discovery schemas and actual invocation examples are validated separately from generated REST OpenAPI. No model tools, new Entra grants, credential/provider/hosting selection or outbound activation. See mcp-tools.md.

## FAU-21 OTLP compatibility (2026-10-21)

Pin official OpenTelemetry.Exporter.OpenTelemetryProtocol1.20.1 with locked SDK/API dependencies in Infrastructure, verified against net10.0. Datadog uses standard OTLP HTTP/protobuf to an explicitly configured same-host Agent; no Datadog SDK/API key is coupled into Domain/Application. Dedicated sanitized trace/metric/log pipeline excludes automatic framework/auth/HTTP/GenAI capture; bounded explicit configuration and defaults remain disabled. Optional Sentry retains the interchangeable adapter boundary and is not enabled by this change. This implements ADR-006 without resolving DEC-003/005 or authorizing remote ingestion/network/cost changes. See observability.md and fau-21-verification.md.

## FAU-22 browser compatibility and Linux delivery (2026-10-21)
Resolve the Angular portion of DEC-004 to Angular/CLI22.2.3, TypeScript6.0.2, Node22.22.3, RxJS7.8.x, Spartan brain1.8.0, Tailwind4.3.3, MSAL Angular6.2.3/browser5.25.1. Exact versions and peers are verified by the frontend lockfile and production compilation. Angular22 requires Node22.22.3+ and TypeScript6; Spartan1.8 supports Angular21/22. See https://angular.dev/reference/versions and https://spartan.ng/documentation/version-support . MSAL uses PKCE redirect with tab-scoped session storage (memoryStorage does not support redirect), no SPA secret or localStorage; see https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/lib/msal-browser/docs/caching.md .

Per the user's explicit October21 direction, ADR-008 Linux/Docker is the delivery platform including on his Mac; native Windows/macOS CI is optional and not a release condition. Existing cancelled native checks are not passing evidence. No hosting or database decision follows. Vite8.3.5 override patches the Angular build dependency within its compatible8.x range; no dev server is shipped in the runtime image.

## FAU-23 demo hosting and branching decision (2026-10-21)

The user authorized a temporary demo production environment on the existing LALAI Linux host and the already-owned GoDaddy domain faultcodeai.com, with no additional server/subscription. Provider and host console identify lalai-staging-runner,134.209.215.92. This resolves only DEC-003 host/domain selection, not durable storage, retention/backups, operating changes affecting shared services or other provider decisions. Preserve LALAI and Simone; request approval before shared-impact changes.

Feature -> development PR/CI -> main PR/CI -> main-only automatic deployment is the requested branch flow; development is not a staging deployment. Main deployment must be serialized and source-verified. Certificate/key pairs for local development and Linux production are separate; production rotation must be automated and verified. Prior no-host/provisioning setup restrictions are superseded only for this explicit authorized scope. DEC-001 and DEC-002 remain unresolved; no database or notification vendor is silently chosen.

## FAU-22 supplied component requirement (2026-10-21)

The user clarified ADR-002: use the official spartan.ng styled Helm components throughout administration and Lucide icons, rather than only Brain button behavior plus custom controls. Nova/neutral is the supplied visual style. Shared page/form/result adapters compose these primitives; no custom visual component library is maintained. All existing routes and API/identity policies remain. Deployment is explicitly paused during this UI work.
