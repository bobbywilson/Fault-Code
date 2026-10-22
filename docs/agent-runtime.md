# Bounded advisory agent runtime

Part of FAU-15. POST /api/v1/incidents/{id}/agent-runs accepts empty JSON `{}`. Requires faults.read AND vehicles.access.read AND knowledge.read; existing Entra catalog unchanged. Original validated tenant/actor/client/correlation and trusted intake ownership are retained. Own-read Reader sees only their incident; all-read requires its separate permission. Missing/cross-owner/cross-tenant404; unknown request fields400. Default disabled503; production incident store503 pending DEC-001.

Every POST creates an ephemeral run GUID, UTC start/correlation, model and advisory-v1 instructions version with a fresh conversation. No run persistence, billing replay, incident/history/work mutation, notification, publication or vehicle operation. Repeated POST may incur another provider call. FAU-16 tools and FAU-17 investigation are separate.

Only trusted incident ID, demo code/system/model/software and immutable approved intake definition/publication/classification IDs/versions reach the model. No driver/contact data, caller prompt, credentials, private draft/remedy prose, document text or client-supplied evidence. Output classification must be approved intake classification with exact evidence or undetermined with empty evidence. Missing intake knowledge never establishes unknown fault. This foundation does not retrieve documentation or evaluate ontology rules; FAU-17 must separately integrate FAU-11 approved remedies, FAU-13 ontology and FAU-14 citations.

Strict output includes incidentId, classification, evidence[{definitionId,publicationVersion,classificationId,classificationVersion}], uncertainties[], recommendedAction and escalationRequired. Unknown/duplicate/missing JSON fields, foreign incident/provenance, invented versions/actions, invalid usage/model, refusal, truncation and tool calls fail closed. Uncertainty enums: missing-evidence, unadjudicated-evidence, investigation-pending. Action always engineering-review; escalationRequired true. No generated repair prose becomes guidance. All results advisory/review-required.

## Compatibility and activation

Infrastructure pins Microsoft.Agents.AI.OpenAI1.25.0, OpenAI2.14.0 and System.ClientModel1.15.0; lock files pin transitives. Official NuGet nuspec includes net10.0. Domain/Application have zero vendor packages. Sources checked October21,2026: https://api.nuget.org/v3-flatcontainer/microsoft.agents.ai.openai/1.25.0/microsoft.agents.ai.openai.nuspec ; https://learn.microsoft.com/en-us/agent-framework/agents/structured-outputs ; https://learn.microsoft.com/en-us/agent-framework/integrations/by-component/model-providers/openai ; https://developers.openai.com/api/docs/guides/structured-outputs .

Agents defaults disabled with no model/key. After DEC-004 model selection, configure Agents__Enabled=true, Agents__Model=<approved dated snapshot>, Agents__ApiKey=<dedicated secret>. No model selected/live compatibility claimed. Identifier must end YYYY-MM-DD and returned model must match exactly; approved model must support chat completions/output cap/strict schema. Limits: Agents__TimeoutSeconds1..120(default30), Agents__MaxOutputTokens64..2048(default512), Agents__TotalTokenBudget1024..64000(default16000), Agents__MaxRetries0..2(default0), Agents__MaxToolCalls=0. Positive tool budgets rejected until future tool implementation. Invalid enabled settings fail startup safely.

Fixed HTTPS api.openai.com/v1/chat/completions, no URL override, redirects or SDK/HTTP content logging. Response buffer64KiB. Native OpenAI strict schema explicitly set. ChatClientAgent UseProvidedChatClientAsIs disables default automatic function invocation/continuation; no tools registered. SDK retry policy0; application controls retries. Linked deadline covers storage/provider/backoff; caller cancellation propagates OperationCanceledException/aborted HTTP, deadline504. Each attempt reserves conservative UTF8 input/instructions bytes +4096 schema/protocol allowance +max output. This is admission control, not measured tokens or monetary guarantee. A retry needs another full reservation, including after uncertain billed failures. Only429/transient unavailable retries,100ms*attempt backoff within deadline. No output repair/tool/refusal retries. Input/output counters are successful returned usage only; reservedTokens covers all attempts, failed billed usage remains unknown.

Safe fixed failureCode values: agent-not-configured503, agent-rate-limited429, agent-provider-unavailable503, agent-provider-rejected503, agent-timeout504, agent-invalid-output502, agent-refused502, agent-provider-invalid502, agent-budget-exhausted422, agent-tool-budget-exhausted422. Correlated Problem Details excludes provider bodies/prompts/keys. Storage failure503 never confirms unknown knowledge. Health is not runtime/model/storage readiness. Compose isolation and no outbound activation remain unchanged.

## Tests and opt-in live smoke

AgentRuntimeTests exercise actual signed API human/machine permissions, trusted resources, spoofing, schema/provenance attacks, aggregate budgets/retries, cancellation/deadline, config and zero domain mutations. AgentProviderTests mock HTTP through actual framework/SDK, testing fixed endpoint, strict schema, output cap, exact request counts, unsolicited tool/refusal/length responses, malformed envelopes and safe failures. No paid CI calls.

Opt_in_live_OpenAI_demo_single_request_smoke is a local/CI no-op without explicit opt-in; its ordinary pass is not live verification. After separate model/key/cost authorization, securely supply FAULTCODE_AGENT_LIVE_SMOKE=authorized-demo-single-request, FAULTCODE_AGENT_LIVE_MODEL=<approved dated snapshot> and FAULTCODE_AGENT_LIVE_KEY=<dedicated test secret>, then run:

```sh
dotnet test tests/FaultCode.IntegrationTests -c Release --filter FullyQualifiedName~Opt_in_live_OpenAI_demo_single_request_smoke
```

Same service/provider, one demo unknown-knowledge incident in test-only memory,30second deadline, output512, conservative budget8000, zero retries/tools. It asserts typed advisory/review output. This fixture is not durable production persistence. No live call, credentials, tenant/cloud provisioning, deployment, model selection or outbound production networking was performed.

## FAU-16 interface isolation

The MCP transport is implemented separately from this runtime. No MCP tools are registered with the model, MaxToolCalls stays0, automatic invocation stays disabled and returned tool calls still fail closed. Entra tokens, provider credentials, filesystem/shell/general network capabilities and consumer intent headers never enter model context. See mcp-tools.md.

## FAU-17 investigation assessment

The additive [investigation API](fault-investigation.md) composes trusted approved knowledge/rules/RAG and stores bounded structured assessment separately. Its fixed investigation schema is selected by trusted application code; existing advisory endpoint/schema remains unchanged. MCP tools remain disconnected from the model. No environment/credential/execution capability is added. Production investigation storage503 pending DEC-001/003.

## Recorded incident resolutions

Resolving an incident with investigation notes commits its status and one follow-up notification. Before Agent Framework runs, the service embeds the exact fault code and resolution and upserts an incident-specific document into a separate Qdrant resolutions collection. The permission-bound notification_fault_lookup tool retrieves that document and requires equality with the canonical SQL incident resolution. notification_email_send revalidates the facts and server-bound recipient before Mailpit delivery. Resolution evidence does not publish an approved fault definition or imply vehicle repair. Index or agent failures retain the SQL resolution and failed notification for retry; sent notifications cannot be resent by replay.
