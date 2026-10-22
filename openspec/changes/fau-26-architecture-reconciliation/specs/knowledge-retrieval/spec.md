## ADDED Requirements

### Requirement: Canonical evidence and live retrieval verification
Traditional RAG SHALL use Qdrant/OpenAI embeddings with approved canonical synthetic document/incident knowledge, exact deterministic fault matching, source citations, access/applicability filtering and unsupported-answer rejection. Live-service acceptance SHALL be separate from fixture tests; relational records remain authoritative.

#### Scenario: Live retrieval acceptance
- **WHEN** indexing/retrieval or a model answer is claimed verified
- **THEN** actual OpenAI/Qdrant responses and canonical evidence/permissions are checked, with mocks labeled and unsupported answers rejected


#### Scenario: Full architecture acceptance details
- **WHEN** this capability is evaluated for public release
- **THEN** all of the following contract details apply: Qdrant SHALL index embeddings for approved synthetic documents and appropriate incident knowledge using configured OpenAI embeddings and traditional RAG. Canonical relational records remain authoritative. Exact code matching SHALL remain deterministic and vector similarity SHALL NOT establish a fault definition. Retrieval evaluation SHALL verify source correctness, permissions, applicability, uncertainty and unsupported-answer rejection using real configured services as well as deterministic fixtures.
