# Tasks
## 1. Linux packaging and lifecycle
- [x] 1.1 Implement pinned Dockerfiles, allowlisted context, Compose isolation/persistence/proxy and environment/startup/reset documentation; verify Compose configuration and automated missing-key/context checks.
- [x] 1.2 Implement optional authenticated readiness with validation and failure tests; verify native tests and formatting.
- [x] 1.3 Add isolated Linux build/smoke CI covering health, DNS, non-root, secret exclusion, proxy, interruption/recovery, restart and retained/deleted data; verify smoke script syntax and CI definition.
## 2. Observed acceptance
- [x] 2.1 Run Linux container build/smoke and record passing runtime evidence for every scenario; inspect GitHub checks.
- [x] 2.2 Review diff/traceability and strictly validate all specs; persist commit/PR with Part of FAU-6 and leave issue open.
## Workflow follow-up
- After all tracked tasks pass, officially archive and strictly validate main specs, commit archive and update the PR evidence.
- Leave PR unmerged and Linear issue open for the user's personal review.
