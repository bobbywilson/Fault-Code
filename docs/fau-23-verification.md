# FAU-23 verification

Part of FAU-23. Read live native Linear requirements; current origin/main09e1f531 was fetched before creating regular branch fau-23-documentation-delivery. No worktree or subagent. OpenSpec proposal/design/tasks and documentation-delivery delta passed official1.14.3 strict validation before implementation.

## Observed local acceptance
- Locked .NET restore and Release build pass: zero warnings/errors. All428native test results pass (38domain/3application/387integration). Supplemental only; Linux remains delivery authority. No paid model or real Qdrant execution is inferred from opt-in method outcomes.
- REST compatibility checker passes all7breaking fixtures and additive optional fixture; generated OpenAPI/examples/v1 compatibility pass. All5actual MCP input/output schemas and examples validate. Only OpenAPI prose/titles change in application source; no model context/authority, Entra, storage or REST/MCP schema changes.
- Exact docs synchronization:77REST operations,74protected;6guides.5documentation gate tests and2release-evidence gate tests pass, including missing/failed/skipped/duplicate evidence, local-link/drift/public-read/proxy/origin/policy negatives. Extractor found64required actual deterministic cases in428passing results.
- Official pinned mint4.2.1085 validate/openapi-check/broken-links pass. openapi-check is deprecated in favor of validate. Actual8Chromium tests pass with clean anonymous contexts, no page JS errors, rendered styles, mobile no overflow, protected permission/401/403 reference and no sending button. Screenshots inspected separately. No hosted public URL or live authorized TryIt is claimed.
- Browser tests exposed test TypeScript being treated as Mintlify custom scripts when colocated with content. Corrected publishable root to mintlify/site, keeping tests/tooling outside; fresh8tests pass. Earlier failing runs are not acceptance.
- Mint CLI development dependency audit reports upstream35findings initially21high/9moderate/5low. Compatible audit fix did not resolve them. Explicit non-gating audit artifact retained, no clean audit claim; tooling does not enter API/admin runtime images. See documentation-delivery.md for scope/remediation limit.
- Docker daemon unavailable locally, verified again. Actual Linux container/release/Qdrant/admin and documentation jobs must pass on reviewed source before merge; no local container success claimed.

## Pending external acceptance
DEC-001canonical storage/outbox/migrations, DEC-002notification channel/contact/consent, DEC-003hosting/canonical storage/retention/backups, remaining DEC-004models/embeddings and DEC-005plan entitlements remain open. Synthetic local delivery/test transactions are not live durable acceptance. No host, database, tenant, subscription, credential, registry publication, cost or actual deployment is introduced. Release runbook preserves these prerequisites and demonstrates image-only disposable rollback separately.

## Linux acceptance
Initial Linux source9997f04 exposed an incorrect administration Docker build context in the new release script. Corrected it to the existing frontend/ context, preserving the established image inputs and network isolation. Added bounded probe timeouts. The failed candidate is not release acceptance; corrected source must pass all checks. Source review also replaced ambiguous bearer prose with explicit public-operation language and generated readable operation titles from stable operation IDs. Actual browser tests pass on that output; schema/security compatibility and dotnet format pass. Evidence gates now require all9injection/5outage/20invalid-identity cases, with a negative omitted-parameter fixture.


All exact-source Linux jobs pass on 7d366f2035a76c6684defaf64d052d8116a8e004:

| Check | Run | Observed duration |
| --- | --- | --- |
| Backend build/tests/contracts/specs/format | 39642060716 | 3m30s |
| Documentation and actual anonymous Chromium | 39642060300 | 2m6s |
| Private Linux archives and actual rollback | 39642060278 | 2m14s |
| Real authenticated Qdrant | 39642060595 | 53s |
| Isolated container lifecycle | 39642060595 | 2m20s |
| Administration/actual production image | 39642060645 | 2m58s |

Downloaded release archives and metadata from run39642060278; independently verified every SHA-256 checksum. Manifest source matches actual head, platform linux/amd64. Candidate API ID695fc379acf6 and admin4ded501cc64d; prior API740a35f9012a. Actual JSONL stages show prior09e1f531→candidate7d366f2→same-image restart→original-image/configuration rollback. Prior/rollback public fixtures compare exactly; script also asserts health, protected401, non-root and rejected blank application configuration. No canonical state or schema migration is involved.

Actual Linux documentation screenshot from corrected source e5e2778 was downloaded and visually inspected: readable styles/cards/warning and no fake deployed state. Latest7d366f2 official renderer/browser acceptance also passes after readable endpoint title and bearer-prose correction. Initial failed9997f04release remains failed evidence; no cancelled job is reported as passing.

Tracked tasks are complete from this observed implementation/verification. Official archive and final all-spec validation follow, then the complete exact-head check set is required again before normal merge. Public hosted docs, live TryIt and durable deployment remain explicitly outstanding external acceptance, not completed by image-only rollback.
