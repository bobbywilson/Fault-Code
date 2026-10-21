# agent-runtime Specification

## Purpose
Define observable Fault Code agent runtime requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Bounded CSharp agent execution
The agent SHALL use Microsoft Agent Framework in C# and OpenAI with configured model, timeout, token/tool budgets, cancellation and structured responses.

#### Scenario: Bounded CSharp agent execution acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** execution reaches a budget limit, provider outage or cancellation
- **THEN** the run terminates with a typed outcome and traceable error; invalid output does not mutate domain state

### Requirement: Typed evidence-bearing responses
Agent output SHALL include incident/run identity, classification, evidence/citations, rule versions, uncertainty, recommended action and escalation requirement.

#### Scenario: Typed evidence-bearing responses acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** a model emits malformed output or unsupported claims
- **THEN** schema validation rejects it or returns an explicit failure; deterministic tests use mocked providers and live tests require controlled authorization/cost

### Requirement: Caller scoped advisory runs
Runtime execution SHALL load a trusted incident under the original caller tenant/ownership, require faults.read, vehicles.access.read and knowledge.read, and return advisory output without changing domain state. Requests SHALL NOT supply identity, evidence, model instructions or privileges. Runs SHALL have server identity/correlation and isolated conversation state.
#### Scenario: Authorized incident analysis
- **WHEN** a permitted human or machine runs analysis of an accessible synthetic incident
- **THEN** only bounded stored incident/approved intake knowledge metadata reaches the model, original caller context is retained, and a unique correlated advisory response is returned without incident/history/work changes
#### Scenario: Untrusted caller context
- **WHEN** a caller lacks exact permission, requests another owner's/tenant's incident or supplies identity/instructions/evidence fields
- **THEN** authentication401, permission403, concealed resource404 or invalid input400 occurs before provider execution

### Requirement: Aggregate execution bounds
Runs SHALL enforce configured timeout, bounded input/output, aggregate token reservations, tool limit and retry limit. Only transient rate-limit/unavailable failures SHALL retry within remaining budget/deadline. Cancellation SHALL terminate the run; exhausted, malformed, refused or tool-bearing responses SHALL NOT trigger domain mutations or repair retries.
#### Scenario: Deadline cancellation and retry
- **WHEN** provider requests time out, caller cancellation occurs, or bounded transient retries exhaust
- **THEN** execution terminates with explicit safe timeout/cancelled/rate-limit/unavailable outcomes; hidden SDK retries and tools cannot exceed configured requests
#### Scenario: Token and tool limits
- **WHEN** another request cannot fit the remaining token reservation, output truncates, usage is absent/invalid or any tool call is returned
- **THEN** the run fails explicitly without another request or tool execution; reported usage is never fabricated

### Requirement: Validated advisory schema
Model output SHALL use an exact schema and server validation of incident identity, classification, approved provenance and bounded uncertainty. Only approved intake classification and publication references SHALL be accepted as evidence. Recommended action SHALL be engineering-review with escalation required; prose SHALL NOT be returned as executable guidance. Provider content SHALL NOT supply actor, approval or tool authorization.
#### Scenario: Unsupported or malicious output
- **WHEN** output has unknown/duplicate/missing fields, invalid identity/provenance, unsupported classification/action, malformed JSON or malicious tool instructions
- **THEN** explicit invalid-output or tool-budget failure returns no advisory content and no domain mutation
#### Scenario: Evidence limits
- **WHEN** no approved intake definition exists or sources conflict
- **THEN** no unknown-fault diagnosis is inferred; runtime classification stays undetermined when appropriate and review remains required; document retrieval/rule adjudication are deferred to FAU-17

### Requirement: Explicit runtime activation and safe failures
Runtime SHALL use the approved Microsoft Agent Framework/OpenAI adapter with pinned SDK versions and no default model/credential. Disabled configuration SHALL fail503 safely; enabled configuration SHALL require an explicit model snapshot. Errors SHALL exclude provider bodies, prompts, tokens and secrets. Live smoke SHALL be opt-in synthetic bounded execution and excluded from paid CI.
#### Scenario: Missing configuration or outage
- **WHEN** configuration is absent/invalid or provider fails
- **THEN** startup validation or a fixed correlated failure code identifies configuration/provider/timeout/output/budget failure without sensitive text; storage outage remains503 and never confirms unknown fault
