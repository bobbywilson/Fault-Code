# fault-investigation Specification

## Purpose
Define observable Fault Code fault investigation requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Workflow A known fault
For a known fault the system SHALL identify the stored approved definition, retrieve technical support, investigate with agent/domain rules, generate driver guidance and record results and notification status.

#### Scenario: Workflow A known fault acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an incident matches an approved synthetic definition without an applicable patch
- **THEN** an evidence-backed known-fault result and notification request are persisted with citations, rule/version references and eventual delivery status

### Requirement: Workflow B approved patch
For a known fault with an approved applicable patch the system SHALL assess dealership service and record the recommendation, supporting evidence and driver notification; it SHALL NOT deploy software to vehicles.

#### Scenario: Workflow B approved patch acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** approved patch applicability matches the incident vehicle context
- **THEN** the driver receives the justified dealership recommendation; no tool, API or workflow performs vehicle software installation

### Requirement: Unknown versus unavailable knowledge
Investigation SHALL distinguish absence of an approved definition from provider/retrieval outage and route unsupported or conflicting evidence explicitly.

#### Scenario: Unknown versus unavailable knowledge acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** no approved definition exists, or the lookup dependency is unavailable
- **THEN** an actually unknown code escalates to engineering; outage is a retryable failure and never falsely confirms that no definition exists

### Requirement: Grounded investigation routing
Investigations SHALL classify KnownFault, KnownFaultWithApprovedPatch, UnknownFault or InsufficientEvidence using current approved definitions, pinned ontology rules and canonical applicable citations. Provider outage SHALL fail retryably without confirming unknown. Only deterministic approved content SHALL supply guidance; the model SHALL NOT author repairs or approve patches.

#### Scenario: Supported A and B routes
- **WHEN** an accessible synthetic incident has an approved definition, matching pinned rule/classification and approved applicable remedy with supporting cited source/version
- **THEN** A records KnownFault with the approved guidance; B records KnownFaultWithApprovedPatch with the exact approved dealership patch metadata and referral proposal; neither deploys software

#### Scenario: Unknown C route and outage
- **WHEN** current approved lookup confirms404 or a dependency fails
- **THEN** confirmed absence records UnknownFault with engineering escalation proposed; outage returns503 with no result or proposed action committed

#### Scenario: Conservative groundedness threshold
- **WHEN** evaluating deterministic fixtures A/B/C, low scores, contradictions, absent rules, unapproved/inapplicable patches and injected text
- **THEN** routing accuracy and exact citation/rule provenance are100 percent with zero unsupported guidance; support requires score at least0.85, exact approved source/version and ordinal full-chunk equality to approved remedy text; all returned citations must support the same remedy text; differing individually approved remedies, any retrieved conflicting/unmapped text or absent support yield InsufficientEvidence and engineering review

### Requirement: Atomic investigation history and replay
Investigation result, immutable evidence versions, proposed action intent, scoped replay and actor/client/correlation audit SHALL commit atomically with cancellation rollback. Replays SHALL bind tenant/actor/client/incident/expected version/key. Original incident ownership and actor/client SHALL govern history and replay reads. Production storage SHALL fail503 until approved durable adapters exist.

#### Scenario: Concurrent retry and history
- **WHEN** identical conditional investigation requests run concurrently
- **THEN** one result and one proposed intent/audit commit; exact retries return the original result without model execution, changed payload or stale incident version409; paginated history is tenant/owner and original actor/client filtered before counting to protect publisher-only evidence

#### Scenario: Cancellation failure and changed context
- **WHEN** cancellation, deadline, invalid model output, failed commit or concurrent authoritative context changes occur
- **THEN** commit no partial result/replay/audit/intent; durable adapter acceptance requires coordinated consistent incident/knowledge/ontology/document snapshots across the asynchronous transaction and crash/restart tests

### Requirement: Proposed actions without dispatch
Investigation SHALL record notification, dealership referral or engineering escalation proposals with Pending status, never infer permission to execute them. Notification delivery/status belongs to FAU-19 and engineering cases to FAU-18. History SHALL preserve the original evidence and proposals without reinterpreting old results as currently approved guidance.

#### Scenario: Malicious evidence or model action
- **WHEN** retrieved text or model output requests publication, dispatch, credentials or vehicle actions
- **THEN** it confers no execution authority, no MCP invocation or incident transition occurs and malformed model output is rejected
