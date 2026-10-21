## ADDED Requirements

### Requirement: Linux Docker delivery platform
Delivery SHALL validate backend builds, tests, contracts and frontend/browser acceptance on Linux and build Linux runtime images. Native Windows/macOS coverage SHALL be optional and SHALL NOT gate the sole developer's Linux Docker workflow, including on Mac. Existing isolation and unavailable-provider boundaries SHALL remain intact.
#### Scenario: Exact source delivery
- **WHEN** a pull request is considered for merge
- **THEN** passing exact-source Linux build and relevant container acceptance provide delivery evidence; cancelled or unrun native jobs are never reported as passed
### Requirement: Isolated administration packaging
The administration frontend SHALL be reproducibly built from locked dependencies and served by a non-root Linux web container with same-origin API proxying, loopback host exposure and existing internal Compose networking. Public docs SHALL remain separately accessible. No test identity or fixture SHALL enter the production bundle.
#### Scenario: Frontend container acceptance
- **WHEN** the Linux frontend image starts without tenant configuration
- **THEN** its administration route loads the honest setup state, protected API remains authenticated and database/vector/model ports remain unavailable to the browser
