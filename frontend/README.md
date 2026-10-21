# Fault Code administration

See [the administration runbook](../docs/administration.md) for configuration, API mappings and limitations.

Use Node22.22.3, `npm ci`, then `npm start` for native development with the API at localhost:5080. The supported Linux Docker client is `docker compose --profile administration up --build -d` at localhost:5082/admin/incidents. No tenant configuration or protected records ship by default.

`npm run build`, `npm test -- --watch=false`, `npm run e2e` and `npm run contracts` verify the client. The e2e auth replacement lives exclusively under tests and never enters the production container.
