## ADDED Requirements

### Requirement: Demo domain and HTTPS evidence
Only the existing faultcodeai.com domain and www routing SHALL be configured for the verified existing server. DNS changes SHALL preserve unrelated records and nameservers. HTTPS SHALL use a valid certificate and hostname-specific routing. Public resolution, TLS, Angular, API and unchanged LALAI routing SHALL be verified separately.

#### Scenario: Existing DNS inventory
- **WHEN** domain routing is prepared
- **THEN** capture the existing zone, propose exact apex/www changes and preserve MX/TXT/SPF/DKIM/DMARC and unrelated domains

### Requirement: Scoped recovery and teardown
A failed deployment SHALL restore the previous accepted Fault Code release without restarting or modifying LALAI or Simone. Teardown SHALL enumerate only Fault Code resources and require explicit approval before permanent data deletion; it SHALL retain shared infrastructure, unrelated DNS/TLS and LALAI data.

#### Scenario: Failed candidate
- **WHEN** candidate health or version verification fails
- **THEN** restore and verify the previous Fault Code release and existing-service baselines; report failure without claiming success

#### Scenario: Teardown dry run
- **WHEN** removal is requested without data-deletion approval
- **THEN** list only Fault Code changes and preserve persistent volumes, shared proxy, unrelated certificates and all LALAI/Simone resources

### Requirement: Honest application readiness
Deployment acceptance SHALL distinguish process/dependency health from durable business workflow functionality. Unimplemented storage, delivery or provider integrations SHALL remain explicit blockers; production SHALL NOT use test stores or simulated authentication to pass acceptance.

#### Scenario: Missing durable adapter
- **WHEN** the current API intentionally returns503 for a business operation
- **THEN** record that operation incomplete rather than treating a healthy container or sign-in page as complete application acceptance

### Requirement: Complete same-domain application services
The Linux deployment SHALL serve Angular, REST/MCP, public static Mintlify docs, API testing and authenticated Mailpit under faultcodeai.com. SQL Server Express, Qdrant and server-side credentials SHALL support real workflows with private database/vector ports. Deployment SHALL migrate, seed and index before accepting health, preserve persistent volumes and leave LALAI services unchanged.

#### Scenario: Accepted development release
- **WHEN** every required Linux check passes for the current development revision
- **THEN** its source-labeled immutable app, API, vector and documentation images deploy, SQL migration and seeds succeed, protected API calls require authentication, and public HTTPS plus documentation and authenticated Mailpit are verified

#### Scenario: Capture-only email
- **WHEN** dispatch or resolution sends a driver notification
- **THEN** Mailpit retains the message and the domain viewer requires authentication; no relay or external driver delivery is enabled
