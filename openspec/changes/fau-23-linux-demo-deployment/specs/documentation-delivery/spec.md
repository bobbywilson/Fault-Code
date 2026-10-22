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

#### Scenario: Anonymous documentation navigation behind the HTTPS proxy
- **WHEN** a signed-out visitor opens API documentation or follows the slashless documentation URL
- **THEN** public documentation loads without authentication and redirects preserve the browser-facing origin rather than exposing the proxy's internal port

### Requirement: Daily authenticated API quota
The API SHALL admit at most 200 authorized resource requests per identity per UTC calendar day across all resource endpoints, with durable atomic counters. Application identity is tenant/application; delegated identity is tenant/user. Token refresh SHALL NOT reset the allowance. John Henry's browser API calls and dedicated Entra application token SHALL share the same allowance. Unauthenticated, forbidden, public documentation, health and preflight requests SHALL NOT consume it.

#### Scenario: Exhausted or unavailable quota
- **WHEN** an authorized identity has already used 200 requests that UTC day
- **THEN** further requests return 429 before endpoint execution, with Retry-After until midnight UTC and limit/remaining/reset headers
- **AND** a new UTC day restores the allowance while a restart, deployment or refreshed token does not
- **AND** unavailable quota persistence returns 503 without executing the endpoint

#### Scenario: Public quota documentation
- **WHEN** a reviewer opens Mintlify authentication guidance or an endpoint in Swagger
- **THEN** the 200-request limit, identity scope, midnight UTC reset and 429 behavior are documented without requiring sign-in

### Requirement: Compact responsive workspace navigation and fault meanings
Workspace navigation SHALL close the mobile sidebar when a link is selected without changing desktop collapse state. Fault-code meanings SHALL open from a compact Spartan button in an accessible Spartan dialog on mobile and desktop rather than stretching table rows.

#### Scenario: Mobile page selection and fault meaning
- **WHEN** a mobile reviewer selects a sidebar page
- **THEN** the selected page becomes visible without dismissing the sidebar separately
- **AND** View meaning opens the complete fault description in a bounded, scrollable dialog whose close control restores focus to its trigger
