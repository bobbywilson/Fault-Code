# Tasks

## 1. Export boundary
- [x] 1.1 Add pinned SDK/OTLP exporter and explicit validated selection; verify locked restore, architecture and startup tests.
- [x] 1.2 Add isolated trace/metric/log pipeline with bounded lifecycle and sampling; verify actual disposable local OTLP wire delivery, correlation, redaction, sampling, outage and saturation tests; document settings and local sink.

## 2. Workflow coverage
- [x] 2.1 Add bounded agent/engineering/notification/provider-HTTP operation scopes; verify focused workflow, permission, retry/cancellation and replay tests and unchanged contracts; document coverage/model ID.
- [x] 2.2 Document Datadog local-Agent configuration, free-tier/retention uncertainty and separately authorized activation checklist; verify defaults require no credentials or remote ingestion.

## 3. Integrated verification
- [x] 3.1 Run full locked restore/Release build/tests/format, REST/MCP contracts, build-input/Compose checks and strict spec validation; record exact results and limitations.
- [x] 3.2 Review source/spec-to-code traceability and record verification evidence.

## Workflow follow-up
- Officially archive and strictly validate resulting main specs.
- Commit/push, create and attach PR, perform COMMENTED source self-review, inspect author controls, and merge passing exact source normally.
- Verify native Linear linkage and open/In Progress state, then create FAU-22 chat.
