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

The separate e2e build replaces only the auth module with `tests/auth.fixture.ts`; production source/assets never import it. Playwright intercepts the test session and API. Unit tests validate configuration, wrapper lifecycle, fields, same-origin token boundaries and401 handling. Browser tests cover the three synthetic workflow boundaries, permission conjunctions,403/503, expiry, retry keys,409, recorded pagination/detail, HTML escaping, late responses, mobile/focus and axe accessibility. They do not prove a live Entra login or durable/provider delivery.

Linux CI runs locked backend build/tests/contracts/format, real disposable Qdrant acceptance, existing container lifecycle checks, frontend unit/browser checks, and `scripts/administration-smoke.sh`. The latter builds actual production images, verifies nonzero frontend UID, default config, absence of test fixture markers, real proxied API401 and a production browser setup route. Native Windows/macOS checks do not gate Linux delivery; cancelled native runs are never reported as passed.

## Separately authorized isolated tenant smoke checklist

After dedicated registrations and durable adapters are approved/provisioned: verify exact SPA/API identifiers and scopes, sign in as each least-privilege persona, inspect PKCE/state/nonce failures with redacted evidence, reject unrelated tenant/audience and missing scope/role server-side, renew/expire/revoke/logout without mutation replay, exercise same-tenant CRUD and all three workflows with strong versions/replay, and verify actual persisted history and notification acknowledgement/provider delivery separately. Never include raw tokens, identities or contacts in screenshots/logs/model context. No live tenant or hosting acceptance is claimed by mocked tests.

## Spartan visual system (2026-10-21)

The user explicitly requested supplied Spartan components throughout all eleven administration routes, replacing the earlier Brain-button-only custom styling. Official `@spartan-ng/cli@1.8.0` generated the Helm sources in `frontend/libs/ui`, with Nova/neutral selected in `frontend/components.json`. The temporary generator was removed after generation; no Nx workspace or runtime was introduced. Compatible ng-icons core/Lucide33.3.0 and class-variance-authority0.7.1 are pinned.

Sidebar, mobile sheet, breadcrumb, badges, cards, alerts, field groups/labels, inputs, native select, textarea, checkboxes, empty states, record tables and buttons use supplied Helm primitives. Header/footer and route adapters compose these primitives with Tailwind spacing; no bespoke visual control CSS remains. Fields and Records bind the API schemas/results to these supplied components. Lucide replaces decorative bespoke icons. Missing data stays unavailable/empty, with no invented dashboard metrics.

Verification: production Angular build passed (931.74kB initial, 205.47kB estimated compressed; existing750kB warning remains below1MB error budget), eleven unit tests and nine Chromium workflows passed, including390px keyboard/focus and Axe checks. `npm audit` found zero vulnerabilities after removing the generator. Authenticated screenshots use isolated e2e fixtures, not a production login or production data claim. Production MSAL source/config and backend policies are unchanged. Deployment remains paused; feature/development changes must not be promoted to main until resumed.

## Fault-code ontology browser (2026-10-21)

The user requested viewing the fault-code ontology on entry rather than creating one. `/admin/ontology` now opens a read-only, searchable Spartan relationship table covering fault codes, incidents, vehicles, drivers, vehicle associations, notifications, classifications, symptoms, remedies and engineering cases. The model reference is curated from current domain contracts in `frontend/src/app/ontology-model.ts`, with explicit source fields for maintenance. Rows describe the model, not tenant records or saved ontology publications. Browse links open the existing operational pages. No ontology API read/mutation is issued on entry, and no Create/Update/Publish controls appear on this route. Versioned taxonomy APIs and their authorization remain unchanged for engineering clients.

Verified: production build, ten browser regressions including relationship search/navigation, no ontology requests on entry and desktop Axe; existing mobile accessibility case also passes. Captured the real Entra-authenticated local route and all eleven signed-in pages on October21. These screenshots show actual current access and initial-page state; they do not establish durable storage or successful execution of every operational action. Deployment remains paused.

## Unmatched vehicle reports

Incidents now defaults to a permission-aware, automatically loaded unmatched-at-intake vehicle report table rather than the generic action selector. Refresh/paging use the server-filtered queue; Review opens protected incident details/actions and Back to incidents returns to the queue. Manual incident creation is absent from the UI. The real local API still reports storage unavailable pending DEC-001; automated fixture rows demonstrate rendering only and are not seeded production records.
