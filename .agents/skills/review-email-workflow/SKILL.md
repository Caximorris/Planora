---
name: review-email-workflow
description: Review Planora transactional email content, Resend delivery, tokens, preferences, privacy, and failure isolation. Use for email flows; do not review in-app notifications alone.
---

# Review Email Workflow

Ensure each email is authorized, correctly addressed, safe, testable, provider-isolated, and consistent with the user action’s transaction semantics.

## Inputs

- Email trigger and user journey.
- Factory/template, transactional service, sender resolver/provider, options, deployment mapping, and tests.
- Recipient rules, preferences, tokens/links, and failure semantics.

## Boundaries

- Never log or commit API keys, auth/reset/invite tokens, or unnecessary personal data.
- Do not let non-critical activity-email provider failure roll back a committed user action.
- Do not change production sender/provider/security-sensitive email behavior without approval.

## Workflow

1. Trace the trigger from endpoint/use case to recipient selection, preferences, message factory, sender resolver, provider, and logs.
2. Verify authorization and tenant context before deriving recipient or link data.
3. Review subject, HTML/text parity, encoding, untrusted content, link base URL, token confidentiality, expiry/single-use semantics, and anti-enumeration.
4. Check sender-kind resolution, startup option validation, Resend request/auth, cancellation, timeout, and response handling.
5. Confirm critical auth email versus best-effort activity-email failure semantics are intentional.
6. Review duplicate-send/idempotency risk, preference enforcement, and test isolation with capturing/fake senders.
7. Check deployment secret names and from-address mapping without accessing values.

## Verification

- Run email factory/template/resolver/provider and relevant integration tests.
- Exercise provider failure and confirm logs are useful but safe and user-action semantics remain correct.
- Inspect rendered HTML/text for encoding and link correctness.

## Outputs

- Email flow and recipient/security map.
- Findings on content, tokens, preferences, provider behavior, and failure isolation.
- Tests, deployment assumptions, and approval needs.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-authz`, `$audit-security`, `$review-error-handling`, and `$review-observability`.
- Add `$review-notifications` when the event also creates in-app notifications/preferences.
