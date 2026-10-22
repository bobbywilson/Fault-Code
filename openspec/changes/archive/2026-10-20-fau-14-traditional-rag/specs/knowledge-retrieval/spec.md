## ADDED Requirements

### Requirement: Canonical demo document lifecycle
Documents SHALL retain tenant-local originals, immutable approved versions, server approver/time, source version, system, exact model/software applicability and tenant or publisher access. Exact knowledge.publish SHALL manage drafts, publication, deletion and reindex. Draft edits SHALL NOT alter approved content; deletion SHALL withdraw retrieval. Production canonical storage SHALL return503 until approved durable storage exists.
#### Scenario: Draft approval and withdrawal
- **GIVEN** an Engineer with exact knowledge.publish
- **WHEN** a document is created, edited, published, replaced or deleted
- **THEN** strong versions prevent lost updates, source identity stays reserved, prior approvals remain immutable, drafts never become evidence, deleted publications cease retrieval and client approval/tenant fields reject400

### Requirement: Reproducible document index
Chunks SHALL have deterministic tenant/document/publication/page/offset/checksum identities and preserve original page text and source citations. OpenAI model/dimensions and chunk algorithm SHALL identify an isolated cosine Qdrant collection. Collection/model/vector incompatibility and provider failure SHALL return503; retries and reindex SHALL converge without duplicates. Qdrant SHALL remain derived, never canonical.
#### Scenario: Repeat replace and reindex
- **WHEN** approved demo pages are indexed repeatedly, replaced or reindexed after index loss
- **THEN** identical chunk IDs converge, new approved versions replace retrieval, originals reproduce chunks, dimensions and collection distance are checked, malformed/nonfinite/zero vectors fail503 and provider outage never confirms unknown knowledge

### Requirement: Authoritative evidence retrieval
Search SHALL require exact knowledge.read, filter tenant/approval/system/applicability/access before vector ranking and revalidate hits against canonical approved chunks. Only tenant-visible or original-publisher documents SHALL be accessible. Low/no evidence SHALL be explicit. Conflicting source text SHALL remain unadjudicated evidence, never automatic repair guidance. Document instructions SHALL confer no authorization or tool execution.
#### Scenario: Access malicious text and missing evidence
- **GIVEN** tenant-visible, publisher-only, draft, stale and malicious demo documents
- **WHEN** a Reader, publisher or application searches
- **THEN** only matching current approved accessible chunks meet the fixed score floor; citations include page/source/publication/checksum/approval; missing evidence is explicit, returned text is labelled untrusted and no model generation/tool action occurs
#### Scenario: Unindexed approval
- **WHEN** eligible approved documents have no compatible completed index or canonical/vector/embedding dependencies fail
- **THEN** return503 rather than an empty result claiming absence; successful explicit reindex restores retrieval

### Requirement: Document concurrency replay and safe audit
Canonical originals, approvals, tombstones, index status, replay and safe actor/client/correlation audit SHALL commit atomically with rollback and cross-process serialization. Create/publication/delete/index/reindex SHALL bind canonical retry payloads to tenant/actor/client/operation. Exact retries SHALL retain original results; changed payloads or stale versions SHALL return409. Index side effects SHALL be idempotent and withdrawn evidence SHALL remain inaccessible after cleanup failure.
#### Scenario: Retry cancellation and partial cleanup
- **WHEN** concurrent identical requests, changed retries, cancellation or index cleanup failure occurs
- **THEN** retries converge, stale/changed requests conflict, canonical transactions roll back on failure, committed tombstones remain withdrawn and explicit retry can finish physical cleanup without duplicate approval/audit
