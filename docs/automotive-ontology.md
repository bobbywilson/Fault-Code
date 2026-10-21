# Automotive ontology management

Part of FAU-13. Typed, provider-independent synthetic steering ontology, deterministic rules and eight authorized API operations are implemented. The production `UnavailableOntologyStore` always returns503 pending DEC-001. The test-only adapter is one-process copy-on-write memory and is not durable production persistence.

| Route | Method / exact policy |
|---|---|
| `/api/v1/ontology/draft` | POST/PUT/GET knowledge.publish |
| `/api/v1/ontology/history` | GET knowledge.publish |
| `/api/v1/ontology/publications` | POST knowledge.publish |
| `/api/v1/ontology/publications/{version}` | GET knowledge.read |
| `/api/v1/ontology/evaluations` | POST knowledge.read |
| `/api/v1/ontology/evaluations/{id}` | GET knowledge.read |

Exact delegated scope plus allowed human role or granular application role follows authorization.md. Engineer+knowledge.publish / Knowledge.Publish.Application manages and approves. knowledge.read / Knowledge.Read.All reads approved catalogs and evaluates; knowledge.read alone cannot see drafts/history. Administrator and Operator have no implicit management authority. Tenant/actor/client are validated token claims; server correlation and UTC time supply audit provenance. DTOs omit tenant and reject unknown input fields, including approval spoofing. No Entra catalog values were added.

## Draft and typed references

A tenant has one catalog with server GUID. POST draft creates version1; PUT replaces the entire bounded draft atomically. Concept, relationship and rule arrays manage their respective entries; omission deletes only the current draft entry. Full replacement lets an engineer coordinate removals/references without intermediate invalid graphs. IDs are stable domain identifiers matching `SYN-` plus ASCII initial alphanumeric and remaining alphanumeric/underscore/hyphen, max100, compared ordinally. Concept kind and system remain reserved across all revisions, even after removal; relationship IDs retain kind/endpoints; rule IDs retain system while rule content may evolve.

Concepts have `id`, `kind`, `system`, `label`, `allowedRemedyKinds`. Kinds are subsystem, component, symptom, classification, severity, applicability, remedy; system is exactly EPS, steer-by-wire or steering-ADAS. Labels are nonblank1–200 characters without controls. Only classification concepts may list remedy kinds (`guidance`, `dealership`, unique, at most2). Max200 concepts,400 relationships,100 rules per catalog; at least1 concept. Empty relationship/rule arrays are allowed. Duplicate IDs and duplicate kind/from/to edges reject400.

Relationships have stable `id`, `kind`, `from`, `to`; both endpoints must exist in the same system and differ:

| Kind | From → To |
|---|---|
| is-a | same concept kind → same kind |
| part-of | component/subsystem → subsystem |
| indicates | symptom → classification |
| classifies | component → classification |
| mitigates | remedy → classification |
| applies-to | applicability → component/subsystem |

The combined is-a/part-of graph is acyclic, including mixed-edge cycles. Other types are direct typed associations, without transitive inference or GraphRAG. The catalog uses no graph database.

## Rules and approval

A rule has `id`, `system`,1–20 unique symptom concept IDs, a classification ID, severity ID, permitted remedy kinds, priority0–1000, and optional model/software pair. All references must have the exact expected kind and system. Remedy kinds must be a subset of the target classification's allowed kinds. Both applicability fields are null (all synthetic models/software) or both exact validated synthetic model/software tokens using the FAU-11 syntax. There are no arbitrary expressions, regex predicates, executable scripts or ranges.

Rules match when every required symptom is present and exact optional applicability matches. Precedence is priority descending, then specificity descending (symptom count plus1 for exact applicability), then ordinal rule ID. Potentially overlapping rules at equal priority/specificity must have identical classification, severity and remedy-kind outputs; otherwise draft validation rejects400. Different positive symptom sets can overlap when input contains both, and are checked accordingly. Different exact model/software pairs do not overlap. Higher-priority conflicting rules are allowed with explicit deterministic precedence. No matching rule returns explicit null outcome IDs, empty kinds/matches and an explanation; it does not establish an unknown fault diagnosis.

POST publications requires current If-Match and snapshots the whole validated draft, incrementing aggregate version and retaining approver/server time. Draft/history remain separate from approved retrieval. Exact numeric publication version is mandatory for approved reads and evaluation; draft versions404. Old publications are immutable and retained after edits/removals/new publications. No withdrawal or replacement of an old publication is exposed.

`ApprovedOntologyTaxonomy` now implements FAU-11 `IFaultTaxonomy` against this store. A fault's ClassificationVersion is the **ontology publication version**, not its fault publication version. ID must identify a classification in that exact approved publication/system, and each remedy kind must be permitted there. Missing/unapproved/incompatible returns false (fault mutation400); unavailable store throws503, never accepting fabricated classification. Immutable retained versions make validation stable across later catalog updates. FAU-11 retains its own approval boundary for fault remedies.

## Stored deterministic evaluation

POST evaluations accepts this synthetic context, with an Idempotency-Key:

```json
{"publicationVersion":2,"system":"EPS","symptoms":["SYN-EPS-SYMPTOM"],"model":"SYN-MODEL-1","softwareVersion":"1.0"}
```

Symptoms must identify existing same-system symptom concepts in the selected approved publication. A result has server ID, explicit publicationVersion, immutable canonical input, winner/classification/severity/remedy kinds, ordered matched rule IDs and explanation, evaluatedBy/evaluatedAt and approvedBy/approvedAt. Published content supplies the exact rule/condition/concept version. GET returns the stored result without recomputation; later publication never rewrites it. These are deterministic rule evidence for FAU-17, not an implemented agent workflow, vehicle diagnosis, engineering escalation, driver guidance or vehicle software operation. External document evidence integration remains later RAG/investigation work.

## Concurrency, replay and errors

PUT draft and POST publications require one strong positive quoted If-Match (aggregate version). Missing, weak, wildcard, list, zero or noncanonical versions400; stale409. POST draft/publications/evaluations require key1–100 ASCII letters/digits/hyphen/underscore. Key scope is tenant+actor+client+operation. Content canonicalization sorts concepts/relationships/rules by ID, symptoms/remedy-kind lists ordinally; input symptoms are sorted. Payload changes with the same key409. Exact create/publication/evaluation retry returns original ID/version/body/ETag before current-version/dependency checks; no extra audit/history/evaluation. Replay storage itself must be available. Keys/publications/results remain retained indefinitely by contract until DEC-001 defines safe retention.

Mutations and single reads return ETag; create/evaluate add Location; every response has fresh X-Correlation-ID. History pages1/20 default, maximum100, ascending revision; huge valid offsets give empty pages with accurate total. Invalid request400, invalid identity401, insufficient exact permissions403, missing/other-tenant/unapproved404, version/replay conflict409, unavailable503; errors are safe correlated Problem Details.

`IOntologyStore` requires detached tenant snapshots and atomic catalog/history/publications/evaluations/replay/safe audit with rollback on exception/cancellation and cross-process versions/uniqueness. Audit records safe actor/client/correlation/operation/ID/version/time, not content. Immutable nested records protect prior content. DEC-001 must supply approved relational engine/provider/schema/migrations, real concurrent connections, restart/crash recovery and retention acceptance before production activation. No provider, tenant, cloud infrastructure or durable outbox is provisioned by FAU-13.
