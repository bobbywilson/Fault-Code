## ADDED Requirements

### Requirement: Original MCP consumer authority
MCP SHALL authenticate each HTTP request with the existing Entra validation, filter discovery and authorize invocation using existing exact policies. Tool arguments and source instructions SHALL NOT supply tenant/actor/client/scopes or workflow intent. Mutation HTTP intent SHALL bind the exact operation independently of model-produced arguments.
#### Scenario: No confused deputy
- **WHEN** an agent requests a tool or retrieved text requests another action
- **THEN** original caller policy and trusted resource boundaries apply; no privileged service grants are merged and read permission never grants mutation
