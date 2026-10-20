# Specification-driven development workflow

OpenSpec defines requirements and decisions; Linear tracks work; GitHub versions specs and implementation; Codex executes explicitly authorized tasks. No previous chat is required: start from AGENTS.md, docs/backlog.md, docs/decisions.md and openspec/specs/.

## Per-ticket lifecycle
1. Read the numbered Linear ticket and its traceability row. Verify identity, exact repository, branch, current status and dependencies.
2. Read existing relevant main specs, ADRs, active changes and applicable local instructions. Search before creating a capability; do not duplicate specs.
3. Create a scoped change using `npx --yes @fission-ai/openspec@1.14.3 new change fau-N-short-purpose` (or the generated Codex propose skill). Inspect `status --change NAME --json` and `instructions ARTIFACT --change NAME --json`.
4. Write proposal, full requirement/scenario deltas, design decisions and tasks in dependency order. Include acceptance, tests, errors, permissions and affected components. Resolve architectural decisions with the user before dependent implementation; do not silently choose a database, channel or host.
5. Validate with `npx --yes @fission-ai/openspec@1.14.3 validate NAME --strict`. Specification readiness does not authorize application work. Obtain the user's implementation instruction before applying development changes.
6. When authorized, implement the ready scope and tests together. Keep specs synchronized with approved behavior changes. Use synthetic data; never deploy vehicle software.
7. Run the ticket's focused tests, contract/security checks and evaluation cases; record exact commands/results and limitations. Do not mark unrun tests as passing or pending dependencies complete.
8. Review the diff and spec-to-code traceability. Mark tracked tasks complete only with evidence, then validate the change again.
9. Complete official archive using `npx --yes @fission-ai/openspec@1.14.3 archive NAME` after tasks/tests are complete; inspect CLI prompts, resulting main specs and archive. Never bypass failed validation. Archive is not proof of application delivery.
10. Run `npx --yes @fission-ai/openspec@1.14.3 validate --all --strict`. Commit the main specs, archived artifacts and authorized code together. Verify GitHub contains the commit so destroying the environment cannot lose requirements.
11. Link the commit/PR to Linear using `Part of FAU-N`, include evidence and spec paths, and request review where applicable. Do not use closing keywords or auto-transition issues without approval. Actual integration-event confirmation is distinct from an installed app or a pasted URL.

## Naming and review
Branches: `fau-N-short-purpose`; commits/PR titles: `FAU-N: concise outcome`; PR body: `Part of FAU-N`, relevant spec/change paths, tests and pending decisions. Avoid Fixes/Closes/Resolves unless explicitly approved. No invented deadlines or assignees. Keep unrelated changes untouched. Use the regular checkout; no worktrees unless requested.

## Reproducible tooling
Node.js 20.19+ (local 22.13.0 used); official OpenSpec pinned to 1.14.3. Run commands from repository root. `OPENSPEC_TELEMETRY=0` opts out of anonymous CLI usage telemetry. `openspec init --tools codex` produces repository-local Codex skills. Validate and commit tool updates intentionally.

## Setup boundaries
Setup may initialize and version specifications and instructions only. Entra tenant, cloud services, subscriptions, database, application source, deployment and vehicle software operations remain unperformed. GitHub integration permission approval and end-to-end verification remain explicit external setup steps.
