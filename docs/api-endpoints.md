# API Endpoints

Base path: `/api`  
Auth: Bearer JWT (15-min access token + 7-day refresh token)  
Rate limit: `[EnableRateLimiting("auth")]` — 10 req/min fixed window

<!-- Scaffold — expand as endpoints are added/changed -->

## Auth (no [Authorize] except logout)

| Method | Path | Notes |
|--------|------|-------|
| POST | /auth/register | Returns AuthResponse (token + refreshToken) |
| POST | /auth/login | Progressive lockout: 3→5min, 5→15min, 8→1h, 10+→24h |
| POST | /auth/refresh | Rotates pair; reuse of revoked token revokes ALL user tokens |
| POST | /auth/logout | AllowAnonymous; accepts optional refreshToken body |
| POST | /auth/demo | Creates a guest account + seeds a demo workspace; returns AuthResponse |

## Health (no /api prefix, anonymous)

| Method | Path | Notes |
|--------|------|-------|
| GET | /health/live | Liveness — no checks (process up); always 200 while serving |
| GET | /health/ready | Readiness — DB `CanConnect` check; 200 healthy, 503 when DB unreachable |

## Workspaces

| Method | Path | Notes |
|--------|------|-------|
| GET | /workspaces | All workspaces user is member of |
| POST | /workspaces | Creates workspace + seeds demo board |
| GET | /workspaces/{id}/boards | Ordered by Position |
| DELETE | /workspaces/{id} | Membership check |

## Boards

| Method | Path | Notes |
|--------|------|-------|
| GET | /boards/{id} | Full detail with columns/cards |
| POST | /boards | CoverColor validated as `^#[0-9A-Fa-f]{6}$` |
| PUT | /boards/{id} | Does NOT accept CoverImageUrl — use upload endpoint |
| DELETE | /boards/{id} | |
| POST | /boards/{id}/cover-image | multipart/form-data; magic bytes + allowlist; max 5MB |
| DELETE | /boards/{id}/cover-image | |

## Columns, Cards, Checklists, Labels

Standard CRUD — all workspace-member gated. See controllers for full signatures.
