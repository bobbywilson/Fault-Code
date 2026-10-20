# Fault Code backlog traceability

The 22 stable Linear IDs are preserved. Numbered titles express approved sequence; do not recreate issues to renumber IDs. FAU-5 adds the initial backend foundation; business features remain unimplemented.

| Order | Linear | Title | Main specification capabilities | Dependencies |
|---|---|---|---|---|
| 01 | [FAU-25](https://linear.app/fault-code/issue/FAU-25) | Configure Fault Code Linear Workspace and GitHub Integration | `openspec/specs/project-governance/spec.md` | None |
| 02 | [FAU-26](https://linear.app/fault-code/issue/FAU-26) | Initialize OpenSpec and Capture Project Architecture | `openspec/specs/solution-architecture/spec.md`, `openspec/specs/development-lifecycle/spec.md` | FAU-25 |
| 03 | [FAU-24](https://linear.app/fault-code/issue/FAU-24) | Establish OpenSpec, GitHub, Linear, and Codex Workflow | `openspec/specs/development-lifecycle/spec.md`, `openspec/specs/project-governance/spec.md` | FAU-26 |
| 04 | [FAU-5](https://linear.app/fault-code/issue/FAU-5) | Initialize ASP.NET Core Solution | `openspec/specs/solution-architecture/spec.md` | FAU-24 |
| 05 | [FAU-6](https://linear.app/fault-code/issue/FAU-6) | Configure Docker Development Environment | `openspec/specs/development-infrastructure/spec.md` | FAU-5 |
| 06 | [FAU-7](https://linear.app/fault-code/issue/FAU-7) | Establish API-First Architecture | `openspec/specs/api-contracts/spec.md` | FAU-5 |
| 07 | [FAU-8](https://linear.app/fault-code/issue/FAU-8) | Configure Microsoft Entra ID Authentication | `openspec/specs/identity/spec.md` | FAU-5 |
| 08 | [FAU-9](https://linear.app/fault-code/issue/FAU-9) | Implement API Authorization and Permissions | `openspec/specs/authorization/spec.md`, `openspec/specs/identity/spec.md` | FAU-7, FAU-8 |
| 09 | [FAU-10](https://linear.app/fault-code/issue/FAU-10) | Implement Vehicle and Driver Management | `openspec/specs/vehicle-driver-management/spec.md`, `openspec/specs/authorization/spec.md` | FAU-9 |
| 10 | [FAU-11](https://linear.app/fault-code/issue/FAU-11) | Implement Fault Code Management | `openspec/specs/fault-knowledge/spec.md`, `openspec/specs/automotive-ontology/spec.md` | FAU-9 |
| 11 | [FAU-12](https://linear.app/fault-code/issue/FAU-12) | Implement Fault Incident Processing | `openspec/specs/incident-lifecycle/spec.md`, `openspec/specs/vehicle-driver-management/spec.md` | FAU-10, FAU-11 |
| 12 | [FAU-13](https://linear.app/fault-code/issue/FAU-13) | Implement Automotive Ontology Management | `openspec/specs/automotive-ontology/spec.md` | FAU-5, FAU-9 |
| 13 | [FAU-14](https://linear.app/fault-code/issue/FAU-14) | Configure Qdrant and Traditional RAG | `openspec/specs/knowledge-retrieval/spec.md` | FAU-6, FAU-7, FAU-9 |
| 14 | [FAU-15](https://linear.app/fault-code/issue/FAU-15) | Integrate Microsoft Agent Framework | `openspec/specs/agent-runtime/spec.md` | FAU-5 |
| 15 | [FAU-16](https://linear.app/fault-code/issue/FAU-16) | Implement MCP Tools | `openspec/specs/mcp-tools/spec.md`, `openspec/specs/authorization/spec.md` | FAU-9, FAU-11, FAU-12, FAU-14, FAU-15 |
| 16 | [FAU-17](https://linear.app/fault-code/issue/FAU-17) | Implement Agent Fault Investigation | `openspec/specs/fault-investigation/spec.md`, `openspec/specs/agent-runtime/spec.md` | FAU-12, FAU-13, FAU-14, FAU-15, FAU-16 |
| 17 | [FAU-18](https://linear.app/fault-code/issue/FAU-18) | Implement Engineering Escalation | `openspec/specs/engineering-escalation/spec.md`, `openspec/specs/fault-knowledge/spec.md` | FAU-11, FAU-12, FAU-14, FAU-17 |
| 18 | [FAU-19](https://linear.app/fault-code/issue/FAU-19) | Implement Driver Notifications and Dealership Referrals | `openspec/specs/driver-notifications/spec.md`, `openspec/specs/fault-investigation/spec.md` | FAU-10, FAU-11, FAU-17, FAU-18 |
| 19 | [FAU-20](https://linear.app/fault-code/issue/FAU-20) | Implement Observability Provider Abstraction | `openspec/specs/observability/spec.md` | FAU-5 |
| 20 | [FAU-21](https://linear.app/fault-code/issue/FAU-21) | Integrate OpenTelemetry and Datadog | `openspec/specs/observability/spec.md` | FAU-15, FAU-20 |
| 21 | [FAU-22](https://linear.app/fault-code/issue/FAU-22) | Build Angular Administration Dashboard | `openspec/specs/administration/spec.md`, `openspec/specs/api-contracts/spec.md`, `openspec/specs/identity/spec.md`, `openspec/specs/authorization/spec.md` | FAU-18, FAU-19, FAU-21 |
| 22 | [FAU-23](https://linear.app/fault-code/issue/FAU-23) | Implement Mintlify Documentation, Automated Testing, and Deployment | `openspec/specs/documentation-delivery/spec.md`, `openspec/specs/fault-investigation/spec.md` | FAU-6, FAU-19, FAU-21, FAU-22 |

## Milestones
1. Project and Specification Foundation: 01–03
2. Application Foundation: 04–08
3. Domain and Knowledge: 09–13
4. Agentic AI: 14–18
5. Administration and Observability: 19–21
6. Documentation and Delivery: 22

Existing FAU-24 is repurposed as lifecycle ticket 03; its former release scope is consolidated into FAU-23 (22). Remove obsolete release blockers on FAU-24 to avoid a lifecycle dependency cycle. Review new dependencies against this table.
