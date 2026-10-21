## ADDED Requirements

### Requirement: Explicit current notification consumption
Immutable investigation proposals SHALL remain historical Pending evidence. An independently authorized notification consumer SHALL revalidate current approval/applicability/canonical evidence and original recipient, deduplicate by source result ID and record notification history separately; model output SHALL NOT authorize delivery.

#### Scenario: Historical proposal and current guidance
- **WHEN** a proposal is enqueued or its approval/source/recipient has changed
- **THEN** approved current A/B guidance may create one notification, changed evidence rejects without dispatch, original result remains immutable and no model tool or automatic action is introduced
