# Fault Code Mailpit capture

Mailpit is the approved required email transport for both local and Linux demo environments. Microsoft365 is explicitly cancelled by the latest user instruction. Demo addresses use example.com; messages are captured for inspection without external relay or real customer delivery.

## Local instance

`docker compose -f compose.mailpit.yaml up -d` runs official Mailpit1.31.6 at a pinned digest with its own network, persistent capture volume,256MiB/0.25CPU limits and relay disabled. Inbox: http://localhost:8025/. SMTP:127.0.0.1:1025. HTTP capture:127.0.0.1:8025. Both published ports bind loopback only. The local bridge permits Docker Desktop loopback publishing; no relay host/configuration is provided.

Configure the backend with `Notifications__Provider=Mailpit` and `Notifications__Endpoint=http://127.0.0.1:8025/`. The `MailpitNotificationDelivery` adapter uses Mailpit's real Send API, not a fake/in-memory sender. It accepts only example.com recipients and bounded text, stamps a stable notification reference and returns the actual capture receipt. HTTP redirects/retries/loggers are disabled. The existing notification service still requires its approved durable store/workflow; configuring capture does not make that workflow complete.

## Linux demo configuration

`deploy/demo/compose.yaml` includes Mailpit in the existing demo project by default, routes the backend to `http://mailpit:8025/`, uses the dedicated internal data network/volume/resource limits, and exposes only127.0.0.1:8025 for an SSH-tunneled inbox. SMTP is not published on the host. No public reverse-proxy route or external relay is configured. Apply only when deployment resumes and shared-host preflight passes; no Linux capture service has been started by this change. Do not restart shared services to enable it.

## Verification and limitations

Real local .NET integration tests verified captured recipient, body and receipt through Mailpit's API; an independent SMTP test was also captured. These tests use explicit test messages, not agent output or completed vehicle incidents. Config/recipient/header-injection guards have automated tests. `FAULTCODE_MAILPIT_TEST_URL` explicitly enables live integration tests; absent configuration is reported as skipped, never a passing mock.

Message-ID/reference is stable, but durable notification outbox, uncertain-acceptance reconciliation and duplicate prevention remain incomplete. Mailpit stores captures; it does not deliver to real email inboxes. Driver notification consent remains disabled in seed data. Microsoft365 credentials, consent and mailbox provisioning are not required for this selected capture workflow.

Teardown only the Fault Code mail Compose project; preserve its persistent volume unless deletion is explicitly approved. Do not stop or remove LALAI mail services or shared infrastructure.

Official references: https://mailpit.axllent.org/docs/install/docker/ and https://mailpit.axllent.org/docs/api-v1/.
