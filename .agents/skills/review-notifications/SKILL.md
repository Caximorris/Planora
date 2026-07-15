---
name: review-notifications
description: Review Planora notification creation, scoping, unread/read/dismiss lifecycle, links, polling, preferences, and UI. Use for in-app notifications; do not review email delivery alone.
---

# Review Notifications

Ensure notifications reach only the intended user, reflect committed events, remain navigable, and behave consistently across API and Web.

## Inputs

- Event/trigger, recipient rules, Notification entity/DTO/controller/service, and UI surfaces.
- Read/unread/dismiss semantics, related resource links, and preferences.
- Tests and provider/email coupling when present.

## Boundaries

- Never expose another user or workspace’s notification or related resource.
- Do not add new polling loops; preserve the documented notifications polling model unless explicitly redesigned.
- Keep transactional email transport/content in `$review-email-workflow`.

## Workflow

1. Trace each trigger after authorization and transaction outcome to recipient selection and stored notification.
2. Verify user/workspace scoping, deduplication expectations, type taxonomy, message content, timestamps, and related board/resource links.
3. Review list ordering, unread count, mark-one/read-all/dismiss queries, and ownership filters.
4. Trace DTO mapping and Web service/UI behavior across nav dropdown and notifications page.
5. Check polling interval/lifecycle, concurrent updates, stale counts, error handling, empty/loading states, and navigation when resources are archived/deleted.
6. Review preference boundaries and whether email and in-app channels intentionally differ.
7. Require two-user cross-workspace and lifecycle integration tests.

## Verification

- Run notification, workspace-access, and related event/email tests.
- Exercise list, unread count, mark read, read all, dismiss, and link navigation.
- Confirm no duplicate polling or cross-user leakage.

## Outputs

- Notification lifecycle and recipient matrix.
- Findings on scoping, state, content, polling, links, and UI.
- Tests, channel-coupling decisions, and residual concurrency risk.

## Composition

- Use with `$planora-workflow`.
- Pair with `$review-authz`, `$review-error-handling`, `$review-blazor-ui`, and `$review-observability`.
- Add `$review-email-workflow` when the same activity also sends email.
