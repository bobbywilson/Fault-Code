## ADDED Requirements

### Requirement: Consented invitations and directory role grants
Administration SHALL support real dedicated-Fault-Code Entra invitations and explicit directory role assignment, with an audited invitation/acceptance lifecycle. Invited people SHALL explicitly accept participation and vehicle registration; consent SHALL NOT be inferred from being an interviewer, administrator input, email address or successful sign-in. Operational email SHALL be sent only to consenting recipients through a real configured provider. No LALAI tenant change is permitted.

#### Scenario: Invitation and consent acceptance
- **WHEN** a consenting invited person accepts and registers a vehicle
- **THEN** acceptance identity/time/scope are retained, exact directory grants and vehicle consent are verified, and duplicate/rejected/revoked invitations cannot trigger unsolicited notifications
