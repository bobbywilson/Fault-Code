# Browser demo login

Part of FAU-26. Explicitly approved October21,2026: one username/password evaluation account named John Henry for Angular; Entra bearer authentication remains the external API boundary. This is a real server-verified account, not a frontend password comparison or an Entra token exchange.

## Provisioning
Configure `DemoAccount:Enabled`, `Username`, `DisplayName`, `ProfilePhoto`, `PasswordHash`, `UserId`, `ClientId`, and `TenantId` through the private server environment/secret store. The tenant must equal Fault Code's configured Entra partition. IDs are distinct stable audit identifiers for the local demo principal, not fabricated Entra directory objects. PasswordHash uses the standard ASP.NET Core Identity v3 format. Store only the hash in server configuration; deliver a generated password privately when needed; never store passwords in source, build arguments, logs or browser assets. Disabled is the default. Production cookies require HTTPS. No password-reset, user CRUD, public signup or new Entra tenant is introduced.

Linux Compose accepts the `FAULTCODE_DEMO_LOGIN_ENABLED`, `FAULTCODE_DEMO_USERNAME`, `FAULTCODE_DEMO_PASSWORD_HASH`, `FAULTCODE_DEMO_USER_ID` and `FAULTCODE_DEMO_CLIENT_ID` private variables. Configuration is prepared; Linux deployment remains paused. Do not claim hosted login until tested there.

## Session boundary
`GET /session` returns availability, current demo identity and an antiforgery token. `POST /session/login` verifies credentials and issues a four-hour, nonpersistent HttpOnly/SameSite=Strict cookie. The frontend then reads `/session` again for an authenticated antiforgery token. `POST /session/logout` validates CSRF and removes the server-held session. These browser infrastructure routes are excluded from the public resource API documentation.

The account-wide sign-in limit is eight attempts per minute. Password verification uses the framework PBKDF2 hasher. Demo tickets are held in a bounded server store; sign-out removes the ticket. Restarting this single-instance demo invalidates all demo sessions. Changing the configured password hash and restarting revokes all existing sessions.

Only `/workspace/api/v1` accepts the demo cookie. It retains tenant scoping and the existing authorization policies, and permits reads plus incident creation/status updates and notification processing. Mutations require the antiforgery header. Cross-site requests are rejected. Deletion, fleet/reference mutations, knowledge publication, user administration and Entra token display are unavailable to the demo account. The original `/api/v1` routes ignore the demo cookie and still require a valid scoped Entra token.

Microsoft owner sign-in remains available. Its token stays in the owner session and is never used to authorize demo users. Credentials, cookies and CSRF values must not be included in screenshots.

## Acceptance evidence
Automated checks cover wrong credentials, disabled login, CSRF rejection, scope separation, throttling, expiry, sign-out, and unchanged external bearer requirements. Real local SQL-backed login and frontend navigation must be verified separately. Hosted login is not verified.

Local acceptance before account rename: a real browser signed in with the password account, loaded all four SQL-backed vehicles, dispatched a fault for Casey Taylor's Sierra, and displayed Sent/one attempt. The new recipient message was also verified in the actual private Mailpit capture API. No Entra access token was issued to the demo account. Linux remains unconfigured; before enabling it, verify the dedicated TLS proxy and explicitly trusted forwarded-protocol configuration so secure cookies and antiforgery operate correctly behind HTTPS termination. Never expose the internal API's HTTP port publicly.

The shared evaluation account is John Henry (`johnhenry`) with JH initials in the Spartan avatar. Bobby Wilson uses Microsoft sign-in and the supplied `/profiles/bobby-wilson.png` photo. The former generic account name is removed. Username, display name and photo join the credential version so renaming the account invalidates old sessions. The UI displays each account's actual name without a generic demo-user label.

After the user supplied the final password, the temporary plaintext credential handoff file was removed. Runtime configuration retains only the Identity password hash. The final username is `johnhenry`; the password account is now John Henry. Bobby Wilson's Microsoft identity and photo remain separate. Drivers were not changed by the account update. Real local sign-in, bearer-only external API rejection, four existing drivers, and logout were reverified after the final credential change.

The account dropdown includes a visible chevron and Sign out for both authentication methods. Development uses full live reloads instead of Angular component HMR to prevent stale dropdown event handlers. John Henry has a distinct local audit identifier and cannot view owner Entra tokens. Drivers remain unchanged.

John Henry uses JH initials rather than a profile image. Bobby Wilson's supplied photo is unchanged.

The signed-out card places equal-size Sign in with credentials and Sign in with Microsoft buttons side by side. Both retain Spartan button styling.

## Dedicated Entra demo API identity
Approved October21,2026: John keeps password/cookie login. A separate single-tenant confidential app named Fault Code Demo API acquires app-only tokens from Entra; it is not a John Henry directory user or a delegated user token. Token navigation is available to both login types. The browser can view/copy/refresh its own API token; Bobby's delegated token is never shared.

Configure `DemoApiToken:Enabled`, `ClientId`, `CertificatePath`, and `PrivateKeyPath` server-side. Use a dedicated certificate and private key, never the owner's or existing machine client's credentials. Mount credential files read-only from a private directory; do not put them in image layers, frontend assets, Compose values or source. Only the public certificate is uploaded to Entra. No redirect URI, implicit grant, password grant or Graph permission is required. Grant only `Drivers.Read.All`, `Vehicles.Read.All`, `Faults.Read.All`, `Faults.Write.All`, and `Knowledge.Read.All` for the Fault Code API.

Authenticated cookie + CSRF protected `POST /session/api-token?refresh=false` is browser infrastructure, excluded from public REST/OpenAPI. `refresh=true` forces Entra acquisition, not token revocation. Results are no-store and retained only in component/server memory. The credential is never sent to the browser. MSAL authenticates with the server-held certificate; returned tokens are signature/issuer/audience/lifetime/client/tenant/type/role validated before display. Provider errors return a generic503 without response bodies or credentials. Retrieval is rate-limited. Configuration disabled/missing fails closed.

The dedicated demo client has an extra API operation boundary allowing reads, POST incidents, PUT incidents and PUT notifications only. This prevents Faults.Write.All from enabling deletes and applies to copied bearer tokens as well as the existing browser session. Owner and unrelated machine permissions are unaffected. Public API auth remains real Entra bearer validation; the demo is not issued a local bearer token.

Provider registration, consent, acquisition and actual copied-token API calls must be verified independently before completion. Linux deployment remains paused.

Verified locally after approval: the dedicated application is registered and certificate-authenticated; all five roles are consented with no Graph permissions. John signs in with `johnhenry`, opens Token and can view/copy/refresh the real Entra application token. The displayed/copied token passed all six resource-list API requests and was denied incident deletion, fleet mutation and knowledge publication. See [provider evidence](entra-provisioning-evidence.md). The private key was not added to source or browser assets; hosted deployment remains pending.
