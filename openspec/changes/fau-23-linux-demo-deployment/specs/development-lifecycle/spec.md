## ADDED Requirements

### Requirement: Main-only validated demo delivery
Changes SHALL follow feature branches, local Docker validation, pull request into development, successful CI, optional promotion pull request into main. Only successful updates to development SHALL automatically deploy the same validated source revision. Deployments SHALL be serialized, use a GitHub environment and least-privilege secrets, publish versioned GHCR images and reject stale or untrusted source events.

#### Scenario: Feature or pull request update
- **WHEN** a feature branch or pull request completes CI
- **THEN** tests run without a production deployment or production credential disclosure

#### Scenario: Accepted development revision
- **WHEN** every required CI check succeeds for current development
- **THEN** deploy only its versioned images, verify source/health and report actual outcome
