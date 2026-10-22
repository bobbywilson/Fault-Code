# FAU-6 verification

Part of FAU-6. Local macOS evidence on 2026-10-20:

- Native Release build: passed, no warnings/errors.
- Native tests: 27 passed (4 Domain, 3 Application, 20 Integration), including new readiness/configuration/failure cases.
- Missing-key Compose configuration: rejected with FAULTCODE_QDRANT_API_KEY guidance.
- Demo-key Compose configuration: passed without printing resolved secrets.
- Bash smoke syntax and diff whitespace: passed.
- Local Docker runtime: unavailable; `docker info` outside sandbox cannot connect to desktop-linux daemon. No local container health/persistence or hostname activation claimed.

## Linux and cross-platform acceptance

Source commit b006dfb on PR https://github.com/bobbywilson/Fault-Code/pull/3:

- Linux container smoke passed: https://github.com/bobbywilson/Fault-Code/actions/runs/39478024437 (2m18s). Verified image builds, all three service health checks, authenticated service DNS readiness, non-root API, no published Qdrant ports, build-context secret-canary exclusion, proxy Host routing, Qdrant interruption/recovery, demo EPS point retention across restart and down/up, and absence after explicit volume deletion.
- Windows/Linux/macOS native checks all passed: https://github.com/bobbywilson/Fault-Code/actions/runs/39478024452. Each runs locked restore, Release build, all 27 tests and formatting.
- Smoke test uses unique project/image names, ephemeral credentials/ports and disposable volumes. It refreshes the API port after restart because Docker can reassign random published ports.
- Invalid-startup tests use actual isolated processes to avoid WebApplicationFactory's deferred-host disposal race; invalid credentials, transport failure and cancellation also have automated coverage.
- GitHub integration visibly attached PR #3 to Linear FAU-6. Issue remains Backlog/open; PR remains unmerged for user review.

Official OpenSpec 1.14.3 archive: openspec/changes/archive/2026-10-20-fau-6-docker-development/. Post-archive `validate --all --strict` passed all 20 main specs (50 normative requirements), with no failures. Local Mac Docker/hostname limitations above remain separate from Linux CI acceptance.
