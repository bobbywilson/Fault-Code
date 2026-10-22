# agent-runtime Specification Delta

## Purpose

Define observable Fault Code agent runtime requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Bounded CSharp agent execution
The agent SHALL use Microsoft Agent Framework in C# and OpenAI with configured model, timeout, token/tool budgets, cancellation and structured responses.

#### Scenario: Bounded CSharp agent execution acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** execution reaches a budget limit, provider outage or cancellation
- **THEN** the run terminates with a typed outcome and traceable error; invalid output does not mutate domain state

### Requirement: Typed evidence-bearing responses
Agent output SHALL include incident/run identity, classification, evidence/citations, rule versions, uncertainty, recommended action and escalation requirement.

#### Scenario: Typed evidence-bearing responses acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a model emits malformed output or unsupported claims
- **THEN** schema validation rejects it or returns an explicit failure; deterministic tests use mocked providers and live tests require controlled authorization/cost
