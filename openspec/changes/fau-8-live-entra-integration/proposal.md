# Proposal

## Why

Part of FAU-8, FAU-9 and FAU-22. The API token validator, authorization policies and Angular MSAL lifecycle exist, but automated test doubles do not establish usable production identity. The user explicitly authorized dedicated Fault Code Entra ID Free provisioning and real authentication verification on 2026-10-21.

## What Changes

- Provision a new dedicated workforce tenant using the Gmail-linked Microsoft account, subject to Microsoft's eligibility and verification requirements.
- Preserve LALAI and every existing tenant; do not purchase subscriptions, enable paid features or deploy the application.
- Register separate API, Angular SPA and machine clients with existing least-privilege scopes and roles; configure public identifiers only after Microsoft confirms them.
- Record actual user and machine token verification separately from deterministic test results, with blockers and incomplete checks explicit.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `identity`: explicit provider-confirmed provisioning inventory, prerequisite handling and live verification evidence.
- `authorization`: live delegated and application permission acceptance evidence for the existing policy matrix.
- `administration`: real Angular sign-in, token acquisition and logout acceptance evidence.

## Impact

Existing Entra configuration, registration runbook, Angular public browser configuration, identity/authorization tests and existing Linear issues. No authentication bypasses, new repository, unrelated infrastructure or deployment. Credentials and verification information remain outside GitHub and evidence artifacts. A tenant creation blocker leaves this change active and live acceptance incomplete.
