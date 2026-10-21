# Fault investigation

Part of FAU-17. Trusted application orchestration combines current approved fault lookup, pinned ontology evaluation and canonical traditional RAG with bounded Microsoft Agent Framework/OpenAI assessment. The model receives curated synthetic context only; no MCP tools, identity token, secrets, environment inventory, filesystem, shell or general network capability. The read services behind MCP are reused in-process under the original caller; MCP remains a separate authenticated consumer interface.

## API

| Operation | Policies and inputs |
|---|---|
| POST /api/v1/incidents/{id}/investigations | faults.write AND faults.read AND vehicles.access.read AND knowledge.read; body{}; strong If-Match and Idempotency-Key |
| GET /api/v1/incidents/{id}/investigations | faults.read AND vehicles.access.read AND knowledge.read; page>=1, pageSize1–100, default1/20 |

Both load original trusted incident tenant/ownership. Lists additionally filter original actor/client before pagination/count: another actor's publisher-only source evidence cannot leak through history. Exact delegated scopes plus allowed human roles or exact machine app roles apply; no grant union. Missing identity401, missing policy403, hidden incident404, invalid400, stale/changed retry409, unavailable503, deadline504 and existing safe agent422/429/502 outcomes use correlated Problem Details. POST requires Operator with all scopes or the four machine roles; read-only callers cannot create persisted results. Input cannot supply identity, instructions, citations, model, approval or tool intent.

Returned data contains server ID/correlation/UTC, original incident version, current approved definition/publication/classification/approval, rule input/outcome/approval/version, exact canonical citation metadata/text, selected approved remedy, uncertainty, model usage metadata and a pending proposed action. Definition/citation text remains historical source evidence, never current dispatch authority. All response objects use explicit API DTOs. Existing advisory /agent-runs behavior and schemas remain unchanged.

## Routing policy investigation-v1

| Classification | Required support | Proposed action |
|---|---|---|
| KnownFault | Current approved definition, pinned matching classification/rule, approved applicable remedy and exact supporting citation | notification/Pending |
| KnownFaultWithApprovedPatch | Same support plus deterministic approved dealership patch metadata matching exact model/software | dealership-referral/Pending |
| UnknownFault | Current authoritative approved lookup404, independent of intake snapshot | engineering-escalation/Pending |
| InsufficientEvidence | Weak/missing/unmapped/conflicting evidence, absent rule/publication or model review request | engineering-review/Pending |

Only current approved lookup404 confirms absence. Knowledge, ontology, canonical documents, embeddings, index or model outages fail without a committed result; they never confirm unknown. Unknown classification does not rely on retrieval finding nothing. An intake snapshot with no definition may later resolve against published knowledge.

Support is deliberately conservative: each returned citation must have score>=0.85, an exact source ID/version in the approved fault references and full chunk text ordinal-equal to an approved applicable remedy. The pinned deterministic rule must match the definition classification and allow the remedy kind. Any returned chunk not meeting this support policy suppresses guidance. The existing retrieval floor0.75 still applies; scores0.75–0.85 are insufficient. This policy abstains on many semantically useful documents. It does not claim general semantic contradiction detection or production measured accuracy. Future semantic grounding changes need explicit spec/evaluation updates. Rules use approved definition symptom identifiers as taxonomy evidence, not new observed telemetry; full rule input remains visible.

Only approved publication text supplies selected guidance; the model emits no repair prose. The fixed schema echoes incident/classification/citation IDs and reviewRequired. It can downgrade a known result to review, never promote the deterministic route. Unknown/missing/duplicate/wrong-case fields, changed provenance, extra actions or tool calls fail. Each call uses a fresh tool-free agent and the existing explicit snapshot/configuration, response buffer, deadline and aggregate token/retry limits. Input is bounded at24000UTF8 bytes with at most5 canonical citations; oversized context fails rather than silently discarding evidence. Native strict JSON schema is selected by a trusted in-process purpose enum, never public request fields. Provider success usage is recorded; failed-attempt billed usage is unknown. No live call or model selection is performed by this ticket.

## Persistence and proposed work

IInvestigationStore requires coordinated authoritative incident/knowledge/ontology/document reads throughout the asynchronous transaction and commit across processes. Result, one Pending proposal, replay and safe actor/client/correlation audit commit atomically or roll back on cancellation/failure. Published evidence and old results remain immutable. Reentrant adapters must join the same transaction; separately nested uncoordinated service locks cannot satisfy this contract. Durable restart/crash/retention tests remain required after DEC-001/003 selection.

Replay binds tenant/actor/client/incident/key and expected incident version. Identical concurrent retries return one stable result before rechecking model/knowledge/retrieval; current incident ownership is still checked. Changed expected version or stale new request409. Distinct keys request independent investigations. A successful model call followed by failed commit may be billed again on retry: exactly-once provider billing is not claimed.

Production UnavailableInvestigationStore returns503; there is no production memory fallback. One-process copy-on-write test storage is not durable storage and does not prove coordinated production cross-store transactions. Normal identity remains disabled/unprovisioned and existing stores503. No dispatch occurs: incident status/history/work stay untouched, notification/referral delivery and eventual delivery status remain FAU-19, engineering case creation remains FAU-18. Pending proposals do not imply an outbox/dispatcher exists. No publication, notification, vehicle deployment, account/provider provisioning or network change occurs.

## Evaluation

InvestigationTests runs deterministic fixtures for A/B/C, score0.85/0.8/0.5, missing/contradictory/injected sources, unapproved/inapplicable patches, missing rule/publication and wrong source/classification versions. Acceptance requires100 percent fixture routing and exact provenance, zero unsupported selected guidance, plus replay/rollback, authorization, schema and bounded-runtime tests. These are synthetic acceptance thresholds, not observed production metrics. The actual SDK mocked-HTTP test checks native strict schema, no tools and curated context. Existing separate Qdrant CI exercises the real retrieval adapter with deterministic embeddings; no live OpenAI inference is claimed.

All returned citations must support the same selected approved remedy text. Different individually approved remedy texts are also unadjudicated conflict and require review. API responses carry an explicit untrusted historical evidence policy.

FAU-18 now offers separately authorized [engineering case creation](engineering-escalation.md) from stored UnknownFault/InsufficientEvidence results. Investigation results/proposals remain immutable historical evidence; no automatic case creation or permission is inferred from them.
