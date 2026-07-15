---
name: review-authz
description: Review Planora authentication, JWT and refresh sessions, Identity, workspace roles, tenancy, and IDOR controls. Use for auth or permission changes and scoped access audits; do not use as a broad security audit.
---

# Review Auth and Authorization

Verify identity provenance, session security, workspace scoping, role enforcement, and cross-tenant isolation without weakening established protections.

## Inputs

- Endpoints, services, queries, tests, or auth flow in scope.
- Caller types, workspace roles, resource hierarchy, and expected decisions.
- Token, lockout, rate-limit, and security-stamp behavior when relevant.

## Boundaries

- Never trust a body/query user ID over `ClaimTypes.NameIdentifier`.
- Do not weaken rate limits, progressive lockout, refresh reuse detection, security-stamp validation, or tenant checks.
- Changing auth, authorization, tenancy, permissions, or rate limits requires approval before implementation.

## Workflow

1. Map identities, credentials/tokens, trust boundaries, and authorization decisions.
2. Trace the caller ID from claims and reject client-supplied ownership assumptions.
3. For each workspace-scoped operation, verify membership and required role before data exposure or mutation; scope resource queries to the tenant.
4. Check nested identifiers for IDOR, including board, column, card, member, invite, notification, search, calendar, attachment, and activity paths.
5. Review JWT lifetime, refresh rotation/reuse, logout/password stamp rotation, 2FA, lockout thresholds, and rate limiting when touched.
6. Check status behavior for unauthenticated, unauthorized, missing, and cross-workspace requests without leaking resource existence.
7. Require focused negative integration tests for every changed decision.

## Verification

- Exercise two users in different workspaces plus role-boundary cases.
- Inspect queries, not only endpoint attributes or client visibility.
- Run relevant auth/security tests and confirm established thresholds remain pinned.

## Outputs

- Authorization matrix and trust-boundary map.
- Findings with concrete cross-tenant or session scenarios.
- Required tests, approval gates, and residual threats.

## Composition

- Use with `$planora-workflow`.
- Add `$audit-security` for broader threat/injection/secret/upload review.
- Compose with endpoint, storage, notification, email, or migration reviews for the affected surface.
