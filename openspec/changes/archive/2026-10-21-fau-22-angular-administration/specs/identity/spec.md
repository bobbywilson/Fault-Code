## ADDED Requirements

### Requirement: Configured MSAL browser lifecycle
The SPA SHALL use maintained MSAL authorization code with PKCE, isolated tenant/client/API identifiers, exact same-origin callback, tab-scoped session cache, explicit login/logout and silent renewal. Access tokens SHALL attach only to same-origin /api/v1 requests and SHALL never enter logs, URLs or model context. Missing configuration SHALL disable protected navigation without inventing identifiers.
#### Scenario: Configured identity and expiry
- **WHEN** browser identity is configured and a session needs renewal
- **THEN** MSAL validates the callback and attempts silent acquisition; interaction-required or401 clears displayed protected data and requests explicit sign-in without replaying mutations
#### Scenario: Unconfigured browser
- **WHEN** identifiers are absent or invalid
- **THEN** show setup unavailable with no identity provider request and no protected records
