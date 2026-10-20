# Fault Code — Codex instructions

Read README.md, docs/decisions.md, docs/development-workflow.md, docs/backlog.md and relevant openspec/specs/*/spec.md before every task. These versioned files are authoritative; never assume prior conversations. Inspect active OpenSpec changes before creating another.

Follow all 15 user-required OpenSpec lifecycle responsibilities documented in docs/development-workflow.md. Each ticket names exact relevant specs, ADRs, scenarios, tests, dependencies and deliverables. Validate before implementation, keep approved specs/code synchronized, commit to GitHub, reference the stable FAU ID and complete the official archive workflow. Mark tasks complete only from observed evidence.

Application implementation is not authorized by this setup. No Entra tenant/cloud provisioning, purchases, invitations, relational database selection, GraphRAG or vehicle software deployment. Synthetic EPS, steer-by-wire and steering ADAS data only. Never change other project resources. Do not silently replace approved stack; request approval for unresolved architecture.

Use ASP.NET Core/C# API-first, Angular/TypeScript with Spartan UI/Tailwind, dedicated Entra Free identity (PKCE/browser and client credentials/machine), Microsoft Agent Framework/OpenAI, Qdrant traditional RAG, permission-aware MCP, provider-independent OpenTelemetry/Datadog with optional Sentry, generated OpenAPI/Mintlify and Docker Compose/Linux. Every UI operation needs an API operation; public docs need no login but protected API requests do.

Use `Part of FAU-N` references; automatic issue closure is prohibited without approval. Do not commit secrets or real driver data. Preserve existing files/history. Keep spec requirements distinct from implemented behavior. Use regular checkout, no worktrees unless requested.
