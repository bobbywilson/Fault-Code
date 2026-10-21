# Fault code management

Part of FAU-11. Provider-independent synthetic knowledge lifecycle and production API controllers are implemented. Production storage remains503 pending DEC-001; ontology validation now uses the FAU-13 approved-version adapter, returning503 when ontology storage is unavailable. No durable production audit/replay, database, tenant or cloud service was provisioned. Test storage is test-only, one-process copy-on-write.

## API and permission boundary

| Route | Method / exact policy |
|---|---|
| `/api/v1/faults` | GET faults.read; POST faults.write |
| `/api/v1/faults/{id}` | GET faults.read; PUT/DELETE faults.write |
| `/api/v1/faults/{id}/history` | GET faults.read |
| `/api/v1/faults/{id}/publications` | POST knowledge.publish |
| `/api/v1/faults/lookup` | GET knowledge.read |

All require dedicated Entra authentication. Delegated permission is exact scope AND allowed human role per authorization.md: Operator writes; Engineer publishes; listed read roles read only with their corresponding scope. Machine clients need exact Faults.Read.All/Faults.Write.All/Knowledge.Publish/Knowledge.Read.All application roles. No role hierarchy, new Entra permission value, own-read fallback or Administrator bypass. Tenant/actor/client come exclusively from validated tokens; correlation is server-generated. Knowledge-only clients cannot inspect management drafts/history.

POST creation requires Idempotency-Key. PUT/DELETE and publication require one strong positive quoted If-Match; publication also requires Idempotency-Key. Same syntax and400/409 rules as vehicle-driver-management.md. Keys are scoped to tenant+actor+client+operation (publication includes fault ID); canonical create payload normalizes code only, preserving exact content. Publication payload includes fault ID and expected version. Exact retry returns original ID/version/ETag before current-version or taxonomy revalidation, without new audit. Changed payload409. Results are retained indefinitely by the contract; retention requires DEC-001. All successes include correlation; mutation results are `{id,version}`, creation adds Location; single get/lookup/mutations add ETag. List/history use `{items,page,pageSize,totalCount}`, default1/20 maximum100, deterministic code ordering (history ascending revision). System/search/retired filter before count/page. Search is literal case-insensitive code/description substring, maximum100. Large offsets return empty accurate pages. Unknown query keys cannot grant access.

## Definition and lifecycle

Stable server GUID and tenant-local normalized synthetic namespaces: EPS=`SYN-EPS:...`, steer-by-wire=`SYN-SBW:...`, steering-ADAS=`SYN-ADAS:...`. Code trim/uppercase; suffix1–40 ASCII uppercase letters/digits/hyphens with initial alphanumeric, total maximum60. Code/system are immutable; update body has content only and rejects unknown fields. Retired codes stay reserved. DELETE retires, no hard deletion/reactivation; update/publish retired records409. Other-tenant/missing IDs404.

Content contains description1–1000, classification ID1–100 and positive exact version, 1–20 symptoms of1–200 characters, 1–20 technical reference identity/version pairs (100/40 characters), and0–20 remedies. Text is nonblank with no control characters. Remedy IDs are unique within the revision; descriptions1–1000, kind guidance/dealership, approval boolean, exact synthetic model (`SYN-` plus ASCII uppercase letters/digits/hyphens, max40), exact software token(max40 ASCII letters/digits/dot/underscore/hyphen). No applicability wildcards, range inference or client dates. Patch metadata is allowed only for dealership remedies, includes bounded advisory reference and exact same applicable software version. No download/install/control endpoint exists.

IFaultTaxonomy must verify exact approved classification identity/version/system and allowed remedy kinds. Unknown, draft, wrong-version, wrong-system or prohibited combination returns400 without mutation; unavailable service503. The registered ApprovedOntologyTaxonomy validates immutable approved ontology publications; ClassificationVersion is the ontology publication version. Missing/unapproved links fail400 and unavailable ontology storage503. See automotive-ontology.md.

Each creation/edit/publication/retirement appends immutable content/state/actor/server UTC history. Engineering publication snapshots the current draft and approval actor/time, increments version, and retains earlier publications. Publishing explicitly approves the selected remedies whose approval flag is true; false remains unapproved even within a publication. Updating a published definition creates a new draft and leaves the previous publication available. No operator edit can change its guidance or approval provenance. Retirement immediately hides guidance while retaining all prior evidence. History returns stored revisions with actor/time for authorized management readers; IDs and tenant are not client-controlled approval fields.

## Approved lookup

GET lookup requires code/system/model/software query context; unknown/unpublished/retired codes404, invalid context400, unavailable knowledge503. Current approved snapshot includes immutable version, classification version, reference versions, approval actor/time, symptoms and only remedies with approved=true AND exact model/software match. No matching remedy returns a known approved definition with an empty remedy list, preserving the distinction from unknown code. Draft/unapproved/inapplicable patches never appear. Patch metadata can recommend dealership service only. FAU-12 must preserve the returned definition/publication/reference versions in incident provenance; FAU-14/17 must consume this approval/applicability boundary rather than management drafts.

## Transaction and deployment limits

IKnowledgeStore requires detached tenant snapshots, atomic records/history/publications/replay/audit, cross-process uniqueness and version checks, rollback on exception/cancellation and safe audit actor/client/correlation/operation/resource/version/server time. Records/content use immutable collections so snapshots cannot mutate committed history. No request content enters audit. The test adapter proves these semantics within one process only.

Before production activation, DEC-001 must resolve relational engine/provider, schemas/migrations, constraints/isolation, durable replay/audit retention and outbox; approved adapters need restart/crash/multiple-connection/cancellation/retention acceptance. Ontology validation/publication uses exact approved versions under FAU-13; its store remains unavailable pending DEC-001. Entra live registration/consent, browser client, RAG, incident processing, deployment and vehicle software operations are not delivered by this ticket. Operational logs/liveness do not prove knowledge durability or readiness.

FAU-18 adds a separate explicit engineering resolution approval transaction with escalations.write AND knowledge.publish plus incident read policies. It atomically publishes proposed bounded knowledge with case/incident/event state and pinned approved ontology/document evidence. Existing draft CRUD/publication APIs remain unchanged. See [engineering escalation](engineering-escalation.md).
