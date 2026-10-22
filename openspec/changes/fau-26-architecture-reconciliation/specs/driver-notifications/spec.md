## ADDED Requirements

### Requirement: Consent-backed workflow and publication fan-out
Notifications SHALL use a real provider with explicit consent, approved applicable meanings/remedies, durable outbox/retries/acknowledgements/audit and duplicate suppression. Unknown faults SHALL receive investigation-pending messages without diagnosis; new approved knowledge SHALL trigger durable reevaluation/follow-up for relevant unresolved incidents. No vehicle software installation is permitted.

#### Scenario: Publication and reliable follow-up
- **WHEN** an engineer publishes missing knowledge while a provider fails or the worker restarts
- **THEN** all relevant work is durably retried without duplicate accepted notifications, original evidence/consent and attempts are retained and missing delivery is never reported as sent


#### Scenario: Full architecture acceptance details
- **WHEN** this capability is evaluated for public release
- **THEN** all of the following contract details apply: Known approved meaning/action and applicable approved-patch dealership guidance SHALL be delivered through a real provider only to consenting participants. Unknown faults SHALL receive investigation-pending messages without diagnosis. Approved new knowledge SHALL trigger durable reevaluation and follow-up notification for every relevant unresolved incident/affected consenting driver. Transactional outbox, idempotency, retries, acknowledgement, uncertain acceptance, consent revocation, crash/restart and duplicate suppression SHALL be audited and verified. No vehicle software installation is permitted.

### Requirement: Simplified fault email workflow
The service SHALL use exact approved-code Qdrant lookup, agent-drafted evidence-grounded messages and the explicitly selected email transport. Unknown codes SHALL create investigation incidents and investigation-pending messages. Resolution SHALL trigger follow-up using the known-fault message path. Datadog telemetry SHALL record processing outcomes without secrets or email bodies.

#### Scenario: Similarity is not a known code
- **WHEN** Qdrant returns a similar document without an exact approved and applicable fault-code match
- **THEN** the service retains an unknown investigation incident and drafts no diagnosis

#### Scenario: Resolved unknown fault
- **WHEN** engineering approves the missing definition and affected incidents are reevaluated
- **THEN** the agent drafts an approved-source notification and durable dispatch prevents duplicate notification messages

### Requirement: Private Mailpit demo capture
The local and Linux demo configurations SHALL support isolated Mailpit capture with relay disabled, server-side transport configuration and private inbox/SMTP bindings. Only approved example.com demo addresses SHALL be accepted by the capture adapter. Mailpit acceptance SHALL NOT be reported as Microsoft365 or external customer delivery, agent execution or end-to-end fault workflow completion.

#### Scenario: Real capture verification
- **WHEN** the real Mailpit integration check submits a test message
- **THEN** Mailpit stores the intended recipient, body and notification reference, with a returned receipt verified through its API

#### Scenario: Linux deployment paused
- **WHEN** the application deployment remains paused
- **THEN** Linux capture configuration may be prepared without starting shared-host services or claiming installation

#### Scenario: Retry uncertainty
- **WHEN** an acknowledgement is lost after a capture request
- **THEN** durable dispatcher reconciliation and duplicate prevention remain required; a stable Message-ID alone does not prove exactly-once delivery
