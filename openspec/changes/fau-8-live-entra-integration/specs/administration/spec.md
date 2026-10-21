## ADDED Requirements

### Requirement: Real administration identity acceptance
Administration acceptance SHALL exercise the production MSAL configuration against the confirmed Fault Code tenant using authorization code with PKCE. Public environment configuration SHALL contain only public tenant/client identifiers, approved redirect URLs and delegated scopes. Production assets SHALL exclude test authentication fixtures and client secrets.

#### Scenario: Interactive sign-in
- **GIVEN** a real permitted account and a registered SPA callback matching the active local test origin
- **WHEN** the user signs in through Angular
- **THEN** Microsoft completes the redirect and the SPA obtains an API access token and calls an authorized API operation

#### Scenario: Logout
- **WHEN** the authenticated user signs out through Angular
- **THEN** the configured Microsoft logout flow returns to the registered local origin and protected client requests no longer use the ended session

#### Scenario: Public configuration audit
- **WHEN** the production bundle and committed environment configuration are inspected
- **THEN** no machine secret, private key, bearer token or test authentication replacement is present
