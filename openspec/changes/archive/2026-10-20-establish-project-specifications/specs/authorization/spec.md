# authorization Specification Delta

## Purpose

Define observable Fault Code authorization requirements and acceptance scenarios for authorized future development.

## ADDED Requirements

### Requirement: Deny by default
Protected operations SHALL validate Entra access tokens and enforce least-privilege roles, delegated scopes or application roles plus resource ownership; only explicitly public operations bypass authentication.

#### Scenario: Deny by default acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a caller lacks a valid token, scope, role or driver ownership
- **THEN** 401 distinguishes invalid identity from 403 insufficient privilege; cross-driver access and unapproved external integrations are denied

### Requirement: Engineering and administration boundaries
Knowledge publication, approved remedies, integration management and user/role administration SHALL require explicit server-side policies and audited actor/context.

#### Scenario: Engineering and administration boundaries acceptance
- **GIVEN** demo authorized Fault Code test context with the stated preconditions
- **WHEN** a reader or agent attempts an engineering publication or integration mutation
- **THEN** the server denies it; hiding a UI control does not replace authorization and MCP does not elevate caller permissions
