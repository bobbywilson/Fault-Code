# Approved Fault Code architecture

Requirements reconciled October21, 2026. This is the intended architecture, not an assertion of completed implementation. Later user clarifications take precedence over the earlier full audit prompt where explicitly noted. Evidence and gaps remain in `docs/audit/architecture-compliance.csv` and implementation-specific records.

## Purpose and stack

Independent temporary API-first automotive steering investigation proof of concept for an approximately30-day public evaluation. No LALAI tenant/data coupling, Nexteer branding/proprietary data, vehicle software deployment or unrelated infrastructure. Backend ASP.NET Core/C#, Angular/TypeScript with Spartan/Tailwind/Lucide, Microsoft Agent Framework/OpenAI, authorized MCP, traditional RAG/OpenAI embeddings, Qdrant, dedicated Fault Code Entra Free, OpenTelemetry/Datadog with future Sentry abstraction, OpenAPI/Mintlify, OpenSpec/Linear/existing GitHub repository. No GraphRAG or unnecessary multiple agents.

## Persistence approval

The user approved SQL Server on October21 after the audit recommendation. Use SQL Server2025 Express free edition, EF Core SQL Server and migrations. Qdrant remains a derived lookup/retrieval index, not the incident/delivery system of record. Isolate data, credentials, volumes and networks. Explicit operator migrations and seeding; least-privilege application credentials; no automatic in-memory fallback. Fleet is the first normalized SQL adapter; other stores are not yet implemented. Verify transactions, concurrency, idempotency, tenant isolation, restart durability and backup/restore before acceptance.

## Domain

Administrator, Driver and Engineer roles; driver-to-vehicle many-to-many associations; each vehicle report refers to one vehicle and a relevant associated driver. Known definitions and occurrences are distinct. Retain unknown reports without definitions. Track investigation incidents, status/history, repeated unknowns, engineering findings, approved remedies/patch applicability, notifications/delivery history, ontology relationships, knowledge/retrieval metadata and integration/audit records. Knowledge resolution and physical vehicle repair are distinct states.

## Latest primary workflow

1. Vehicle consumer submits a validated authenticated fault event through the versioned API.
2. Exact normalized approved/applicable-code lookup in Qdrant determines a known match. Semantic similarity alone never proves a definition; retain source identity/version/approval evidence.
3. Known fault: agent prepares a structured evidence-grounded driver/customer email; deterministic validation/dispatch sends to Mailpit capture and records acceptance.
4. Unknown: retain the report, create an investigation incident/escalation, log telemetry and send an agent-written investigation-pending email without a diagnosis.
5. Engineering resolution supplies approved knowledge/remedy and triggers reevaluation/follow-up to affected drivers using the known-fault messaging path. Never claim vehicle repaired or deploy a patch merely because knowledge is published.
6. Reliable durable delivery requires consent, retries, duplicate prevention and audit. No unsolicited operational email to interviewers or fictional addresses.

The agent accesses bounded authorized MCP tools describing and operating the service; no unrestricted shell/network/credential access. Application code owns authentication, persistence, exact matching, lifecycle transitions, dispatch and approval checks. The agent preserves structured findings, source evidence, tool activity and uncertainty. Mailpit is the approved local/Linux email capture provider; actual capture and workflow acceptance must be verified. Instrument API, SQL, RAG, MCP, agent, workflow and delivery; real Datadog ingestion is required before claiming integration complete.

## UI clarification

Vehicles lists vehicles and associated drivers with a fault dispatch action. Drivers lists driver email addresses. Use demo Chevrolet Silverado/GMC Sierra examples and fictional consenting-demo placeholders; no verified OEM fault mappings are implied. The Ontology page is a **non-editable relationship table**, superseding the earlier request for Angular ontology CRUD. Remove Fault definitions, Documents, Engineering, Agent activity and Operational health pages from navigation; keep the backend services needed by the workflow. Remaining UI includes Incidents, Vehicles, Drivers, Ontology, Notifications, Users & roles. All mutations use authenticated APIs. Successful event ingestion is not proof that an email was delivered.

## Identity and API boundary

Dedicated real Fault Code Entra tenant, separate from LALAI. Angular MSAL authorization code/PKCE, API issuer/audience/expiry/scopes/roles validation, scoped application permissions/client credentials for machines and Postman. No browser secrets, mock production auth or authorization bypass. Invitations/role grants and participation/vehicle consent are explicit; real email recipients must consent. Standardized errors, input validation, versioned REST and generated OpenAPI; Mintlify public reading with authenticated interactive requests. Role metadata alone does not prove directory assignment.

## Deployment and lifecycle

Existing hosted LALAI Linux server only, fully isolated Compose directory/project/containers/networks/volumes/database/secrets/resource limits. Preserve LALAI/Simone health and shared proxy/traffic. faultcodeai.com/GoDaddy DNS and HTTPS, preserve unrelated records. Feature→development PR/CI→main PR/CI; only validated main updates deploy, serialized with health/version/rollback checks. No staging server required. Document scoped reversible teardown; deleting persistent data requires explicit approval. No purchases or shared-resource changes without approval. Deployment is currently paused.

The user separately approved resizing/restarting the existing local development VM from2GiB to4GiB for SQL testing. That is not authorization to restart or modify the hosted LALAI server.

## Acceptance and reporting

Audit every requirement; label VERIFIED, IMPLEMENTED BUT UNVERIFIED, PARTIAL, MISSING, BLOCKED or NOT APPLICABLE. Unit mocks are not real Entra, Microsoft365, Datadog, OpenAI, Qdrant or SQL integration evidence. Verify real token acceptance/rejection, consent, driver associations, known/patch/unknown processing, follow-up/retries/deduplication, read-only ontology, retrieval accuracy/unsupported-answer rejection, authorized MCP execution, API/UI parity/docs, Datadog ingestion, deployment/version/rollback/teardown and continued LALAI/Simone health. Update existing Linear tickets without duplicates or premature closure. Keep unfinished changes active; never equate an audit or passing CI with end-to-end application completion.


## Latest presentation and capture decisions

Subsequent user instructions simplify the demo further: no vehicle/driver management UI, only preloaded test data and dispatch from Vehicles. Display plain names and readable vehicle labels with a separate email column and left-aligned fleet tables. Use example.com email addresses. Assign one active driver per demo vehicle: Alex Morgan→Silverado001, Jordan Lee→Silverado002, Casey Taylor→Sierra001, Riley Parker→Sierra002. Preserve historical associations and the backend's separately approved relationship capability; do not invent a one-driver restriction for unrelated domain records from this table-only request.

Use Mailpit for local and Linux demo capture with relay disabled and private access. The local instance/real API and SMTP receipt are verified; Linux configuration is prepared while deployment stays paused. Capture is not real external email delivery, and direct transport tests are not agent-written incident workflow evidence. The versioned seed upgrades profiles and associations without rewriting original replay payloads or deleting history. Actual notification dispatch/outbox and agent-to-MCP orchestration still need completion.

## Latest presentation and API reconciliation

Incidents and Notifications retain their short navigation names. All tables, including Vehicles and Drivers, use the centered content width. Fleet labels omit numeric suffixes; driver name/email are separate; the Protected workspace badge is removed. Incident records must originate from an authorized vehicle and driver association and link to both resources plus all related notification records. The incident investigation list uses unmatched-at-intake filtering; known ingestion report history must not be silently deleted. Notifications has a tenant/ownership-filtered paginated list API with optional driver and incident filters. Ontology's read-only relationship model is served by the API; no duplicate production frontend model remains.

## Required backend integrations override
Datadog telemetry and Mailpit email capture are mandatory backend integrations. Microsoft365 email is cancelled. Remove the Integrations navigation item and redirect its previous URL. Mailpit capture is required for both local and Linux; Linux Compose includes it by default. Do not modify LALAI resources or purchase mailbox/subscription licensing without authorization. Missing Datadog credentials/ingestion evidence or Linux capture acceptance are blockers; none may be reported as optional or complete.

## Browser password demo account — later October21 decision
The user explicitly selected username/password authentication for the frontend and a shared demo account. This supersedes the Entra-only browser requirement for that account. External `/api/v1` consumers still require dedicated Entra bearer tokens. The server verifies a provisioned ASP.NET Core Identity password hash, holds expiring sessions behind HttpOnly/SameSite cookies, rate-limits sign-in, and validates CSRF for browser mutations. A browser-only `/workspace/api/v1` boundary reuses application services and authorization while restricting demo actions to incident dispatch/status and notification processing. Demo sessions cannot issue Entra tokens, administer Users, publish knowledge, or mutate fleet/reference data. Microsoft owner sign-in remains available separately. No anonymous access is enabled. See browser-demo-login.md.
