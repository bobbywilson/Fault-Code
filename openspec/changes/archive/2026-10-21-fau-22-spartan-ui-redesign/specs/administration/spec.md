## ADDED Requirements

### Requirement: Supplied Spartan visual components
All administration views SHALL compose the supplied Spartan Helm styled components for applicable navigation, headers/footers, menus, cards, controls, forms, alerts, records and pagination. Icons SHALL use Lucide. Application-specific data binding SHALL NOT introduce bespoke visual controls or override the library's supplied visual system with a custom component library.

#### Scenario: Consistent visual system across routes
- **WHEN** an operator visits any existing administration route
- **THEN** the shared responsive shell and applicable controls use supplied Spartan styles and Lucide icons, with no emoji or hand-drawn replacement icons

#### Scenario: Accessible mobile and protected behavior
- **WHEN** the viewport narrows or the user navigates by keyboard, lacks permissions or receives unavailable data
- **THEN** supplied mobile navigation, labels, visible focus and honest status states remain usable and existing server authorization/API semantics are preserved

#### Scenario: Reviewable visual evidence
- **WHEN** the redesign is presented for review
- **THEN** screenshots show the actual updated application on desktop and mobile, with demo test contexts distinguished from real Entra authentication and no fabricated production metrics
