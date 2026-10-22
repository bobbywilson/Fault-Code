# Design — Part of FAU-22

Use official Spartan CLI1.8 matching installed Brain1.8, Nova style and supplied neutral theme. Generated Helm source is library code copied by Spartan's supported workflow, not a custom replacement component library. Application composition/data binding remains Angular code; every applicable visual control uses Helm. Keep existing Fields/Records application adapters only to bind API schemas/data to supplied components, with no bespoke control styles.

Adopt the official responsive sidebar/inset structure, supplied header/footer/menu elements, breadcrumb, cards, alerts, empty/skeleton states, tables, inputs/textarea/native select/checkbox, buttons and pagination. Use Lucide via ng-icons as Spartan's examples do. Use Tailwind only for component composition and spacing, not hand-designed replacements for library controls. Do not fabricate chart/dashboard values or add new business actions.

Retain current routes and API behavior. Preserve labels, skip link, focus, disabled permission states, status announcements, validation and mobile navigation. Fixtures remain isolated in e2e build replacement; production uses real MSAL and no test auth. Verify using existing unit/API-contract/e2e tests plus actual browser screenshots; label fixture screenshots honestly. Keep feature branch unmerged into main while deployment is paused.
