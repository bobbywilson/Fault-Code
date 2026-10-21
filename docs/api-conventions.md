# API conventions

Part of FAU-7. ADR-001 and ADR-007; normative behavior is in `openspec/specs/api-contracts/spec.md`.

All application consumers, including Angular and MCP/integrations, use `/api/v1` REST contracts. They never access storage, Qdrant or AI directly. Business capabilities arrive in their own tickets. Explicit version routes avoid implicit version fallback; unknown versions return 404. A breaking change requires a new API version and migration documentation.

## Current public operations

| Operation | Behavior |
|---|---|
| GET /api/v1/health | Process-only JSON `{ "status": "Healthy" }`; no provider check |
| GET /api/v1/examples | Stable synthetic steering fixtures; no real vehicle/driver records |
| GET /api/v1/examples/{id} | Fixture by exact ID, otherwise 404 |
| GET /openapi/v1.json | Generated public OpenAPI, no Swagger UI |

Unversioned `/health/live` and `/health/ready` remain infrastructure probes. The latter can check Qdrant when configured. They are excluded from the application contract. Example endpoints are explicitly public read-only fixtures in every environment. No diagnostic failure endpoints ship.

## DTOs and queries

JSON properties are camelCase; IDs are opaque strings, timestamps in future DTOs use UTC ISO 8601. Do not expose domain/persistence objects directly. Collections return `{items, page, pageSize, totalCount}`; deterministic ID order, one-based page, default page=1/pageSize=20, pageSize 1–100. `system` optionally matches exactly EPS, steer-by-wire or steering-ADAS. Invalid numbers, overflow, unsupported filters and out-of-range pagination return 400. Empty pages return `items: []` with the filtered total. Offset arithmetic uses 64 bits to avoid overflow.

Framework validation returns field names with generic `Invalid value.` messages, avoiding echoing supplied query values. Unknown query keys follow ASP.NET binding behavior and are ignored; no filtering SQL is accepted.

## Errors and correlation

All application errors return RFC 9457 `application/problem+json`, with `type`, `title`, `status`, `instance` (path only), and `correlationId`. Validation adds `errors`. Server-generated X-Correlation-ID matches the body and is available on successes too; inbound correlation headers are not trusted. No stack traces, exception messages or query values are sent. Framework logs remain server-side.

| Status | Meaning |
|---|---|
| 400 | Binding/validation failure |
| 401 | Missing or invalid identity (FAU-8) |
| 403 | Valid identity lacks permission (FAU-9) |
| 404 | Unknown route/resource/version |
| 409 | Domain/version/idempotency conflict |
| 429 | Throttled; future limiter must include Retry-After |
| 500 | Unexpected failure, safe generic detail |

FAU-7 tests load controllers from the test assembly to exercise 401/403/409/429/500 response formatting. FAU-8 separately exercises real JWT middleware and synthetic signed tokens/discovery, including 401 and a test-only role policy's 403. [Identity configuration and registration](entra-authentication.md) documents the distinction from pending live tenant/browser and FAU-9 business permission checks. No fake identity scheme or test route ships.

## Protected operations and future mutations

FAU-8 implements configurable dedicated Entra v2 issuer/audience/signature/lifetime validation; it remains disabled until provisioned. FAU-9 enforces delegated scopes or machine app roles and resource ownership. Browser clients use authorization code + PKCE; machines use client credentials, with application roles rather than delegated scopes. GET /api/v1/authorization/me implements the [FAU-9 permission summary](authorization.md) with exact delegated scope plus human role or machine app role. Each protected operation must declare its exact delegated scopes and application roles in OpenAPI and server policy. `EntraBearer` describes HTTP bearer JWT validation; current foundation operations explicitly have `security: []`. No tenant URL, scope identifier or registration is invented here.

Persistent update/delete operations must expose a strong ETag and require If-Match. Missing preconditions are invalid requests (400); stale versions return 409. Retryable creation must accept an Idempotency-Key, bound to authenticated actor, operation and canonical payload. A durable transaction stores the result with the mutation, replays the same result for an identical retry, and returns 409 for payload mismatch. Each mutation ticket must specify retention, simultaneous retries, cancellation and failure atomicity after DEC-001 is resolved. An in-memory cache does not satisfy this requirement. No mutations are implemented by FAU-7.

## Build and contract checks

Native ASP.NET Core OpenAPI 10.0.13 plus Microsoft.Extensions.ApiDescription.Server 10.0.13 generate `artifacts/openapi/faultcode-v1.json` during Release build. On this Mac both DOTNET_ROOT and PATH must point at the pinned SDK (see local-development.md). The standard build does not require provider credentials. Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0.

```sh
dotnet build FaultCode.slnx -c Release
python -m pip install -r scripts/requirements-contracts.txt
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

Use a Python virtual environment locally. CI uses Python 3.12, validates OpenAPI 3.1 and response examples, and compares against both the committed baseline and the PR base branch, preventing baseline edits from bypassing compatibility. The first baseline has no predecessor. Existing schemas, request bodies, parameters, responses and security are conservatively immutable except prose/examples; new operations, responses, schemas and optional parameters can be additive. Some compatible schema changes are deliberately rejected for review/new versioning. Negative fixtures cover removed paths/properties/responses, tightened pagination, required new inputs, and changed/removed security. Actual OAuth behavior remains dependent on FAU-8/9.

## FAU-10 lifecycle operations

[Vehicle/driver API contracts](vehicle-driver-management.md) add protected CRUD/list/association operations and 503 unavailable storage. Mutation status201 is a success and its examples are schema-validated alongside200; all non-2xx responses require Problem Details. Existing foundation contract schemas remain unchanged. OpenAPI policy requirements describe composite read alternatives and ANDed association writes. No in-memory production persistence or fabricated durable replay is provided.
