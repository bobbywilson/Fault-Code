# knowledge-retrieval Specification Delta

## Purpose

Define observable Fault Code knowledge retrieval requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Approved document ingestion
Ingestion SHALL preserve source/version/approval/access metadata, produce deterministic chunks and OpenAI embeddings, and maintain a rebuildable Qdrant vector index.

#### Scenario: Approved document ingestion acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a demo approved document is ingested again, replaced or deleted
- **THEN** duplicates are avoided; replacement/deletion updates retrieval; model dimensions and collection compatibility are checked

### Requirement: Traditional grounded retrieval
Traditional RAG SHALL retrieve permission-filtered approved document chunks with source citations and communicate missing or conflicting evidence; document text SHALL not override tool authorization.

#### Scenario: Traditional grounded retrieval acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** retrieval returns low-confidence evidence, unapproved material or malicious instructions
- **THEN** restricted/draft chunks are excluded, uncertainty is explicit and no unsupported repair or unauthorized tool action occurs
