# Administration workspace

## Approved focused demo UI

Navigation: Dashboard, Vehicles, Drivers, Incidents, Notifications, Users (owner only), Ontology, Fault codes, Token and Tickets. The generic logo/name and Protected workspace badge are removed. All pages use Spartan/Tailwind and Lucide; centered tables share the standard content width. Fleet is seed-only, with separate name/email columns and no management controls. Vehicle labels show Chevrolet Silverado or GMC Sierra without numeric suffixes.

Incidents is a vehicle-originated investigation table with links to vehicle, driver and notification records. Drivers links to filtered notifications. Notifications lists all visible driver messages and delivery statuses as a data table, using the new authenticated list endpoint rather than requiring an incident ID form.

Ontology reads the application-owned versioned relationship model from `GET /api/v1/ontology/relationships`; Angular no longer owns a duplicate production model. This reference is non-editable and is distinct from internal versioned knowledge publications used by the investigation engine. Links to removed workspaces are absent. Fault definitions, Documents, Engineering, Agent activity and Operational health remain omitted from navigation; their former URLs redirect to Incidents. Backend services required for processing remain protected.

All operations use API permissions; the signed-in Reader cannot dispatch without write permission. Service errors remain visible; no fake records substitute for unavailable storage.

## Earlier implementation reference

# Administration workspace

Part of FAU-22. Angular/TypeScript, official Spartan Helm Nova components with the neutral theme and Lucide icons, Tailwind4 and maintained MSAL Angular/browser. This is an API client: no database, Qdrant, model SDK, generic URL editor or MCP/model tool is exposed to the browser. All73 business operations (74 protected REST operations including caller permissions) remain described in generated OpenAPI metadata. Operational views expose their named actions; Ontology is now a read-only domain-model browser. Existing five MCP schemas and model context remain unchanged.

## Linux Docker

The supported delivery platform is Linux Docker, including on the developer's Mac. Optional native .NET development remains useful for quick checks. With Docker available and an external disposable/local Qdrant key:

```sh
docker compose --profile administration up --build -d
```

Open http://localhost:5082/admin/incidents. The administration service is opt-in, non-root, read-only, drops capabilities and proxies same-origin `/api/` and `/openapi/` to the existing API. It joins only the edge network; Qdrant stays on the internal backend network without host port. `faultcode.test` resolver/proxy settings are unchanged. Normal teardown retains Qdrant volumes; existing Docker reset guidance applies. No hosting deployment is performed.

Without browser configuration the production route displays **Sign-in setup unavailable** and loads no protected records. Without configured API identity every protected request returns401. Even after identity is configured, canonical business stores return503 until DEC-001/003 adapters are selected and implemented. No test identity or in-memory production fallback exists.

## Browser configuration and identity

`frontend/public/browser-config.json` contains the dedicated Fault Code public registration identifiers. Environment overrides must use that approved dedicated registration; never reuse Lalai or put secrets here. For Compose, mount an operator-managed public JSON file read-only at `/usr/share/nginx/html/browser-config.json` using a local Compose override, or replace the public config before building. API `Entra__TenantId` and `Entra__ClientId` must match its actual dedicated API registration. The SPA client is a separate registration. The API still validates issuer/audience/signature/lifetime and exact scopes/roles.

The JSON fields are `tenantId`, `clientId` (SPA), `apiClientId` and `scopes` (array of full `api://{apiClientId}/{permission}` names). All identifiers must be lowercase canonical GUIDs; scopes must target that API. Include `authorization.read` and only the operation scopes needed by the signed-in user's explicitly assigned human roles. No privileged role hierarchy is inferred. Driver-only users may use own-read scopes. Browser clients are delegated-only; machine roles remain an API interface.

Register the exact origin plus `/auth/callback` as a SPA redirect URI and the origin plus `/` for logout. Use HTTPS for hosted origins; localhost HTTP is for local development. MSAL handles code/PKCE S256, state/nonce, redirect completion and silent acquisition. MSAL5 redirect handling disables navigation to arbitrary prior URLs. SessionStorage is tab-scoped and supports redirects; memoryStorage does not support redirects per Microsoft's caching documentation. No localStorage, SPA secret or implicit flow. PII logging is disabled. Tokens attach only to normalized same-origin `/api/v1/` paths, redirects are rejected, and a401 clears the protected view. An expired or failed renewal never automatically replays a mutation.

Primary compatibility sources checked October21: https://angular.dev/reference/versions ; https://spartan.ng/documentation/version-support ; https://spartan.ng/documentation/installation ; https://learn.microsoft.com/en-us/entra/msal/javascript/angular/initialization ; https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/lib/msal-browser/docs/caching.md . Exact package/peer versions are locked in `frontend/package-lock.json`; Vite8.3.5 patches Angular's build-time dependency. Npm audit reports zero vulnerabilities at verification time.

## Workspace and operation mapping

Navigation covers incidents, vehicles, drivers, fault definitions, ontology, documents, engineering, agent activity, notifications, users/roles and integrations. Each area presents named business actions with labeled fields and nested/array field groups generated from the authoritative API schema. No raw JSON editor or arbitrary endpoint entry ships. Select an action, enter required values and choose Load records or the named mutation. Paginated collections use server totals; Open record loads the actual detail. Read the record before changing it; selecting a mutation carries its ID and strong version into the form. Concurrent changes return409 and block stale resubmission; perform a fresh detail read before retrying. Confirmation is explicit for business mutations. A failed request retry retains its scoped idempotency key while the operation/payload/version remains unchanged.

`frontend/src/app/operations.json` is the complete action-to-operation mapping: ID, HTTP method/path, explicit required policies with own/all alternatives, fields and precondition headers. It is generated by `python3 frontend/tools/generate-contracts.py` from `contracts/openapi-v1.json`; `--check` rejects drift. API authorization remains authoritative even if presentation controls are modified.

| Workspace | Authoritative API family / workflow |
|---|---|
| Fleet | `/api/v1/vehicles`, `/api/v1/drivers`, associations and local preferences |
| Fault definitions | `/api/v1/faults`, history, approved lookup and Engineer publication |
| Incidents | `/api/v1/incidents`, lifecycle, investigation history and replayable investigation |
| Ontology | `/api/v1/ontology`, draft/history/publications/evaluations |
| Documents | `/api/v1/knowledge`, draft/approval/index/reindex and grounded search |
| Engineering | `/api/v1/engineering-escalations`, queue, assignment, notes, proposal and explicit approval |
| Agent activity | incident advisory run and recorded investigation history; absent usage remains unavailable |
| Notifications | incident notification history/enqueue plus notification detail/history/dispatch/cancel |
| Users/roles | `/api/v1/admin/users`, detail, local role replacement and `/api/v1/admin/roles` |
| Integrations | `/api/v1/admin/integrations`, detail and local enabled-metadata replacement |

Public API reference remains anonymously readable; the protected workspace is distinct from future public Mintlify documentation (FAU-23). Errors distinguish invalid400, expired401, forbidden403, hidden404, conflict409 and unavailable503. Results are text-escaped; no record content becomes HTML or model instructions. Late responses cannot populate another route. Empty lists show no recorded items, never fake metrics. A reported notification state is the returned server state, not an invented delivery claim.

### Administration adapter boundary

Users/roles and integration endpoints require exact `users.manage` or `integrations.manage` plus Administrator for delegated users, or corresponding granular machine application role. Lists validate page1+/size1–100; detail reads return ETag. Local role replacement accepts only distinct Reader/Operator/Engineer/Administrator values (1–4). Integration replacement accepts only an explicit enabled boolean. Both use strong If-Match and bounded Idempotency-Key, reject unknown fields, and derive tenant/actor/client/correlation from authenticated server context. No secrets, token claims, provider endpoints or credentials enter these DTOs.

`IAdministrationStore` requires tenant filtering before counting/pagination, hidden cross-tenant404, immutable local identity/provider binding and atomic record/audit/replay. A durable implementation and provisioning/import policy remain pending DEC-001/003; production registers only `UnavailableAdministrationStore`. Local role records do not change Entra token roles, consent or external privileges; enabled metadata does not activate an external provider or configure an exporter. Test doubles demonstrate the interface but are not durable storage. Tenant grants, provider activation and credentials remain separately authorized work.

## Verification

```sh
# Node22.22.3; from frontend/
npm ci --no-audit --no-fund
npm audit --audit-level=moderate
python3 tools/generate-contracts.py --check
npm run build
npm test -- --watch=false
npx playwright install chromium
npm run e2e
```

The separate e2e build replaces only the auth module with `tests/auth.fixture.ts`; production source/assets never import it. Playwright intercepts the test session and API. Unit tests validate configuration, wrapper lifecycle, fields, same-origin token boundaries and401 handling. Browser tests cover the three demo workflow boundaries, permission conjunctions,403/503, expiry, retry keys,409, recorded pagination/detail, HTML escaping, late responses, mobile/focus and axe accessibility. They do not prove a live Entra login or durable/provider delivery.

Linux CI runs locked backend build/tests/contracts/format, real disposable Qdrant acceptance, existing container lifecycle checks, frontend unit/browser checks, and `scripts/administration-smoke.sh`. The latter builds actual production images, verifies nonzero frontend UID, default config, absence of test fixture markers, real proxied API401 and a production browser setup route. Native Windows/macOS checks do not gate Linux delivery; cancelled native runs are never reported as passed.

## Separately authorized isolated tenant smoke checklist

After dedicated registrations and durable adapters are approved/provisioned: verify exact SPA/API identifiers and scopes, sign in as each least-privilege persona, inspect PKCE/state/nonce failures with redacted evidence, reject unrelated tenant/audience and missing scope/role server-side, renew/expire/revoke/logout without mutation replay, exercise same-tenant CRUD and all three workflows with strong versions/replay, and verify actual persisted history and notification acknowledgement/provider delivery separately. Never include raw tokens, identities or contacts in screenshots/logs/model context. No live tenant or hosting acceptance is claimed by mocked tests.

## Spartan visual system (2026-10-21)

The user explicitly requested supplied Spartan components throughout all eleven administration routes, replacing the earlier Brain-button-only custom styling. Official `@spartan-ng/cli@1.8.0` generated the Helm sources in `frontend/libs/ui`, with Nova/neutral selected in `frontend/components.json`. The temporary generator was removed after generation; no Nx workspace or runtime was introduced. Compatible ng-icons core/Lucide33.3.0 and class-variance-authority0.7.1 are pinned.

Sidebar, mobile sheet, breadcrumb, badges, cards, alerts, field groups/labels, inputs, native select, textarea, checkboxes, empty states, record tables and buttons use supplied Helm primitives. Header/footer and route adapters compose these primitives with Tailwind spacing; no bespoke visual control CSS remains. Fields and Records bind the API schemas/results to these supplied components. Lucide replaces decorative bespoke icons. Missing data stays unavailable/empty, with no invented dashboard metrics.

Verification: production Angular build passed (931.74kB initial, 205.47kB estimated compressed; existing750kB warning remains below1MB error budget), eleven unit tests and nine Chromium workflows passed, including390px keyboard/focus and Axe checks. `npm audit` found zero vulnerabilities after removing the generator. Authenticated screenshots use isolated e2e fixtures, not a production login or production data claim. Production MSAL source/config and backend policies are unchanged. Deployment remains paused; feature/development changes must not be promoted to main until resumed.

## Fault-code ontology browser (2026-10-21)

The user requested viewing the fault-code ontology on entry rather than creating one. `/admin/ontology` now opens a read-only, searchable Spartan relationship table covering fault codes, incidents, vehicles, drivers, vehicle associations, notifications, classifications, symptoms, remedies and engineering cases. The model reference is curated from current domain contracts in `frontend/src/app/domain-model.json`, with explicit source fields for maintenance. Rows describe the model, not tenant records or saved ontology publications. The table has only Concept, Relationship, Related concept and Meaning columns. Browse links and the action column are removed. No ontology API read/mutation is issued on entry, and no Create/Update/Publish controls appear on this route. Versioned taxonomy APIs and their authorization remain unchanged for engineering clients.

Verified: production build, ten browser regressions including relationship search/navigation, no ontology requests on entry and desktop Axe; existing mobile accessibility case also passes. Captured the real Entra-authenticated local route and all eleven signed-in pages on October21. These screenshots show actual current access and initial-page state; they do not establish durable storage or successful execution of every operational action. Deployment remains paused.

## Unmatched vehicle reports

Incidents now defaults to a permission-aware, automatically loaded unmatched-at-intake vehicle report table rather than the generic action selector. Refresh/paging use the server-filtered queue; Review opens protected incident details/actions and Back to incidents returns to the queue. Manual incident creation is absent from the UI. The real local API still reports storage unavailable pending its approved SQL Server adapter; automated fixture rows demonstrate rendering only and are not seeded production records.

## Required backend providers
Integrations is removed from navigation and its old URL redirects to Incidents. Datadog and Mailpit are required server-managed integrations, not optional UI features. They require external configuration and actual ingestion/delivery evidence. Administrative integration metadata alone does not configure either provider. Mailpit is retained for local testing. Local Mailpit capture is verified; Linux capture deployment and real Datadog ingestion remain unverified.

## Users table
Users & roles automatically lists the authenticated `/api/v1/admin/users` resource, with Name and assigned Roles columns, paging, refresh and explicit unavailable/forbidden states. There is no generic action selector or role-mutation form. These are local managed-user records; the unavailable administration store and lack of Entra directory synchronization remain blockers. The table does not fabricate directory users from driver seed records.

## Account control and current role verification
The header uses generated Spartan Avatar and Dropdown Menu components. The supplied Bobby Wilson image is mapped to his exact dedicated-tenant object ID in browser configuration; other accounts show their own configured picture or initials. No Graph User.Read permission or mailbox access was added. The previous authentication badge and standalone sign-out link are removed; Sign out is an accessible dropdown item. Profile mappings affect presentation only, never permissions. The workspace route is lazy loaded to keep new UI code outside the initial bundle budget.

After explicit user approval, Microsoft confirmed Bobby Wilson's Operator role on the Fault Code API enterprise application in the dedicated tenant. The application refreshes its first API token on startup so changed roles are evaluated without trusting local role flags. The real signed-in Vehicles page then enabled Dispatch fault code and opened a form preselected with the clicked vehicle and its driver. This verifies permissions and form navigation, not persisted intake, agent execution or email capture of an incident.

Incidents no longer renders a large service-unavailable alert or correlation reference. The user requested a records-only table without unavailable/load-error text. Failed API requests remain failures and do not create rows or a successful empty count; the UI keeps the header and disables paging.


## Six-resource API and Entra Users clarification
The public API now exposes only five CRUD operations for each of incidents, vehicles, drivers, notifications, fault codes and ontology. Vehicle dispatch uses POST /api/v1/incidents with vehicleId/code; resolution uses PUT /api/v1/incidents/{id}. Notification retry uses PUT /api/v1/notifications/{id}. Vehicle reads include permitted associated drivers and catalog data. The read-only ontology is a versioned client reference, not an editable resource. Users displays the signed-in Entra identity, roles, API access token, copy and force-refresh controls; user/role creation and assignment remain in Entra. Refresh does not revoke an existing JWT. No personal-token issuance endpoint exists.

## Dashboard, navigation and incident review (2026-10-22)

Dashboard explains the agentic flow/Shriram first, followed by OpenSpec/Kristof and ontology/Nathan, using the user's topic associations without invented quotations. Red outlines distinguish the three name badges. The responsive diagram shows dispatch, SQL records, Agent Framework/MCP, exact Qdrant lookup, known/unknown handling and Mailpit delivery. Development stack sits below those sections; a sidebar footer link beneath API reference anchors directly to it. Both footer icons have collapsed-sidebar tooltips. Navigation orders Vehicles, Drivers and Incidents after Dashboard.

Vehicle selectors distinguish catalog codes from an unlisted system-valid code, allowing both lookup outcomes without adding invented approved knowledge. Dispatch keeps the selected value and reports completion with a toast.

Incident review replaces the generic GET submit controls with required resolution notes (maximum2,000 characters) and Resolve incident. It invokes the existing conditional/idempotent PUT; SQL retains findings, author, time and version. A separate Qdrant case-evidence collection indexes code plus findings, and scoped MCP lookup must match the canonical SQL record before the agent sends a follow-up. Unknown codes can be resolved without publishing a catalog remedy. Failed index/agent/delivery work remains auditable and retryable.

View in Datadog opens the actual Incident Management record, using the public incident number returned by Datadog and retained in SQL. No log-search, guessed ID or generic landing-page fallback is used. Before a provider record is accepted the button remains disabled. Create/status updates use a server-only adapter; provider sync state is independent of driver email delivery. Ambiguous creates are retained as Unknown and cannot be blindly repeated. Existing records can be backfilled with the explicit operator command `Database__Command=sync-datadog-incidents` after credentials are configured. No public endpoint was added.

Datadog Incident Management onboarding is reachable separately from Log Management. The latter remains blocked by the observed free plan. The approved incident-only credential expires January22,2027 and is server-only. All five existing local incidents have real acknowledged Datadog records. The public testing link reads Test API, and Kristof's section calls out Playwright checks against specified journeys.

A later status-sync test returned Datadog403 Seat Required. The existing direct link remains usable; the failed projection is not reported as synchronized. Application resolution and Mailpit follow-up still complete.

## Ticket review and Spartan controls

The authenticated Tickets page reads the Fault Code Linear workspace and FAU team through server-managed, read-only GraphQL requests. Reviewers use their existing Fault Code sign-in; they do not need Linear accounts. The searchable table filters by status and opens the description, acceptance criteria, status, priority, assignee, labels, project/milestone, cycle, estimate, dates, comments, attachments and related/subtask tickets inside the app.

`GET /workspace/tickets` and `GET /workspace/tickets/{id}` are browser support routes, excluded from the public OpenAPI resource surface. The server independently validates delegated `faults.read` or the bounded demo session; anonymous callers, Driver-only identities and machine tokens are rejected. `LinearReview:ApiKey` stays server-side, restricted in Linear to Read permission and the Fault Code team. The adapter checks organization `fault-code` and team `FAU`, uses a fixed upstream URL, implements no mutations, pages results, rejects partial GraphQL errors and bounded-overflow results, and records retrieval time. List caching lasts two minutes; Refresh requests fresh data. Failures cannot masquerade as empty lists. Raw HTML and external images from ticket Markdown do not execute or load; Angular sanitization remains enabled.

Vehicle fault selectors, shared action selectors, schema enum fields and ticket status filtering use Spartan Select with portalled, labeled listboxes and keyboard support. Buttons, fields, inputs, textareas, tables, dialogs, menus, badges, alerts, avatars, navigation and toasts use the installed Spartan Helm components. Structural HTML and ticket prose remain semantic content rather than replacement UI widgets.

Local verification covers the real read-only FAU credential, pagination/scope/error/authorization tests, in-app details and comments, safe Markdown, stale responses, keyboard selection, accessibility and mobile overflow. Hosted promotion remains paused.
