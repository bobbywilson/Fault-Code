# Fault Code

Independent API-first agentic automotive fault investigation proof of concept. The initial ASP.NET Core backend foundation is implemented; business workflows remain specified for later tickets.

## Start here
- [Run and test the backend](docs/local-development.md)
- [API conventions and contracts](docs/api-conventions.md)
- [Docker development and reset](docs/docker-development.md)
- [Architectural decisions](docs/decisions.md)
- [Development workflow](docs/development-workflow.md)
- [22-ticket traceability](docs/backlog.md)
- [Normative requirements](openspec/specs/)
- [Codex instructions](AGENTS.md)

Workflow A: identify an approved known fault, retrieve documentation, investigate, notify driver and record results/status.
Workflow B: identify an approved applicable patch/remedy, retrieve evidence, assess dealership service, notify driver and record recommendation. Never deploy vehicle software.
Workflow C: confirm no approved definition, escalate to engineering, track investigation, approve/publish knowledge, link findings and notify affected drivers while preserving history.

## Project resources
GitHub: https://github.com/bobbywilson/Fault-Code (private)
Linear workspace: https://linear.app/fault-code
Linear project: https://linear.app/fault-code/project/fault-code-976d37e9ded9
Team: Fault Code (FAU), Free plan. Existing issue IDs retained; see traceability for numbered order.

## OpenSpec
Requires Node.js 20.19+. Tool version pinned in commands to official `@fission-ai/openspec@1.14.3`.

```sh
OPENSPEC_TELEMETRY=0 npx --yes @fission-ai/openspec@1.14.3 validate --all --strict
OPENSPEC_TELEMETRY=0 npx --yes @fission-ai/openspec@1.14.3 list --specs
```

Specs are versioned in GitHub, not only a temporary agent environment. Before implementation follow the complete lifecycle in docs/development-workflow.md. Database, notification provider and deployment target remain unresolved; no application features, tenant or infrastructure are provisioned.
