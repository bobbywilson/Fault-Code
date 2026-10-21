# Driver notifications and dealership referrals

Part of FAU-19. Eight additive REST operations implement explicit approved A/B/C enqueue, history, local test preferences, conditional dispatch and cancellation. Production INotificationStore and INotificationDelivery return503. No database, real contact/consent policy, external channel/provider, background worker or outbound delivery is activated. LocalNotificationSink is an explicit test adapter, never registered in production. Its acknowledgement means local sink acceptance only.

## APIs and authority

| Method/path | Policies |
|---|---|
| GET /api/v1/drivers/{id}/notification-preferences | drivers.access.read |
| PUT /api/v1/drivers/{id}/notification-preferences | drivers.access.read AND drivers.write |
| GET /api/v1/incidents/{id}/notifications | faults.read AND vehicles.access.read AND drivers.access.read AND knowledge.read |
| POST /api/v1/incidents/{id}/notifications | Same four reads AND faults.write |
| GET /api/v1/notifications/{id} | Same four reads |
| GET /api/v1/notifications/{id}/history | Same four reads |
| POST /api/v1/notifications/{id}/dispatches | Same four reads AND faults.write |
| POST /api/v1/notifications/{id}/cancellations | Same four reads AND faults.write |

Each policy independently requires exact delegated scope plus allowed human role or granular machine application role. No new grants or privilege union. Operator+faults.write does not imply read permissions. Reader own-read requires both stored original driver and vehicle ownership. Incident access is checked before paging/history/replay; absent or inaccessible404. DTOs omit tenant/owner, local contact and lease token. Detail exposes bounded attempts, safe failure codes and acknowledgement receipts. All operations retain correlated Problem Details400/401/403/404/409/503. GET detail/preference and writes return ETag. Lists/history page1/size20/max100.

Every write requires strong If-Match and Idempotency-Key. Enqueue matches incident version; preferences driver version; dispatch/cancel notification version. Keys are1–100 ASCII letters/digits/hyphens/underscore and bind tenant/actor/client/operation/resource/exact serialized payload including version. Exact completed retry returns original mutation before changed-dependency validation; access is always first. Source identity kind+ID deduplicates across keys/actors/clients indefinitely; retrying an old cancelled/superseded source returns its retained message and cannot redeliver. No automatic issue closure or vehicle actions.

## Explicit local preference

PUT body `{"enabled":true,"contact":"SYN-CONTACT-1"}` opts into the local synthetic sink only. Missing preference defaults disabled. Contact is a bounded SYN label, never an email/address/URL. Enabled requires contact; false permits null. Existing Driver.Email/EmailNotifications are independent and never grant local or real consent. Preference changes increment the shared driver version. Any unrelated driver update invalidates the captured preference until explicitly renewed; GET returns current driver ETag and effective disabled state when stale. Production channel/contact/consent rules remain DEC-002.

Before enqueue and each claim, require original driver identity/owner, active same-tenant driver/vehicle, original association still active, explicit current preference and valid synthetic contact. Changes after enqueue block dispatch409; never redirect a queued message. Cancel and re-investigate/reapprove to create a new source when needed. Preference changes cannot revoke a provider call that was already claimed; no real provider is enabled.

## Approved content and A/B/C

POST enqueue body `{"sourceKind":"investigation","sourceId":"<result ID>"}` or `engineering` with stable event ID. Models/history alone do not authorize this API call.

A/B require a current supported investigation from the same original actor/client, matching the Investigating incident version, current fault publication/remedy, pinned ontology classification/remedy kind and exact investigation-v1 canonical chunks (score>=0.85, reference/version, text equality, chunk identity/page/offset/checksum). No threshold weakening. Publisher-only evidence cannot be sent even to its approver; driver content requires tenant-accessible canonical sources. Latest source document publication/checksum, applicability and approved knowledge must still match. Draft, retired, changed, unsupported or cross-recipient data409/404. Enqueue atomically resolves A/B and enters NotificationPending; no automatic notification from investigation Run.

C requires a currently Resolved case, its latest approval matching event ID/incident/driver/fault/publication/ontology, exact current approved publication and canonical evidence snapshot, and matching resolved incident version. Pending event state is historical intent only. Reopened/changed approvals cannot dispatch. Each case/event targets only its own linked original driver; no hidden bulk notification.

The fixed driver-notification-v1 template includes incident reference and approved remedy description. Applicable patch metadata adds exact approved patch reference, dealership assessment recommendation and explicit no-installation wording. No LLM repair text, directory lookup, provider token, model tool, remote control or installation path is added. Original investigation proposals and engineering Pending events remain unchanged historical evidence; notification state is separate.

## Honest attempts and cancellation

An explicit dispatch POST with `{}` first commits a30-second lease and Pending attempt, incrementing notification version. It revalidates all authoritative evidence/recipient/preference pins. External I/O is outside the store transaction, uses a5-second linked deadline and stable notification ID as provider deduplication key. A bounded nonblank acknowledgement alone records Sent and normally advances the linked incident to Notified. Existing generic incident lifecycle reports remain workflow reports, never evidence of provider acknowledgement.

Timeout/cancellation acceptance uncertainty, missing/invalid acknowledgement or provider exception never means Sent. Failure records safe acceptance-unknown, invalid-acknowledgement or delivery-unavailable, attempt history and min(300,2^attempts) seconds backoff. At most5 attempts; early/exhausted retries409. No hidden automatic retries. Use a fresh key and latest ETag after failure. A completed retry key replays its old result, not another send.

If provider acknowledgement succeeds but final storage commit fails, the committed lease/attempt remains Pending and recoverable. After expiry, dispatch with current ETag records acceptance-unknown and backoff without sending. That recovery mutation is replayable. Later retry uses the same provider key. An expired/fenced lease cannot commit a late acknowledgement. No exactly-once external effect is claimed; future adapters require documented deduplication and reconciliation. Concurrent claim/cancel conflicts409. Caller cancellation leaves committed lease for recovery. Source/consent may change after claim; no transactional promise spans external effects.

Cancellation POST `{}` requires no active lease (recover an expired one first), matching incident version and notification-stage state. Pending/Failed becomes Cancelled; Sent becomes Superseded while retaining its acknowledgement. Atomically return incident to Resolved and complete its notify work. Existing engineering reopen can then move to Investigating and retain old events/publications. A fresh approval creates a new event; old source deduplication prevents redelivery. Historical notifications never erase delivery evidence.

## Persistence activation

INotificationStore joins fleet plus all six engineering transaction states in ONE authoritative transaction, including all existing adapters' concurrent writes. Notification/outbox/source uniqueness/attempt/lease/preferences/replay/audit/incident history/work commit or roll back together. No database transaction spans delivery. Production unavailable adapters503 remain until DEC-001/002/003 engine/provider/schema/migration/outbox/retention/backup and consent decisions. The one-process combined copy-on-write test store and local sink prove behavior, never durable storage. Crash/restart/multiple-process connections and provider reconciliation must pass before activation.
