# FAU-6 verification

Part of FAU-6. Local macOS evidence on 2026-10-20:

- Native Release build: passed, no warnings/errors.
- Native tests: 27 passed (4 Domain, 3 Application, 20 Integration), including new readiness/configuration/failure cases.
- Missing-key Compose configuration: rejected with FAULTCODE_QDRANT_API_KEY guidance.
- Synthetic-key Compose configuration: passed without printing resolved secrets.
- Bash smoke syntax and diff whitespace: passed.
- Local Docker runtime: unavailable; `docker info` outside sandbox cannot connect to desktop-linux daemon. No local container health/persistence or hostname activation claimed.

The Linux containers workflow must pass before runtime acceptance and official archive. Its smoke project/volumes are disposable and separate from developer resources. The user reviews the open issue and unmerged PR. See the OpenSpec change tasks for outstanding observed acceptance.
