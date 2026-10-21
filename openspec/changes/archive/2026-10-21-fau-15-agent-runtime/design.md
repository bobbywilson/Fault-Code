# Design

## Context
Part of FAU-15; ADR-004 approves Microsoft Agent Framework/OpenAI. FAU-8/9 identity and FAU-12 intake snapshots exist. Production IIncidentStore still returns503 pending DEC-001. FAU-16 tools and FAU-17 investigation are separate.

## Goals / Non-Goals
Provide a protected bounded advisory invocation with deterministic real-framework tests. No domain writes, run persistence, MCP tools, generated repair prose, publication, notification, external provisioning or live paid requests.

## Decisions
Application owns vendor-free IAgentProvider/AgentRuntimeService DTOs. API reuses trusted IncidentService.Get and exact named policies. Empty request rejects unknown fields. Original caller remains in run context; only incident ID/system/code and approved intake classification/publication metadata go to model, no driver/contact/token details. Fresh framework agent/session per invocation. No retrieved document text accepted by this foundation; later FAU-17 must validate retrieval/ontology separately.

Pin Microsoft.Agents.AI.OpenAI 1.25.0, OpenAI2.14.0 and System.ClientModel1.15.0 in Infrastructure, verified net10 target from official NuGet feed. Domain/Application have no vendor dependencies. Official guidance: https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs and https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/openai ; OpenAI strict schema: https://developers.openai.com/api/docs/guides/structured-outputs . Use ChatClientAgent with OpenAI Chat Completions, strict raw schema, no tools, explicit max output and disabled SDK retry. Fixed api.openai.com endpoint, redirects/logging disabled. Model remains explicit configuration pending DEC-004 snapshot choice; activation not part of this implementation.

One overall linked deadline includes loading/provider/retry. Per-attempt reserve conservative UTF8-byte input upper bound plus schema/instructions overhead and max output; charge reservation even on network failure because billing is unknown. MaxRetries0..2; total reservation must fit. SDK automatic retries disabled. Tool budget fixed0 now; positive config rejected until FAU-16. Output usage/finish/model validated. No model/tool calls can mutate domain because no tools or mutating services are available. Bounded validated output enum/action/evidence references only; raw provider prose excluded. Unknown incident knowledge returns undetermined plus review, never unknown diagnosis. Classification can only equal approved intake classification with exact publication/classification versions; this does not claim current ontology/rule adjudication.

## Risks / Trade-offs
Model snapshot, dedicated API key, outbound network and durable stores remain activation blockers. Tests mock OpenAI HTTP while exercising actual SDK/framework. Live smoke is manually opt-in and invokes the same adapter with synthetic input and single bounded attempt; no live call here. Ephemeral run IDs are not durable audit/history. No generic text generation endpoint. Conservative reservation can reject runs before provider usage; explicit configuration documents this. No automatic retries of malformed/refused/tool responses.
