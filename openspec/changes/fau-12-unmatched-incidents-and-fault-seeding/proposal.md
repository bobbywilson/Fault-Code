# Unmatched vehicle incidents and demo fault catalog

## Why

The user clarified Incidents should list vehicle reports whose fault code cannot be matched to known faults, and requested seed faults related to Nexteer systems. The current page is a generic action form and production persistence is still unavailable.

## What Changes

Add a server-filtered, paginated unmatched-at-intake queue and show it by default in administration. Preserve authenticated intake/replay, immutable history and known occurrences for existing investigation clients. Prepare demo seed examples inspired by publicly documented Nexteer steering product families; never represent them as verified OEM diagnostic codes. Loading a real database is dependent on the user's explicit DEC-001 engine choice. Keep this change active until seed persistence is implemented and verified.

## Impact

Incident query/API contract and administration, focused tests, seed manifest and documentation. No deployment, no LALAI changes, no real vehicle data or diagnostic repair instructions.
