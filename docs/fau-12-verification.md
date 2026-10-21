# FAU-12 verification

Part of FAU-12. Provider-independent incident processing and seven protected production API operations. Production storage/context remains unavailable503 pending DEC-001. Linear stays open for personal review.

## Local verification — 2026-10-20

SDK10.0.402/.NET10.0.13, with both DOTNET_ROOT and PATH per local-development.md:

```sh
dotnet restore FaultCode.slnx --locked-mode
dotnet build FaultCode.slnx -c Release --no-restore
dotnet test FaultCode.slnx -c Release --no-build
dotnet format FaultCode.slnx --verify-no-changes --no-restore
python3 scripts/check-build-inputs.py
python scripts/check-api-contract.py artifacts/openapi/faultcode-v1.json contracts/openapi-v1.json
```

162 tests passed (22 Domain,3 Application,137 integration), zero skips; Release build zero warnings/errors. Locked restore, formatting and whitespace checks pass. OpenAPI schema/examples and baseline compatibility pass; all7 breaking fixtures rejected, optional additive input accepted. 57 explicit build inputs scanned with no recognized credential patterns. No package or lockfile changes. Route inventory now29 protected operations across21 OpenAPI paths. Local Docker backend remains unavailable; Linux container CI is separately required on the exact PR source before merge.

## Acceptance evidence

| Scenario | Observed evidence |
|---|---|
| Known and unknown intake | IncidentTests exercises actual production controllers using signed JWT middleware and test-only transactional adapter; known snapshot has approved publication2, exact reference/classification versions and only applicable approved remedy; unknown accepted with null knowledge |
| Draft/unapproved/inapplicable boundary | Private management draft is excluded, unapproved/other-model remedies excluded, other software gives empty approved remedy list; unpublished/retired yield no approved snapshot |
| Source identity and replay | Source derives from validated azp; unknown sourceClientId input rejected400;16 concurrent events with distinct keys converge to1 incident/intent/audit; actor/key changes replay same source result; changed source payload409, different authenticated client is separate source |
| Dependency outage vs unknown | Context503 aborts new intake; committed replay succeeds without context reads even after20days; no outage is translated to unknown |
| Invalid fleet/event context | Missing resources404; inactive vehicle/driver, closed/wrong/future interval, malformed owner, system mismatch, non-UTC/future/old timestamp, invalid IDs/code/model/software/source400; cross-tenant context404 |
| Immutable history/ownership | Later context deactivation, reassignment, identifier/version change and knowledge retirement do not alter original incident/publication/association; original owner retains access; new owner404/empty filtered list; cross-tenant404; huge offsets preserve accurate counts |
| Lifecycle matrix | Domain exhaustively checks36 status pairs; application executes all36 from valid histories, only7 edges accepted; invalid/repeated/terminal/stale changes409 without corruption |
| Visible failure/retry | Failure retains incident state and stable work ID, increments attempt and stores safe code/history; failed work blocks advance; retry requeues same ID without duplicate intent and retains past failure; retry without failure409; exact failure/retry/transition replays do not append history/work/audit |
| Atomicity and concurrency |10 competing transitions yield1success/9conflicts; failure-before-commit leaves no incident/source/replay/history/work/audit; failure update rollback retains original Pending state; thrown callback/cancellation-before-commit rollback; detached reads cannot clear committed data |
| Authorization and source safety | Real JWT401; Administrator write403; missing knowledge scope403 on intake/reads; read requires faults.read plus exact knowledge.read plus own/all vehicle permission; other-owner404 despite spoof query; own Reader cannot mutate; exact machine roles accepted; existing FAU-9 full matrix remains passing |
| HTTP contract | Strong/missing/weak/list/zero/leading-zero preconditions400; bad/missing keys400; unknown body fields400; safe correlated Problem Details; ETag/Location on create/replay; pagination/status validation; production storage503 distinct from missing404 |
| Public OpenAPI and images | Existing public routes remain anonymous; generated security metadata describes all ANDed policies and vehicle alternatives; operation-specific request/response examples validate; new source files explicitly allowlisted into Docker inputs |

Self-review checked the final domain/application/controller/DTO/DI changes against the specs, original owner mapping, scoped reads before pagination, source-event uniqueness independent of actor/key, replay before context/clock validation, approved immutable nested content, exact publication/reference/classification provenance, mutation rollback and stable work identities, no external dispatch in transaction callbacks, no production memory fallback and unchanged packages. Review identified and fixed knowledge-read permission on incident snapshot reads. Same-account GitHub approval is not fabricated.

## Lifecycle and limitations

Official OpenSpec change validated strictly before implementation; apply instructions inspected and tracked work verified. Official archive synchronizes7 added requirements across incident-lifecycle, vehicle-driver-management and api-contracts; main specs are strictly validated before commit. PR includes Part of FAU-12, synchronized specs/code/tests/runbooks. Exact-source CI and merge are verified separately from local evidence.

IIncidentStore requires a single consistent transaction with authoritative fleet/approved knowledge context and atomic incident/source/replay/audit/history/pending-work updates. Test adapter proves one-process semantics only. No production adapter, relational engine/schema/migrations, durable outbox dispatcher, retention, restart/crash/multiple-process verification exists until DEC-001. This is not durable production persistence. Production always503; no silent memory fallback. Pending work/status reports are not proof of agent investigation, engineering escalation or provider delivery; those integrations remain FAU-17/18/19. No external notifications, live tenant, cloud deployment or vehicle software operations were performed. See incident-processing.md for precise contracts and remaining production acceptance.
