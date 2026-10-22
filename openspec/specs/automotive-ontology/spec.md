# automotive-ontology Specification

## Purpose
Define observable Fault Code automotive ontology requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Typed domain concepts
The ontology SHALL define versioned EPS, steer-by-wire and steering ADAS concepts, classifications, relationships and deterministic domain rules, without GraphRAG.

#### Scenario: Typed domain concepts acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** an authorized engineer creates relationships and rules
- **THEN** valid types and references are accepted; duplicates, invalid references and prohibited cycles are rejected

### Requirement: Traceable rule evaluation
Investigation rule evaluation SHALL use approved versions and retain rule/evidence provenance without rewriting historical outcomes after taxonomy changes.

#### Scenario: Traceable rule evaluation acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a published rule changes after an incident investigation
- **THEN** new investigations use the new approved version while the historical record retains its original rule version and explanation

### Requirement: Fault taxonomy validation boundary
Fault writes and publication SHALL validate exact approved classification identity/version/system and remedy kinds through an ontology contract. Missing, unapproved or incompatible links SHALL reject the mutation; unavailable validation SHALL fail503.
#### Scenario: Invalid taxonomy or remedy combination
- **GIVEN** a demo fault draft
- **WHEN** a classification is absent, unapproved, belongs to another system, or forbids the remedy kind
- **THEN** the mutation returns400 without changing revisions, audit or replay; unavailable ontology returns503

### Requirement: Tenant ontology catalog management
Ontology SHALL expose tenant-local bounded draft replacement for versioned subsystem, component, symptom, classification, severity, applicability and remedy concepts across the three demo steering systems. Stable IDs SHALL retain kind/system; invalid references, duplicate IDs/edges, incompatible relationship types and is-a/part-of cycles SHALL reject400 atomically. Draft/history management SHALL require knowledge.publish; approved reads SHALL require knowledge.read.
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
