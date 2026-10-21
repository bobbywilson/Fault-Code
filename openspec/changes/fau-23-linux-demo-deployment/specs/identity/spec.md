## ADDED Requirements

### Requirement: Environment-specific real identity
Deployment SHALL reuse the existing dedicated Fault Code Free tenant and API/SPA registrations, exact HTTPS callback/logout/API origins and issuer/audience validation. Production SHALL NOT use mock authentication. Local-development and Linux-production machine clients SHALL use distinct private keys and certificates.

#### Scenario: Deployed user identity
- **WHEN** a user authenticates through the public HTTPS Angular origin
- **THEN** a real Entra-issued API token succeeds only with its exact authorized scope/role, invalid identity returns401 and insufficient permission403

### Requirement: Automatic production credential rotation
A scheduled production rotation process SHALL create a replacement key/certificate before expiry, register its public key with the current valid credential, verify a real token and permitted API call, switch atomically and retire the old public key after overlap. Failures SHALL preserve the working credential and alert without logging secrets.

#### Scenario: Replacement failure
- **WHEN** registration, token acquisition or authorized API verification fails
- **THEN** retain the old credential, retry safely, report failure before expiry and never claim rotation completed
