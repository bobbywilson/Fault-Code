# Fault-code ontology browser

## Why

Part of FAU-22. The user requests a fault-code-centered ontology showing incidents, vehicles, drivers and notifications on entry, rather than an ontology creation form.

## What Changes

Replace the route presentation with a searchable, read-only relationship table composed of supplied Spartan components. Preserve underlying approved taxonomy/publication APIs and their authorization. This is a domain-model reference, not fabricated tenant records or a change to persistence.

## Impact

Administration route and presentation tests; versioned administration specification. No backend authorization, taxonomy behavior, database, deployment or infrastructure change.
