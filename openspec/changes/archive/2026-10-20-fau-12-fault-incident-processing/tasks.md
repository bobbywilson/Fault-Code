# FAU-12 tasks
## 1. Intake and lifecycle
- [x] 1.1 Implement immutable incident/rules with full transition matrix and timestamp/token tests; document exact graph.
- [x] 1.2 Implement atomic context/store/source/key replay contracts and fail503 production adapter; verify known/unknown/provenance, concurrent duplicates, rollback/cancel and retry/history tests; document DEC-001 limitations.
## 2. Protected API
- [x] 2.1 Implement seven production operations and DTOs; exercise real signed JWT permissions, source spoofing, immutable owner access, filtering and conditional mutation tests; document routes and payloads.
- [x] 2.2 Add operation examples and explicit Docker inputs; verify route inventory, OpenAPI schema/examples/baseline compatibility and build-input scan.
## 3. Integrated verification
- [x] 3.1 Run locked restore/build/full tests/format/contracts/spec validation; inspect final diff and record exact evidence/limitations in docs/fau-12-verification.md.
## Workflow follow-up
- Officially archive and strictly validate main specs, commit and publish Part of FAU-12 PR, attach PR, verify exact-source CI and merge; confirm Linear remains open with native PR link.
- Launch FAU-13 chat carrying standing authorization and technical context. Production restart/crash/outbox acceptance remains pending DEC-001; do not mark durable production persistence delivered.
