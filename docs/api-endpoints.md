# API Endpoints

Base path: `/api`  
Auth: Bearer JWT (15-min access token + 7-day refresh token)  
Rate limit: `[EnableRateLimiting("auth")]` — 10 req/min fixed window

<!-- Scaffold — expand as endpoints are added/changed -->

## Auth (no [Authorize] except logout)

| Method | Path | Notes |
|--------|------|-------|
| POST | /auth/register | Returns AuthResponse (token + refreshToken) |
| POST | /auth/login | Progressive lockout: 3→5min, 5→15min, 8→1h, 10+→24h. If 2FA is on, returns AuthResponse{RequiresTwoFactor=true} with no tokens |
| POST | /auth/login/2fa | Completes login for a 2FA account: re-verifies password (same lockout) + TOTP or recovery code |
| POST | /auth/refresh | Rotates pair; reuse of revoked token revokes ALL user tokens |
| POST | /auth/logout | AllowAnonymous; accepts optional refreshToken body |
| POST | /auth/demo | Creates a guest account + seeds a demo workspace; returns AuthResponse |
| POST | /auth/forgot-password | Rate-limited; no account enumeration |
| POST | /auth/reset-password | Single-use Identity token; revokes refresh tokens |
| POST | /auth/confirm-email | Optional verification while local/console email is used |
| POST | /auth/send-email-confirmation | Authorized; sends/resends verification |
| POST | /auth/sessions | Authorized; lists active refresh-token sessions |
| POST | /auth/sessions/revoke | Authorized; revokes one session by id |
| POST | /auth/sessions/revoke-others | Authorized; keeps the current refresh-token session |
| GET  | /auth/2fa/status | Authorized; { enabled, recoveryCodesRemaining } |
| POST | /auth/2fa/setup | Authorized; returns shared key + otpauth URI (does not enable yet) |
| POST | /auth/2fa/enable | Authorized; verifies TOTP, enables 2FA, returns 10 one-time recovery codes |
| POST | /auth/2fa/disable | Authorized; requires a valid TOTP/recovery code to disable |
| POST | /auth/2fa/recovery-codes | Authorized; regenerates recovery codes (requires a valid code) |

## Health (no /api prefix, anonymous)

| Method | Path | Notes |
|--------|------|-------|
| GET | /health/live | Liveness — no checks (process up); always 200 while serving |
| GET | /health/ready | Readiness — DB `CanConnect` check; 200 healthy, 503 when DB unreachable |

## Workspaces

| Method | Path | Notes |
|--------|------|-------|
| GET | /workspaces | All workspaces user is member of |
| GET | /workspaces/{id} | Member-gated |
| POST | /workspaces | Creates workspace + seeds demo board |
| PUT | /workspaces/{id} | Owner/Admin only |
| GET | /workspaces/{id}/members | Member-gated |
| DELETE | /workspaces/{id}/members/{userId} | Owner/Admin; cannot remove workspace owner |
| PATCH | /workspaces/{id}/members/{userId} | Owner only; cannot assign Owner here |
| GET | /workspaces/{id}/boards | Ordered by Position |
| GET | /workspaces/{id}/calendar | Member-gated due-date feed |
| GET | /workspaces/{id}/invitations | Owner/Admin; lists pending invitations (marks stale pendings Expired) |
| POST | /workspaces/{id}/invitations | Owner/Admin; creates pending invitation |
| DELETE | /workspaces/{id}/invitations/{invitationId} | Owner/Admin; revokes pending invitation |
| POST | /workspaces/{id}/transfer-ownership | Owner only; target must already be a member |
| POST | /workspaces/{id}/leave | Member self-leave; current Owner must transfer first |
| DELETE | /workspaces/{id} | Owner only |

## Invitations

| Method | Path | Notes |
|--------|------|-------|
| GET | /invitations/{token} | Anonymous lookup; expires stale pending invitations |
| POST | /invitations/{token}/accept | Authorized; invitee email must match |
| POST | /invitations/{token}/decline | Authorized; invitee email must match |

## Boards

| Method | Path | Notes |
|--------|------|-------|
| GET | /boards/{id} | Full detail with columns/cards |
| POST | /boards | CoverColor validated as `^#[0-9A-Fa-f]{6}$` |
| PUT | /boards/{id} | Requires `RowVersion`; stale versions return 409. Does NOT accept CoverImageUrl — use upload endpoint |
| DELETE | /boards/{id} | **Soft delete** — moves the board to the workspace trash (recoverable) |
| GET | /boards/trash?workspaceId={id} | Member-gated list of trashed boards, newest-deleted first |
| PATCH | /boards/{id}/restore | Restores a trashed board (clears DeletedAt) |
| DELETE | /boards/{id}/permanent | Hard delete — only a trashed board; cascades + removes cover image |
| POST | /boards/{id}/cover-image | multipart/form-data; magic bytes + allowlist; max 5MB |
| DELETE | /boards/{id}/cover-image | |
| GET | /boards/{id}/activity | Member-gated board activity feed, newest first |

## Columns, Cards, Checklists, Labels

Standard CRUD — all workspace-member gated. See controllers for full signatures.
`PUT /columns/{id}` and `PUT /cards/{id}` require `RowVersion`; stale versions return 409.

Cards additionally support soft-delete / trash, mirroring boards:

| Method | Path | Notes |
|--------|------|-------|
| DELETE | /cards/{id} | **Soft delete** — moves the card to its board's trash (recoverable) |
| GET | /cards/trash?boardId={id} | Member-gated list of trashed cards for a board, newest-deleted first |
| PATCH | /cards/{id}/restore | Restores a trashed card (clears DeletedAt) |
| DELETE | /cards/{id}/permanent | Hard delete — only a trashed card |
| POST | /cards/{id}/attachments | multipart/form-data; member-gated; allowlisted PNG/JPEG/WEBP/GIF/PDF/TXT; max 10MB |
| DELETE | /cards/{id}/attachments/{attachmentId} | Member-gated; deletes attachment row + stored file |

Trash is orthogonal to archive: a board/card can be archived and/or trashed. The global EF query
filter hides both archived and trashed rows from all normal reads (lists, GetById, search, calendar).
