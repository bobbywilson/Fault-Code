# Proposal — Part of FAU-22

## Why
The current workspace uses only Spartan Brain button behavior with bespoke styling. The user requires Spartan's supplied styled components throughout all existing views and Lucide icons, with screenshots of the working result.

## What Changes
- Replace the bespoke page shell with Spartan's official sidebar/inset composition and theme.
- Use generated official Helm components for cards, alerts, buttons, form controls, records, pagination and footer/navigation composition; Lucide through Spartan's documented ng-icons integration.
- Preserve every API operation, permission, Entra flow, validation, concurrency/replay protection and honest unavailable state.
- Verify desktop/mobile and representative authenticated demo test states, clearly separating fixtures from real sign-in.
- Keep deployment paused; do not merge this UI into main or deploy it during this task.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `administration`: require supplied Spartan styled visual primitives and Lucide icons across all views instead of bespoke UI controls.

## Impact
Angular templates/styles, official generated Helm sources, frontend dependencies/tests, administration documentation. No backend API or identity contract changes. Existing Angular containers remain the packaging target.
