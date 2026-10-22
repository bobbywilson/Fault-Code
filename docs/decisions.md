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
| ADR-009 | Demo automotive data only | EPS, steer-by-wire and steering-related ADAS; no proprietary data or other-company branding. |
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

## FAU-22 fault-code ontology presentation (2026-10-21)

The user clarified that Ontology should display the fault-code domain and its incident, vehicle, driver and notification relationships in a table, rather than offer catalog creation on entry. This changes administration presentation to a read-only model reference. It does not replace or relax the approved versioned taxonomy, classification/rule validation, engineering publication APIs or durable-storage decisions.


## Later October 21 workflow and demo data clarification

The latest user instructions supersede conflicting earlier editable-ontology/full-admin-page requirements: use a read-only ontology table; remove Fault definitions, Documents, Engineering, Agent activity and Operational health pages from navigation while preserving backend processing services. Vehicles lists associated drivers and submits fault events; Drivers lists emails. Microsoft 365 is the selected email provider (DEC-002 provider selection is resolved; mailbox permissions, consent and actual delivery remain unverified). Exact approved-code Qdrant lookup leads to agent-drafted email, while unknown faults create investigations and resolution triggers follow-up. See `docs/audit/simplified-workflow.md` for the complete clarification.

The user selected demo Chevrolet Silverado/GMC Sierra demo vehicles and fictional drivers. `DemoFleetFactory` and `DemoFleetSeeder` prepare four vehicles, four drivers and six many-to-many links, with reserved `.invalid` emails and notifications disabled. They create no real Entra accounts or consent records and send no email. Seeding is explicit, validated, audited and repeatable through the existing application service, never an in-memory production fallback. DEC-001 remains pending: no relational engine or EF Core provider is selected by this seed implementation. Real persistent loading/restart verification must follow database approval and adapter implementation.


## DEC-001 approved: SQL Server (2026-10-21)

The user explicitly instructed: "Use sqlsver for the database." Adopt the previously recommended SQL Server 2025 Express free edition with Microsoft EF Core SQL Server 10.0.13 and migrations. Database-engine selection is resolved. Fleet persistence is the first normalized adapter; other canonical stores/outbox remain incomplete and must not be described as durable. Use an isolated Fault Code database/volume/network, separate least-privilege application and migration credentials, explicit migrations/seeding and no startup auto-provisioning or memory fallback. The SQL overlay is local only; deployment remains paused and shared-server installation is not approved by this database choice.

Local live testing is blocked by capacity: the existing x86 Linux VM reports1966MiB total RAM and currently hosts other development services. SQL Server requires at least2GiB and the proposed container cap is3GiB. No VM restart/resize or shared-host installation has been performed. New EF tooling brought a vulnerable transitive Pkcs9.0.11 dependency; explicitly pin System.Security.Cryptography.Pkcs10.0.13 instead of suppressing NuGet security warnings.


### Local capacity approval and architecture correction

The user subsequently approved the local VM resize/restart. Parallels and Homestead now configure4GiB, and nginx/mysql/php8.3-fpm were active afterward. Fresh inspection showed the VM/Docker engine are ARM64, correcting the earlier unverified x86 assumption. SQL Server's official x64 container failed to execute; it was stopped without deleting its dedicated volume. Local live SQL loading remains blocked by CPU architecture, not database approval or RAM. Use a real x64 GitHub CI integration test for verification; its fixture database is not local production/demo data. Shared hosted-server installation remains separately unapproved and deployment paused.


### User-authorized existing SQL container takeover

The user then supplied Docker Desktop's existing `sql2022` and explicitly said its old data was not needed and to take it over. The instance runs SQL Server2022 Developer16.0.4225.2, free for local development/testing. This approved local-container reuse supersedes local capacity-blocked status for fleet verification; no claim of native ARM support or Developer production licensing is made. Dedicated FaultCode databases/scoped logins/migration/seeds are verified, including actual SQL restart and real Entra Angular reads. Hosted SQL Server2025 Express remains the intended free production edition and deployment stays paused. The existing container has no mounted persistent volume; container-removal durability/backup remains a deployment prerequisite.


## Demo-only fleet and Mailpit clarification

The user requested test data only (no vehicle/driver management UI), plain names, separate email columns, left-aligned tables, example.com addresses and one driver per vehicle on the demo table. Seed upgrades preserve original v1 replay inputs, update only matching demo profiles, and end superseded associations rather than deleting history. Current main data:4vehicles/4drivers/4active links,7history intervals,22audit events; rerun creates no additional mutations.

Mailpit is now the selected local/Linux demo capture provider, superseding Microsoft365 delivery for the test environment. Use official pinned v1.31.6, private UI/SMTP, own network/volume/resource limits and no relay. Local real HTTP capture adapter and SMTP tests passed. Linux Compose overlay is prepared but not installed while deployment is paused. Future real external email still requires consent, directory/mailbox permissions and an approved delivery transport; capture is not external delivery. Stable Message-ID is not durable exactly-once dispatch.

## Final demo email and presentation clarification — October21,2026
Microsoft365 email is cancelled. Mailpit capture is required locally and on Linux, with no relay and example.com recipients; Linux Compose includes it by default, but deployment stays paused. Datadog remains mandatory and actual ingestion remains an acceptance gap. Navigation is Incidents, Vehicles, Drivers, Notifications, Users & roles, Ontology. Users lists API-backed names/roles. The account uses a Spartan photo-avatar dropdown; protected/authenticated pills and standalone sign-out are removed. The user approved the dedicated Fault Code Operator grant and supplied the avatar image. No Graph scope or paid service was added.

## API reference and testing clarification
The user explicitly approved a separate Swagger-style OpenAPI tester. Mintlify covers endpoints and authentication only; local Swagger UI uses the generated contract at `/swagger/index.html` in Development, with transient bearer authorization and unchanged endpoint policies. This supersedes ADR-007's blanket no-Swagger UI clause for local testing; hosted exposure is not enabled by default. Users and roles are created and assigned in the dedicated Entra directory, not by Fault Code CRUD endpoints. Incidents, vehicles, drivers and notifications require list/get/create/update/delete resource endpoints; writes remain authorized and auditable.

## Public entry and request authentication clarification
The API reference describes bearer headers, claim validation, scopes/roles and request errors only; it does not document token issuance, refresh or reset. Users displays and refreshes the Entra API token through MSAL, without a local token endpoint or revocation claim. Signed-out and expired workspaces hide breadcrumbs, administration titles and navigation, and show public API documentation/testing links. Alternate local development origins redirect to the registered localhost:4200 origin before MSAL starts; no Entra registration or LALAI setting is changed.

## Username/password frontend demo — October21
User authorized one demo account for the frontend, while retaining Entra for API tokens. Use a hashed server-managed credential and revocable cookie sessions on a browser-only boundary. No Entra tokens or Users administration are exposed to demo sessions. This supersedes Entra-only frontend sign-in for the shared demo account; no anonymous access, public signup, password grant, tenant change or hosted deployment is authorized by it. See browser-demo-login.md.

## Separate evaluator account clarification — October21
The user requested the username/password evaluator account back, named John Henry, username `johnhenry`, using the supplied password (stored only as a server-side hash). It is distinct from Bobby Wilson's Microsoft sign-in and photo. The avatar dropdown retains Sign out and a visible arrow. Drivers and their data remain unchanged. No new Entra account, grant or hosted deployment is performed.

## Dedicated Entra application token for the password account
On October21,2026 the user approved a separate confidential application for the demo API token while John Henry keeps local password login. The Token side-nav page replaces token controls in Users and labels the application identity separately from Bobby's delegated token. This supersedes the earlier no-demo-token restriction; no Entra user, password grant, app secret in Angular, local bearer issuer or owner-token sharing is introduced. A dedicated server-held certificate and exactly five API application roles were provisioned and locally verified; see entra-provisioning-evidence.md. The demo application's bearer calls have the same restricted mutation boundary as the browser account. Deployment remains paused.
