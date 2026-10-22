## MODIFIED Requirements

### Requirement: Caller scoped advisory runs
Runtime execution SHALL load trusted accessible incidents under original caller identity and exact policies, isolate conversation state and provide bounded investigation context. The agent SHALL invoke relevant authorized MCP tools through a server-managed allowlist; credentials, raw tokens, shell/filesystem and arbitrary network capabilities SHALL NOT reach the model. Source/tool text SHALL never grant authorization. Deterministic ingestion, exact lookup, persistence, lifecycle, event delivery and notification dispatch SHALL remain application-owned.

#### Scenario: Authorized MCP investigation
- **WHEN** the agent requests a relevant permitted tool for an accessible incident
- **THEN** the server binds original caller permissions/resources, validates inputs, executes through MCP, audits the invocation/result provenance and returns bounded evidence without autonomous publication/notification/vehicle actions

#### Scenario: Untrusted or unauthorized tool request
- **WHEN** model/source content requests an unlisted tool, another tenant resource or a privileged mutation
- **THEN** the request fails without service privilege union, credential disclosure or domain mutation

#### Scenario: Authorized incident analysis
- **WHEN** a permitted human or machine analyzes an accessible synthetic incident
- **THEN** bounded trusted context and explicitly authorized MCP results reach an isolated conversation, with correlated structured advice and no autonomous domain mutation

#### Scenario: Untrusted caller context
- **WHEN** caller/model/source content lacks permissions or supplies another tenant/owner identity, instructions or privileges
- **THEN** reject before protected tool execution or provider access; source text never supplies authorization

### Requirement: Aggregate execution bounds
Runs SHALL enforce timeout, bounded input/output and aggregate token/tool/retry budgets across model and MCP calls. Only bounded transient failures SHALL retry. Cancellation SHALL terminate downstream calls. Malformed/refused/unsupported output SHALL NOT trigger domain mutations, vehicle repair or budget expansion.

#### Scenario: Token and tool limits
- **WHEN** a model/tool request exceeds remaining budget, deadline or allowlist
- **THEN** execution stops with a structured recorded failure; no extra request/tool invocation runs and usage is never fabricated

#### Scenario: Grounded tool completion
- **WHEN** a bounded permitted MCP lookup succeeds
- **THEN** its authorization, source evidence, uncertainty and activity are preserved in the structured result rather than treating a tool response as an instruction or approval

#### Scenario: Deadline cancellation and retry
- **WHEN** timeout, caller cancellation or transient retry exhaustion occurs
- **THEN** terminate with safe structured outcomes and propagate cancellation to MCP/model work without hidden budget expansion
