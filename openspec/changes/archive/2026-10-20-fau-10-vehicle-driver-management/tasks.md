## 1. Specification
- [x] 1.1 Inspect live FAU-10, baseline specs and merged FAU-9; document scoped design and unresolved DEC-001.
- [x] 1.2 Strict validate proposal before implementation.
## 2. Implementation
- [x] 2.1 Implement provider-free records, transaction contract, validation and application lifecycle rules.
- [x] 2.2 Add authorized API contracts, concurrency/replay, tenant and ownership filtering, unavailable production adapter.
- [x] 2.3 Test real controllers with test-only transactional storage and signed bearer tokens; verify rollback, replay, audit, association and security failures.
## 3. Delivery
- [x] 3.1 Run build/tests/format/contracts/Docker inputs/strict specs; review and record evidence plus durable-adapter limits.
- [x] 3.2 Official archive and validate main specs; prepare versioned PR-ready delivery evidence. Exact-source CI, merge and Linear linkage are observed separately after publishing the archived patch.

Official archive completed with the self-referential archive task still pending (6/7); after observed synchronization of four requirements into three main specs and strict validation of all20 specs, this final archive task is marked complete. Durable production adapter acceptance is outside this completed provider-independent change and remains pending DEC-001.
