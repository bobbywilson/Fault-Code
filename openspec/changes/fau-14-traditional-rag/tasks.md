# Tasks
## 1. Canonical lifecycle and deterministic evidence
- [x] 1.1 Implement immutable documents, validation/chunks, lifecycle/replay/audit/transaction boundaries; domain and service tests verify replacement/deletion/retry/concurrency and docs describe limits.
## 2. Provider adapters and API
- [x] 2.1 Implement configurable OpenAI and Qdrant adapters with dimension/profile/filter checks; HTTP tests verify safe failures and docs record explicit model/storage activation.
- [x] 2.2 Implement exact-policy API/DTOs/examples and fail-closed DI; signed-token tests verify human/machine access, spoofing, tenant isolation, approved-only citations and safe errors.
## 3. System verification
- [ ] 3.1 Run disposable real Qdrant acceptance with mock embeddings in Linux CI; verify index replacement/deletion/rebuild and malicious/empty/low-score evidence behavior.
- [ ] 3.2 Run full tests, build, format, OpenAPI schema/examples/baseline/PR-base compatibility and negative fixtures; self-review, update verification evidence and strict validate.
## Workflow follow-up
- Official archive then strict all-main validation; commit/push, passing exact-source PR checks, authorized merge, native Linear link with issue left open; continue FAU-15 new chat.
