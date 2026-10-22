# Design
## Context
See proposal. FAU-10 supplies scoped production controllers, exact authorization, safe problems, ETags and test-only transactional adapters. DEC-001 is unresolved.
## Goals / Non-Goals
Deliver provider-independent fault lifecycle. No durable production store, ontology administration, RAG or vehicle installation.
## Decisions
- Stable tenant-local normalized `SYN-EPS:...`, `SYN-SBW:...`, `SYN-ADAS:...` codes; immutable code/system. PUT replaces a bounded draft. DELETE retires, preserving code reservation and history.
- Every edit appends immutable revision data. Publish snapshots the exact current draft with server approval actor/time, increments aggregate version and leaves old publications immutable. Draft edits never alter published lookup. Retirement hides all guidance.
- Operator+faults.write creates/edits/retires; Engineer+knowledge.publish publishes; faults.read inspects management data; knowledge.read accesses published-only lookup. App roles remain exact. No raw role/scope bypass.
- Ontology interface validates classification ID/version/system and each remedy kind against approved taxonomy. Unavailable production adapter fails503; test adapter supplies demo typed taxonomy. No guessed production taxonomy or silent validation fallback.
- Advisory remedy kind `guidance` or `dealership`; patch metadata only on dealership remedy. Every remedy has approval state; engineering publication approves only explicitly approved remedies. Lookup requires system/model/software context and returns only approved matching remedies. No executable patch URL or deployment operation.
- Store contract atomically commits revision/current state/replay/audit, rollback and detached tenant snapshots. Reuse strong If-Match semantics; create and publish require scoped replay keys; publish replay precedes version checks. Test store proves one process only.
## Risks / Trade-offs
- DEC-001 unresolved -> production503, no durable audit claims. Adapter must later prove crash/restart/multi-process transactions.
- FAU-13 unresolved -> unavailable ontology adapter503; production cannot publish invalid/unverified links.
- Old publication remains available during draft editing -> version-specific provenance and explicit retire remove guidance.
## Migration Plan
Additive routes/contracts, no package/schema migration. Production registration deliberately unavailable. Roll back by reverting additive source; no data migration exists.
