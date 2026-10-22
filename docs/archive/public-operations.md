---
title: "Operations"
description: "Configuration, health and provider boundaries."
---

Linux Docker containers are the delivery platform, including on the developer's Mac. Native Windows/macOS CI is optional.

## Configuration
Supply secrets only through an approved secret store or runtime environment. Browser configuration contains public tenant/SPA/API identifiers and exact scopes only; it defaults empty and the administration UI reports sign-in setup unavailable. Entra, canonical stores, model and telemetry activation need their documented approved configuration. No fixture authentication or in-memory production fallback exists.

The standalone administration image serves `/admin` and uses a same-origin API proxy. The backend/Qdrant network remains internal. No docs change activates `faultcode.test`, publishes Qdrant or changes CORS/network isolation.

## Health
`/health/live` checks the process. `/health/ready` can check configured Qdrant; neither establishes durable business storage, identity, model, delivery or complete production readiness. Verify protected 401/403 behavior and authorized supported requests separately.

## Storage and migrations
DEC-001 has not selected a relational engine, schema/migration tool or durable outbox. No migration command exists. DEC-003 still covers hosting, canonical storage, retention and backups. Do not invent SQL, apply an empty migration or interpret process health as proof of persistence. Before production, approve adapters and test transactions, crash recovery, backups/restores and upgrade compatibility.

## Telemetry
Provider-independent OpenTelemetry supports an explicitly configured same-host Datadog Agent. It defaults disabled. Deterministic OTLP receiver tests establish sanitized export behavior, not Datadog account ingestion, allowances or retention. Sentry remains optional and unimplemented. Model calls remain mocked in CI; no paid smoke call is required.

See [release and rollback](/release) before loading or promoting an image.


## Focused demo tables and relationship navigation

The demo uses preloaded Silverado/Sierra vehicles and fictional example.com drivers. Fleet management controls are absent; fault intake begins from a vehicle. `POST /api/v1/incidents` requires an authorized vehicle, driver and active association. Incidents link to their vehicle, driver and all related notifications through the response `links` object.

`GET /api/v1/notifications` returns a paginated driver-notification summary table. Optional `driverId` and `incidentId` filters combine; tenant/ownership filtering precedes page counts. Summary rows include driver name/email, message, state, attempt count, next retry and last failure. Listing never sends email. `GET /api/v1/incidents/{id}/notifications` remains the incident history collection.

`GET /api/v1/ontology/relationships` supplies the versioned, non-editable domain relationship reference. It is separate from internal approved knowledge publications. No frontend-only copy is used as the system of record.

Local Mailpit captures transport test email without external relay. Linux Mailpit is prepared but not deployed. Incident and notification canonical persistence is still unavailable; 503 is a blocker, not an empty inbox or successful workflow. No captured configuration message is presented as a real incident notification.
