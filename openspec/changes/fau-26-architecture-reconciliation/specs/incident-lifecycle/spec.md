## ADDED Requirements

### Requirement: Knowledge resolution and physical repair separation
Fault events SHALL retain one-vehicle occurrences and nullable fault-definition links for unknown codes. A/B/C processing SHALL remain deterministic for lookup, persistence and transitions. Knowledge resolution SHALL have distinct state/evidence from physical vehicle repair; publishing a definition SHALL NOT mark a vehicle repaired. Unknown occurrences SHALL support repeated-code correlation, automatic engineering intake and investigation-pending notification without a fabricated diagnosis.

#### Scenario: Unknown and repaired-state boundary
- **WHEN** missing knowledge is approved or an unknown fault repeats
- **THEN** relevant unresolved occurrences are retained/correlated and reevaluation/follow-up work is queued durably while physical repair remains unconfirmed without separate authorized evidence
