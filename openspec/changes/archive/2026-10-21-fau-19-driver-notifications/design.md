# FAU-19 design

## Context
FAU-17 investigation results and FAU-18 engineering events are immutable historical intent, not dispatch authority. DEC-001/002/003 remain unresolved.

## Goals / Non-Goals
Implement provider-free application behavior, REST contracts and demo A/B/C acceptance. No external delivery, provisioning, model tools or vehicle operations.

## Decisions
Reuse existing policies: notification reads AND faults.read/vehicles.access.read/drivers.access.read/knowledge.read; writes add faults.write. Preferences reuse drivers.write and drivers.access.read. No grant provisioning.

One INotificationStore transaction joins NotificationState, FleetState and the complete EngineeringTransaction. All existing authoritative adapters MUST join it, including driver preference writes. Never sequentially copy/commit existing stores. Production unavailable adapter503; combined test fixture proves only one-process logic.

Explicit local preferences are independent of email opt-in and permit SYN labels only. They are test-sink consent, never a real contact/channel policy. Capture preference/driver versions and contact at enqueue; recheck before dispatch; change requires cancel and fresh source, never redirect an existing message.

A/B require the latest incident investigation, exact source incident version, current approved fault/remedy/rule and exact canonical citation pins using unchanged investigation-v1 support. C requires latest case approval/event, exact current publication and pinned canonical evidence. Tenant-accessible sources only; engineering adjudication is approval authority, not model inference. Templates are deterministic approved remedy text plus fixed dealership wording and incident reference.

Enqueue deduplicates globally per tenant/source kind/source ID and one active notification per incident. Resolve A/B Investigating to Resolved and then NotificationPending atomically; C starts Resolved. No implicit case creation or bulk recipient expansion.

Dispatch is explicit REST operation: transaction reserves a 30-second lease and attempt, provider call outside transaction with linked 5-second deadline, completion uses the same lease. Stable notification ID is provider deduplication key; transient failure/timeout retains safe failure, maximum5 attempts, backoff min(300, 2^attempts) seconds. Expired lease records uncertain failure before another attempt; completion after expiry is fenced. Failed completion commit retains claim for recovery. Once claimed, cancellation/preference changes cannot revoke an already in-flight message; there is no exactly-once guarantee. At claim time revalidate all authoritative evidence/recipient/preferences. No background worker or automatic external activation.

Cancellation blocks active lease; unsent becomes Cancelled, Sent becomes Superseded while retaining acknowledgement. It coordinates incident back to Resolved, allowing existing engineering reopen to Investigating. Old event IDs remain historical and deduplicate forever. Reopened cases/new approvals require new explicit enqueue.

## Risks / Trade-offs
External acknowledgement cannot be atomic with local commit; stable key requires a future provider with documented deduplication/reconciliation semantics. Authorization/consent can change after claim; only local sink is supplied and production adapter remains unavailable pending policy. Driver history returns approved guidance/versions without private source text, contacts, tenant/owner mappings or original investigation prose.

## Migration Plan
Additive source/contracts only. No database migrations selected. No deployed service activation. Rollback removes the additive feature; retained records must be preserved by any future durable migration.
