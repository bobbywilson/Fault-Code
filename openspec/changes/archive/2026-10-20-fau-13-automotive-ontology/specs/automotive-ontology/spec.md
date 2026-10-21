## ADDED Requirements
### Requirement: Tenant ontology catalog management
Ontology SHALL expose tenant-local bounded draft replacement for versioned subsystem, component, symptom, classification, severity, applicability and remedy concepts across the three synthetic steering systems. Stable IDs SHALL retain kind/system; invalid references, duplicate IDs/edges, incompatible relationship types and is-a/part-of cycles SHALL reject400 atomically. Draft/history management SHALL require knowledge.publish; approved reads SHALL require knowledge.read.
#### Scenario: Typed management and denial
- **WHEN** an Engineer or exactly granted application replaces a catalog or a Reader attempts management
- **THEN** valid catalog changes succeed with strong versions; invalid graphs reject400, stale versions409 and unauthorized callers401/403; drafts never appear in approved reads
### Requirement: Immutable ontology approval
Publication SHALL snapshot the exact draft with increasing version, approver and server UTC time. Published versions SHALL remain retrievable and support exact classification ID/version/system and remedy-kind validation after later edits. Unknown/unapproved versions SHALL return404 for retrieval and false for taxonomy validation; unavailable authoritative storage SHALL return503.
#### Scenario: Classification compatibility
- **WHEN** FAU-11 validates an approved classification or an incompatible/missing link
- **THEN** only exact approved compatible identity/version/system/kinds succeed; later publication cannot rewrite old approval and unavailable is never invalid or unknown
### Requirement: Deterministic pinned rule evaluation
Rules SHALL validate symptom/classification/severity concept references, optional exact model/software and permitted remedy kinds. Evaluation SHALL require explicit approved version and known same-system symptom IDs, order by priority then specificity, and reject potentially overlapping equal-rank conflicting outcomes. Results SHALL retain immutable inputs, matched rules, chosen outcome, version, explanation and actor/time; no-match SHALL be explicit.
#### Scenario: Rule precedence and historical result
- **WHEN** general and specific rules match and a later publication changes the rule
- **THEN** deterministic priority/specificity selects the original pinned rule, stored results preserve original provenance and later evaluations use only the requested approved version
#### Scenario: Contradiction and missing concepts
- **WHEN** equal-rank overlapping rules disagree or evaluation references unknown/wrong-system symptoms
- **THEN** reject400 without committing changes/results; no arbitrary conflicting outcome becomes guidance
### Requirement: Ontology transaction and replay boundary
Catalog changes, immutable revisions/publications/evaluations, replay and safe tenant/actor/client/correlation audit SHALL commit atomically or roll back on exception/cancellation. Create, publish and evaluate SHALL use scoped canonical payload replay keys; exact retries SHALL return original results before version revalidation and changed payload SHALL conflict409. Production storage SHALL remain unavailable503 until approved durable implementation.
#### Scenario: Concurrent retries and storage failure
- **WHEN** identical concurrent requests, changed retries, cancellation or adapter failure occur
- **THEN** one result is committed for exact retries, changed retries conflict, failures commit nothing and another tenant cannot retrieve results
