# Parity change log

Every deviation of the ported e2e suite from the MVC original. Classes:
**Cosmetic** (selectors/waits) — free. **Structural** (URL shape) — listed.
**Semantic** (assertion meaning changes or check dropped) — REQUIRES USER SIGN-OFF.

| # | Original check | Change | Class | Status |
|---|---|---|---|---|
| 1 | `Unauthenticated root redirects to login` — expects `/Account/Login` | SPA route is `/login?returnUrl=…` | Structural | applied |
| 2 | `SEC: POST without CSRF token rejected` | No anti-forgery tokens in the port; replaced by SameSite + JSON content-type assertions | **Semantic** | **awaiting sign-off** |
| 3 | `SEC: POST with invalid CSRF token rejected` | Same as #2 | **Semantic** | **awaiting sign-off** |

> Tests are frozen before app code is written. Any test edited *after* seeing it fail
> must be added here with a justification.
