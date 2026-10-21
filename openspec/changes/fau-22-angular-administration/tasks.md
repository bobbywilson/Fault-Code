# Tasks
## 1. Protected administration contracts
- [x] 1.1 Add explicit administration store/DTOs/controllers; verify tenant filtering, exact role matrix, invalid fields, concurrency/replay and unavailable production with integration tests.
- [x] 1.2 Generate compatible OpenAPI including examples; verify existing REST and five MCP schemas remain compatible.
## 2. Administration client
- [x] 2.1 Pin compatible Angular/Spartan/Tailwind/MSAL tooling and implement isolated PKCE lifecycle; verify production build, config/token boundaries and expired-session tests.
- [x] 2.2 Implement permission-aware resource/actions/forms and recorded history; verify manifest coverage, input validation and all three mocked synthetic workflows.
- [x] 2.3 Verify mobile/keyboard/focus, loading/empty/error403/409/503, pagination and mutation replay through component/service and Playwright tests; visually inspect exact production and fixture routes.
## 3. Linux delivery
- [ ] 3.1 Add non-root frontend Linux packaging and same-origin Compose proxy; verify container acceptance and unchanged internal isolation.
- [x] 3.2 Align Linux-only CI/spec guidance; verify local locked restore, Release tests/build/format, contract/build-input checks, frontend/browser checks and strict specs. Exact-source Linux jobs remain required for delivery.
- [x] 3.3 Document API action mapping, setup, live tenant checklist and observed limits; inspect source diff against approved requirements.
## Workflow follow-up
- Official archive then strict all-spec validation.
- Commit/push, create and attach PR, actual source self-review and author-control inspection, normal exact-source passing Linux merge; retain Linear In Progress.
- Create FAU-23 chat with evidence and remaining boundaries.
