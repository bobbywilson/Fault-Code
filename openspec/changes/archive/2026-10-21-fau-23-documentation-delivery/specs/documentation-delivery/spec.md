## ADDED Requirements

### Requirement: Verifiable public reference
Documentation SHALL provide anonymous Mintlify guides and a reference synchronized exactly with generated REST OpenAPI, preserving permission extensions and bearer security. Reading SHALL require no account. Interactive requests SHALL use browser-direct transport, never a third-party bearer proxy, and SHALL remain unavailable without an approved API origin and valid Entra authorization. No Swagger UI or implicit token acquisition SHALL be introduced.

#### Scenario: Contract drift or broken navigation
- **WHEN** a generated operation, security requirement or referenced local page changes without synchronized documentation
- **THEN** verification fails before release and reports drift or a broken local link

#### Scenario: Anonymous reading and authorized execution boundary
- **WHEN** an anonymous reader opens the documentation and attempts a protected request
- **THEN** reading succeeds without login while the API returns401; a valid insufficient grant returns403; an explicitly authorized supported request can succeed in an approved environment

#### Scenario: No approved interactive target
- **WHEN** hosting and Entra registrations remain undecided
- **THEN** documentation explains the unavailable live Try It prerequisite and provides no invented origin, secret, saved token or paid integration

### Requirement: Inspectable Linux release evidence
Release verification SHALL retain exact source identifiers, test results, deterministic A/B/C routing and permission evaluations, versioned Linux images with checksums and observed disposable rollback/configuration/health evidence. Existing component tests SHALL continue to run. Test doubles SHALL be identified; unapproved relational storage, live identity, external notification and hosting SHALL remain explicit outstanding release prerequisites.

#### Scenario: Candidate image replacement and rollback
- **WHEN** two source-tagged Linux images are built in a disposable acceptance environment
- **THEN** replacement, restart and restoration of the prior image are observed using image identity and health/configuration assertions without modifying developer volumes or deploying to a host

#### Scenario: Grounded workflow release evidence
- **WHEN** deterministic synthetic workflow fixtures execute
- **THEN** A records approved known guidance, B records approved dealership metadata without vehicle deployment and C records engineering approval/publication and notification acknowledgement; exact citations, zero unauthorized actions and failure/outage/redaction criteria are recorded separately from live durable acceptance

#### Scenario: Failed or incomplete acceptance
- **WHEN** a required job fails, is cancelled or omits expected cases
- **THEN** the candidate is not accepted and unavailable live provider acceptance is never reported as passing
