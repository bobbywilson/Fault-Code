# Docker development

Part of FAU-6; requires Docker Engine/Desktop with Linux containers, Compose v2.24+, and free loopback ports. Linux smoke checks also require Bash, curl and OpenSSL. No native SDK is required to build the containers. API runs as the .NET image's non-root APP_UID. Qdrant is internal and is not a relational database. No frontend exists yet; its profile belongs to FAU-22.

From the repository root:

```sh
cp .env.example .env
# Generate a local-only key, then set FAULTCODE_QDRANT_API_KEY in ignored .env:
openssl rand -hex 32
docker compose config --quiet
docker compose up --build -d --wait --wait-timeout 180
docker compose ps
curl --fail http://localhost:5081/health/live
curl --fail http://localhost:5081/health/ready
curl --fail --resolve faultcode.test:80:127.0.0.1 http://faultcode.test/health/live
```

Do not paste real credentials into logs or commit .env. No OpenAI/Entra secrets are needed. An empty key stops Compose before service creation. Wrong API/Qdrant keys or an unavailable Qdrant return readiness HTTP 503 with generic output; API liveness remains HTTP 200. Native API readiness is provider-free unless Qdrant__Enabled=true. Readiness uses authenticated /collections via qdrant:6333 with a two-second timeout; it does not implement RAG.

API defaults to loopback 5081, preserving native 5080. Proxy defaults to loopback port 80. Change FAULTCODE_API_PORT / FAULTCODE_PROXY_PORT in .env for conflicts; use that port in URLs. Qdrant has no host port, even for health checks. Qdrant stays on the internal backend network. API and proxy also use a project-scoped edge network for loopback port publishing. No provider credentials are configured.

## faultcode.test

The Compose proxy is supplied, but browser hostname activation also requires the local resolver entry `127.0.0.1 faultcode.test` in /etc/hosts. Preserve every existing Homestead/Lalai entry. `curl --resolve` verifies proxy routing independently of that entry. A successful proxy smoke is not proof the host resolver is configured. This Mac's host entry has not been activated; administrative access was unavailable. No sudo password is requested or stored.

## Lifecycle

```sh
docker compose restart
docker compose up -d --wait --wait-timeout 180
docker compose down                 # retains named storage/snapshot volumes
docker compose up -d --wait         # reuses those volumes
```

Only when intentionally deleting this project's local synthetic Qdrant data:

```sh
docker compose down --volumes       # DESTRUCTIVE: deletes this project's volumes
docker compose up -d --wait
```

Do not use Docker system prune or another project's Compose name. Qdrant storage and snapshots are separate named volumes. Docker volumes are development persistence, not a backup policy. No database, cloud service or production credential is selected.

## Verification and troubleshooting

```sh
bash -n scripts/container-smoke.sh
bash scripts/container-smoke.sh
```

Smoke uses a unique project with ephemeral credentials, random loopback ports and synthetic EPS data. It builds Linux images, waits for health, verifies proxy/DNS/readiness, non-root API, Qdrant exposure, secret-file exclusion, dependency interruption, restart, teardown retention and explicit volume reset. Cleanup deletes only its own test volumes. Native readiness tests additionally cover invalid credentials, startup validation, transport failure and cancellation. GitHub Linux containers CI runs the same script.

If Docker cannot connect, verify its Linux engine is running before retrying. If a host port is occupied, change the local env port; do not terminate unrelated services. `docker compose ps` and `docker compose logs --tail=100 api qdrant proxy` show startup state; never share rendered `docker compose config` with real keys. If a service is unhealthy, check configuration before resetting data. Version updates require deliberate tag changes and rerunning smoke. No Docker acceptance is claimed from configuration validation alone.

## FAU-14 retrieval acceptance

Linux CI adds a separately disposable authenticated Qdrant1.19.2 service for actual adapter/lifecycle/access/reindex tests with deterministic mock embeddings. Normal Compose retains its internal network/no Qdrant host port. Embeddings remain disabled and canonical storage503 pending approved decisions; this is not working production RAG. Live OpenAI outbound networking is not enabled by weakening the internal backend network. See knowledge-retrieval.md for configuration, test invocation and activation boundaries.
