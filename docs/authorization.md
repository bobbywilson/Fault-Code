# API authorization

Part of FAU-9; ADR-001, ADR-003 and ADR-012. All permissions are case-sensitive and opt-in. Nothing is provisioned in Entra by these code changes.

## Exact registration and access matrix

A delegated access token requires both the exact scope below and at least one listed human role in `roles`. Register human roles with `allowedMemberTypes: [User]`. Define each scope on the API, then grant only needed scopes to approved clients. The machine column is an independent application role with `allowedMemberTypes: [Application]`; it is never a delegated scope. Do not assign human roles to applications. Machines request the API's `.default` scope after explicit role assignment/consent. No wildcard, prefix match, implicit role hierarchy or Administrator bypass exists.

| Permission / delegated scope | Allowed human roles | Application role |
|---|---|---|
| authorization.read | Reader, Operator, Engineer, Administrator | Authorization.Read |
| drivers.read.own | Reader | unavailable |
| vehicles.read.own | Reader | unavailable |
| drivers.read | Reader, Operator, Engineer, Administrator | Drivers.Read.All |
| vehicles.read | Reader, Operator, Engineer, Administrator | Vehicles.Read.All |
| drivers.write | Operator | Drivers.Write.All |
| vehicles.write | Operator | Vehicles.Write.All |
| faults.read | Reader, Operator, Engineer, Administrator | Faults.Read.All |
| faults.write | Operator | Faults.Write.All |
| knowledge.read | Reader, Operator, Engineer, Administrator | Knowledge.Read.All |
| knowledge.publish | Engineer | Knowledge.Publish |
| escalations.read | Reader, Operator, Engineer, Administrator | Escalations.Read.All |
| escalations.write | Engineer | Escalations.Write |
| integrations.manage | Administrator | Integrations.Manage |
| users.manage | Administrator | Users.Manage |

Full delegated scope request strings use `api://{api-client-id}/{scope}`; the token's `scp` contains the short value. `Reader` with only own-read scopes is the driver profile; do not grant all-resource scopes to drivers. Reader does not by itself grant all-resource access. Operator operates; Engineer publishes knowledge/approved remedy metadata and manages escalations; Administrator manages integrations/users. A user needing multiple capabilities must be assigned each role and scope explicitly. Machines and agents get only explicit granular application roles; a role named Reader/Administrator/Agent never substitutes for one. Delegated roles without scope, scopes without allowed roles, scope substrings/case changes and app roles in delegated tokens deny.

Each catalog name is a registered ASP.NET authorization policy. `{permission}.Delegated` and `{permission}.Application` restrict it to that caller kind; own-read has no machine grant in any variant. Bare `[Authorize]` and mapped routes with no authorization metadata deny valid tokens. Only explicit `[AllowAnonymous]` or `.AllowAnonymous()` makes an operation public. Unknown routes remain 404. The production route inventory is tested.

## Protected contract

`GET /api/v1/authorization/me` requires `authorization.read`. It returns `{"callerKind":"delegated","permissions":["authorization.read"]}` (or `application`) with sorted permission names inferred from validated claims. This is a permission summary, not proof of ownership or authorization for a specific resource. It returns no token, principal identifiers or driver records. Missing/invalid identity yields generic correlated 401 with Bearer challenge; valid insufficient permission yields correlated 403. Public OpenAPI declares `EntraBearer`, `x-faultcode-permission`, `x-delegated-scopes`, `x-human-roles` and `x-application-roles` from the same catalog. HTTP bearer scheme scope arrays remain empty per OpenAPI; extensions document the exact Entra permission values. Public fixtures retain `security: []`.

Other catalog policies are reusable boundaries for later domain tickets, not delivered CRUD/publication/integration routes. No placeholder mutation endpoints or pretend business operations ship. Future protected endpoints must use the named permission policy and update route coverage/security metadata tests. Multiple policies are ANDed; do not combine caller-kind-only variants as if they were alternatives. Domain mutations still need engineering approval state checks, durable audit, concurrency and idempotency in their own tickets.

## Resource ownership

`drivers.resource.read` / `vehicles.resource.read` authorize a `DriverResource` containing **server-loaded** tenant and owner Entra object IDs. Load the actual record through a trusted repository and construct this value from that record. Do not bind `DriverResource` from JSON, headers, route or query; a driver identifier alone is not the owner's oid. FAU-10 must store the verified owner mapping and use tenant-filtered queries. Resource authorization denies absent/malformed/unowned/cross-tenant records. Matching `tid` is required even for all-resource roles.

Own-read requires Reader + the corresponding own scope + `oid == OwnerObjectId`. Machine callers cannot own-read. Staff/machines need the corresponding explicit all-read scope+role/app role. Future list queries must apply the same tenant/ownership restriction before pagination/counting, and writes must enforce operation-specific write permission plus applicable trusted resource constraints. These read handlers do not authorize writes. HTTP-level own-read policy alone cannot replace resource authorization: call `IAuthorizationService.AuthorizeAsync(User, trustedResource, "vehicles.resource.read")` before returning private data. Synthetic test-only routes prove this boundary; no database or production vehicle data exists yet.

MCP/integrations must call the API with the original caller's validated token and permissions; never union agent service privileges with delegated caller claims. App-only consumers use their separately granted role and never impersonate a driver. MCP execution itself remains FAU-16.

## Decision evidence and limits

Policy handlers emit structured permission/caller-kind/outcome/oid/azp/server correlation context. Resource handlers log area/outcome/actor/client/correlation without the owner or resource payload. Only validated GUID identity fields enter these logs; tokens, raw claims, request bodies and driver records are excluded. Logs are operational decision evidence, not a durable business audit store. FAU-10 and later mutation tickets must implement durable actor/context audit with their selected persistence strategy.

No live isolated tenant, roles/consent, browser or machine credential has been provisioned/tested. Deterministic tests use ephemeral RSA and actual Microsoft bearer middleware with in-memory discovery. Live signing-key rollover remains pending; no instant issued-JWT revocation guarantee is made. Follow [Entra registration](entra-authentication.md) only after specific provisioning authorization.

Design sources: [Microsoft policy authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0), [resource authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-10.0).

## FAU-10 lifecycle access

Production vehicle/driver routes now exist; see [lifecycle contract](vehicle-driver-management.md). They use `drivers.access.read` / `vehicles.access.read` composite entry policies (exact all-read OR Reader+own-read), then tenant/owner-filtered application queries and trusted resource authorization on single GET. These policies add no Entra catalog values. Writes use the unchanged exact write policies; association writes AND both. Owner mappings are immutable in general updates, trusted from storage for reads, and never bound as `DriverResource`. Production storage is deliberately unavailable pending DEC-001; test audit/replay atomicity is not durable production audit. OpenAPI adds `x-faultcode-required-policies` (AND) and `x-faultcode-policy-requirements` (anyOf grants per policy), documenting all alternatives and multi-policy operations precisely.

## FAU-13 ontology access

Draft creation/replacement/read, history and approval require exact knowledge.publish (Engineer or Knowledge.Publish application role). Approved publication read and stored deterministic evaluation require exact knowledge.read (listed read roles or Knowledge.Read.All). No new permissions or implicit Operator/Administrator access. Both stored evaluation and publication are tenant-local; approved reads never expose drafts.

## FAU-14 document access

Document create/detail/update/publication/delete/index and tenant reindex require exact knowledge.publish. Search requires exact knowledge.read and filters current approved tenant-local exact system/model/software and tenant-or-publisher access before vector ranking; authoritative canonical chunk IDs are checked again after ranking. Publisher-only means the publication approver oid, never a supplied claim/owner. Drafts/history are never search evidence. No new scopes/app roles, agent privilege union or Administrator bypass. See knowledge-retrieval.md.

## FAU-16 MCP authority

POST /api/v1/mcp authenticates each stateless HTTP request through Entra. Internal mcp.access is an authenticated transport policy only, not an Entra grant; each listed/invoked tool enforces its existing exact policy conjunction. No privileged service grants are added. MCP mutation intent must be independently bound in the authenticated consumer's HTTP header; arguments/metadata/model/retrieved text cannot authorize it. See mcp-tools.md. The model runtime remains separate with no tools connected.

## FAU-18 engineering case authority

Engineering case reads require escalations.read, faults.read, vehicles.access.read and knowledge.read. Mutations require escalations.write plus the three incident read policies; approval additionally requires knowledge.publish. Tenant/historical owner filtering precedes counts and replay. Assigned object ID is a work label only. No extra Entra grant, role bypass, model authority or MCP tool. See [engineering escalation](engineering-escalation.md).

## FAU-19 notification access

Notification reads AND faults.read, vehicles.access.read, drivers.access.read, knowledge.read; writes add faults.write. Preference GET uses drivers.access.read; PUT adds drivers.write. Reuse exact FAU-9 human/machine grants; no new Entra grants or service privilege union. Stored original driver/vehicle owner and tenant checked before page/count/history/replay. See driver-notifications.md for routes and local-only preference boundary.

## FAU-22 local administration
User list/detail/local role replacement and role catalog require users.manage; integration list/detail/local enabled metadata replacement require integrations.manage. Existing scope+Administrator or granular machine roles apply unchanged. No catalog grants, role hierarchy, Entra Graph permission or browser authority is added. Production store503; see administration.md. Local records never rewrite token claims or activate external privileges.
