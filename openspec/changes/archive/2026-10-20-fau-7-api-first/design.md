# Design
## Context
FAU-5 provides MVC composition and FAU-6 health checks. See proposal.md. ADR-001 and ADR-007 apply.
## Goals / Non-Goals
Deliver reusable API conventions using read-only synthetic fixtures. Authentication belongs to FAU-8/9; no production mutation or persistence adapter is added.
## Decisions
Use explicit /api/v1 MVC routes rather than an additional versioning package. Use native ASP.NET Core OpenAPI and build-time generation, with a checked-in baseline and semantic compatibility checks. Use framework validation and Problem Details, including status-only errors and exception redaction. Test-only controllers exercise failures without exposing diagnostic routes in production. Documentation reserves OAuth delegated scopes and app roles; no fake authentication handler is shipped.
Pagination is one-based, default size 20, maximum 100; filtering uses a finite system vocabulary. IDs and dates use JSON strings, properties camelCase. Future mutations use strong ETag/If-Match and durable actor-scoped idempotency; no process-memory guarantee is claimed.
## Risks / Trade-offs
Synthetic examples are explicitly public fixtures. Security wiring and actual mutation atomicity require later tickets. Contract checking is conservative and must reject removed operations, narrowed schemas and weakened security.
## Migration Plan
No data migration. Rollback by reverting the change; preserve unversioned infrastructure health paths.
