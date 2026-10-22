# Vehicle incident processing

## Current demo requirements

Incidents is a paginated data table of vehicle-originated unknown-fault investigations. There is no standalone incident creation form on this page. Fault dispatch starts from Vehicles and posts to the authenticated `POST /api/v1/incidents` ingestion boundary. Backend validation requires a nonzero vehicle and driver, active same-tenant records and their authoritative association; client-supplied identifiers cannot bypass ownership or association checks.

Each row links to its vehicle, driver and notification history. One incident may have several delivery records, including investigation-pending and resolution follow-up. Adding knowledge is not evidence of vehicle repair. Known-fault ingestion reports remain historical application records; the Incidents UI selects `unmatchedOnly=true` for the investigation list. A later processing reconciliation must distinguish ingestion reports from investigation lifecycle records consistently rather than deleting known-fault history.

**Current blocker:** fleet SQL persistence works, but the incident/knowledge joined durable adapter remains unavailable. Fault intake and incident listing therefore return503 rather than pretending to persist or send email. Table headers remain visible alongside the error; no unavailable count is reported as a successful empty result.

## Existing application contracts

# Fault incident processing

Part of FAU-12. Seven production API operations and provider-independent domain/application contracts are implemented. The production `UnavailableIncidentStore` always returns503. SQL Server is approved and the fleet adapter works locally, but the durable incident/knowledge/outbox adapters and worker remain missing; there is no fallback memory store. Local Mailpit capture is configured separately.

| Route | Method / policies |
|---|---|
| `/api/v1/incidents` | POST faults.write AND knowledge.read; GET faults.read AND vehicles.access.read AND knowledge.read |
| `/api/v1/incidents/{id}` | GET faults.read AND vehicles.access.read AND knowledge.read |
| `/api/v1/incidents/{id}/history` | GET faults.read AND vehicles.access.read AND knowledge.read |
| `/api/v1/incidents/{id}/status` | PUT faults.write |
| `/api/v1/incidents/{id}/failures` | POST faults.write |
| `/api/v1/incidents/{id}/retries` | POST faults.write |

Exact FAU-9 delegated scope+allowed human role or granular machine role applies to each ANDed policy. Intake needs knowledge.read to consume approved guidance; no write role implies knowledge access. Read access requires faults.read, knowledge.read and vehicle all-read or Reader own-read. Server-loaded original owner is checked for detail/history; own lists filter before count/page. Source/client, tenant and owner are never trusted from request fields. Historical owner access is independent of current association changes; DTOs omit tenant/owner and driver contact fields.

## Intake

```json
{
  "vehicleId": "55555555-5555-4555-8555-555555555555",
  "driverId": "66666666-6666-4666-8666-666666666666",
  "sourceEventId": "SYN-EVENT-1",
  "code": "SYN-EPS:001",
  "system": "EPS",
  "model": "SYN-MODEL-1",
  "softwareVersion": "1.0",
  "occurredAt": "2026-10-20T12:00:00Z"
}
```

Use an actual UTC timestamp within seven days before server time, never future. IDs must reference active same-tenant records and the current open association; occurrence must fall in its `[from, until)` interval. Ended associations reject even when the occurrence predates closure. Vehicle system must match. SourceEventId and Idempotency-Key are1–100 ASCII alnum/hyphen/underscore. The source identity is the authenticated azp; no additional source registration or Entra provisioning is implemented. Code normalizes trim/uppercase using the FAU-11 demo system namespace; model/software use the same exact bounded demo validation. Model/software are event-reported context, not independently verified against a vehicle inventory. Unknown JSON properties return400.

A committed event is unique by tenant+validated client+sourceEventId, independently of actor or retry key. Canonical payload binds vehicle/driver, code/system, source event, exact model/software and UTC occurrence. Same payload returns original201 ID/version/ETag/Location; changed payload409. A separate key binds tenant+actor+client+operation to the same hash/result. Both indexes, incident, context, initial history, audit and one pending investigation intent commit together. Replay precedes clock/fleet/knowledge revalidation, preserving original result during dependency outage or later fleet changes. Keys/source identities are retained indefinitely by contract; production retention awaits DEC-001.

The store transaction's Context callback must load authoritative same-tenant fleet/knowledge within the **same consistent transaction** as incident creation, coordinating concurrent association/deactivation/publication. Sequential independent store reads do not meet this contract. Context failure503 must not fabricate an empty knowledge catalog. Only the current approved publication is snapshotted; its definition ID/publication version, classification/reference versions, content and approval actor/time are retained. Approved remedies require exact model/software match. Unknown/unpublished/retired codes have null knowledge; known approved faults with no applicable remedies have nonnull knowledge and an empty remedy array. Draft management content never becomes incident guidance. Knowledge lookup failure aborts intake503.

Snapshots retain vehicle/driver IDs, demo identifiers and record versions, association identity/interval and private original owner mapping. Later publication edits, retirement, reassignment or deactivation do not change the incident. Original intake correlation remains stored; each response has a fresh server-generated X-Correlation-ID.

## Lifecycle, failure and retry

Allowed edges:

```text
Received -> Investigating
Investigating -> Escalated | Resolved
Escalated -> Investigating | Resolved
Resolved -> NotificationPending
NotificationPending -> Notified
```

PUT status body is `{"status":"Investigating"}`. All conditional mutations require one strong quoted positive If-Match and an Idempotency-Key. Missing/weak/list/wildcard/zero/leading-zero ETag400; stale/illegal/repeated edge409. Exact retry replays original result before current-version checks; changing status or expected version with the same key409. Success returns `{id,version}` and ETag. Every mutation records version/action/status, validated actor/client, fresh request correlation and server UTC time. History is immutable.

Intake creates one Pending investigate work intent with a stable server ID. Received->Investigating keeps that ID. Leaving investigation completes its work; returning from Escalated to Investigating creates a new investigation intent. NotificationPending creates a notify intent; Notified completes it. These are authorized workflow reports, not proof that an agent investigated or an external provider delivered anything. Actual evidence-backed investigation/escalation/notification integrations remain FAU-17/18/19; no software deployment/control endpoint exists.

POST failures body is `{"code":"dispatch-unavailable"}` (also processing-unavailable or dependency-unavailable). It marks the current pending work Failed, increments its attempt counter and appends history without losing incident state. Arbitrary exception text is rejected400. Failed work blocks status advancement409. POST retries has no body, requeues that same failed work ID, clears current failure and preserves attempts/history. Retrying without a failure409. Repeated failure/retry keys replay once; no duplicate dispatch/notification intent. No automatic worker, dispatch attempt, timer/backoff or actual delivery is performed here.

Reads use page1/pageSize20/max100 and deterministic incident-ID order; list accepts exact status/vehicleId; history orders ascending version. Invalid input400, invalid identity401, insufficient permission403, missing/cross-tenant/other-owner404, replay/version/lifecycle conflicts409, unavailable dependencies503. All errors are safe correlated Problem Details. Audit excludes payload/contact data.

## Required production acceptance

Test-only copy-on-write adapter proves one-process behavior; it is not durable production persistence. An approved DEC-001 adapter must coordinate fleet/knowledge locking/snapshots and enforce cross-process uniqueness/version isolation, crash-before/after-commit, cancellation, restart recovery, migrations, retained replay/audit/history, safe retention and durable outbox dispatch/consumer deduplication. Pending work contracts do not constitute an implemented durable outbox. No database/provider/schema/migration was selected, no live Entra tenant was provisioned and no cloud deployment was performed.

## FAU-16 lifecycle escalation adapter

POST /api/v1/incidents/{id}/escalations requires exact escalations.write (Engineer or Escalations.Write.Application application role), strong If-Match and Idempotency-Key. It shares one service operation/replay namespace with the MCP incident_escalate tool and records the existing legal Investigating -> Escalated transition, history and safe audit. This lifecycle marker is not FAU-18 engineering case management. Production storage remains503. See mcp-tools.md for separate MCP consumer intent and model isolation.

## Unmatched-incident queue (2026-10-21)

The user clarified that the Incidents entry page should list vehicle reports whose code was not found in known faults. It now automatically requests `GET /api/v1/incidents?unmatchedOnly=true&page=1&pageSize=20` after real authentication and required permission resolution. The table shows code, vehicle, driver, system, reported UTC time and status, with refresh, pagination and explicit review. Manual Create Incident is removed from the administration UI; authenticated vehicle clients retain idempotent intake. Existing investigation/notification/history APIs and known occurrence records remain intact.

`unmatchedOnly` is optional and defaults false for existing clients. It filters `Knowledge == null` before accessible totals/paging: no approved catalog match at intake, including unpublished/retired definitions. This preserves the intake snapshot after later catalog changes. Lookup/storage unavailability stays503; it is never recorded or displayed as unknown/empty success. The UI does not locally filter a partially paginated full list.

Nine demo steering fault drafts and classification concepts are prepared in `data/demo/steering-faults.json`, with public product-scope provenance and explicit non-OEM labeling. They pass existing ontology/fault validators. They are **not loaded, published or persisted**: DEC-001 engine choice is pending the user's response. No production in-memory fallback was added. Nexteer driveline scope has also been asked explicitly; preparation currently stays within approved EPS/SbW/steering-ADAS boundaries. The scoped OpenSpec change stays active until actual database seeding and restart/idempotency verification complete.
