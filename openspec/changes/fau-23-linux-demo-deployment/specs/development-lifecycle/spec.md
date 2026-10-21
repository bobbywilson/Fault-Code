## ADDED Requirements

### Requirement: Main-only validated demo delivery
Changes SHALL follow feature branches, local Docker validation, pull request into development, successful CI, pull request into main and successful CI. Only successful updates to main SHALL automatically deploy the same validated source revision. Deployments SHALL be serialized, use a GitHub environment and least-privilege secrets, publish versioned GHCR images and reject stale or untrusted source events.

#### Scenario: Feature or development update
- **WHEN** a feature branch, pull request or development branch completes CI
- **THEN** tests run without a production deployment or production credential disclosure

#### Scenario: Accepted main revision
- **WHEN** every required CI check succeeds for current main
- **THEN** deploy only its versioned images, verify source/health and report actual outcome
