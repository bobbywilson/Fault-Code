# FAU-19 proposal

## Why
Approved investigations and engineering resolutions currently retain only pending intents. Authorized consumers need approved driver guidance, dealership referrals and honest retryable delivery history.

## What Changes
- Add provider-independent notification outbox, conditional enqueue/dispatch/cancellation and recipient history APIs.
- Add explicit synthetic local-sink preferences; keep existing email flags independent of consent.
- Revalidate current publication, canonical evidence, applicability, recipient and approval at enqueue and dispatch; deduplicate source IDs.
- Retain attempts, acknowledgement, backoff and cancellation history; production fails503 pending DEC-001/002/003.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `driver-notifications`: approved A/B/C templates, local preferences, atomic outbox, acknowledgement/retry/cancellation.
- `fault-investigation`: explicit authorized consumer of immutable proposals.
- `api-contracts`: additive protected notification and preference operations.

## Impact
Domain/Application notification records, shared authoritative store contract, Infrastructure unavailable adapters/local test sink, API DTOs/controllers/OpenAPI, security and synthetic integration tests. ADR-010/012 retained. Existing REST/MCP/model schemas preserved; no new grants, provider choice, external messages, vehicle actions or deployment.
