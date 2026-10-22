# Approved workflow clarification — 2026-10-21

This later user instruction supersedes the reconciliation prompt where it requires editable ontology or a more elaborate primary workflow. Implementation and external acceptance remain pending; this document records requirements, not completion.

1. An authenticated vehicle consumer posts its vehicle identity and fault code to the existing versioned API. Validate ownership, replay/idempotency and consent before processing.
2. Look up the normalized fault code in Qdrant using an exact payload filter. Semantic similarity may retrieve supporting documents but must never establish that a different or unapproved code is known. Index entries must carry definition identity/version, approval and applicability evidence. Stale or conflicting entries cannot authorize a diagnosis.
3. For a verified known fault, the Microsoft Agent Framework/OpenAI agent prepares a structured email from approved meaning, applicability, recommended action and source evidence. Deterministic validation and dispatch send it through Microsoft 365 to the consenting associated driver/customer. Record delivery, retry failures and prevent duplicate sends. No vehicle software deployment.
4. For an unknown fault, retain the vehicle report and create an investigation incident. The agent drafts an investigation-pending email without inventing a diagnosis. Dispatch through Microsoft 365 and record delivery.
5. Engineering resolution supplies an approved definition/remedy. Reevaluate affected unresolved incidents and send the same evidence-grounded driver notification used for an initially known fault. Knowledge resolved does not mean vehicle repaired.
6. Expose bounded authenticated MCP operations describing and accessing the service. The agent uses authorized tools; credentials stay server-side. Microsoft 365 dispatch and Datadog telemetry integrations are bounded application adapters, not unrestricted model access. Log ingestion, lookup, incident transitions, agent/MCP activity and email outcomes through OpenTelemetry to Datadog. Verify actual receipt before claiming integration complete.
7. Ontology is a non-editable domain relationship table. No creation forms or management controls in the Angular ontology page. Existing write endpoints require explicit reconciliation before removal; hiding controls is not proof those APIs are read-only.
8. Seed clearly synthetic drivers, vehicles and steering/driveline fault examples relevant to the demonstration. Do not claim invented codes are genuine Nexteer diagnostic codes or use Nexteer branding/proprietary data. Public source material may inform terminology only after verification.

## Retained constraints

Keep the approved API-first ASP.NET Core/Angular/Spartan stack, dedicated Entra tenant, consent, authorization, source evidence and isolated deployment. Qdrant is a derived lookup/retrieval index, not a transactional incident/delivery store. Relational database selection remains pending approval. Deployment remains paused. No emails, purchases, tenant modifications or shared-host changes were performed by this clarification.

## Current gaps

Production stores and email delivery are unavailable adapters; the agent-to-MCP bridge, real Microsoft 365 delivery and real Datadog ingestion still need implementation/configuration and external acceptance. The existing demo seed is a prepared synthetic manifest, not a populated database. The audit matrix must retain these gaps rather than treating the simplified design as completed behavior.

## Fleet workspace clarification

Use synthetic Chevrolet Silverado and GMC Sierra vehicle examples. Vehicles opens a paginated list with active associated drivers and fault-code dispatch through the existing authenticated ingestion endpoint. Drivers opens a list containing recorded email addresses and notification preferences. Remove Fault definitions, Documents, Engineering, Agent activity and Operational health from the UI and redirect their old URLs to Incidents. Preserve backend services required for knowledge lookup, investigation and telemetry; this UI reduction does not delete stored data or remove security controls. A successful ingestion response is not evidence of email delivery.
