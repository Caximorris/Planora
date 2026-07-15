---
name: audit-security
description: Audit Planora trust boundaries, input, secrets, uploads, web controls, providers, and abuse cases. Use for explicit security reviews; do not use for correctness-only review.
---

# Audit Security

Identify exploitable or high-impact weaknesses with evidence, severity, and practical remediation while preserving existing controls.

## Inputs

- Defined surface, threat actor, assets, data sensitivity, and deployment context.
- Relevant code, configuration, workflows, tests, and external-provider boundaries.
- Known incidents, findings, or compliance constraints when supplied.

## Boundaries

- Keep the audit scoped; do not claim whole-system assurance from a partial review.
- Do not access production secrets or perform destructive/exploitative testing.
- Do not weaken security controls to reproduce a failure.

## Workflow

1. Define assets, actors, entry points, trust boundaries, and out-of-scope areas.
2. Review authentication/authorization, tenant isolation, input validation, output encoding, file handling, SSRF/path traversal/injection, CORS/CSRF, headers, rate limits, secrets, logging, and dependency exposure as applicable.
3. Trace data from untrusted input to sinks and from storage/provider responses to clients.
4. Inspect failure modes, abuse limits, replay/idempotency, temporary URLs/tokens, and background/provider behavior.
5. Validate suspected findings with safe local tests or concrete code paths.
6. Rank by exploitability and impact; distinguish confirmed, likely, and defense-in-depth issues.
7. Recommend the narrowest remediation and required security regression tests.

## Verification

- Ensure every finding has an attack precondition, path, impact, and evidence.
- Check existing mitigations before reporting.
- Run relevant focused tests and record untested production-only assumptions.

## Outputs

- Scope and threat model summary.
- Severity-ranked findings with evidence and remediation.
- Positive controls observed, required tests, and residual risk.

## Composition

- Use with `$planora-workflow`.
- Use `$review-authz` for detailed identity/tenancy decisions and `$review-dependencies` for package risk.
- Add `$review-blob-storage`, `$review-email-workflow`, `$review-container`, or `$review-azure-deployment` for provider-specific controls.
