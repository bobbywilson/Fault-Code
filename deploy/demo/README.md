# Fault Code Linux deployment

The approved target is `134.209.215.92`, serving `https://faultcodeai.com`.
Fault Code has its own containers, networks, volumes, credentials and deployment
account. Deployment never reads, modifies or restarts another application.

## Delivery

Merges into `development` run seven required Linux checks: `verify`,
`administration`, `documentation`, `retrieval`, `smoke`, `release` and `sql-server`.
The deployment gate accepts only successful checks for the current development
SHA. It publishes four source-labelled immutable images and deploys their digests
through the restricted SSH receiver. Main is a promotion branch and does not
trigger deployment. `DEMO_DEPLOY_ENABLED` controls delivery eligibility.

The dedicated `faultcode-ci` account runs its own rootless Docker daemon and two
build workers, plus a separate gate worker. Its aggregate limits are 4.5GB memory
and two CPUs. It cannot access the host Docker socket or production credentials.
The GitHub `demo-production` environment allows only the development branch.
Hosted-runner billing is not required for these workflows.

## Runtime

The fixed `faultcode-demo` Compose project contains seven services:

- ASP.NET API, running as UID 1654 with a read-only filesystem.
- Angular administration app with Spartan UI.
- SQL Server Express for canonical application records.
- Qdrant for derived, rebuildable vector retrieval.
- Mailpit for captured email; outbound relay is disabled.
- Static Mintlify API documentation.
- Nginx HTTPS proxy, publishing only ports 80 and 443.

Public routes are the app at `/`, documentation at `/docs/`, and interactive API
exploration at `/swagger/`. `/api/v1/` remains authenticated. `/mailpit/` requires
the credential-login browser session. Unauthenticated Mailpit requests return to
sign-in. Model provider credentials, SQL passwords, Qdrant credentials, integration
keys and the Entra client private key remain server-side.

Root-owned private configuration lives under `/opt/faultcode-demo` with directory
mode 0700 and configuration files mode 0600. The API's certificate mount grants
only its runtime UID read access. Database migrations use a temporary SA child
process; the serving API uses a Fault Code-only database reader/writer principal.
Idempotent fleet/catalog seeds and vector indexing run before acceptance.

The receiver accepts only `deploy SOURCE_SHA API_DIGEST WEB_DIGEST QDRANT_DIGEST
DOCS_DIGEST`, or the fixed read-only `ci-status` command. Its account has no Docker
group membership or general sudo shell. Registry credentials arrive on SSH stdin
and use a temporary private Docker configuration. Host keys are pinned.

The deployer checks source labels, disk/memory headroom, configuration ownership,
TLS, container health, public HTTPS, protected API behavior and documentation.
It records accepted source/digests and retains the previous release for rollback.
A failed initial candidate is stopped without deleting persistent volumes. Never
prune the shared host or remove persistent data as part of routine deployment.

## Operations

Install the supplied monitor and TLS renewal units only for Fault Code. The
monitor checks the seven services and host headroom without restarting anything.
The TLS timer uses the project's isolated Certbot state and webroot, validates
replacement certificates and reloads only the Fault Code proxy. Verify the live
renewal dry-run after first deployment. Local journal failures are operational
signals; an external alert channel is not configured by these units.

Use `ci-status` through the restricted deployment SSH key to inspect the isolated
CI daemon and its worker state. Runtime records and image provenance are stored
under the root-only release directory. Keep all private runtime files out of Git,
images, CI artifacts and support screenshots.

The John Henry account uses credential login and a dedicated Entra application
identity for its API token. The client certificate requires explicit rotation
before expiry; automatic certificate rotation is not implemented. Microsoft SPA
login additionally requires the exact production callback in its Entra app.
Verify that provider flow separately from credential login.

Datadog incident creation produces direct incident links. The account currently
requires an incident-management seat for status updates; do not report a Datadog
status change unless the provider accepted it. Mailpit capture is separate from
external email delivery.

See `docs/deployment/teardown-plan.md` for scoped recovery and removal.
