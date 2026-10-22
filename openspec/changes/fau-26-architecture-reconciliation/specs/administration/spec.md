## MODIFIED Requirements

### Requirement: Fault-code ontology browser
The authenticated Ontology route SHALL open a readable fault-code-centered relationship table including incidents, vehicles, drivers and notifications. Schema references SHALL be labeled as model references and not fabricated live data. The Ontology page SHALL be non-editable and SHALL expose no creation, editing or publication controls. The page SHALL NOT create or mutate on entry; unauthorized users SHALL receive no editing controls.

#### Scenario: Read-only visitor
- **WHEN** a user lacks knowledge.publish or is signed out
- **THEN** editing is hidden or denied without granting permissions or using fake records

#### Scenario: Browse the fault-code model
- **WHEN** an authenticated user opens Ontology
- **THEN** the default model table displays fault-code/domain relationships without requiring creation or version input

#### Scenario: Search and open related records
- **WHEN** the user filters the ontology relationships
- **THEN** the table filters reference relationships and shows only Concept, Relationship, Related concept and Meaning columns
- **AND** no Browse links or action column appear

#### Scenario: No ontology mutation on navigation
- **WHEN** a signed-in user enters the default Ontology view
- **THEN** no draft/create/update/publish request or mutation control executes automatically; editing controls remain absent

## ADDED Requirements

### Requirement: Complete evaluation administration
Administration SHALL provide the later approved focused workspace through authenticated APIs. Backend knowledge, investigation, delivery and telemetry services SHALL remain available independently of removed UI pages. Invitations and real email SHALL require explicit recipient authorization and consent. Unconfigured external providers SHALL remain visible blockers.

#### Scenario: External administration acceptance
- **WHEN** invitations, role grants, email or provider configuration are reported complete
- **THEN** actual authorized external actions and server-side secret custody are evidenced; local metadata/test sinks alone never count as completion

### Requirement: Focused vehicle fault workspace
The UI SHALL omit the five removed workspaces. Vehicles SHALL list associated drivers and support authenticated idempotent fault ingestion. Drivers SHALL list recorded emails. All lists SHALL show loading, pagination, empty and unavailable states without fabricated data. Backend processing services SHALL remain intact. Accepted ingestion SHALL NOT imply email delivery.

#### Scenario: Dispatch from a vehicle
- **WHEN** an authorized user selects Dispatch fault code for an active vehicle
- **THEN** the form identifies that vehicle, offers its associated drivers, validates event fields and submits through the authenticated API after confirmation

#### Scenario: Unavailable data
- **WHEN** fleet persistence or association access fails
- **THEN** the UI displays an error or unavailable association state without fabricated vehicles, drivers or dispatch success

#### Scenario: Removed page URL
- **WHEN** a user opens a removed administration page URL
- **THEN** Fault definitions, Documents, Engineering, Agent activity and Operational health URLs redirect to Incidents rather than rendering the removed workspace

### Requirement: Repeatable demo fleet seed
An explicit administrative seed operation SHALL prepare four demo Silverado/Sierra vehicles, four fictional drivers and four active driver-vehicle links using the existing fleet application service. Repeated execution SHALL preserve identifiers and avoid duplicate records or audit events. Demo emails SHALL use example.com addresses with notifications disabled. No fake production store or implicit user consent is permitted.

#### Scenario: Resume or repeat seed
- **WHEN** the same trusted administrator reruns the versioned fleet seed
- **THEN** service validation and stable replay keys preserve four vehicles, four drivers and four active associations without duplicate mutations or overwriting unrelated data

#### Scenario: Production persistence unavailable
- **WHEN** the fleet SQL connection or migrated store is unavailable
- **THEN** seed execution fails visibly and prepared/test-seeded records are not reported as real persisted application data

### Requirement: Seed-only fleet presentation
Vehicles and Drivers SHALL be read-only demo lists without create/edit/delete or management controls. Each seeded demo vehicle SHALL have one active assigned driver and one name/email pair per row. Plain driver names and readable Silverado/Sierra labels SHALL replace internal demo prefixes in presentation. Email SHALL occupy a separate column using example.com addresses, and fleet tables SHALL use the same centered content width as the other administration pages.

#### Scenario: Seed-only demo assignment upgrade
- **WHEN** the approved seed is applied to previously loaded demo records
- **THEN** one active driver remains for each vehicle, ended association history remains auditable, replay creates no duplicates and unrelated tenant/user data is not overwritten

#### Scenario: View fleet
- **WHEN** an authorized reader opens Vehicles or Drivers
- **THEN** read-only test data appears without management forms; vehicle dispatch still requires its actual API permissions and remains a separate operation

### Requirement: Linked incident and notification tables
Incidents SHALL list vehicle-originated investigations with vehicle, driver and notification-history links. Drivers SHALL link to their notifications. Notifications SHALL list authorized messages and delivery states with driver/incident filters before pagination. Unavailable storage SHALL remain an error. Ontology SHALL read its non-editable relationship reference from the API. Navigation SHALL end with Notifications, Users & roles, Ontology and omit the Protected workspace badge.

#### Scenario: Follow an incident relationship
- **WHEN** an authorized user selects a vehicle, driver or notifications link in an incident row
- **THEN** the destination loads the exact authorized resource or filtered history rather than an unrelated page of records

#### Scenario: Notification scope enforcement
- **WHEN** a caller lists notifications or selects driver/incident filters
- **THEN** only permitted tenant and ownership records are included in both page items and totals; invalid filters fail validation and inaccessible filters return404

### Requirement: Required backend integrations without management navigation
The UI SHALL omit Integrations and redirect its old URL to Incidents. Datadog telemetry and Mailpit capture SHALL be required backend integrations with server-held credentials and verified external ingestion/delivery. Local capture tests SHALL NOT count as Linux deployment or end-to-end incident workflow verification. Missing provider configuration SHALL remain an explicit acceptance blocker.

#### Scenario: Required providers unavailable
- **WHEN** required Datadog or Mailpit integration lacks credentials, permissions or real receipt evidence
- **THEN** completion remains blocked and metadata/configuration screens are not presented as a working integration

### Requirement: Users and assigned roles table
Users and roles SHALL show a paginated read-only table of authorized managed-user records with display name and assigned roles from the backend API. Driver seed records SHALL NOT be treated as directory users. Storage and permission errors SHALL remain explicit.

#### Scenario: Browse users
- **WHEN** an authorized administrator opens Users and roles
- **THEN** the UI automatically reads the users API and shows names and roles without a generic action selector or mutation form

### Requirement: Profile avatar account control
The signed-in header SHALL use Spartan Avatar and Dropdown Menu for the account image, name and Sign out. Supplied profile images SHALL map to the exact account identity, with initials fallback. The authentication badge and standalone sign-out control SHALL be absent. The Incidents table SHALL omit unavailable/load-error text and retain API failure semantics without fabricated rows or successful empty counts.

#### Scenario: Avatar sign out
- **WHEN** a signed-in user opens their avatar menu
- **THEN** their account name and Sign out appear, and selecting Sign out follows the real MSAL logout flow

### Requirement: Read-only fleet fault-code catalog
The workspace SHALL place Fault codes immediately below Ontology and expose a read-only API-backed table of the steering demonstration codes associated with the Silverado and Sierra test fleet. The catalog SHALL identify demonstration scope without claiming verified manufacturer diagnostic codes or approved vehicle compatibility.

#### Scenario: Browse the fault-code catalog
- **WHEN** an authenticated caller with knowledge.read opens Fault codes
- **THEN** the API returns the persisted tenant definitions seeded from the demonstration manifest with code, system, meaning and test vehicle names
- **AND** the workspace renders those records in a Spartan table without creation or editing controls

### Requirement: Inline vehicle dispatch with durable fault seed
The Vehicles table SHALL provide a fault-code select immediately after Status and a Dispatch button. Codes SHALL come from the tenant SQL catalog and match the row system. Dispatch SHALL remain disabled until a code is selected and SHALL call the two-field dispatch endpoint without a secondary form. Retries of an unconfirmed identical submission SHALL reuse the idempotency key.

#### Scenario: Dispatch from the vehicle row
- **WHEN** the user selects a catalog code and clicks Dispatch
- **THEN** the client sends only vehicleId and code with an idempotency key
- **AND** an accepted response identifies both incident and notification without claiming delivery
- **AND** the row selection remains unchanged and a Spartan toast confirms dispatch

#### Scenario: Seed fault definitions explicitly
- **WHEN** the operator runs seed-demo-faults against the dedicated SQL database
- **THEN** all nine demonstration definitions persist with audit records and no approved remedies
- **AND** repeating the command preserves existing definitions without duplicates
- **AND** the catalog API reads those persisted tenant records rather than a static fallback

### Requirement: Vehicle incident counts and navigation
The Vehicles table SHALL end with an Incidents column containing a Spartan button and number badge based on the authenticated incident API's filtered totalCount. Vehicles with zero incidents SHALL have a disabled button with a zero badge. Unavailable or unauthorized counts SHALL remain disabled without inventing zero. A successful dispatch SHALL refresh the affected count.

#### Scenario: View a vehicle's incidents
- **WHEN** a vehicle has incidents visible to the caller and the user selects View incidents
- **THEN** the Incidents page requests only that vehicle's incidents using vehicleId
- **AND** pagination retains the vehicle filter and ordinary Incidents navigation returns to the unfiltered list
- **AND** the filtered list shows a vehicle filter indicator and Clear filter button that removes vehicleId and loads all visible incidents from page one

### Requirement: Compact incident review controls
Incident review SHALL display the Action selector and parameter fields at half the card's content width on desktop and full width on small screens.

#### Scenario: Responsive incident review
- **WHEN** a user reviews an incident on a desktop viewport
- **THEN** the Action selector and ID field align at 50 percent of the card's content width
- **AND** small viewports use the available full width without horizontal form overflow

### Requirement: Entra Users and API token display
The Users workspace SHALL display the signed-in Entra identity and assigned roles without creating users or roles locally. It SHALL display the current Fault Code API access token, expiry, a copy action and an explicit Entra token refresh action. It SHALL not issue personal API credentials or claim that a refresh revokes a previously issued Entra token. Tokens SHALL not be included in logs, URLs, screenshots, static bundles or server-side persisted personal-token records.

#### Scenario: Copy and refresh an Entra token
- **WHEN** the signed-in user opens Users
- **THEN** their identity and Entra roles appear with their current API access token and expiry
- **AND** Copy copies that token and Refresh obtains a token through MSAL
- **AND** leaving the workspace or signing out clears the displayed token

### Requirement: Public signed-out entry
Until authentication succeeds the workspace SHALL hide protected navigation, Workspace breadcrumbs, resource headings and administration badges. It SHALL expose public API documentation and API testing links alongside sign-in. Expired sessions SHALL use the same public presentation without loading protected records.

#### Scenario: Open a protected route while signed out
- **WHEN** a signed-out visitor opens any administration route
- **THEN** sign-in and public documentation links appear without the Workspace breadcrumb, resource title, sidebar, or account menu
- **AND** no resource API requests occur until sign-in succeeds

#### Scenario: Center public sign-in
- **WHEN** the signed-out page renders on desktop or mobile
- **THEN** the sign-in card is centered horizontally and vertically in the available area below the documentation header
- **AND** it remains within the viewport width

### Requirement: Password demo frontend session
The frontend SHALL support an explicitly provisioned demo username/password using a server-verified password hash and an HttpOnly, SameSite session cookie. It SHALL protect browser mutations with antiforgery validation and rate-limit sign-in. Demo access SHALL permit fleet browsing, incident dispatch/resolution and Mailpit notification processing, without Entra tokens, user administration or knowledge publication. Public resource API requests SHALL continue to require Entra bearer tokens.

#### Scenario: Demo sign-in and resource isolation
- **WHEN** the configured demo credentials are accepted
- **THEN** the frontend uses an expiring browser-only session and server-derived demo permissions
- **AND** external API paths reject that cookie without an Entra token
- **AND** demo Users/token controls are unavailable

#### Scenario: Reject invalid or forged sessions
- **WHEN** a wrong password, missing antiforgery token, modified cookie or expired session is submitted
- **THEN** the request is rejected without exposing credentials or granting anonymous access
- **AND** repeated sign-in attempts are rate-limited and sign-out revokes the current browser session

#### Scenario: Separate evaluator and owner identities
- **WHEN** the configured password account signs in
- **THEN** its name is John Henry, username johnhenry, with JH initials in the profile avatar
- **AND** Bobby Wilson retains Microsoft sign-in and his supplied profile photo
- **AND** the identities have distinct audit identifiers and account changes invalidate previous sessions

#### Scenario: Reliable account dropdown
- **WHEN** a signed-in user clicks the avatar dropdown or opens it with the keyboard
- **THEN** the account menu shows their name and a working Sign out action
- **AND** a dropdown arrow makes the menu discoverable

#### Scenario: Equal inline sign-in options
- **WHEN** credential sign-in is available
- **THEN** equal-size buttons appear side by side labeled Sign in with credentials and Sign in with Microsoft

### Requirement: Dedicated demo application token
John Henry SHALL retain password browser sign-in and use a dedicated Entra application token for the API. Only the five existing demo permissions SHALL be granted. Certificate credentials SHALL remain server-side. The Token page SHALL support view, copy and refresh, label the application identity, and show expiry. Bobby's delegated token SHALL remain isolated. No local token issuer or user impersonation is introduced.

#### Scenario: Session-gated token retrieval
- **WHEN** the password account requests its application token
- **THEN** the server checks the authenticated cookie and antiforgery token before contacting Entra
- **AND** anonymous requests, invalid CSRF, disabled configuration and unexpected token grants are rejected without disclosing credentials or tokens
- **AND** responses are not cached and tokens do not enter logs, URLs, local storage or model context

#### Scenario: Restricted bearer operations
- **WHEN** the dedicated demo application calls the public API with its real Entra token
- **THEN** existing issuer, audience, signature, expiry and role checks apply
- **AND** only resource reads, incident creation, incident updates and notification updates are allowed, matching the browser demo mutation boundary
- **AND** deletes, fleet changes, knowledge publication and administration are denied

#### Scenario: Token navigation
- **WHEN** either account is signed in
- **THEN** a Token side-nav link opens view, copy and refresh controls for its own delegated or demo-application token
- **AND** signed-out users see no Token link or protected token data
- **AND** refresh requests a new token from Entra without claiming existing bearer tokens have been revoked

### Requirement: Interview-context dashboard
Authenticated navigation SHALL place Dashboard above Incidents. The dashboard SHALL explain the agentic fault flow associated by the user with Shriram, the ontology associated with Nathan, and the OpenSpec package associated with Kristof. It SHALL use Spartan components, Lucide icons and an accessible responsive flow diagram. Explanatory content SHALL describe the project without invented interview quotations, operational metrics or external-integration success claims.

#### Scenario: Explain the fault workflow
- **WHEN** an authorized user opens Dashboard
- **THEN** a diagram connects vehicle fault submission, API persistence, Agent Framework/MCP, exact Qdrant lookup, known and unknown outcomes, driver notifications and resolution follow-up
- **AND** telemetry is identified as part of the workflow without claiming verified Datadog ingestion

#### Scenario: Explain domain knowledge and specifications
- **WHEN** the dashboard is read
- **THEN** it describes fault-code relationships to vehicles, drivers, incidents, notifications, symptoms and remedies
- **AND** explains OpenSpec proposals, specifications, tasks and validation with the actual package name
- **AND** links to existing vehicle and ontology views plus the official OpenSpec repository
- **AND** red outlines distinguish the three topic/name badges, and Kristof's section calls out Playwright verification of specified browser journeys

#### Scenario: Protected responsive overview
- **WHEN** the user is signed out or views the page on a narrow viewport
- **THEN** authentication still gates the dashboard and its sidebar
- **AND** the diagram and explanation cards reflow within the content width

### Requirement: Token field width
The token input and copy control SHALL occupy half the available card width on desktop and fit the content width on mobile.

#### Scenario: Responsive token controls
- **WHEN** Token is opened on desktop or mobile
- **THEN** its token/copy row uses fifty percent width on desktop and remains usable without page overflow on mobile

### Requirement: Resolution entry and agent follow-up
Incident review SHALL replace the generic GET submit button with required resolution notes and Resolve incident. Resolution SHALL use the existing authenticated incident PUT with concurrency and idempotency. SQL SHALL retain the resolution and its author/time/version; Qdrant SHALL index the exact fault code and resolution before the agent reads it via scoped MCP and sends a follow-up. Investigation resolution SHALL NOT assert physical vehicle repair or create an OEM-approved remedy.

#### Scenario: Resolve an unknown incident
- **WHEN** an authorized user supplies valid notes and resolves an incident with no catalog definition
- **THEN** the transaction records Resolved and one follow-up notification, and the agentic flow indexes and retrieves the recorded resolution before sending the driver's follow-up
- **AND** the dashboard order is Agentic flow/Shriram, OpenSpec/Kristof, Ontology/Nathan

#### Scenario: Safe resolution retries
- **WHEN** resolution text is missing, a version is stale, or the idempotency key is reused with different notes
- **THEN** reject the mutation without creating another notification
- **AND** identical retries reuse the same result and never resend a successfully sent message

#### Scenario: Index or delivery unavailable
- **WHEN** Qdrant indexing/retrieval or email processing fails
- **THEN** retain the SQL resolution and auditable pending/failed notification for retry, without claiming successful delivery
- **AND** mismatched vector data cannot supply a different incident's resolution or overwrite approved fault knowledge

### Requirement: Workflow navigation and test paths
The sidebar SHALL order Dashboard, Vehicles, Drivers, Incidents, Notifications, Users where permitted, Ontology, Fault codes and Token. The collapsed API reference icon SHALL have a tooltip. Vehicle fault selection SHALL offer catalog-backed codes and a valid unlisted code for the vehicle system without publishing an invented catalog entry.

#### Scenario: Exercise both fault outcomes
- **WHEN** a vehicle's fault selector is opened
- **THEN** catalog choices and an unlisted choice are distinguished and both can use the existing dispatch endpoint
- **AND** Dispatch remains disabled until a choice is selected

#### Scenario: Collapsed documentation access
- **WHEN** the sidebar is collapsed and the API reference icon is hovered or focused
- **THEN** a tooltip identifies API reference

#### Scenario: Development stack reference
- **WHEN** a signed-in user selects Development stack below API reference
- **THEN** Dashboard opens at its Development stack section after the interview-topic sections
- **AND** it describes the actual frontend, API, identity, agent/MCP, persistence/retrieval, email/telemetry, specifications/documentation and verification tools
- **AND** the collapsed link has its own tooltip and does not expose configuration secrets

#### Scenario: Public API testing label
- **WHEN** a signed-out visitor sees the documentation header
- **THEN** the Swagger link reads Test API beside API documentation

### Requirement: Incident Datadog link
Incident review SHALL open the actual Datadog Incident Management record using a provider-acknowledged ID/public number persisted in SQL. It SHALL NOT substitute a log search, guessed ID or generic landing page. Creation and status changes SHALL synchronize through a server-only adapter with private credentials. No extra public REST endpoints SHALL be added.

#### Scenario: Open the provider incident
- **WHEN** Datadog accepts the incident and returns its canonical identifiers
- **THEN** SQL retains that reference and View in Datadog opens `/incidents/{publicNumber}` on the observed Datadog site
- **AND** later incident resolution updates the same Datadog record without creating another

#### Scenario: Provider failure or uncertain acceptance
- **WHEN** credentials are absent, access is denied or a create response is uncertain
- **THEN** retain an auditable failure state without a fabricated link
- **AND** uncertain creates are not blindly retried, while a known provider ID permits safe status-update retries
- **AND** the incident and driver email workflow remain durable independently of Datadog availability

### Requirement: In-app team ticket review
Authenticated reviewers SHALL have a Spartan UI Tickets workspace with a searchable/filterable table and complete ticket details without needing a Linear account. A server-only read-only credential SHALL query only the Fault Code workspace and FAU team. Browser support routes SHALL remain outside the six-resource public REST/OpenAPI contract. No Linear issue, comment or status SHALL be mutated by this feature.

#### Scenario: Review important ticket information
- **WHEN** a reviewer opens a ticket
- **THEN** its identifier, title, description and acceptance criteria, status, priority, assignee, creator, labels, project/cycle, dates, estimate, parent/subtasks, related tickets, attachments and discussion are displayed when recorded
- **AND** the description and comments are safely formatted in the app instead of requiring a visit to Linear
- **AND** missing optional values are represented without invented assignments or progress

#### Scenario: Protected read-only provider access
- **WHEN** an anonymous, expired, wrong-tenant, machine-token or driver-only caller requests tickets
- **THEN** access is denied without contacting Linear
- **AND** an issue from another team or workspace cannot be returned by guessing its ID
- **AND** the credential stays in server configuration and never enters browser responses, logs or model context

#### Scenario: Provider freshness and partial failures
- **WHEN** the table is loaded or refreshed
- **THEN** it shows the last successful retrieval time, handles paginated issue/comment data and checks GraphQL errors even on HTTP200
- **AND** denied/unavailable/partial provider responses are not mislabeled as an empty backlog
- **AND** route changes and sign-out suppress stale pending responses

#### Scenario: Safe ticket content
- **WHEN** descriptions or comments contain markup, code, links or active HTML
- **THEN** formatting remains readable while executable HTML and unsafe URLs cannot run
- **AND** external images do not make unapproved background requests from the reviewer browser

### Requirement: Spartan controls throughout the workspace
Every visible application control SHALL use the installed Spartan UI components and Helm directives. Select lists SHALL use Spartan Select triggers, portalled content and selectable items instead of browser-native select widgets. Existing enabled, disabled, selected-value and validation behavior SHALL be preserved.

#### Scenario: Choose a vehicle fault with Spartan Select
- **WHEN** an authorized reviewer opens a vehicle fault selector by pointer or keyboard
- **THEN** a Spartan Select listbox offers catalog and unlisted choices, selection retains the exact code and enables Dispatch, and Escape closes the list without changing selection

#### Scenario: Shared action and enum fields
- **WHEN** a shared action or enumerated request field is rendered
- **THEN** its label identifies a Spartan Select trigger, options are keyboard accessible, enum values retain their original type and validation still rejects missing required values
